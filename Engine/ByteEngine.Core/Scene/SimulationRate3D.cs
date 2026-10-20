using System.ComponentModel;
namespace ByteEngine.Core.Scene;
/// <summary>Local clock for gameplay, animation and events. Physics uses the scene clock.</summary>
public sealed class SimulationRate3D : Component
{
    private float _rate=1;
    [Description("Gameplay and animation rate inherited by children. Dynamic physics uses the scene clock.")]
    public float Rate { get=>_rate;set=>_rate=float.IsFinite(value)?Math.Clamp(value,0,4):1; }
    [SceneObjectReference(typeof(SimulationRate3D)),Description("Optional shared clock object, for example the clock controlling a spawned projectile.")]
    public Guid SourceObjectId {get;set;}
    internal static float Resolve(GameObject obj)
    {
        for(var current=obj;current!=null;current=current.Parent)
            if(current.GetComponent<SimulationRate3D>() is {Enabled:true} clock)
            {
                if(clock.SourceObjectId!=Guid.Empty&&obj.Scene?.FindGameObject(clock.SourceObjectId)?.GetComponent<SimulationRate3D>() is {Enabled:true} source)return source.Rate;
                return clock.Rate;
            }
        return 1;
    }
}
