using ByteEngine.Core.Net;
using System.Collections.Concurrent;
internal sealed record BrowserNetworkCommand(int id,string kind,int channel=0,int buffer=-1,bool offerer=false,NetworkSignal? signal=null);
internal sealed class BrowserNetworkLink : INetworkLink
{
    internal static readonly Dictionary<int,BrowserNetworkLink> Links=[];
    internal static readonly Queue<(BrowserNetworkCommand Command,byte[]? Bytes)> Outbound=new();
    static int nextId;readonly int id=++nextId;readonly Queue<Action> inbound=new();bool closed,started;int pendingBytes;
    public bool IsConnected {get;private set;}
    public event Action<NetworkSignal>? Signal;public event Action<NetworkChannel,byte[]>? Message;public event Action<string>? Disconnected;
    public BrowserNetworkLink(){Links.Add(id,this);}
    public void Start(bool offerer){if(started)throw new InvalidOperationException("Already started");started=true;Outbound.Enqueue((new(id,"start",offerer:offerer),null));}
    public void Accept(NetworkSignal signal){if(signal.Content.Length>131072)throw new ArgumentException("Signaling too large");Outbound.Enqueue((new(id,"signal",signal:signal),null));}
    public bool Send(NetworkChannel channel,ReadOnlySpan<byte> payload){if(closed||!IsConnected||payload.Length>(channel==NetworkChannel.State?1200:65536)||pendingBytes+payload.Length>131072||Outbound.Count>=256)return false;pendingBytes+=payload.Length;Outbound.Enqueue((new(id,"send",channel:(int)channel),payload.ToArray()));return true;}
    internal void Flushed()=>pendingBytes=0;
    internal void Receive(int channel,byte[] bytes){if(closed)return;if(bytes.Length>65536||inbound.Count>=256){Notify("error","receive queue capacity exceeded");return;}var copy=bytes.ToArray();inbound.Enqueue(()=>Message?.Invoke((NetworkChannel)channel,copy));}
    internal void Notify(string kind,string content){if(closed)return;if(kind=="connected"){IsConnected=true;return;}if(kind=="signal"){var signal=System.Text.Json.JsonSerializer.Deserialize<NetworkSignal>(content);if(inbound.Count<256)inbound.Enqueue(()=>Signal?.Invoke(signal));return;}IsConnected=false;inbound.Clear();inbound.Enqueue(()=>Disconnected?.Invoke(content));}
    public void Poll(){int n=256;while(n-->0&&inbound.TryDequeue(out var callback))callback();}
    public void Dispose(){if(closed)return;closed=true;IsConnected=false;inbound.Clear();Links.Remove(id);Outbound.Enqueue((new(id,"close"),null));}
}
public static partial class BrowserGame
{
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static void NetworkReceive(int id,int channel,[System.Runtime.InteropServices.JavaScript.JSMarshalAs<System.Runtime.InteropServices.JavaScript.JSType.Array<System.Runtime.InteropServices.JavaScript.JSType.Number>>] byte[] bytes){if(BrowserNetworkLink.Links.TryGetValue(id,out var link)&&channel is 0 or 1)link.Receive(channel,bytes);}
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static void NetworkNotify(int id,string kind,string content){if(BrowserNetworkLink.Links.TryGetValue(id,out var link))link.Notify(kind,content);}
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static string NetworkExportLog()=>NetworkDiagnostics.Export();
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static void NetworkStart(bool host){var settings=Loop.Scenes.ActiveScene?.GameObjects.Select(o=>o.GetComponent<NetworkSettings>()).FirstOrDefault(s=>s!=null)??throw new InvalidOperationException("Add Network Settings to this scene");settings.StartSession(host);}
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static int NetworkOpen(bool offerer)=>NetworkRuntime.OpenConnection(offerer);
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static void NetworkSignal(int id,string kind,string content,string mid)=>NetworkRuntime.Accept(id,new(kind,content,mid));
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static void NetworkStop()=>NetworkRuntime.Stop();
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static bool NetworkAvailable()=> (Loop.Scenes.ActiveScene?.GameObjects.Any(o=>o.GetComponent<NetworkSettings>()!=null)??false)&&!(Loop.Scenes.ActiveScene?.GameObjects.Any(o=>o.Components.Any(c=>c.GetType().Name=="PvpMatch3D"))??false);
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static string NetworkCompatibilityInfo()=>System.Text.Json.JsonSerializer.Serialize(Loop.Scenes.ActiveScene!.GameObjects.Select(o=>o.GetComponent<NetworkSettings>()).First(s=>s!=null)!.Compatibility());
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static string NetworkState()=>System.Text.Json.JsonSerializer.Serialize(new {players=NetworkRuntime.Session?.Players.Values.Select(p=>new{id=p.PeerId,x=p.Position.X,y=p.Position.Y,z=p.Position.Z}).ToArray(),predictedX=NetworkRuntime.Session?.PredictedLocal.Position.X??0,engine=NetworkBuildIdentity.Value});
    [System.Runtime.InteropServices.JavaScript.JSExport]
    public static string NetworkStatus()=>System.Text.Json.JsonSerializer.Serialize(new {active=NetworkRuntime.Session?.IsActive??false,host=NetworkRuntime.Session?.IsHost??false,peer=NetworkRuntime.Session?.LocalPeerId??-1,players=NetworkRuntime.Session?.Players.Count??0,sentBytes=NetworkRuntime.Session?.SentBytes??0,receivedBytes=NetworkRuntime.Session?.ReceivedBytes??0,rttMs=NetworkRuntime.Session?.RoundTripMilliseconds??0,corrections=NetworkRuntime.Session?.Corrections??0,iceServers=NetworkRuntime.IceServers});
}
