using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text.Json;

namespace ChatMixOverlay;

public sealed class SonarObserver : IDisposable
{
    private readonly HttpClient http;
    private readonly CancellationTokenSource stop = new();
    private readonly object readingGate = new();
    private long socketRevision;
    private double? lastBalance;
    public event Action<double, string, string>? Reading;
    public event Action<string>? Status;
    public SonarObserver()
    {
        http = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
                request.RequestUri?.Host == "127.0.0.1" || errors == System.Net.Security.SslPolicyErrors.None
        })
        { Timeout = TimeSpan.FromSeconds(3) };
    }
    public static Uri LocalAddress(string address, string scheme)
    {
        if (!address.Contains("://")) address = scheme + "://" + address;
        var uri = new Uri(address);
        if (uri.Host != "127.0.0.1" || uri.Scheme != scheme || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Sonar discovery must resolve to IPv4 loopback.");
        return uri;
    }
    private async Task<Uri> Discover(CancellationToken ct)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SteelSeries", "GG", "coreProps.json");
        using var props = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        var core = LocalAddress(props.RootElement.GetProperty("ggEncryptedAddress").GetString()!, "https");
        using var doc = JsonDocument.Parse(await http.GetStringAsync(new Uri(core, "/subApps"), ct));
        return LocalAddress(doc.RootElement.GetProperty("subApps").GetProperty("sonar").GetProperty("metadata").GetProperty("webServerAddress").GetString()!, "http");
    }
    public static bool TryBalance(JsonElement data, out double balance, out string state)
    {
        balance = 0; state = "unknown";
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("balance", out var b) ||
            b.ValueKind != JsonValueKind.Number || !b.TryGetDouble(out balance) || !double.IsFinite(balance) || balance < -1.001 || balance > 1.001) return false;
        balance = Math.Clamp(balance, -1, 1);
        if (data.TryGetProperty("state", out var s) && s.ValueKind == JsonValueKind.String) state = s.GetString()!;
        return true;
    }
    private void Emit(JsonElement data, string source)
    {
        lock (readingGate)
        {
            if (source == "wheel/socket") socketRevision++;
            if (!TryBalance(data, out var value, out var state)) throw new InvalidDataException("Unrecognized ChatMix payload.");
            if (source != "baseline" && lastBalance.HasValue && Math.Abs(lastBalance.Value - value) <= .001) return;
            lastBalance = value;
            Reading?.Invoke(value, state, source);
        }
    }
    public async Task RunAsync()
    {
        var ct = stop.Token;
        var delay = 1;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var address = await Discover(ct);
                string route = "/v1/chatMix";
                using (var response = await http.GetAsync(new Uri(address, route), ct))
                {
                    if (response.StatusCode == HttpStatusCode.NotFound) route = "/chatMix";
                    else response.EnsureSuccessStatusCode();
                }
                using (var initial = JsonDocument.Parse(await http.GetStringAsync(new Uri(address, route), ct))) Emit(initial.RootElement, "baseline");
                Status?.Invoke("Connected to Sonar"); delay = 1;
                using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var socket = Listen(address, session.Token);
                try
                {
                    while (!ct.IsCancellationRequested)
                    {
                        await Task.Delay(350, ct);
                        long revision;
                        lock (readingGate) revision = socketRevision;
                        using var doc = JsonDocument.Parse(await http.GetStringAsync(new Uri(address, route), ct));
                        // A socket reading arriving during this GET is newer than its snapshot.
                        lock (readingGate)
                            if (revision == socketRevision) Emit(doc.RootElement, "refresh");
                    }
                }
                finally { session.Cancel(); await socket; }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Status?.Invoke("Waiting for Sonar: " + ex.Message); }
            try { await Task.Delay(TimeSpan.FromSeconds(delay), ct); } catch (OperationCanceledException) { break; }
            delay = Math.Min(30, delay * 2);
        }
    }
    private async Task Listen(Uri address, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                ws.Options.Proxy = null;
                await ws.ConnectAsync(new UriBuilder(address) { Scheme = "ws", Path = "/sock" }.Uri, ct);
                var buffer = new byte[16384];
                using var message = new MemoryStream();
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (message.Length + result.Count > 2 * 1024 * 1024) throw new InvalidDataException("Oversized Sonar message.");
                    message.Write(buffer, 0, result.Count);
                    if (!result.EndOfMessage) continue;
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using var doc = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                        var root = doc.RootElement;
                        if (root.TryGetProperty("event", out var e) && e.GetString() == "EVENT_SONAR_CHATMIX_DATA" && root.TryGetProperty("data", out var data)) Emit(data, "wheel/socket");
                    }
                    message.SetLength(0);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { Status?.Invoke("Socket unavailable; read-only refresh active: " + ex.Message); }
            try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { return; }
        }
    }
    public void Dispose() { stop.Cancel(); }
}
