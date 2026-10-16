using CubeBridge.Providers;

namespace CubeBridge;

/// <summary>Latest one-line summary, shown as the tray tooltip.</summary>
public sealed class BridgeStatus
{
    public string Summary { get; private set; } = "CubeBridge: starting…";
    public event Action<string>? Changed;

    public void Update(IEnumerable<(string Name, ProviderUsage Usage)> usages, string? cubeError)
    {
        var parts = usages.Select(x => x.Usage.Ok
            ? $"{x.Name} {x.Usage.Session?.Percent:0}%/{x.Usage.Weekly?.Percent:0}%"
            : $"{x.Name} ✕");
        Summary = string.Join(" · ", parts) + (cubeError is null ? "" : " · cube offline");
        Changed?.Invoke(Summary);
    }
}
