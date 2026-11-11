using System.Text;
using System.Text.Json;
namespace ByteEngine.Core.Net;
public static class NetworkDiagnostics
{
 static readonly Queue<string> lines=new();static readonly object gate=new();
 public static string LastError {get;private set;}="";
 public static Action<string,string>? Download {get;set;}
 public static void Record(string message,bool error=false){lock(gate){if(lines.Count>=256)lines.Dequeue();lines.Enqueue(DateTime.UtcNow.ToString("O")+" "+message);if(error)LastError=message;}}
 public static string Export(){lock(gate)return "ByteEngine multiplayer diagnostics\nSource: "+NetworkBuildIdentity.Value+"\n"+string.Join("\n",lines);}
 public static void ClearError(){lock(gate)LastError="";}
 public static void Save(){string name="ByteEngine-network-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".log";if(Download!=null){Download(name,Export());return;}string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");Directory.CreateDirectory(folder);string file=Path.Combine(folder,name);File.WriteAllText(file,Export());Record("Saved log: "+file);}
}
public sealed record LobbyRoom(string Id,string Name,int Players);
public sealed class NetworkLobbyClient : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly string address;
    private readonly string token = Guid.NewGuid().ToString("N");
    private readonly Dictionary<int, string> destinations = new();
    private readonly Dictionary<string, int> connections = new();
    private readonly Queue<(string Destination, NetworkSignal Signal)> signals = new();
    private Task<string>? request;
    private Action<string>? completion;
    private Action? pendingOperation;
    private bool refreshPending, operationPending, host, disposed;
    private double timer;
    private string room = "";

    public IReadOnlyList<LobbyRoom> Rooms { get; private set; } = [];
    public string Status { get; private set; } = "Ready";
    public string RoomCode => room;
    public bool IsOperationPending => operationPending;

    public NetworkLobbyClient(string address)
    {
        this.address = address.TrimEnd('/');
        NetworkRuntime.Signal += Signal;
    }

    private void Call(string route, object? body, Action<string> complete)
    {
        if (request != null) throw new InvalidOperationException("Overlapping internal lobby request");
        completion = complete;
        request = Fetch(route, body);
    }

    private async Task<string> Fetch(string route, object? body)
    {
        using var response = body == null
            ? await http.GetAsync(address + route)
            : await http.PostAsync(address + route, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
        string text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            string detail = text;
            try { using var json = JsonDocument.Parse(text); detail = json.RootElement.GetProperty("error").GetString() ?? text; }
            catch (JsonException) { }
            catch (KeyNotFoundException) { }
            throw new HttpRequestException($"Lobby HTTP {(int)response.StatusCode}: {detail}");
        }
        return text;
    }

    public void Refresh() => refreshPending = true;

    public void Host(string name)
    {
        if (operationPending) return;
        operationPending = true;
        pendingOperation = () => LeaveThen(() =>
        {
            host = true;
            Call("/rooms", new { token, name }, text =>
            {
                using var json = JsonDocument.Parse(text);
                room = json.RootElement.GetProperty("id").GetString()!;
                operationPending = false;
                Status = "Hosting " + room;
                NetworkDiagnostics.Record(Status);
            });
        });
    }

    public void Join(string id)
    {
        if (operationPending) return;
        operationPending = true;
        pendingOperation = () => LeaveThen(() =>
        {
            host = false;
            Call("/join", new { token, id }, text =>
            {
                using var json = JsonDocument.Parse(text);
                room = id;
                string remote = json.RootElement.GetProperty("host").GetString()!;
                int connection = NetworkRuntime.OpenConnection(false);
                destinations[connection] = remote;
                connections[remote] = connection;
                operationPending = false;
                Status = "Joining " + room;
                NetworkDiagnostics.Record(Status);
            });
        });
    }

    private void LeaveThen(Action next)
    {
        destinations.Clear();
        connections.Clear();
        signals.Clear();
        if (room.Length == 0) { next(); return; }
        Call("/leave", new { token }, _ => { room = ""; next(); });
    }

    private void Signal(int connection, NetworkSignal signal)
    {
        if (!destinations.TryGetValue(connection, out var destination)) return;
        if (signals.Count >= 128) { NetworkDiagnostics.Record("Signaling queue overflow", true); return; }
        signals.Enqueue((destination, signal));
    }

    public void Poll(double delta)
    {
        if (disposed) return;
        timer += delta;
        if (request is { IsCompleted: true })
        {
            var finished = request;
            var callback = completion;
            request = null;
            completion = null;
            try { callback?.Invoke(finished.GetAwaiter().GetResult()); }
            catch (Exception error)
            {
                operationPending = false;
                pendingOperation = null;
                Status = error.Message;
                NetworkDiagnostics.Record("Lobby: " + error.Message, true);
            }
        }
        if (request != null) return;
        if (pendingOperation != null)
        {
            var next = pendingOperation;
            pendingOperation = null;
            next();
            return;
        }
        if (refreshPending)
        {
            refreshPending = false;
            Call("/rooms", null, text =>
            {
                Rooms = JsonSerializer.Deserialize<LobbyRoom[]>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                Status = "Match list refreshed";
            });
            return;
        }
        if (room.Length == 0 || timer < .1) return;
        timer = 0;
        if (signals.TryDequeue(out var signal))
        {
            Call("/signal", new { token, to = signal.Destination, signal = signal.Signal }, _ => { });
            return;
        }
        Call("/poll?token=" + token, null, ProcessMessages);
    }

    private void ProcessMessages(string text)
    {
        using var json = JsonDocument.Parse(text);
        foreach (var message in json.RootElement.EnumerateArray())
        {
            string kind = message.GetProperty("kind").GetString()!;
            string from = message.GetProperty("from").GetString()!;
            if (kind == "join" && host && !connections.ContainsKey(from))
            {
                int connection = NetworkRuntime.OpenConnection(true);
                connections[from] = connection;
                destinations[connection] = from;
            }
            else if (kind == "signal" && connections.TryGetValue(from, out int connection))
                NetworkRuntime.Accept(connection, message.GetProperty("signal").Deserialize<NetworkSignal>());
            else if (kind == "left" && connections.Remove(from, out int closed))
            {
                NetworkRuntime.CloseConnection(closed);
                destinations.Remove(closed);
                if (!host) room = "";
            }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        NetworkRuntime.Signal -= Signal;
        // Dispose only after the leave attempt, including any in-flight polling request.
        _ = LeaveAndDispose();
        pendingOperation = null;
    }

    private async Task LeaveAndDispose()
    {
        try
        {
            if (request != null) { try { await request; } catch { } }
            using var response = await http.PostAsync(address + "/leave", new StringContent(JsonSerializer.Serialize(new { token }), Encoding.UTF8, "application/json"));
        }
        catch (Exception error) { NetworkDiagnostics.Record("Lobby leave: " + error.Message); }
        finally { http.Dispose(); }
    }
}
