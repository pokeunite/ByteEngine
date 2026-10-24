using System.Numerics;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed partial class SimpleEnemyAI3D
{
    public bool DodgeEnabled {get;set;}
    public float DodgeCooldown {get;set;}=3.5f;
    public float DodgeSpeed {get;set;}=7;
    public float DodgeSeconds {get;set;}=.22f;
    public bool RetreatEnabled {get;set;}
    public bool SeekCover {get;set;}
    public float PreferredRange {get;set;}=8;
    public float CoverSearchRadius {get;set;}=6;
    public float CoverHoldSeconds {get;set;}=1.1f;
    public float CoverInterval {get;set;}=3.5f;
    public bool AdaptiveBoss {get;set;}
    public float JumpHeight {get;set;}=5;
    public float JumpSeconds {get;set;}=.85f;
    public float SlamWarningSeconds {get;set;}=.55f;
    public float SlamRadius {get;set;}=3.2f;
    public float SlamDamage {get;set;}=22;
    public float FeintDistance {get;set;}=2.5f;
    public float FeintWarningSeconds {get;set;}=.45f;
    public int EvadedCharges {get;private set;}
    public int EvadedSlams {get;private set;}
    public string CurrentTactic {get;private set;}="Advance";
    public string BossAttack => _bossAttack;
    private string _bossAttack="Charge";
    private float _dodgeClock,_dodgeTime,_coverClock,_coverTime,_jumpTime;
    private bool _requestedDodge,_coverMoving,_shortSlam,_hasUsedFeint;
    private Vector3 _dodgeDirection,_coverPoint,_jumpOrigin;
    private int _attackCounter,_side;
    private HealthComponent? _tacticHealth;
    public void RequestDodge(){if(DodgeEnabled&&_dodgeClock<=0)_requestedDodge=true;}
    private void OnTacticDamage(HealthComponent h,float amount){if(amount>0)RequestDodge();}
    private void OnTacticDeath(HealthComponent h){ClearWarning();_impactPlayer?.Stop(true);}
    private void ObserveTacticHealth()
    {var h=GameObject.GetComponent<HealthComponent>();if(h==_tacticHealth)return;UnsubscribeTactics();_tacticHealth=h;if(h!=null){h.Damaged+=OnTacticDamage;h.Died+=OnTacticDeath;}}
    private void UnsubscribeTactics(){if(_tacticHealth!=null){_tacticHealth.Damaged-=OnTacticDamage;_tacticHealth.Died-=OnTacticDeath;}_tacticHealth=null;}
    private void ResetTactics()
    {
        if(AttackPhase=="Leap")Transform.WorldPosition=_jumpOrigin;
        _dodgeClock=_dodgeTime=_coverClock=_coverTime=_jumpTime=0;_requestedDodge=_coverMoving=_hasUsedFeint=false;
        EvadedCharges=EvadedSlams=_attackCounter=0;_bossAttack="Charge";CurrentTactic="Advance";
        _impactPlayer?.Stop(true);
    }
    private bool ClearRoute(Vector3 direction,float distance)=>!GameplayQuery3D.SphereCast(GameObject.Scene!,Transform.WorldPosition+Vector3.UnitY*Math.Max(.8f,CollisionRadius+.1f),direction,Math.Max(.05f,CollisionRadius),out _,distance,GameObject,includeTriggers:false);
    private bool SimulateTactics(GameObject target,float dt)
    {
        ObserveTacticHealth();
        _dodgeClock=Math.Max(0,_dodgeClock-dt);_coverClock=Math.Max(0,_coverClock-dt);
        if(AdaptiveBoss&&(AttackPhase=="SlamWindup"||AttackPhase=="Leap"))
        {
            State=SimpleEnemyAIState.Attack;
            if(AttackPhase=="SlamWindup")
            {if(_phaseTime<=0){AttackPhase="Leap";_jumpTime=0;_jumpOrigin=Transform.WorldPosition;}return true;}
            float duration=Math.Max(.2f,JumpSeconds*(_shortSlam?.65f:1)/(_shortSlam?1:BossTempo));_jumpTime+=dt;float t=Math.Clamp(_jumpTime/duration,0,1);
            Transform.WorldPosition=Vector3.Lerp(_jumpOrigin,_lockedPoint,t)+Vector3.UnitY*(MathF.Sin(t*MathF.PI)*Math.Max(.5f,JumpHeight*(_shortSlam?.55f:1)));
            if(t>=1)
            {
                Transform.WorldPosition=_lockedPoint;var delta=target.Transform.WorldPosition-_lockedPoint;delta.Y=0;
                bool hit=delta.Length()<=Math.Max(.5f,SlamRadius)&&Math.Abs(target.Transform.WorldPosition.Y-_lockedPoint.Y)<2;
                if(hit)target.GetComponent<HealthComponent>()?.Damage(SlamDamage);else EvadedSlams++;
                ClearWarning();SpawnImpact(SlamRadius);AttackFiredThisFrame=true;Recover();
            }
            return true;
        }
        if(AttackPhase!="Ready")return false;
        if(_dodgeTime>0)
        {
            float step=Math.Min(dt,_dodgeTime);_dodgeTime=Math.Max(0,_dodgeTime-dt);MovePattern(_dodgeDirection,Math.Max(0,DodgeSpeed)*step);CurrentTactic="Dodge";
            if(_dodgeTime<=0&&!BehaviorControlled)GameObject.GetComponent<NavigationAgent3D>()?.SetDestination(target.Transform.WorldPosition);
            return true;
        }
        if(_requestedDodge&&DodgeEnabled)
        {
            _requestedDodge=false;var toward=target.Transform.WorldPosition-Transform.WorldPosition;toward.Y=0;
            if(toward.LengthSquared()>.001f)
            {
                toward=Vector3.Normalize(toward);var side=new Vector3(-toward.Z,0,toward.X)*((_side++%2)==0?1:-1);
                float distance=Math.Max(.05f,DodgeSpeed)*Math.Clamp(DodgeSeconds,.05f,.6f);
                if(!ClearRoute(side,distance))side=-side;
                if(ClearRoute(side,distance)){_dodgeDirection=side;_dodgeTime=Math.Clamp(DodgeSeconds,.05f,.6f);_dodgeClock=Math.Max(.5f,DodgeCooldown);GameObject.GetComponent<NavigationAgent3D>()?.Stop();CurrentTactic="Dodge";return true;}
            }
            _dodgeClock=.5f;
        }
        return false;
    }
    private NavigationRegion3D? TacticRegion()
    {var agent=GameObject.GetComponent<NavigationAgent3D>();return agent==null?null:GameObject.Scene?.FindGameObject(agent.RegionObject)?.GetComponent<NavigationRegion3D>();}
    private bool IsHiddenFrom(GameObject target,Vector3 point)
    {
        var start=target.Transform.WorldPosition+Vector3.UnitY*.8f;var delta=point+Vector3.UnitY*.8f-start;float d=delta.Length();
        return d>.1f&&GameplayQuery3D.Raycast(GameObject.Scene!,start,delta/d,out var hit,d,target,includeTriggers:false)&&hit.GameObject.GetComponent<HealthComponent>()==null;
    }
    private bool RangedTactics(GameObject target,Vector3 toward,float distance,float dt)
    {
        if(!RetreatEnabled||AttackStyle is not (EnemyAttackStyle.BoltCaster or EnemyAttackStyle.GroundCaster))return false;
        var region=TacticRegion();if(region?.Ready!=true)return false;
        if(_coverMoving)
        {
            CurrentTactic="Move to cover";Navigate(_coverPoint,dt);
            if(Vector3.Distance(Transform.WorldPosition,_coverPoint)<.6f){_coverMoving=false;_coverTime=Math.Max(.1f,CoverHoldSeconds);}
            if(_coverClock<=0){_coverMoving=false;_coverClock=1.5f;}
            return true;
        }
        if(_coverTime>0)
        {_coverTime=Math.Max(0,_coverTime-dt);if(_coverTime<=0)_coverClock=Math.Max(_coverClock,2);CurrentTactic="Hold cover";return true;}
        bool pressure=distance<Math.Max(2,PreferredRange*.7f);
        bool frontLine=GameObject.Scene!.GameObjects.Any(o=>o.ActiveInHierarchy&&o.GetComponent<SimpleEnemyAI3D>() is {AttackStyle:EnemyAttackStyle.Rusher}&&o.GetComponent<HealthComponent>()?.IsDead!=true&&Vector3.Distance(o.Transform.WorldPosition,target.Transform.WorldPosition)<14);
        if(_coverClock<=0&&(pressure||SeekCover&&frontLine))
        {
            _coverClock=Math.Max(.75f,CoverInterval);Vector3? best=null;float score=float.MaxValue;
            for(int i=0;i<12;i++)
            {
                float angle=i*MathF.PI/6;var dir=new Vector3(MathF.Sin(angle),0,MathF.Cos(angle));
                var candidate=Transform.WorldPosition+dir*Math.Clamp(CoverSearchRadius,2,10);
                float fromPlayer=Vector3.Distance(candidate,target.Transform.WorldPosition);
                if(pressure&&fromPlayer<distance+1.5f)continue;
                bool hidden=SeekCover&&frontLine&&IsHiddenFrom(target,candidate);
                if(!pressure&&!hidden)continue;
                var path=region.FindPath(Transform.WorldPosition,candidate,4000);if(path.Count==0)continue;
                candidate=path[^1];if(!pressure&&!IsHiddenFrom(target,candidate))continue;float value=Math.Abs(fromPlayer-PreferredRange)+(hidden?-5:0)+path.Count*.025f;
                if(value<score){score=value;best=candidate;}
            }
            if(best is {} point){_coverPoint=point;_coverMoving=true;Navigate(point,dt);CurrentTactic=pressure?"Retreat":"Move to cover";return true;}
        }
        CurrentTactic="Peek and fire";return false;
    }
    private string SelectBossAttack()
    {
        _attackCounter++;
        if(EvadedCharges>0&&_attackCounter==2)return "Slam";
        if(EvadedSlams>0&&!_hasUsedFeint){_hasUsedFeint=true;return "Feint";}
        int choice=(int)((uint)HashCode.Combine(GameObject.Id,_attackCounter,EvadedCharges,EvadedSlams)%100);
        if(EvadedSlams>0&&choice<30&&_bossAttack!="Feint")return "Feint";
        if(EvadedCharges>0&&choice<65)return "Slam";
        return "Charge";
    }
    private bool ChooseBossAttack(GameObject target)
    {
        _bossAttack=SelectBossAttack();
        if(_bossAttack!="Slam")return false;
        BeginSlam(target,false);return true;
    }
    private void BeginSlam(GameObject target,bool shortSlam)
    {
        _shortSlam=shortSlam;_lockedPoint=target.Transform.WorldPosition;
        if(TacticRegion() is {} region)
        {var path=region.FindPath(Transform.WorldPosition,_lockedPoint,10000);_lockedPoint=path.Count>0?path[^1]:Transform.WorldPosition;}
        _lockedPoint.Y=Transform.WorldPosition.Y;GameObject.GetComponent<NavigationAgent3D>()?.Stop();
        AttackPhase="SlamWindup";_phaseTime=Math.Max(.2f,shortSlam?FeintWarningSeconds:SlamWarningSeconds/BossTempo);CurrentTactic=shortSlam?"Rush feint into slam":"Leap slam";
        Warning(_lockedPoint,Math.Max(.5f,SlamRadius),new(1,.18f,.05f,1),_phaseTime+Math.Max(.2f,JumpSeconds*(shortSlam?.65f:1))+.2f);
    }
}
