using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
namespace ByteEngine.Core.Net;

public readonly record struct NetworkPlayerState(int PeerId, uint AcknowledgedInput, Vector3 Position, Vector3 Velocity);
public sealed record NetworkCompatibility(string Engine, string Plugins, string Scene)
{
    internal byte[] Bytes() => SHA256.HashData(Encoding.UTF8.GetBytes(Engine)).Concat(SHA256.HashData(Encoding.UTF8.GetBytes(Plugins))).Concat(SHA256.HashData(Encoding.UTF8.GetBytes(Scene))).ToArray();
}
/// <summary>Replayable movement step. Collision queries can constrain the proposed position on both peers.</summary>
public sealed class NetworkCharacterMotor
{
    public float Speed { get; set; } = 6;
    public Func<int, Vector3, Vector3, Vector3>? ConstrainPlayerMovement { get; set; }
    public Func<Vector3, Vector3, Vector3>? ConstrainMovement { get; set; }
    public NetworkPlayerState Step(NetworkPlayerState state, PlayerInput input, float dt)
    {
        var velocity = new Vector3(input.Move.X, 0, input.Move.Y) * Speed;
        var proposed = state.Position + velocity * dt;
        var position = ConstrainPlayerMovement?.Invoke(state.PeerId, state.Position, proposed) ?? ConstrainMovement?.Invoke(state.Position, proposed) ?? proposed;
        return state with { Position = position, Velocity = (position - state.Position) / dt, AcknowledgedInput = input.Sequence };
    }
}
/// <summary>Host-authoritative fixed-tick star session. No scene mutation or callbacks occur on socket threads.</summary>
public sealed class NetworkSession : IDisposable
{
    sealed class Remote(INetworkLink link) { public readonly INetworkLink Link = link; public int Id = -1; public bool Accepted; public double Age, Silence, PingAge; public long PingStamp; public double RequestWindow; public int RequestCount; public uint EventSequence; public bool HasInput, HasSeen; public PlayerInput Input; public uint LastSeen; public readonly Queue<PlayerInput> Inputs=new(8); public double InputAge, Budget=2; }
    readonly List<Remote> links = [];
    readonly Dictionary<int, NetworkPlayerState> players = [];
    readonly Queue<PlayerInput> pending = new();
    readonly byte[] identity;
    readonly int ownerThread = Environment.CurrentManagedThreadId;
    readonly Dictionary<int, Vector3> spawns = [];
    public event Action<INetworkLink>? LinkClosed;
    public void CloseLink(INetworkLink link){var remote=links.FirstOrDefault(r=>ReferenceEquals(r.Link,link));if(remote!=null)Remove(remote,"connection closed");}
    public void Teleport(int peer,Vector3 position){Owner();if(!IsHost||!Finite(position))throw new InvalidOperationException("Only host can teleport");if(players.TryGetValue(peer,out var state))players[peer]=state with{Position=position,Velocity=Vector3.Zero};}
    public Vector3 PredictionErrorOffset {get;private set;}
    readonly NetworkPlayerState[] decodedStates=new NetworkPlayerState[MaxPeers];
    public void SetSpawnPosition(int peerId, Vector3 position) { Owner(); if (!Finite(position)) throw new ArgumentException("Invalid spawn position"); spawns[peerId] = position; if (IsHost && players.TryGetValue(peerId, out var state)) players[peerId] = state with { Position = position }; }
    double accumulator; uint sequence, tick, lastSnapshot; bool seenSnapshot, disposed;
    public const int MaxPeers = 4;
    public const float FixedDelta = 1f / 30;
    public long SentBytes { get; private set; }
    public long ReceivedBytes { get; private set; }
    public long SentPackets { get; private set; }
    public long ReceivedPackets { get; private set; }
    public double RoundTripMilliseconds { get; private set; }
    public int Corrections { get; private set; }
    public bool IsHost { get; }
    public bool IsActive { get; private set; } = true;
    public int LocalPeerId { get; private set; } = -1;
    public IReadOnlyDictionary<int, NetworkPlayerState> Players => players;
    public NetworkPlayerState PredictedLocal { get; private set; }
    public NetworkCharacterMotor Motor { get; } = new();
    public Func<PlayerInput> ReadInput { get; set; } = () => default;
    public event Action<int>? PeerJoined;
    public event Action<int>? PeerLeft;
    public event Action<string>? SessionEnded;
    public event Action<int, string>? PeerRejected;
    public event Action? SnapshotApplied;
    public event Action<int, PlayerInput>? AuthoritativeInput;
    public event Action<int, ushort, byte[]>? IntentReceived;
    public event Action<ushort, byte[]>? EventReceived;
    public Func<NetworkPlayerState, PlayerInput, float, NetworkPlayerState>? SimulatePlayer { get; set; }
    uint eventSequence;
    NetworkPlayerState Step(NetworkPlayerState state, PlayerInput input) => SimulatePlayer?.Invoke(state, input, FixedDelta) ?? Motor.Step(state, input, FixedDelta);
    public bool SendIntent(ushort kind, ReadOnlySpan<byte> data) { Owner(); if (IsHost || data.Length > 512 || links.Count == 0 || !links[0].Accepted) return false; Span<byte> packet = stackalloc byte[515]; packet[0] = 9; BinaryPrimitives.WriteUInt16LittleEndian(packet[1..], kind); data.CopyTo(packet[3..]); return Send(links[0], NetworkChannel.Events, packet[..(3 + data.Length)]); }
    public void BroadcastEvent(ushort kind, ReadOnlySpan<byte> data) { Owner(); if (!IsHost) throw new InvalidOperationException("Only the host publishes authoritative events"); if (data.Length > 40000) throw new ArgumentException("Event too large"); byte[] packet = new byte[7 + data.Length]; packet[0] = 7; BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1), ++eventSequence); BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(5), kind); data.CopyTo(packet.AsSpan(7)); foreach (var r in links.Where(x => x.Accepted).ToArray()) if (!Send(r, NetworkChannel.Events, packet)) Remove(r, "reliable event backpressure"); }

    public NetworkSession(bool host, NetworkCompatibility compatibility)
    {
        IsHost = host; identity = compatibility.Bytes();
        if (host) { LocalPeerId = 0; players[0] = new(0, 0, Vector3.Zero, Vector3.Zero); }
    }
    void Owner() { if (Environment.CurrentManagedThreadId != ownerThread) throw new InvalidOperationException("Session must run on its owner thread"); ObjectDisposedException.ThrowIf(disposed, this); }
    public void AddLink(INetworkLink link, bool offerer)
    {
        Owner(); if (!IsActive || links.Count >= (IsHost ? MaxPeers - 1 : 1)) throw new InvalidOperationException("Session capacity reached");
        var remote = new Remote(link); links.Add(remote);
        link.Message += (channel, data) => Receive(remote, channel, data);
        link.Disconnected += reason => Remove(remote, IsHost ? reason : "host left");
        try{link.Start(offerer);}catch{links.Remove(remote);link.Dispose();throw;}
    }

    /// <summary>Signaling progress proves the peer is alive, so the handshake window restarts on every signal.</summary>
    public void NoteSignal(INetworkLink link)
    {
        if (disposed) return;
        var remote = links.FirstOrDefault(r => ReferenceEquals(r.Link, link));
        if (remote == null) return;
        remote.Silence = 0; remote.Age = Math.Min(remote.Age, 0);
    }
    public void Update(double elapsed)
    {
        Owner(); if (!IsActive) return;
        if (!double.IsFinite(elapsed) || elapsed < 0) throw new ArgumentOutOfRangeException(nameof(elapsed));
        Span<byte> ping = stackalloc byte[16];
        foreach (var r in links.ToArray())
        {
            r.Link.Poll(); if (!links.Contains(r)) continue; r.Age += elapsed; r.Silence += elapsed;r.InputAge+=elapsed;r.Budget=Math.Min(3,r.Budget+elapsed/FixedDelta); r.RequestWindow += elapsed; if (r.RequestWindow >= 1) { r.RequestWindow = 0; r.RequestCount = 0; }
            if (!r.Accepted && !IsHost && r.Link.IsConnected && r.Age >= 0) { byte[] hello = new byte[100]; hello[0] = 2; hello[1] = 1; identity.CopyTo(hello, 4); if (Send(r, NetworkChannel.Events, hello)) r.Age = -1e6; }
            if (r.Accepted) { r.PingAge += elapsed; if (r.PingAge >= 1) { r.PingAge = 0; ping.Clear(); ping[0] = 10; r.PingStamp = System.Diagnostics.Stopwatch.GetTimestamp(); BinaryPrimitives.WriteInt64LittleEndian(ping[8..], r.PingStamp); Send(r, NetworkChannel.State, ping); } }
            // Relayed signaling plus intercontinental ICE routinely needs more than fifteen seconds.
            if ((!r.Accepted && r.Silence > 60) || (r.Accepted && r.Silence > 10)) Remove(r, "connection timed out");
        }
        PredictionErrorOffset*=MathF.Exp(-12*(float)Math.Min(elapsed,.25));
        accumulator += Math.Min(elapsed, .25); int steps = 0;
        while (accumulator >= FixedDelta && steps++ < 8 && IsActive) { accumulator -= FixedDelta; Tick(); }
    }
    bool Send(Remote remote, NetworkChannel channel, ReadOnlySpan<byte> packet) { if (!remote.Link.Send(channel, packet)) return false; SentBytes += packet.Length; SentPackets++; return true; }
    PlayerInput LocalInput() { var i = ReadInput(); var result = i with { Tick = tick, Sequence = ++sequence, Buttons = i.Buttons & 255 }; Span<byte> b = stackalloc byte[36]; return NetworkInputCodec.TryWrite(result, b) ? result : result with { Move = Vector2.Zero, Look = Vector2.Zero, Buttons = 0 }; }
    void Tick()
    {
        tick++;
        if (LocalPeerId < 0) return;
        var input = LocalInput();
        if (IsHost)
        {
            players[0] = Step(players[0], input);
            AuthoritativeInput?.Invoke(0, input);
            foreach (var r in links.Where(x => x.Accepted).ToArray()) if (players.TryGetValue(r.Id, out var state))
                {
                    int applied=0;
                    while(applied<2 && r.Budget>=1 && r.Inputs.TryDequeue(out var command)){
                        players[r.Id]=Step(players[r.Id],command);AuthoritativeInput?.Invoke(r.Id,command);r.Input=command;r.Budget--;applied++;
                    }
                    if(applied==0 && r.InputAge<.4 && r.Budget>=1){var held=r.Input with{Buttons=0};players[r.Id]=Step(state,held);r.Budget--;}
                    r.HasInput=false;
                }
            SendSnapshot();
        }
        else
        {
            if (pending.Count >= 128) { pending.Clear(); PredictedLocal = players.GetValueOrDefault(LocalPeerId); }
            pending.Enqueue(input); PredictedLocal = Step(PredictedLocal, input);
            // Redundancy across adjacent unreliable packets recovers isolated loss.
            var recent = pending.TakeLast(3).ToArray(); Span<byte> packet = stackalloc byte[110]; packet[0] = 4; packet[1] = (byte)recent.Length;
            for (int n = 0; n < recent.Length; n++) NetworkInputCodec.TryWrite(recent[n], packet[(2 + n * 36)..]);
            if (links.Count > 0) Send(links[0], NetworkChannel.State, packet[..(2 + recent.Length * 36)]);
        }
    }
    void Receive(Remote r, NetworkChannel channel, byte[] packet)
    {
        if (!IsActive || !links.Contains(r) || packet.Length < 1) return;
        ReceivedBytes += packet.Length; ReceivedPackets++;
        if (packet[0] == 2 && IsHost && channel == NetworkChannel.Events)
        {
            if (r.Accepted || packet.Length != 100 || packet[1] != 1 || packet[2] != 0 || packet[3] != 0) return;
            string? mismatch = null; for (int n = 0; n < 3; n++) if (!packet.AsSpan(4 + n * 32, 32).SequenceEqual(identity.AsSpan(n * 32, 32))) mismatch = new[] { "engine mismatch", "plugin mismatch", "scene mismatch" }[n];
            if (mismatch != null) { Reject(r, mismatch); return; }
            r.Id = Enumerable.Range(1, MaxPeers - 1).First(id => !players.ContainsKey(id)); r.Accepted = true; r.Silence = 0; players[r.Id] = new(r.Id, 0, spawns.GetValueOrDefault(r.Id), Vector3.Zero);
            byte[] welcome = new byte[20]; welcome[0] = 3; welcome[1] = 1; BinaryPrimitives.WriteInt32LittleEndian(welcome.AsSpan(4), r.Id);Vector(welcome.AsSpan(8),players[r.Id].Position); if (!Send(r, NetworkChannel.Events, welcome)) { Remove(r, "welcome backpressure"); return; }
            PeerJoined?.Invoke(r.Id); SendSnapshot(); return;
        }
        if (!IsHost && packet[0] == 3 && channel == NetworkChannel.Events && packet.Length == 20 && packet[1] == 1 && BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4)) > 0 && !r.Accepted) { r.Accepted = true; r.Id = 0; r.Silence = 0; LocalPeerId = BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(4)); PredictedLocal = new(LocalPeerId, 0, Vector(packet.AsSpan(8)), Vector3.Zero);players[LocalPeerId]=PredictedLocal; PeerJoined?.Invoke(LocalPeerId); return; }
        if (!IsHost && packet[0] == 6 && channel == NetworkChannel.Events && packet.Length <= 256) { End(Encoding.UTF8.GetString(packet, 1, packet.Length - 1)); return; }
        if (!r.Accepted) return;
        if (channel == NetworkChannel.State && packet.Length == 16 && packet[0] is 10 or 11) { long stamp = BinaryPrimitives.ReadInt64LittleEndian(packet.AsSpan(8)); if (packet[0] == 10) { packet[0] = 11; Send(r, NetworkChannel.State, packet); } else if (stamp == r.PingStamp) RoundTripMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(stamp).TotalMilliseconds; r.Silence = 0; return; }
        if (IsHost && packet[0] == 9 && channel == NetworkChannel.Events && packet.Length is >= 3 and <= 515) { if (++r.RequestCount <= 32) { r.Silence = 0; IntentReceived?.Invoke(r.Id, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(1)), packet.AsSpan(3).ToArray()); } return; }
        if (!IsHost && packet[0] == 7 && channel == NetworkChannel.Events && packet.Length is >= 7 and <= 40007) { uint seq = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(1)); if (NetworkInputCodec.IsNewer(seq, r.EventSequence)) { r.EventSequence = seq; r.Silence = 0; EventReceived?.Invoke(BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(5)), packet.AsSpan(7).ToArray()); } return; }
        if (IsHost && packet[0] == 4 && channel == NetworkChannel.State)
        {
            if (packet.Length < 2 || packet[1] is < 1 or > 3 || packet.Length != 2 + packet[1] * 36) return;
            for (int n = 0; n < packet[1]; n++) if (NetworkInputCodec.TryRead(packet.AsSpan(2 + n * 36, 36), out var input) && (!r.HasSeen || NetworkInputCodec.IsNewer(input.Sequence, r.LastSeen)) && (input.Buttons & ~255u) == 0)
                {
                    // Per-link identity and one consumed command per host tick prevent fabricated owners and tick acceleration.
                    if (r.HasSeen && unchecked(input.Sequence - r.LastSeen) > 128) continue;
                    if(r.Inputs.Count>=8)continue;r.Inputs.Enqueue(input);r.LastSeen=input.Sequence;r.HasSeen=true;r.HasInput=true;r.InputAge=0;r.Silence=0;
                }
            return;
        }
        if (!IsHost && packet[0] == 5 && channel == NetworkChannel.State) ApplySnapshot(r, packet);
    }
    void Reject(Remote remote, string reason) { byte[] text = Encoding.UTF8.GetBytes(reason); byte[] packet = new byte[text.Length + 1]; packet[0] = 6; text.CopyTo(packet, 1); Send(remote, NetworkChannel.Events, packet); PeerRejected?.Invoke(remote.Id, reason); remote.Age = 0; }
    void SendSnapshot()
    {
        Span<byte> packet = stackalloc byte[8 + MaxPeers * 32]; packet[0] = 5; packet[1] = (byte)players.Count; packet[2] = packet[3] = 0; BinaryPrimitives.WriteUInt32LittleEndian(packet[4..], tick); int offset = 8;
        foreach (var state in players.Values) { BinaryPrimitives.WriteInt32LittleEndian(packet[offset..], state.PeerId); BinaryPrimitives.WriteUInt32LittleEndian(packet[(offset + 4)..], state.AcknowledgedInput); Vector(packet[(offset + 8)..], state.Position); Vector(packet[(offset + 20)..], state.Velocity); offset += 32; }
        foreach (var r in links.Where(x => x.Accepted).ToArray()) Send(r, NetworkChannel.State, packet[..offset]);
    }
    void ApplySnapshot(Remote r, byte[] packet)
    {
        if (packet.Length < 8 || packet[1] is < 1 or > MaxPeers || packet.Length != 8 + packet[1] * 32 || packet[2] != 0 || packet[3] != 0) return;
        uint incomingTick = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)); if (seenSnapshot && !NetworkInputCodec.IsNewer(incomingTick, lastSnapshot)) return;
        int mask=0,count=0;
        for(int o=8;o<packet.Length;o+=32){int id=BinaryPrimitives.ReadInt32LittleEndian(packet.AsSpan(o));var pos=Vector(packet.AsSpan(o+8));var vel=Vector(packet.AsSpan(o+20));if(id<0||id>=MaxPeers||!Finite(pos)||!Finite(vel)||(mask&(1<<id))!=0)return;mask|=1<<id;decodedStates[count++]=new(id,BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(o+4)),pos,vel);}
        if((mask&1)==0||(mask&(1<<LocalPeerId))==0)return;
        for(int id=0;id<MaxPeers;id++)if((mask&(1<<id))==0&&players.Remove(id))PeerLeft?.Invoke(id);
        for(int n=0;n<count;n++){var state=decodedStates[n];bool added=!players.ContainsKey(state.PeerId);players[state.PeerId]=state;if(added&&state.PeerId!=LocalPeerId)PeerJoined?.Invoke(state.PeerId);}

        r.Silence = 0; seenSnapshot = true; lastSnapshot = incomingTick;
        var authority = players[LocalPeerId]; while (pending.TryPeek(out var command) && !NetworkInputCodec.IsNewer(command.Sequence, authority.AcknowledgedInput)) pending.Dequeue();
        var previousPrediction = PredictedLocal; PredictedLocal = authority; foreach (var command in pending) PredictedLocal = Step(PredictedLocal, command); float error=Vector3.DistanceSquared(previousPrediction.Position,PredictedLocal.Position);if(error>.0001f){Corrections++;PredictionErrorOffset=error<.36f?previousPrediction.Position-PredictedLocal.Position:Vector3.Zero;} SnapshotApplied?.Invoke();
    }
    void Remove(Remote remote, string reason) { if (!links.Remove(remote)) return; remote.Link.Dispose();LinkClosed?.Invoke(remote.Link); if (IsHost) { if (players.Remove(remote.Id)) PeerLeft?.Invoke(remote.Id); } else End(reason); }
    void End(string reason) { if (!IsActive) return; IsActive = false; foreach (var remote in links.ToArray()) remote.Link.Dispose(); links.Clear(); SessionEnded?.Invoke(reason); }
    public void Dispose() { if (disposed) return; foreach (var r in links.ToArray()) r.Link.Dispose(); links.Clear(); disposed = true; End(IsHost ? "host left" : "client left"); }
    static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    static void Vector(Span<byte> p, Vector3 v) { BinaryPrimitives.WriteSingleLittleEndian(p, v.X); BinaryPrimitives.WriteSingleLittleEndian(p[4..], v.Y); BinaryPrimitives.WriteSingleLittleEndian(p[8..], v.Z); }
    static Vector3 Vector(ReadOnlySpan<byte> p) => new(BinaryPrimitives.ReadSingleLittleEndian(p), BinaryPrimitives.ReadSingleLittleEndian(p[4..]), BinaryPrimitives.ReadSingleLittleEndian(p[8..]));
}
