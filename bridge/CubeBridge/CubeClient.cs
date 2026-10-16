using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace CubeBridge;

public sealed class CubeClient(HttpClient http, IOptions<CubeOptions> options)
{
    public async Task PushAsync(JsonObject payload, CancellationToken ct)
    {
        // StringContent sets Content-Length; ESP8266WebServer cannot read chunked request bodies.
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var res = await http.PostAsync($"http://{options.Value.Host}/api/usage", content, ct);
        res.EnsureSuccessStatusCode();
    }
}
