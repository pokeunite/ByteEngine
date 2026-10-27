using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using Component = ByteEngine.Core.Scene.Component;

namespace ByteEngine.Core.Gameplay;
public enum DashDirectionMode { MovementOrFacing, Facing, Cursor }

/// <summary>Reusable ability. Event Sheets request activation; the character motor sweeps collisions.</summary>
public sealed class DashAbility3D : Component
{
    [Category("Dash")] public float Distance { get; set; } = 4.2f;
    [Category("Dash")] public float Duration { get; set; } = .18f;
    [Category("Dash")] public float Cooldown { get; set; } = 1.25f;
    [Category("Dash")] public DashDirectionMode DirectionMode { get; set; }
    [Category("Dash")] public bool RequireGrounded { get; set; } = true;
    [Category("Energy")] public float EnergyCost { get; set; } = 25;
    [Category("Energy"), SceneObjectReference(typeof(ResourceMeter3D))] public Guid EnergyObjectId { get; set; }
    [Category("Energy")] public float RegenerationDelay { get; set; } = .8f;
    public float CooldownRemaining { get; private set; }
    public float CooldownFraction => Cooldown > 0 ? Math.Clamp(CooldownRemaining / Cooldown, 0, 1) : 0;
    public bool IsDashing => GameObject.GetComponent<CharacterController3D>()?.HasBurstMotion == true;
    public bool CanRegenerateEnergy => !IsDashing && _sinceDash >= Safe(RegenerationDelay, .8f, 0, 30);
    public bool CanDash => Enabled && GameObject.ActiveInHierarchy && !IsDashing && CooldownRemaining <= 0 &&
        GameObject.GetComponent<HealthComponent>()?.IsDead != true &&
        GameObject.GetComponent<CharacterController3D>() is { Enabled:true } motor && (!RequireGrounded || motor.IsGrounded) &&
        (Safe(EnergyCost,25,0,100000) <= 0 || Energy?.Current >= Safe(EnergyCost,25,0,100000));
    public string LastResult { get; private set; } = "";
    public string AvailabilityStatus => CanDash ? "READY" : GameObject.GetComponent<HealthComponent>()?.IsDead == true ? "DEAD" :
        IsDashing ? "DASHING" : CooldownRemaining > 0 ? "COOLDOWN" : EnergyCost > 0 && (Energy?.Current ?? 0) < EnergyCost ? "ENERGY" :
        RequireGrounded && GameObject.GetComponent<CharacterController3D>()?.IsGrounded != true ? "AIRBORNE" : "UNAVAILABLE";
    public long ActivationSequence { get; private set; }
    private float _sinceDash = float.MaxValue;
    private ResourceMeter3D? Energy => (EnergyObjectId == Guid.Empty ? GameObject : GameObject.Scene?.FindGameObject(EnergyObjectId))?.GetComponent<ResourceMeter3D>();
    public override int UpdateOrder => -150;
    public bool TryDash()
    {
        if (!CanDash) { LastResult = IsDashing ? "Already dashing" : CooldownRemaining > 0 ? "Cooling down" : EnergyCost > 0 && (Energy?.Current ?? 0) < EnergyCost ? "Not enough energy" : "Dash unavailable"; return false; }
        var motor = GameObject.GetComponent<CharacterController3D>()!;
        var control = GameObject.GetComponent<TopDownController3D>();
        if(control?.Enabled!=true)control=null;
        var thirdPerson=GameObject.GetComponent<PlayerController3D>();
        Vector3 direction = DirectionMode == DashDirectionMode.Cursor && control?.HasCursorAim == true ? control.AimWorldPoint - Transform.WorldPosition :
            DirectionMode == DashDirectionMode.MovementOrFacing ? control?.ReadDesiredMovement() ?? (thirdPerson?.Enabled==true?thirdPerson.ReadDesiredMovement():motor.Velocity) : Transform.Forward;
        direction.Y = 0;
        if (direction.LengthSquared() < .001f) direction = Transform.Forward;
        direction.Y = 0;
        if (!float.IsFinite(direction.LengthSquared()) || direction.LengthSquared() < .001f) return false;
        float cost = Safe(EnergyCost,25,0,100000);
        if (cost > 0 && Energy?.TrySpend(cost) != true) return false;
        float duration = Safe(Duration,.18f,.03f,2);
        motor.BeginBurstMotion(Vector3.Normalize(direction) * Safe(Distance,4.2f,.1f,30) / duration, duration);
        CooldownRemaining = Safe(Cooldown,1.25f,0,60); _sinceDash = 0; ActivationSequence++; LastResult = "Dashed"; return true;
    }
    public void Cancel() => GameObject.GetComponent<CharacterController3D>()?.CancelBurstMotion();
    protected override void OnUpdate()
    {
        float dt = Math.Clamp((float)Time.DeltaTime,0,.1f);
        CooldownRemaining = Math.Max(0,CooldownRemaining-dt); _sinceDash = Math.Min(float.MaxValue/2,_sinceDash+dt);
        if (GameObject.GetComponent<HealthComponent>()?.IsDead == true) Cancel();
    }
    protected override void OnStop() { Cancel(); CooldownRemaining = 0; _sinceDash = float.MaxValue; }
    internal static float Safe(float v,float fallback,float min,float max) => Math.Clamp(float.IsFinite(v)?v:fallback,min,max);
}
