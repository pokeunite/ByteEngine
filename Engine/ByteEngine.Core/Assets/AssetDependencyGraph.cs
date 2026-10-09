namespace ByteEngine.Core.Assets;
/// <summary>Reverse dependencies with cycle-safe transitive invalidation.</summary>
public sealed class AssetDependencyGraph
{
    private readonly Dictionary<Guid,HashSet<Guid>> _dependencies = new();
    private readonly Dictionary<Guid,HashSet<Guid>> _dependents = new();
    public void SetDependencies(Guid asset,IEnumerable<Guid> dependencies)
    {
        if(_dependencies.Remove(asset,out var previous)) foreach(var id in previous)
            if(_dependents.TryGetValue(id,out var users)){users.Remove(asset);if(users.Count==0)_dependents.Remove(id);}
        var current=dependencies.Where(id=>id!=Guid.Empty&&id!=asset).ToHashSet();
        _dependencies[asset]=current;
        foreach(var id in current){if(!_dependents.TryGetValue(id,out var users))_dependents[id]=users=new();users.Add(asset);}
    }
    public IReadOnlyCollection<Guid> Affected(IEnumerable<Guid> changed)
    {
        var result=changed.ToHashSet();var queue=new Queue<Guid>(result);
        while(queue.TryDequeue(out var id))if(_dependents.TryGetValue(id,out var users))foreach(var user in users)if(result.Add(user))queue.Enqueue(user);
        return result.Order().ToArray();
    }
    public void Remove(Guid asset)=>SetDependencies(asset,Array.Empty<Guid>());
}
