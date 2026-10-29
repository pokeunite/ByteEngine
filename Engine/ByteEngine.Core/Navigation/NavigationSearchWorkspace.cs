namespace ByteEngine.Core.Navigation;

/// <summary>Reusable A* scratch, never a cached route. Bounded dense storage
/// accelerates ordinary grids; large maps use sparse searches instead.</summary>
internal sealed class NavigationSearchWorkspace
{
    private readonly uint[]? _seen,_closed;
    private readonly float[]? _cost;
    private readonly int[]? _parents;
    private uint _generation;
    private readonly Dictionary<int,float> _sparseCost=new();
    private readonly Dictionary<int,int> _sparseParents=new();
    private readonly HashSet<int> _sparseClosed=new();
    public PriorityQueue<int,(float,int)> Open {get;}=new();
    public int ClosedCount {get;private set;}
    public NavigationSearchWorkspace(int cells){if(cells<=262144){_seen=new uint[cells];_closed=new uint[cells];_cost=new float[cells];_parents=new int[cells];}}
    public void Begin(){Open.Clear();ClosedCount=0;if(_seen!=null){if(++_generation==0){Array.Clear(_seen);Array.Clear(_closed!);_generation=1;}}else{_sparseCost.Clear();_sparseParents.Clear();_sparseClosed.Clear();}}
    public bool IsClosed(int cell)=>_closed!=null?_closed[cell]==_generation:_sparseClosed.Contains(cell);
    public bool Close(int cell){if(IsClosed(cell))return false;if(_closed!=null)_closed[cell]=_generation;else _sparseClosed.Add(cell);ClosedCount++;return true;}
    public bool TryCost(int cell,out float value){if(_seen!=null){value=_cost![cell];return _seen[cell]==_generation;}return _sparseCost.TryGetValue(cell,out value);}
    public float Cost(int cell)=>_cost!=null?_cost[cell]:_sparseCost[cell];
    public void SetCost(int cell,float value){if(_seen!=null){_seen[cell]=_generation;_cost![cell]=value;}else _sparseCost[cell]=value;}
    public void SetParent(int cell,int parent){if(_parents!=null)_parents[cell]=parent;else _sparseParents[cell]=parent;}
    public int Parent(int cell)=>_parents!=null?_parents[cell]:_sparseParents[cell];
}
