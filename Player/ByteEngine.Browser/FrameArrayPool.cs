/// <summary>Exact-length frame buffers, valid until the next frame starts. Bounded retained memory.</summary>
internal sealed class FrameArrayPool<T>(int maximumElements, int maximumArrays = 4096)
{
    private readonly Dictionary<int, Stack<T[]>> available = new();
    private readonly List<T[]> leased = new();
    private int retainedElements, retainedArrays;
    public int RetainedElements => retainedElements;
    public T[] Rent(int length)
    {
        if (length == 0) return [];
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
        T[] array;
        if (available.TryGetValue(length, out var bucket) && bucket.Count > 0)
        {
            array = bucket.Pop(); retainedElements -= length; retainedArrays--;
        }
        else array = new T[length];
        leased.Add(array); return array;
    }
    public void BeginFrame()
    {
        // Drop buffers not reused by the last frame. One-off startup uploads must
        // not fill the budget forever and starve the steady gameplay buffers.
        foreach (var bucket in available.Values) bucket.Clear();
        retainedElements = retainedArrays = 0;
        foreach (var array in leased)
        {
            if (array.Length > maximumElements - retainedElements || retainedArrays >= maximumArrays) continue;
            if (!available.TryGetValue(array.Length, out var bucket))
            {
                if (available.Count >= maximumArrays) continue;
                available.Add(array.Length, bucket = new());
            }
            bucket.Push(array); retainedElements += array.Length; retainedArrays++;
        }
        leased.Clear();
    }
}
