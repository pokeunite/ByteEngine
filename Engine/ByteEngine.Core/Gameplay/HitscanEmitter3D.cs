using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Classification;
namespace ByteEngine.Core.Gameplay;

/// <summary>Reusable bounded ray weapon. Input, equipment and presentation are supplied by callers.</summary>
public sealed class HitscanEmitter3D : Component
{
    public float Damage { get; set; } = 25;
    public float Range { get; set; } = 90;
    public float FireInterval { get; set; } = .18f;
    public float Impulse { get; set; } = 3;
    public LayerMask HitMask { get; set; } = LayerMask.All;
    public int ShotsFired { get; private set; }
    private float _cooldown;
    public bool Ready => _cooldown <= 0;
    public event Action<RaycastHit3D>? Hit;
    public bool Fire(Vector3 origin, Vector3 direction, GameObject? owner = null)
    {
        var scene = AttachedGameObject?.Scene;
        if (scene == null || !Ready || !float.IsFinite(direction.LengthSquared()) || direction.LengthSquared()<1e-6f) return false;
        _cooldown = Math.Clamp(float.IsFinite(FireInterval)?FireInterval:.18f,.02f,30);
        ShotsFired++;
        if (GameplayQuery3D.Raycast(scene,origin,Vector3.Normalize(direction),out var hit,Math.Clamp(Range,.1f,1000),owner??GameObject,HitMask,includeTriggers:false))
        {
            for(GameObject? target=hit.GameObject;target!=null;target=target.Parent)
            {
                if(target.GetComponent<HealthComponent>() is {} health){health.Damage(Math.Max(0,Damage));break;}
            }
            hit.GameObject.GetComponent<Rigidbody3D>()?.AddImpulseAtPosition(Vector3.Normalize(direction)*Math.Max(0,Impulse),hit.Point);
            Hit?.Invoke(hit);
        }
        return true;
    }
    public void ResetCooldown() => _cooldown=0;
    protected override void OnUpdate()=>_cooldown=Math.Max(0,_cooldown-(float)Time.DeltaTime);
    protected override void OnStop(){_cooldown=0;}
}
