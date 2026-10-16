using CubeBridge.Providers;

namespace CubeBridge.Tests;

public class ParserTests
{
    static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Claude_reads_five_hour_and_seven_day()
    {
        var u = ClaudeUsageProvider.Parse(Fixture("claude-usage.json"));

        Assert.True(u.Ok);
        Assert.Equal(21, u.Session!.Percent);
        Assert.Equal(7, u.Weekly!.Percent);
        Assert.Equal(new DateTimeOffset(2026, 10, 22, 15, 59, 59, 655, TimeSpan.Zero), u.Weekly.ResetsAt);
    }

    [Fact]
    public void Claude_falls_back_to_limits_array()
    {
        var u = ClaudeUsageProvider.Parse(Fixture("claude-usage-limits-only.json"));

        Assert.True(u.Ok);
        Assert.Equal(64, u.Session!.Percent);
        Assert.Equal(33, u.Weekly!.Percent);
    }

    [Fact]
    public void Claude_unknown_shape_is_not_ok()
    {
        var u = ClaudeUsageProvider.Parse("""{"something":"else"}""");

        Assert.False(u.Ok);
        Assert.NotNull(u.Error);
    }

    [Fact]
    public void Codex_reads_primary_and_secondary_windows()
    {
        var u = CodexUsageProvider.Parse(Fixture("codex-usage.json"));

        Assert.True(u.Ok);
        Assert.Equal(4, u.Session!.Percent);
        Assert.Equal(22, u.Weekly!.Percent);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1792149549), u.Session.ResetsAt);
    }

    [Fact]
    public void Codex_missing_rate_limit_is_not_ok()
    {
        Assert.False(CodexUsageProvider.Parse("""{"plan_type":"free"}""").Ok);
    }
}
