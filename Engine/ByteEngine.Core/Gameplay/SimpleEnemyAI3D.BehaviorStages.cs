using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Gameplay;
/// <summary>Behaviour tasks own stage transitions; native attack effects/physics are shared with legacy AI.</summary>
public sealed partial class SimpleEnemyAI3D
{
    private bool _behaviorStaged;
    public string SelectedBehaviorAttack {get;private set;}="Primary";
    public string BehaviorAttackError {get;private set;}="";
    public bool SelectBehaviorAttack(GameObject target,string option)
    {
        BehaviorAttackError="";
        if(AttackPhase!="Ready"||_cooldownRemaining>0){BehaviorAttackError="Attack cooldown or previous attack has not ended";return false;}
        if(option is "Slam" or "Feint" && !AdaptiveBoss){BehaviorAttackError="Slam and feint choices require Adaptive Boss";return false;}
        if(option=="Charge"&&AttackStyle!=EnemyAttackStyle.Charger){BehaviorAttackError="Charge choice requires a Charger attack style";return false;}
        _behaviorStaged=true;
        SelectedBehaviorAttack=option.Equals("Automatic",StringComparison.OrdinalIgnoreCase)?(AdaptiveBoss?SelectBossAttack():AttackStyle.ToString()):option;
        _bossAttack=SelectedBehaviorAttack is "Slam" or "Feint" or "Charge"?SelectedBehaviorAttack:"Charge";
        return true;
    }
    public bool TickBehaviorBackground(float delta)
    {
        AttackFiredThisFrame=false;IsMoving=false;if(AttackPhase=="Ready")State=SimpleEnemyAIState.Idle;
        if(!PredatoryRush || AttackPhase=="Ready")_cooldownRemaining=Math.Max(0,_cooldownRemaining-delta);
        UpdateRushVelocity(delta);
        return AttackPhase=="Ready"&&GameObject.Scene?.FindGameObject(TargetId) is {} target&&SimulateTactics(target,delta);
    }
    public void SetBehaviorMovement(bool moving){IsMoving=moving;if(AttackPhase=="Ready")State=moving?SimpleEnemyAIState.Chase:SimpleEnemyAIState.Idle;}
    public void BeginBehaviorTelegraph(GameObject target)
    {
        if(PredatoryRush){BeginRush(target);return;}
        if(AdaptiveBoss&&SelectedBehaviorAttack=="Slam"){BeginSlam(target,false);return;}
        var d=target.Transform.WorldPosition-Transform.WorldPosition;d.Y=0;
        if(d.LengthSquared()<.001f)d=-Vector3.UnitZ;
        BeginStandardTelegraph(target,Vector3.Normalize(d));
    }
    public bool TickBehaviorTelegraph(float delta)
    {
        if(PredatoryRush){TickRushWindup(delta);return _phaseTime<=0;}
        State=SimpleEnemyAIState.Attack;IsMoving=false;AttackFiredThisFrame=false;_phaseTime=Math.Max(0,_phaseTime-delta);
        if(_fireball is {Active:true})
        {
            float t=1-Math.Clamp(_phaseTime/Math.Max(.05f,WindupSeconds),0,1);
            _fireball.Transform.WorldPosition=_lockedPoint+Vector3.UnitY*(Math.Max(1,FireballHeight)*(1-t*t)+Math.Max(.15f,FireballSize)*.3f);
        }
        return _phaseTime<=0;
    }
    public void BeginBehaviorStrike(GameObject target)
    {
        if(PredatoryRush){LaunchRush();return;}
        if(AttackPhase=="SlamWindup"){AttackPhase="Leap";_jumpTime=0;_jumpOrigin=Transform.WorldPosition;return;}
        _phaseTime=0;SimulatePattern(target,0);
    }
    public bool TickBehaviorStrike(GameObject target,float delta)
    {
        if(PredatoryRush){TickRushCharge(target,delta);return AttackPhase=="Recover";}
        if(AttackPhase=="Recover")return true;
        SimulatePattern(target,delta);return AttackPhase=="Recover";
    }
    private bool StagedMeleeCanHit(GameObject target,Vector3 direction)
    {
        if(!_behaviorStaged)return true;
        if(Vector3.Dot(direction,_lockedDirection)<.5f)return false;
        var origin=Transform.WorldPosition+Vector3.UnitY*.8f;var delta=target.Transform.WorldPosition+Vector3.UnitY*.8f-origin;
        return delta.LengthSquared()<.001f||!ByteEngine.Core.Physics.GameplayQuery3D.Raycast(GameObject.Scene!,origin,Vector3.Normalize(delta),out var hit,delta.Length(),GameObject,includeTriggers:false)||hit.GameObject==target||hit.GameObject.IsDescendantOf(target);
    }
    public bool TickBehaviorRecovery(GameObject target,float delta)
    {
        if(PredatoryRush){TickRushRecovery(delta);return AttackPhase=="Ready";}
        if(AttackPhase=="Ready")return true;
        if(AttackPhase!="Recover")return false;
        SimulatePattern(target,delta);if(AttackPhase=="Ready")_behaviorStaged=false;return AttackPhase=="Ready";
    }
}
