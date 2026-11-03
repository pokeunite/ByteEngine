namespace ByteEngine.Core.Scene;

public sealed partial class Scene
{
    // Weak keys also allow a miss for a collectible plugin type to unload.
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Type,object> _componentSnapshots = new();
    private readonly List<Type> _invalidSnapshots=new();
    private void InvalidateComponentSnapshots(Component? changed,IReadOnlyList<Component>? members)
    {
        if(changed==null&&members==null){_componentSnapshots.Clear();return;}
        if(changed==null&&members!.Count==0)return;
        try{
            foreach(var entry in _componentSnapshots){
                bool affected=changed!=null&&entry.Key.IsInstanceOfType(changed);
                if(changed==null)for(int i=0;i<members!.Count&&!affected;i++)affected=entry.Key.IsInstanceOfType(members[i]);
                if(affected)_invalidSnapshots.Add(entry.Key);
            }
            foreach(var type in _invalidSnapshots)_componentSnapshots.Remove(type);
        }finally{_invalidSnapshots.Clear();} // Do not root collectible plugin types.
    }

    /// <summary>Immutable membership snapshot in scene/component order. Includes
    /// inactive and disabled components; callers filter their current state.
    /// Reused until objects or components are added, removed or reordered.</summary>
    public IReadOnlyList<T> GetComponentsSnapshot<T>() where T:Component
    {
        if (_componentSnapshots.TryGetValue(typeof(T),out var cached))return (IReadOnlyList<T>)cached;
        var items=new List<T>();
        for(int i=0;i<_gameObjects.Count;i++)
        {
            var components=_gameObjects[i].Components;
            for(int j=0;j<components.Count;j++)if(components[j] is T match)items.Add(match);
        }
        var snapshot=items.AsReadOnly();
        _componentSnapshots.Add(typeof(T),snapshot);
        return snapshot;
    }
}
