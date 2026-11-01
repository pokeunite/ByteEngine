using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Scene;
public sealed partial class Scene
{
    private bool _physicsRegistryDirty=true;
    private readonly HashSet<GameObject> _physicsTransformOwners=new();
    internal void NotifyPhysicsTransformChanged(GameObject owner){if(_physicsRegistryDirty||_physicsTransformOwners.Contains(owner))InvalidateQueryBounds();}
    private readonly List<Collider3D> _physicsColliders=new();
    private readonly List<Rigidbody3D> _physicsBodies=new();
    private readonly List<Component> _physicsConstraints=new();
    internal IReadOnlyList<Collider3D> PhysicsColliders {get{RefreshPhysicsRegistry();return _physicsColliders;}}
    internal IReadOnlyList<Rigidbody3D> PhysicsBodies {get{RefreshPhysicsRegistry();return _physicsBodies;}}
    internal IReadOnlyList<Component> PhysicsConstraints {get{RefreshPhysicsRegistry();return _physicsConstraints;}}
    internal long AIRegistryRevision {get;private set;}
    internal long QueryBoundsRevision {get;private set;}
    internal void InvalidateQueryBounds()=>QueryBoundsRevision++;
    internal void InvalidatePhysicsRegistry(){_physicsRegistryDirty=true;AIRegistryRevision++;InvalidateQueryBounds();}
    private void RefreshPhysicsRegistry()
    {
        if(!_physicsRegistryDirty)return;
        _physicsColliders.Clear();_physicsBodies.Clear();_physicsConstraints.Clear();_physicsTransformOwners.Clear();
        foreach(var obj in _gameObjects)
            for(int i=0;i<obj.Components.Count;i++)
            {
                var component=obj.Components[i];
                if(component is Collider3D collider){_physicsColliders.Add(collider);for(var parent=obj;parent!=null;parent=parent.Parent)_physicsTransformOwners.Add(parent);}
                if(component is Rigidbody3D body)_physicsBodies.Add(body);
                if(component is IPhysicsConstraint3D)_physicsConstraints.Add(component);
            }
        _physicsRegistryDirty=false;
    }
}
