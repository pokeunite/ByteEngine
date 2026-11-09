namespace ByteEngine.Core.Runtime;

/// <summary>Exact-length, bounded storage for synchronous host frame transfers.
/// Handles remain valid until BeginFrame. Consumers must finish reading a frame
/// before starting the next; no asset backing array is retained or mutated.</summary>
public sealed class FrameTransferBuffers
{
    private sealed class Entry
    {
        internal readonly byte[] Bytes;
        internal readonly LinkedListNode<Entry> SizeNode,AgeNode;
        internal Entry(byte[] bytes){Bytes=bytes;SizeNode=new(this);AgeNode=new(this);}
    }
    private readonly List<Entry> _active=new();
    private readonly Dictionary<int,LinkedList<Entry>> _available=new();
    private readonly LinkedList<Entry> _oldestFirst=new();
    private readonly long _maximumRetainedBytes;
    private readonly bool _pinned;
    public int Count=>_active.Count;
    public long ActiveBytes {get;private set;}
    public long RetainedBytes {get;private set;}
    public long CreatedBytes {get;private set;}
    public long ReuseHits {get;private set;}
    public FrameTransferBuffers(long maximumRetainedBytes=64L*1024*1024,bool pinned=false)
    { if(maximumRetainedBytes<0)throw new ArgumentOutOfRangeException(nameof(maximumRetainedBytes));_maximumRetainedBytes=maximumRetainedBytes;_pinned=pinned; }
    public void BeginFrame()
    {
        foreach(var entry in _active)
        {
            int length=entry.Bytes.Length;
            if(length==0||length>_maximumRetainedBytes)continue;
            // Retain recently consumed sizes, rather than letting old burst
            // buffers occupy the budget forever and force allocations each frame.
            while(_oldestFirst.First is {} oldest&&(RetainedBytes+length>_maximumRetainedBytes||(!_available.ContainsKey(length)&&_available.Count>=256)))RemoveAvailable(oldest.Value);
            if(!_available.TryGetValue(length,out var pool)){ if(_available.Count>=256){int empty=-1;foreach(var pair in _available)if(pair.Value.Count==0){empty=pair.Key;break;}if(empty>=0)_available.Remove(empty);} _available.Add(length,pool=new()); }
            pool.AddLast(entry.SizeNode);_oldestFirst.AddLast(entry.AgeNode);RetainedBytes+=length;
        }
        _active.Clear();ActiveBytes=0;
    }
    private void RemoveAvailable(Entry entry)
    {
        var pool=_available[entry.Bytes.Length];pool.Remove(entry.SizeNode);
        _oldestFirst.Remove(entry.AgeNode);RetainedBytes-=entry.Bytes.Length;
    }
    public int Allocate(int length,out byte[] buffer)
    {
        if(length<0)throw new ArgumentOutOfRangeException(nameof(length));
        Entry entry;
        if(length==0)entry=new(Array.Empty<byte>());
        else if(_available.TryGetValue(length,out var pool)&&pool.Last is {} reused)
        {entry=reused.Value;RemoveAvailable(entry);ReuseHits++;}
        else{entry=new(GC.AllocateArray<byte>(length,pinned:_pinned));CreatedBytes+=length;}
        buffer=entry.Bytes;
        int index=_active.Count;_active.Add(entry);ActiveBytes+=length;return index;
    }
    public int Copy(ReadOnlySpan<byte> source)
    {int index=Allocate(source.Length,out var buffer);source.CopyTo(buffer);return index;}
    public byte[] this[int index]=>_active[index].Bytes;
    /// <summary>Only pinned pools expose stable addresses. Valid until BeginFrame;
    /// hosts must consume synchronously and reacquire their heap view after memory growth.</summary>
    public nint GetPinnedAddress(int index)
    {
        if(!_pinned)throw new InvalidOperationException("This frame pool does not pin its buffers.");
        var buffer=_active[index].Bytes;
        return buffer.Length==0?0:System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(buffer,0);
    }
}
