using System.Text.Json.Nodes;
using CubeBridge.Providers;

namespace CubeBridge;

/// <summary>
/// Builds the compact JSON the firmware expects on POST /api/usage:
/// {"c":{"ok":true,"s":21,"sr":142,"w":7,"wr":9876},"x":{...},"ts":1792136821}
/// s/w = percent used (0-100), sr/wr = minutes until reset (-1 unknown), e = short error when ok=false.
/// </summary>
public static class CubePayload
{
    public static JsonObject Build(IEnumerable<(string Key, ProviderUsage Usage)> usages, DateTimeOffset now)
    {
        var root = new JsonObject();
        foreach (var (key, u) in usages)
        {
            var o = new JsonObject { ["ok"] = u.Ok };
            if (u.Ok)
            {
                o["s"] = Percent(u.Session);
                o["sr"] = Minutes(u.Session, now);
                o["w"] = Percent(u.Weekly);
                o["wr"] = Minutes(u.Weekly, now);
            }
            else
            {
                o["e"] = Truncate(u.Error ?? "error", 24);
            }
            root[key] = o;
        }
        root["ts"] = now.ToUnixTimeSeconds();
        return root;
    }

    static int Percent(UsageWindow? w) => w is null ? -1 : (int)Math.Clamp(Math.Round(w.Percent), 0, 100);

    static int Minutes(UsageWindow? w, DateTimeOffset now) =>
        w?.ResetsAt is { } t ? (int)Math.Max(0, Math.Ceiling((t - now).TotalMinutes)) : -1;

    static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
