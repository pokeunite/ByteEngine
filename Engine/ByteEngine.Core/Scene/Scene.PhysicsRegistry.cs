using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Scene;
public sealed partial class Scene
{
    private bool _physicsRegistryDirty=true;
    private readonly HashSet<GameObject> _physicsTransformOwners=new();
    private readonly Dictionary<GameObject,List<Collider3D>> _queryTransformColliders=new();
    internal readonly HashSet<Collider3D> ChangedQueryColliders=new();
    internal long QueryStructureRevision {get;private set;}
    internal void NotifyPhysicsTransformChanged(GameObject owner)
    {
        if(_physicsRegistryDirty){InvalidateQueryBounds();return;}
        if(!_queryTransformColliders.TryGetValue(owner,out var colliders))return;
        QueryBoundsRevision++;foreach(var collider in colliders)ChangedQueryColliders.Add(collider);
    }
    private readonly List<Collider3D> _physicsColliders=new();
    private readonly List<Rigidbody3D> _physicsBodies=new();
    private readonly List<Component> _physicsConstraints=new();
    internal IReadOnlyList<Collider3D> PhysicsColliders {get{RefreshPhysicsRegistry();return _physicsColliders;}}
    internal IReadOnlyList<Rigidbody3D> PhysicsBodies {get{RefreshPhysicsRegistry();return _physicsBodies;}}
    internal IReadOnlyList<Component> PhysicsConstraints {get{RefreshPhysicsRegistry();return _physicsConstraints;}}
    internal long AIRegistryRevision {get;private set;}
    internal long QueryBoundsRevision {get;private set;}
    internal void InvalidateQueryBounds(){QueryBoundsRevision++;QueryStructureRevision++;}
    internal void InvalidatePhysicsRegistry(){_physicsRegistryDirty=true;AIRegistryRevision++;InvalidateQueryBounds();}
    internal static bool AffectsPhysics(Component component)=>component is Collider3D or Rigidbody3D or IPhysicsConstraint3D;
    internal void NotifyPhysicsHierarchyChanged(GameObject owner)
    {
        // The owner map includes every collider ancestor, even for disabled
        // colliders. Visual/UI-only objects cannot change query membership.
        if(_physicsRegistryDirty||_physicsTransformOwners.Contains(owner))InvalidatePhysicsRegistry();
    }
    internal void NotifyPhysicsActiveChanged(GameObject owner)
    {
        if(_physicsRegistryDirty||_physicsTransformOwners.Contains(owner))InvalidateQueryBounds();
    }
    private void RefreshPhysicsRegistry()
    {
        if(!_physicsRegistryDirty)return;
        _physicsColliders.Clear();_physicsBodies.Clear();_physicsConstraints.Clear();_physicsTransformOwners.Clear();_queryTransformColliders.Clear();
        foreach(var obj in _gameObjects)
            for(int i=0;i<obj.Components.Count;i++)
            {
                var component=obj.Components[i];
                if(component is Collider3D collider){_physicsColliders.Add(collider);for(var parent=obj;parent!=null;parent=parent.Parent){_physicsTransformOwners.Add(parent);if(!_queryTransformColliders.TryGetValue(parent,out var affected))_queryTransformColliders[parent]=affected=new();affected.Add(collider);}}
                if(component is Rigidbody3D body)_physicsBodies.Add(body);
                if(component is IPhysicsConstraint3D)_physicsConstraints.Add(component);
            }
        _physicsRegistryDirty=false;
    }
}
