using CubeBridge.Providers;
using Microsoft.Extensions.Options;

namespace CubeBridge;

/// <summary>Polls every enabled provider and pushes the combined snapshot to the cube.</summary>
public sealed class Worker(
    IEnumerable<IUsageProvider> providers,
    CubeClient cube,
    BridgeStatus status,
    IOptions<CubeOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var opt = options.Value;
        var enabled = providers.Where(p => p.Key switch { "c" => opt.Claude, "x" => opt.Codex, _ => true }).ToList();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(15, opt.PollSeconds)));

        do
        {
            var results = await Task.WhenAll(enabled.Select(async p => (p, usage: await SafeGet(p, ct))));
            foreach (var (p, u) in results)
            {
                if (u.Ok) logger.LogInformation("{Name}: 5h {S}% (reset {SR:t}), week {W}% (reset {WR:g})",
                    p.Name, u.Session?.Percent, u.Session?.ResetsAt?.ToLocalTime(), u.Weekly?.Percent, u.Weekly?.ResetsAt?.ToLocalTime());
                else logger.LogWarning("{Name}: {Error}", p.Name, u.Error);
            }

            var payload = CubePayload.Build(results.Select(r => (r.p.Key, r.usage)), DateTimeOffset.UtcNow);
            string? cubeError = null;
            if (opt.DryRun)
            {
                logger.LogInformation("dry-run payload: {Payload}", payload.ToJsonString());
            }
            else
            {
                try { await cube.PushAsync(payload, ct); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    cubeError = ex.Message;
                    logger.LogWarning("push to {Host} failed: {Error}", opt.Host, ex.Message);
                }
            }
            status.Update(results.Select(r => (r.p.Name, r.usage)), cubeError);
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    async Task<ProviderUsage> SafeGet(IUsageProvider p, CancellationToken ct)
    {
        try { return await p.GetAsync(ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "{Name} fetch failed", p.Name);
            return ProviderUsage.Fail(ex.GetType().Name);
        }
    }
}
