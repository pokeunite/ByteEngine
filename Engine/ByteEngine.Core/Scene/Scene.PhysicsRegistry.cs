using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Scene;
public sealed partial class Scene
{
    private bool _physicsRegistryDirty=true;
    private readonly List<Collider3D> _physicsColliders=new();
    private readonly List<Rigidbody3D> _physicsBodies=new();
    private readonly List<Component> _physicsConstraints=new();
    internal IReadOnlyList<Collider3D> PhysicsColliders {get{RefreshPhysicsRegistry();return _physicsColliders;}}
    internal IReadOnlyList<Rigidbody3D> PhysicsBodies {get{RefreshPhysicsRegistry();return _physicsBodies;}}
    internal IReadOnlyList<Component> PhysicsConstraints {get{RefreshPhysicsRegistry();return _physicsConstraints;}}
    internal void InvalidatePhysicsRegistry()=>_physicsRegistryDirty=true;
    private void RefreshPhysicsRegistry()
    {
        if(!_physicsRegistryDirty)return;
        _physicsColliders.Clear();_physicsBodies.Clear();_physicsConstraints.Clear();
        foreach(var obj in _gameObjects)
            for(int i=0;i<obj.Components.Count;i++)
            {
                var component=obj.Components[i];
                if(component is Collider3D collider)_physicsColliders.Add(collider);
                if(component is Rigidbody3D body)_physicsBodies.Add(body);
                if(component is IPhysicsConstraint3D)_physicsConstraints.Add(component);
            }
        _physicsRegistryDirty=false;
    }
}
