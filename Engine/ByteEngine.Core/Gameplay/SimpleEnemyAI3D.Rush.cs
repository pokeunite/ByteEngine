using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public enum RushOutcome { None, PlayerHit, Miss, WallCrash, Interrupted }

/// <summary>A committed swept attack; settings are scene data, never enemy-name dispatch.</summary>
public sealed partial class SimpleEnemyAI3D
{
    [Category("Predatory rush"), Description("Use a predictive, committed charge instead of the Rusher contact strike.")]
    public bool PredatoryRush { get; set; }
    [Category("Predatory rush")] public float RushMinimumRange { get; set; } = 2.8f;
    [Category("Predatory rush")] public float RushPredictionSeconds { get; set; } = .15f;
    [Category("Predatory rush")] public float RushPredictionLimit { get; set; } = .75f;
    [Category("Predatory rush")] public float RushLockSeconds { get; set; } = .2f;
    [Category("Predatory rush")] public float RushAccelerationSeconds { get; set; } = .12f;
    [Category("Predatory rush")] public float RushDuration { get; set; } = .6f;
    [Category("Predatory rush")] public float RushHitRecovery { get; set; } = .5f;
    [Category("Predatory rush")] public float RushCrashRecovery { get; set; } = 1.2f;
    [Category("Predatory rush")] public float RushCrashDamageMultiplier { get; set; } = 1.5f;
    [Category("Predatory rush"), Description("Swept displacement on a successful charge hit, in metres.")]
    public float RushKnockback { get; set; } = 1.3f;
    [Category("Live State")] public RushOutcome LastRushOutcome { get; private set; }
    [Category("Live State")] public Vector3 RushDirection => _lockedDirection;
    [Category("Live State")] public float StaggerRemaining { get; private set; }
    [Category("Live State")] public float AttackCooldownRemaining => _cooldownRemaining;
    private Vector3 _rushLastTarget, _rushVelocity;
    private Guid _rushSampleTarget;
    private float _rushElapsed;
    private bool _rushLocked;
    public bool CanBeginRush(GameObject target)
    {
        if(!PredatoryRush || AttackPhase!="Ready" || _cooldownRemaining>0 || StaggerRemaining>0)return false;
        var d=target.Transform.WorldPosition-Transform.WorldPosition;d.Y=0;
        float distance=d.Length();
        if(distance<Math.Max(.1f,RushMinimumRange)||distance>AttackRange||distance<.001f)return false;
        // Check the full attack corridor before telegraphing, not just a thin sight ray.
        return !GameplayQuery3D.SphereCast(GameObject.Scene!,Transform.WorldPosition+Vector3.UnitY*.8f,d/distance,
            Math.Max(.05f,CollisionRadius),out var hit,distance,GameObject,includeTriggers:false)
            || hit.GameObject==target || hit.GameObject.IsDescendantOf(target);
    }
    private void UpdateRushVelocity(float dt)
    {
        if(!PredatoryRush||dt<=0||GameObject.Scene?.FindGameObject(TargetId) is not {} target)return;
        var point=target.Transform.WorldPosition;
        if(_rushSampleTarget==target.Id)_rushVelocity=(point-_rushLastTarget)/dt;
        else {_rushSampleTarget=target.Id;_rushVelocity=Vector3.Zero;}
        _rushLastTarget=point;_rushVelocity.Y=0;
    }
    private void AimRush(GameObject target)
    {
        var offset=_rushVelocity*Math.Clamp(RushPredictionSeconds,0,.5f);
        float cap=Math.Max(0,RushPredictionLimit);
        if(offset.Length()>cap&&offset.LengthSquared()>.00001f)offset=Vector3.Normalize(offset)*cap;
        _lockedPoint=target.Transform.WorldPosition+offset;
        var direction=_lockedPoint-Transform.WorldPosition;direction.Y=0;
        if(direction.LengthSquared()>.00001f)_lockedDirection=Vector3.Normalize(direction);
        Transform.EulerAngles=new(0,MathF.Atan2(-_lockedDirection.X,-_lockedDirection.Z)*180/MathF.PI,0);
    }
    private void BeginRush(GameObject target)
    {
        GameObject.GetComponent<ByteEngine.Core.Navigation.NavigationAgent3D>()?.Stop();
        ClearWarning();LastRushOutcome=RushOutcome.None;_rushElapsed=0;_rushLocked=false;_chargeTravel=0;_chargeHit=false;
        AttackPhase="Windup";_phaseTime=Math.Max(.05f,WindupSeconds);AimRush(target);
        if(ShowChargePath)
        {
            _chargeTell=GameObject.Scene!.CreateGameObject("Rush warning - "+GameObject.Name);
            _chargeTell.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,CastShadows=false,
                Material=new Material{Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.55f,.05f,1)}});
            UpdateRushWarning();
        }
    }
    private void UpdateRushWarning()
    {
        if(_chargeTell==null)return;
        float length=Math.Max(.1f,ChargeDistance);
        _chargeTell.Transform.WorldPosition=Transform.WorldPosition+_lockedDirection*length*.5f+Vector3.UnitY*.045f;
        _chargeTell.Transform.EulerAngles=new(0,MathF.Atan2(-_lockedDirection.X,-_lockedDirection.Z)*180/MathF.PI,0);
        _chargeTell.Transform.LocalScale=new(Math.Max(.1f,CollisionRadius*2),.025f,length);
        _chargeTell.GetComponent<MeshRenderer>()!.Material.BaseColor=_rushLocked?new(1,.06f,.025f,1):new(1,.55f,.05f,1);
    }
    private void TickRushWindup(float dt)
    {
        State=SimpleEnemyAIState.Attack;IsMoving=false;
        if(!_rushLocked)
        {
            if(GameObject.Scene?.FindGameObject(TargetId) is {} target)AimRush(target);
            if(_phaseTime-dt<=Math.Clamp(RushLockSeconds,0,Math.Max(.05f,WindupSeconds)))_rushLocked=true;
        }
        _phaseTime=Math.Max(0,_phaseTime-dt);UpdateRushWarning();
    }
    private void LaunchRush()
    {
        ClearWarning();_rushLocked=true;AttackPhase="Charge";_rushElapsed=0;AttackFiredThisFrame=true;
    }
    private void TickRushCharge(GameObject target,float dt)
    {
        if(AttackPhase!="Charge")return;
        State=SimpleEnemyAIState.Attack;IsMoving=false;
        float duration=Math.Max(.01f,RushDuration), acceleration=Math.Max(.001f,RushAccelerationSeconds);
        float next=Math.Min(duration,_rushElapsed+Math.Max(0,dt));
        // Integrate the acceleration ramp so distance is stable at different frame rates.
        float Travel(float t)=>t<acceleration?t*t/(2*acceleration):t-acceleration*.5f;
        float step=Math.Min(Math.Max(0,ChargeDistance-_chargeTravel),Math.Max(0,ChargeSpeed)*(Travel(next)-Travel(_rushElapsed)));
        _rushElapsed=next;
        if(step>0)
        {
            var origin=Transform.WorldPosition+Vector3.UnitY*.8f;
            if(GameplayQuery3D.SphereCast(GameObject.Scene!,origin,_lockedDirection,Math.Max(.05f,CollisionRadius),out var hit,step,GameObject,includeTriggers:false))
            {
                Transform.WorldPosition+=_lockedDirection*Math.Clamp(hit.Distance-.015f,0,step);
                bool player=hit.GameObject==target||hit.GameObject.IsDescendantOf(target);
                if(player&&!_chargeHit)
                {
                    _chargeHit=true;if(AutomaticDamage)target.GetComponent<HealthComponent>()?.Damage(Damage,_lockedDirection);
                    PushRushTarget(target);ResolveRush(RushOutcome.PlayerHit);return;
                }
                ResolveRush(RushOutcome.WallCrash);return;
            }
            Transform.WorldPosition+=_lockedDirection*step;_chargeTravel+=step;IsMoving=true;
        }
        if(_rushElapsed>=duration||_chargeTravel>=Math.Max(0,ChargeDistance))ResolveRush(RushOutcome.Miss);
    }
    private void PushRushTarget(GameObject target)
    {
        float amount=Math.Max(0,RushKnockback);
        var origin=target.Transform.WorldPosition+Vector3.UnitY*.8f;
        if(GameplayQuery3D.SphereCast(GameObject.Scene!,origin,_lockedDirection,.35f,out var hit,amount,target,includeTriggers:false))amount=Math.Max(0,hit.Distance-.02f);
        target.Transform.WorldPosition+=_lockedDirection*amount;
    }
    private void ResolveRush(RushOutcome outcome)
    {
        if(AttackPhase!="Charge")return;
        LastRushOutcome=outcome;AttackPhase="Recover";IsMoving=false;State=SimpleEnemyAIState.Idle;
        _phaseTime=Math.Max(.05f,outcome==RushOutcome.WallCrash?RushCrashRecovery:outcome==RushOutcome.PlayerHit?RushHitRecovery:RecoverySeconds);
        _cooldownRemaining=Math.Max(0,AttackCooldown);SetRushExposure(outcome==RushOutcome.WallCrash);
        Warning(Transform.WorldPosition,Math.Max(.4f,CollisionRadius),outcome==RushOutcome.WallCrash?new(1,.8f,.1f,1):new(.3f,.7f,1,1),_phaseTime);
    }
    private void SetRushExposure(bool exposed)
    {
        if(GameObject.GetComponent<HealthComponent>() is {} h)h.IncomingDamageMultiplier=exposed?Math.Max(1,RushCrashDamageMultiplier):1;
    }
    private void TickRushRecovery(float dt)
    {
        IsMoving=false;State=SimpleEnemyAIState.Idle;
        _phaseTime=Math.Max(0,_phaseTime-dt);
        if(_phaseTime<=0){AttackPhase="Ready";ClearWarning();SetRushExposure(false);_behaviorStaged=false;}
    }
    public void Stagger(float seconds)
    {
        if(!float.IsFinite(seconds)||seconds<=0)return;
        ResetAttackPattern();StaggerRemaining=seconds;LastRushOutcome=RushOutcome.Interrupted;
        _cooldownRemaining=Math.Max(0,AttackCooldown);
    }
    public bool TickStagger(float dt)
    {
        if(StaggerRemaining<=0)return false;
        StaggerRemaining=Math.Max(0,StaggerRemaining-dt);IsMoving=false;State=SimpleEnemyAIState.Idle;return true;
    }
    private void ResetRush(){StaggerRemaining=0;_rushSampleTarget=Guid.Empty;_rushVelocity=Vector3.Zero;_rushElapsed=0;_rushLocked=false;SetRushExposure(false);}
    private void SimulatePredatoryRush(GameObject target,float dt)
    {
        if(AttackPhase=="Windup"){TickRushWindup(dt);if(_phaseTime<=0)LaunchRush();return;}
        if(AttackPhase=="Charge"){TickRushCharge(target,dt);return;}
        if(AttackPhase=="Recover"){TickRushRecovery(dt);return;}
        if(CanBeginRush(target)){BeginRush(target);return;}
        if(Vector3.Distance(Transform.WorldPosition,target.Transform.WorldPosition)<=DetectionRange)
        {if(UseNavigation)Navigate(target.Transform.WorldPosition,dt);}
    }
}
