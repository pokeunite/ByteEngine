using System.Collections.Concurrent;
using ByteEngine.Core.Net;
using DataChannelDotnet;
using DataChannelDotnet.Impl;
using DataChannelDotnet.Data;
namespace ByteEngine.Network.Native;

/// <summary>WebRTC data channels compatible with the browser link. Native callbacks only enqueue.</summary>
public sealed class WebRtcNetworkLink : INetworkLink
{
    static int activeLinks;
    readonly IRtcPeerConnection peer;
    readonly ConcurrentQueue<Action> incoming = new();
    readonly Dictionary<NetworkChannel, IRtcDataChannel> channels = new();
    readonly object gate = new();
    long sendStamp=System.Diagnostics.Stopwatch.GetTimestamp();int sentInWindow;
    int queued, overflow; volatile bool disposed; bool started, notified;
    public bool IsConnected { get { lock(gate) return !disposed && channels.Count == 2 && channels.Values.All(c=>c.IsOpen); } }
    public event Action<NetworkSignal>? Signal;
    public event Action<NetworkChannel,byte[]>? Message;
    public event Action<string>? Disconnected;
    public WebRtcNetworkLink(string[]? iceServers = null)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("This native networking build supports Windows; use the browser runtime on other platforms");
        if(Interlocked.Increment(ref activeLinks)==1)RtcTools.Preload();
        peer = new RtcPeerConnection(new RtcPeerConfiguration { IceServers=iceServers ?? [], MaxMessageSize=65536 });
        peer.OnLocalDescriptionSafe += (_,d)=>Enqueue(()=>Signal?.Invoke(new(d.Type.ToString().ToLowerInvariant(),d.Sdp)));
        peer.OnCandidateSafe += (_,c)=>Enqueue(()=>Signal?.Invoke(new("candidate",c.Content,c.Mid)));
        peer.OnDataChannel += (_,c)=>Bind(c);
        peer.OnConnectionStateChange += (_,s)=> { if(s.ToString().Contains("FAILED",StringComparison.OrdinalIgnoreCase) || s.ToString().Contains("CLOSED",StringComparison.OrdinalIgnoreCase)) Enqueue(()=>End(s.ToString()??"connection closed")); };
    }
    void Enqueue(Action action) { if(disposed)return; if(Interlocked.Increment(ref queued)>256){Interlocked.Decrement(ref queued);Interlocked.Exchange(ref overflow,1);return;} incoming.Enqueue(action); }
    void End(string reason) { if(notified)return; notified=true; Disconnected?.Invoke(reason); }
    void Bind(IRtcDataChannel channel)
    {
        NetworkChannel kind;
        if(channel.Label=="ben-state")kind=NetworkChannel.State;
        else if(channel.Label=="ben-events")kind=NetworkChannel.Events;
        else { channel.Dispose(); return; }
        lock(gate) { if(disposed || channels.ContainsKey(kind)){channel.Dispose();return;} channels.Add(kind,channel); }
        channel.OnBinaryReceivedSafe += (_,data)=> { if(data.Data.Length>65536){Interlocked.Exchange(ref overflow,1);return;} var bytes=data.Data.ToArray(); Enqueue(()=>Message?.Invoke(kind,bytes)); };
        channel.OnClose += _=>Enqueue(()=>End("channel closed"));
        channel.OnError += (_,error)=>Enqueue(()=>End(error??"data channel error"));
    }
    public void Start(bool offerer)
    {
        ObjectDisposedException.ThrowIf(disposed,this); if(started)throw new InvalidOperationException("Link already started"); started=true;
        if(!offerer)return;
        Bind(peer.CreateDataChannel(new(){Label="ben-state",Unordered=true,Unreliable=true,MaxRetransmits=0}));
        Bind(peer.CreateDataChannel(new(){Label="ben-events"}));
    }
    public void Accept(NetworkSignal signal)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(signal.Content.Length>131072)throw new ArgumentException("Signaling message too large");
        if(signal.Kind=="candidate")peer.AddRemoteCandidate(new(){Content=signal.Content,Mid=signal.Mid});
        else if(signal.Kind is "offer" or "answer")peer.SetRemoteDescription(new(){Sdp=signal.Content,Type=Enum.Parse<RtcDescriptionType>(signal.Kind,true)});
        else throw new ArgumentException("Unknown signaling message");
    }
    public bool Send(NetworkChannel channel,ReadOnlySpan<byte> payload)
    {
        if(payload.Length> (channel==NetworkChannel.State?1200:65536))return false;
        lock(gate) { if(disposed || !channels.TryGetValue(channel,out var target) || !target.IsOpen)return false;if(System.Diagnostics.Stopwatch.GetElapsedTime(sendStamp).TotalSeconds>=1){sendStamp=System.Diagnostics.Stopwatch.GetTimestamp();sentInWindow=0;}if(sentInWindow+payload.Length>131072)return false; try { target.Send(payload);sentInWindow+=payload.Length; return true; } catch(Exception error) when(error is not OutOfMemoryException) { Enqueue(()=>End(error.Message));return false; } }
    }
    public void Poll(){if(Interlocked.Exchange(ref overflow,0)!=0){End("receive queue capacity exceeded");Dispose();return;}int budget=256;while(budget-->0 && incoming.TryDequeue(out var action)){Interlocked.Decrement(ref queued);action();}}
    public void Dispose(){IRtcDataChannel[] close;lock(gate){if(disposed)return;disposed=true;close=channels.Values.ToArray();channels.Clear();}foreach(var c in close)c.Dispose();peer.Dispose();while(incoming.TryDequeue(out _)){}if(Interlocked.Decrement(ref activeLinks)==0)RtcTools.Cleanup(); }
}
