namespace CubeBridge;

/// <summary>Keeps the last raw response per provider in %LOCALAPPDATA%\CubeBridge for debugging format changes.</summary>
public sealed class RawDump
{
    readonly string _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CubeBridge");

    public void Save(string name, string body)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, $"last-{name}.json"), body);
        }
        catch (IOException) { }
    }
}
