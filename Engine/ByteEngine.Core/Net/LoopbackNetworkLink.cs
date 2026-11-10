namespace ByteEngine.Core.Net;
/// <summary>Bounded in-process transport with deterministic unreliable loss/jitter. Advance its clock explicitly.</summary>
public sealed class LoopbackNetworkLink : INetworkLink
{
    sealed class Packet { public readonly byte[] Bytes = new byte[65536]; public int Length; public double Due; public long Order; public NetworkChannel Channel; }
    readonly Packet[] slots = Enumerable.Range(0, 64).Select(_ => new Packet()).ToArray();
    readonly List<int> queued = new(64); readonly Random random;
    LoopbackNetworkLink? other; double clock, lastReliable; long order; bool started, disposed, remoteClosed, reported;
    public double Loss { get; set; }
    public double Latency { get; set; }
    public double Jitter { get; set; }
    public bool IsConnected => started && !disposed && other is { started: true, disposed: false };
    public event Action<NetworkSignal>? Signal { add { } remove { } }
    public event Action<NetworkChannel, byte[]>? Message;
    public event Action<string>? Disconnected;
    LoopbackNetworkLink(int seed) { random = new(seed); }
    public static (LoopbackNetworkLink A, LoopbackNetworkLink B) Pair(int seed = 1) { var a = new LoopbackNetworkLink(seed); var b = new LoopbackNetworkLink(seed + 1); a.other = b; b.other = a; return (a, b); }
    public void Advance(double seconds) { if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds)); clock += seconds; }
    public void Start(bool offerer) { ObjectDisposedException.ThrowIf(disposed, this); if (started) throw new InvalidOperationException("Already started"); started = true; }
    public void Accept(NetworkSignal signal) { }
    public bool Send(NetworkChannel channel, ReadOnlySpan<byte> payload)
    {
        if (!IsConnected || payload.Length > (channel == NetworkChannel.State ? 1200 : 65536)) return false;
        var target = other!; if (target.queued.Count >= 64) return false;
        if (channel == NetworkChannel.State && random.NextDouble() < Loss) return true;
        int index = 0; while (target.queued.Contains(index)) index++;
        var packet = target.slots[index]; payload.CopyTo(packet.Bytes); packet.Length = payload.Length; packet.Channel = channel; packet.Order = ++order;
        packet.Due = target.clock + Math.Max(0, Latency + (random.NextDouble() * 2 - 1) * Jitter);
        if (channel == NetworkChannel.Events) { packet.Due = Math.Max(packet.Due, target.lastReliable); target.lastReliable = packet.Due; }
        target.queued.Add(index); return true;
    }
    public void Poll()
    {
        if (disposed) return;
        if (remoteClosed && !reported) { reported = true; Disconnected?.Invoke("remote closed"); return; }
        int budget = 64;
        while (budget-- > 0)
        {
            int selected = -1; for (int n = 0; n < queued.Count; n++) { var p = slots[queued[n]]; if (p.Due <= clock && (selected < 0 || p.Due < slots[queued[selected]].Due || p.Due == slots[queued[selected]].Due && p.Order < slots[queued[selected]].Order)) selected = n; }
            if (selected < 0) return; int index = queued[selected]; queued.RemoveAt(selected); var packet = slots[index]; var copy = packet.Bytes.AsSpan(0, packet.Length).ToArray(); Message?.Invoke(packet.Channel, copy); if (disposed) return;
        }
    }
    public void Dispose() { if (disposed) return; disposed = true; queued.Clear(); if (other != null) other.remoteClosed = true; }
}
