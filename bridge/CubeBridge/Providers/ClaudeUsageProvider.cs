using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace CubeBridge.Providers;

/// <summary>
/// Reads Claude subscription limits from the (undocumented) endpoint Claude Code's /usage uses.
/// The OAuth token is taken from CLAUDE_CODE_OAUTH_TOKEN or ~/.claude/.credentials.json and is
/// never refreshed here: refresh tokens rotate, so refreshing outside Claude Code would log it out.
/// </summary>
public sealed class ClaudeUsageProvider(HttpClient http, RawDump dump) : IUsageProvider
{
    const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

    public string Key => "c";
    public string Name => "Claude";

    public async Task<ProviderUsage> GetAsync(CancellationToken ct)
    {
        var token = Environment.GetEnvironmentVariable("CLAUDE_CODE_OAUTH_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", ".credentials.json");
            if (!File.Exists(path)) return ProviderUsage.Fail("no ~/.claude/.credentials.json (log in to Claude Code)");

            var oauth = JsonNode.Parse(await File.ReadAllTextAsync(path, ct))?["claudeAiOauth"];
            token = oauth?["accessToken"]?.GetValue<string>();
            if (string.IsNullOrEmpty(token)) return ProviderUsage.Fail("no claudeAiOauth.accessToken");

            var expiresAt = oauth?["expiresAt"]?.GetValue<long>();
            if (expiresAt is not null && DateTimeOffset.FromUnixTimeMilliseconds(expiresAt.Value) < DateTimeOffset.UtcNow)
                return ProviderUsage.Fail("token expired (use Claude Code once to refresh)");
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("anthropic-beta", "oauth-2025-04-20");

        using var res = await http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized) return ProviderUsage.Fail("401 unauthorized (use Claude Code once to refresh)");
        if (!res.IsSuccessStatusCode) return ProviderUsage.Fail($"HTTP {(int)res.StatusCode}");

        dump.Save("claude", body);
        return Parse(body);
    }

    /// <summary>Prefers five_hour/seven_day; falls back to the newer limits[] array.</summary>
    public static ProviderUsage Parse(string json)
    {
        var root = JsonNode.Parse(json);
        if (root is null) return ProviderUsage.Fail("empty response");

        var session = ReadLegacy(root["five_hour"]) ?? ReadLimit(root["limits"], "session");
        var weekly = ReadLegacy(root["seven_day"]) ?? ReadLimit(root["limits"], "weekly");

        return session is null && weekly is null
            ? ProviderUsage.Fail("unrecognised response shape")
            : new ProviderUsage(true, session, weekly);
    }

    static UsageWindow? ReadLegacy(JsonNode? node)
    {
        var pct = node?["utilization"]?.GetValue<double>();
        return pct is null ? null : new UsageWindow(pct.Value, ParseTime(node?["resets_at"]));
    }

    static UsageWindow? ReadLimit(JsonNode? limits, string group)
    {
        if (limits is not JsonArray arr) return null;
        var node = arr.FirstOrDefault(l => l?["group"]?.GetValue<string>() == group);
        var pct = node?["percent"]?.GetValue<double>();
        return pct is null ? null : new UsageWindow(pct.Value, ParseTime(node?["resets_at"]));
    }

    static DateTimeOffset? ParseTime(JsonNode? node) =>
        DateTimeOffset.TryParse(node?.GetValue<string>(), out var t) ? t : null;
}
