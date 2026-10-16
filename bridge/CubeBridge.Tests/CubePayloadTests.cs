using CubeBridge.Providers;

namespace CubeBridge.Tests;

public class CubePayloadTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Builds_compact_payload_with_minutes_until_reset()
    {
        var claude = new ProviderUsage(true,
            new UsageWindow(21.4, Now.AddMinutes(142).AddSeconds(-30)),
            new UsageWindow(7, Now.AddDays(6)));

        var json = CubePayload.Build([("c", claude)], Now).ToJsonString();

        Assert.Equal("""{"c":{"ok":true,"s":21,"sr":142,"w":7,"wr":8640},"ts":1792141200}""", json);
    }

    [Fact]
    public void Failed_provider_sends_short_error()
    {
        var json = CubePayload.Build([("x", ProviderUsage.Fail("401 unauthorized (run codex once to refresh)"))], Now).ToJsonString();

        Assert.Equal("""{"x":{"ok":false,"e":"401 unauthorized (run co"},"ts":1792141200}""", json);
    }

    [Fact]
    public void Clamps_percent_and_handles_unknown_reset()
    {
        var u = new ProviderUsage(true, new UsageWindow(104, null), new UsageWindow(-3, Now.AddMinutes(-5)));

        var json = CubePayload.Build([("c", u)], Now).ToJsonString();

        Assert.Equal("""{"c":{"ok":true,"s":100,"sr":-1,"w":0,"wr":0},"ts":1792141200}""", json);
    }
}
