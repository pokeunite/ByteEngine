namespace ByteEngine.Core.Navigation;

/// <summary>Reusable concrete A* heap. Avoids shared-value-type generic comparer
/// trampolines in the WASM runtime; insertion sequence preserves path ties.</summary>
internal sealed class NavigationOpenQueue
{
    private struct Entry {internal int Cell,Sequence;internal float Priority;}
    private Entry[] _items=new Entry[256];
    public int Count {get;private set;}
    public void Clear()=>Count=0;
    private static bool Less(Entry a,Entry b)
    {int order=a.Priority.CompareTo(b.Priority);return order<0||(order==0&&a.Sequence<b.Sequence);}
    public void Enqueue(int cell,(float Priority,int Sequence) priority)
    {
        if(Count==_items.Length)Array.Resize(ref _items,checked(_items.Length*2));
        var item=new Entry{Cell=cell,Priority=priority.Priority,Sequence=priority.Sequence};int index=Count++;
        while(index>0){int parent=(index-1)/2;if(!Less(item,_items[parent]))break;_items[index]=_items[parent];index=parent;}
        _items[index]=item;
    }
    public int Dequeue()
    {
        if(Count==0)throw new InvalidOperationException("Empty navigation queue");
        int result=_items[0].Cell;var last=_items[--Count];int index=0;
        while(index*2+1<Count){int child=index*2+1;if(child+1<Count&&Less(_items[child+1],_items[child]))child++;if(!Less(_items[child],last))break;_items[index]=_items[child];index=child;}
        _items[index]=last;return result;
    }
}
