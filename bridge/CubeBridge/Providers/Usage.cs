namespace CubeBridge.Providers;

/// <summary>One rate-limit window (e.g. 5-hour session or 7-day weekly).</summary>
public sealed record UsageWindow(double Percent, DateTimeOffset? ResetsAt);

/// <summary>Usage snapshot of one provider. <see cref="Ok"/> is false when data could not be fetched.</summary>
public sealed record ProviderUsage(bool Ok, UsageWindow? Session, UsageWindow? Weekly, string? Error = null)
{
    public static ProviderUsage Fail(string error) => new(false, null, null, error);
}

public interface IUsageProvider
{
    /// <summary>Short key used in the cube payload ("c" = Claude, "x" = Codex).</summary>
    string Key { get; }
    string Name { get; }
    Task<ProviderUsage> GetAsync(CancellationToken ct);
}
