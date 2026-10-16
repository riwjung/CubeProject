namespace CubeBridge;

public sealed class CubeOptions
{
    /// <summary>Cube IP or hostname (firmware announces itself as cube.local via mDNS).</summary>
    public string Host { get; set; } = "cube.local";
    public int PollSeconds { get; set; } = 60;
    /// <summary>Log the payload instead of sending it to the cube.</summary>
    public bool DryRun { get; set; }
    public bool Claude { get; set; } = true;
    public bool Codex { get; set; } = true;
}
