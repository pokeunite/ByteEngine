namespace ByteEngine.Core.Net;
public enum NetworkChannel { State, Events }
public readonly record struct NetworkSignal(string Kind, string Content, string Mid = "");
/// <summary>A single authenticated session link. Poll dispatches all callbacks on its calling thread.</summary>
public interface INetworkLink : IDisposable
{
    bool IsConnected { get; }
    event Action<NetworkSignal>? Signal;
    event Action<NetworkChannel, byte[]>? Message;
    event Action<string>? Disconnected;
    void Start(bool offerer);
    void Accept(NetworkSignal signal);
    bool Send(NetworkChannel channel, ReadOnlySpan<byte> payload);
    void Poll();
}
