namespace ByteEngine.Core.Runtime;

/// <summary>Exact-length, bounded storage for synchronous host frame transfers.
/// Handles remain valid until BeginFrame. Consumers must finish reading a frame
/// before starting the next; no asset backing array is retained or mutated.</summary>
public sealed class FrameTransferBuffers
{
    private readonly List<byte[]> _active=new();
    private readonly Dictionary<int,Stack<byte[]>> _available=new();
    private readonly long _maximumRetainedBytes;
    public int Count=>_active.Count;
    public long ActiveBytes {get;private set;}
    public long RetainedBytes {get;private set;}
    public long CreatedBytes {get;private set;}
    public long ReuseHits {get;private set;}
    public FrameTransferBuffers(long maximumRetainedBytes=64L*1024*1024)
    { if(maximumRetainedBytes<0)throw new ArgumentOutOfRangeException(nameof(maximumRetainedBytes));_maximumRetainedBytes=maximumRetainedBytes; }
    public void BeginFrame()
    {
        foreach(var buffer in _active)
        {
            if(buffer.Length==0||RetainedBytes+buffer.Length>_maximumRetainedBytes)continue;
            if(!_available.TryGetValue(buffer.Length,out var pool))
            {if(_available.Count>=256)continue;_available.Add(buffer.Length,pool=new());}
            pool.Push(buffer);RetainedBytes+=buffer.Length;
        }
        _active.Clear();ActiveBytes=0;
    }
    public int Allocate(int length,out byte[] buffer)
    {
        if(length<0)throw new ArgumentOutOfRangeException(nameof(length));
        if(length==0)buffer=Array.Empty<byte>();
        else if(_available.TryGetValue(length,out var pool)&&pool.TryPop(out var reused))
        {buffer=reused;RetainedBytes-=length;ReuseHits++;}
        else{buffer=new byte[length];CreatedBytes+=length;}
        int index=_active.Count;_active.Add(buffer);ActiveBytes+=length;return index;
    }
    public int Copy(ReadOnlySpan<byte> source)
    {int index=Allocate(source.Length,out var buffer);source.CopyTo(buffer);return index;}
    public byte[] this[int index]=>_active[index];
}
