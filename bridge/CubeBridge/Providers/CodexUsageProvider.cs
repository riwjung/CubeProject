using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace CubeBridge.Providers;

/// <summary>
/// Reads Codex (ChatGPT plan) limits from the endpoint the Codex CLI uses for /status.
/// Token comes from ~/.codex/auth.json and is re-read every poll; the Codex CLI owns refreshing it.
/// </summary>
public sealed class CodexUsageProvider(HttpClient http, RawDump dump) : IUsageProvider
{
    const string UsageUrl = "https://chatgpt.com/backend-api/wham/usage";

    public string Key => "x";
    public string Name => "Codex";

    public async Task<ProviderUsage> GetAsync(CancellationToken ct)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json");
        if (!File.Exists(path)) return ProviderUsage.Fail("no ~/.codex/auth.json (log in to Codex CLI)");

        var tokens = JsonNode.Parse(await File.ReadAllTextAsync(path, ct))?["tokens"];
        var token = tokens?["access_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token)) return ProviderUsage.Fail("no tokens.access_token");

        using var req = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (tokens?["account_id"]?.GetValue<string>() is { Length: > 0 } accountId)
            req.Headers.Add("ChatGPT-Account-Id", accountId);
        req.Headers.UserAgent.ParseAdd("codex_cli_rs");

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized) return ProviderUsage.Fail("401 unauthorized (run codex once to refresh)");
        if (!res.IsSuccessStatusCode) return ProviderUsage.Fail($"HTTP {(int)res.StatusCode}");

        dump.Save("codex", body);
        return Parse(body);
    }

    public static ProviderUsage Parse(string json)
    {
        var rl = JsonNode.Parse(json)?["rate_limit"];
        if (rl is null) return ProviderUsage.Fail("unrecognised response shape");

        var session = ReadWindow(rl["primary_window"]);
        var weekly = ReadWindow(rl["secondary_window"]);
        return session is null && weekly is null
            ? ProviderUsage.Fail("no rate-limit windows")
            : new ProviderUsage(true, session, weekly);
    }

    static UsageWindow? ReadWindow(JsonNode? node)
    {
        var pct = node?["used_percent"]?.GetValue<double>();
        if (pct is null) return null;
        var resetAt = node?["reset_at"]?.GetValue<long>();
        return new UsageWindow(pct.Value, resetAt is null ? null : DateTimeOffset.FromUnixTimeSeconds(resetAt.Value));
    }
}
