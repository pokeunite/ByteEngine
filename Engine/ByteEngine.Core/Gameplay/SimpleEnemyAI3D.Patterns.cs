using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public enum EnemyAttackStyle { Contact, Rusher, BoltCaster, GroundCaster, Charger }

/// <summary>Reusable, inspector-authored telegraphed enemy attacks. Encounter sequencing belongs in Event Sheets.</summary>
public sealed partial class SimpleEnemyAI3D
{
    public EnemyAttackStyle AttackStyle { get; set; }
    public bool UseNavigation { get; set; }
    public bool ShowChargePath {get;set;}
    public bool FallingFireball { get; set; }
    public float FireballHeight { get; set; } = 12;
    public float FireballSize { get; set; } = 1.3f;
    public ByteEngine.Core.Assets.AssetReference FireballEffect { get; set; } = ByteEngine.Core.Assets.AssetReference.Empty;
    public ByteEngine.Core.Assets.AssetReference ImpactEffect { get; set; } = ByteEngine.Core.Assets.AssetReference.Empty;
    public float WindupSeconds { get; set; } = .8f;
    public float RecoverySeconds { get; set; } = 1.2f;
    public float ChargeSpeed { get; set; } = 10f;
    public float ChargeDistance { get; set; } = 8f;
    public float HazardRadius { get; set; } = 2f;
    public float CollisionRadius { get; set; } = .35f;
    public float RecoveryDamageMultiplier { get; set; } = 2f;
    public string AttackPhase { get; private set; } = "Ready";
    public bool IsExposed => AttackPhase == "Recover" && AttackStyle == EnemyAttackStyle.Charger;
    private float _phaseTime, _chargeTravel;
    private Vector3 _lockedPoint, _lockedDirection;
    private GameObject? _warning, _fireball, _chargeTell;
    private ByteEngine.Core.Vfx.VfxPlayer? _impactPlayer;
    private bool _effectsWarmed;
    private bool _chargeHit;

    public void ResetAttackPattern()
    {
        _behaviorStaged=false;
        ResetTactics();
        GameObject.GetComponent<CombatArmour3D>()?.SetExposed(false);
        GameObject.GetComponent<ByteEngine.Core.Navigation.NavigationAgent3D>()?.Stop();
        ClearWarning(); AttackPhase = "Ready"; _phaseTime = 0; _cooldownRemaining = 0;
        if (AttachedGameObject?.GetComponent<HealthComponent>() is {} h) h.IncomingDamageMultiplier = 1;
    }
    private void ClearWarning()
    {
        if (_warning?.Scene is {} scene) scene.DestroyGameObject(_warning);
        _warning = null;
        if(_chargeTell?.Scene is {} tellScene)tellScene.DestroyGameObject(_chargeTell);_chargeTell=null;
        if(_fireball?.Scene!=null){_fireball.GetComponent<ByteEngine.Core.Vfx.VfxPlayer>()?.Stop(true);_fireball.Active=false;}
    }
    private void Warning(Vector3 point, float radius, Vector4 color, float duration = -1)
    {
        ClearWarning();
        var scene = GameObject.Scene!;
        _warning = scene.CreateGameObject("Attack warning - " + GameObject.Name);
        _warning.Transform.WorldPosition = point + new Vector3(0,.04f,0);
        _warning.Transform.LocalScale = new(radius * 2,.025f,radius * 2);
        _warning.AddComponent(new MeshRenderer { UsePrimitive=true, Primitive=PrimitiveMeshType.Sphere,
            CastShadows=false, Material=new Material { BaseColor=color, Roughness=1 } });
        _warning.AddComponent(new LifetimeComponent { LifetimeSeconds=Math.Max(.1f,duration<0?WindupSeconds:duration)+.1f });
    }
    private bool MovePattern(Vector3 direction, float amount)
    {
        if(amount <= 0) return true;
        if(UseNavigation&&TacticRegion()?.Grid is {} grid)
        {
            var p=Transform.WorldPosition+direction*amount;
            int x=(int)MathF.Floor((p.X-grid.Origin.X)/grid.CellSize),z=(int)MathF.Floor((p.Z-grid.Origin.Y)/grid.CellSize);
            if(!grid.IsWalkable(x,z))return false;
        }
        if(GameplayQuery3D.SphereCast(GameObject.Scene!,Transform.WorldPosition+Vector3.UnitY*.65f,
            direction, Math.Max(.05f,CollisionRadius), out _, amount+.04f, GameObject, includeTriggers:false)) return false;
        Transform.WorldPosition += direction*amount;
        IsMoving=true; return true;
    }
    private void SimulatePattern(GameObject target, float dt)
    {
        var health=GameObject.GetComponent<HealthComponent>();
        if(health != null) health.IncomingDamageMultiplier=IsExposed?Math.Max(1,RecoveryDamageMultiplier):1;
        _phaseTime=Math.Max(0,_phaseTime-dt);
        var delta=target.Transform.WorldPosition-Transform.WorldPosition; delta.Y=0;
        float distance=delta.Length(); var direction=distance>.001f?delta/distance:-Vector3.UnitZ;
        if(SimulateTactics(target,dt))return;
        if(AttackPhase=="Recover")
        {
            State=SimpleEnemyAIState.Idle;
            if(_phaseTime<=0){AttackPhase="Ready";GameObject.GetComponent<CombatArmour3D>()?.SetExposed(false);}
            return;
        }
        if(AttackPhase=="Charge")
        {
            State=SimpleEnemyAIState.Attack;
            float step=Math.Min(Math.Max(0,ChargeSpeed)*dt*BossTempo,Math.Max(0,ChargeDistance-_chargeTravel));
            if(_bossAttack!="Feint" && !_chargeHit && distance<Math.Max(1,CollisionRadius+.65f))
            {target.GetComponent<HealthComponent>()?.Damage(Damage); _chargeHit=true;}
            bool moved=MovePattern(_lockedDirection,step); _chargeTravel+=step;
            if(!moved||_chargeTravel>=(_bossAttack=="Feint"?FeintDistance:ChargeDistance)||step<=0)
            {
                if(AdaptiveBoss&&_bossAttack=="Feint")BeginSlam(target,true);
                else {if(AdaptiveBoss&&!_chargeHit)EvadedCharges++;Recover(!AdaptiveBoss||!_chargeHit);}
            }
            return;
        }
        if(AttackPhase=="Windup")
        {
            State=SimpleEnemyAIState.Attack;
            if(_fireball!=null)
            {
                float t=1-Math.Clamp(_phaseTime/Math.Max(.05f,WindupSeconds),0,1);
                _fireball.Transform.WorldPosition=_lockedPoint+Vector3.UnitY*(Math.Max(1,FireballHeight)*(1-t*t)+Math.Max(.15f,FireballSize)*.3f);
            }
            if(_phaseTime>0) return;
            ClearWarning(); AttackFiredThisFrame=true;
            if(AttackStyle==EnemyAttackStyle.Charger)
            {AttackPhase="Charge";_chargeTravel=0;_chargeHit=false;return;}
            if(AttackStyle==EnemyAttackStyle.BoltCaster)
                GameObject.GetComponent<ProjectileLauncher3D>()?.Fire(_lockedPoint+Vector3.UnitY*.8f-(Transform.WorldPosition+Vector3.UnitY*.8f));
            else if(AttackStyle==EnemyAttackStyle.GroundCaster)
            {
                if(FallingFireball)SpawnImpact();
                var d=target.Transform.WorldPosition-_lockedPoint; d.Y=0;if(d.Length()<=HazardRadius)target.GetComponent<HealthComponent>()?.Damage(Damage);
            }
            else if(distance<=AttackRange+.25f&&StagedMeleeCanHit(target,direction)) target.GetComponent<HealthComponent>()?.Damage(Damage);
            Recover(); return;
        }
        if(distance>DetectionRange)return;
        State=SimpleEnemyAIState.Chase;
        Transform.EulerAngles=new(0,MathF.Atan2(-direction.X,-direction.Z)*180/MathF.PI,0);
        if(RangedTactics(target,direction,distance,dt))return;
        if(distance>StopDistance)
        {
            float amount=Math.Min(Math.Max(0,MoveSpeed)*dt,distance-StopDistance);
            if(UseNavigation)Navigate(target.Transform.WorldPosition,dt);
            else if(!MovePattern(direction,amount))
            {
                // Slide around a piece of cover rather than walking through it.
                var side=new Vector3(-direction.Z,0,direction.X);
                if(!MovePattern(Vector3.Normalize(direction+side*1.5f),amount))MovePattern(Vector3.Normalize(direction-side*1.5f),amount);
            }
        }
        if(distance>AttackRange||_cooldownRemaining>0)return;
        if(GameplayQuery3D.Raycast(GameObject.Scene!,Transform.WorldPosition+Vector3.UnitY*.8f,direction,
            out var obstruction,distance,GameObject,includeTriggers:false) && obstruction.GameObject!=target)return;
        if(AdaptiveBoss&&ChooseBossAttack(target))return;
        BeginStandardTelegraph(target,direction);
    }
    private void BeginStandardTelegraph(GameObject target,Vector3 direction)
    {
        GameObject.GetComponent<ByteEngine.Core.Navigation.NavigationAgent3D>()?.Stop();
        _lockedPoint=target.Transform.WorldPosition; _lockedDirection=direction;
        AttackPhase="Windup"; _phaseTime=Math.Max(.05f,WindupSeconds/BossTempo);
        Warning(AttackStyle==EnemyAttackStyle.GroundCaster?_lockedPoint:Transform.WorldPosition,
            AttackStyle==EnemyAttackStyle.GroundCaster?HazardRadius:Math.Max(.45f,CollisionRadius),new(1,.14f,.06f,1));
        if(ShowChargePath&&AttackStyle==EnemyAttackStyle.Charger)
        {
            _chargeTell=GameObject.Scene!.CreateGameObject("Charge path - "+GameObject.Name);
            float length=Math.Max(1,ChargeDistance);
            _chargeTell.Transform.WorldPosition=Transform.WorldPosition+_lockedDirection*length*.5f+Vector3.UnitY*.045f;
            _chargeTell.Transform.EulerAngles=new(0,MathF.Atan2(-_lockedDirection.X,-_lockedDirection.Z)*180/MathF.PI,0);
            _chargeTell.Transform.LocalScale=new(Math.Max(.5f,CollisionRadius*2),.025f,length);
            _chargeTell.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,CastShadows=false,Material=new Material{Shading=MaterialShadingMode.Unlit,BaseColor=new(.8f,.055f,.03f,1)}});
        }
        if(AttackStyle==EnemyAttackStyle.GroundCaster && FallingFireball)
        {
            EnsureFireball();_fireball!.Active=true;
            _fireball.Transform.WorldPosition=_lockedPoint+Vector3.UnitY*Math.Max(1,FireballHeight);
            _fireball.Transform.LocalScale=new(Math.Max(.15f,FireballSize));
            var flame=_fireball.GetComponent<ByteEngine.Core.Vfx.VfxPlayer>()!;if(_effectsWarmed)flame.Play();else flame.PlayOnStart=true;
        }
    }
    private void Navigate(Vector3 destination,float dt)
    {
        if(GameObject.GetComponent<ByteEngine.Core.Navigation.NavigationAgent3D>() is not {} agent)return;
        agent.ExternallyDriven=true;agent.Destination=destination;agent.Speed=Math.Max(0,MoveSpeed);agent.Moving=true;
        var before=Transform.WorldPosition;agent.Tick(dt);IsMoving=Vector3.DistanceSquared(before,Transform.WorldPosition)>.000001f;
    }
    private void EnsureFireball()
    {
        if(_fireball?.Scene==GameObject.Scene)return;
        _fireball=GameObject.Scene!.CreateGameObject("Falling fireball - "+GameObject.Name);
        _fireball.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,CastShadows=false,Material=new Material{BaseColor=new(1,.36f,.025f,1),Shading=MaterialShadingMode.Unlit}});
        _fireball.AddComponent(new ByteEngine.Core.Vfx.VfxPlayer{Effect=FireballEffect,Preset=ByteEngine.Core.Vfx.VfxPreset.Fireball,PlayOnStart=false,Size=1.3f,Brightness=1.7f});
        _fireball.AddComponent(new PointLight{Color=new(1,.25f,.03f),Intensity=3,Range=7});_fireball.Active=false;
    }
    private void EnsureImpact()
    {
        if(_impactPlayer?.GameObject.Scene==GameObject.Scene)return;
        var impact=GameObject.Scene!.CreateGameObject("Fireball impact - "+GameObject.Name);
        _impactPlayer=impact.AddComponent(new ByteEngine.Core.Vfx.VfxPlayer{Effect=ImpactEffect,Preset=ByteEngine.Core.Vfx.VfxPreset.Explosion,PlayOnStart=false,Size=Math.Max(.3f,HazardRadius*.6f),Brightness=1.6f});
    }
    private void PrewarmAttackEffects()
    {
        _effectsWarmed=true;
        if(FallingFireball){EnsureFireball();_fireball!.GetComponent<ByteEngine.Core.Vfx.VfxPlayer>()!.Prewarm();}
        if(FallingFireball||AdaptiveBoss){EnsureImpact();_impactPlayer!.Prewarm();}
    }
    private void SpawnImpact(float radius=-1)
    {
        EnsureImpact();_impactPlayer!.Transform.WorldPosition=_lockedPoint+Vector3.UnitY*.1f;
        _impactPlayer.Size=Math.Max(.3f,(radius<0?HazardRadius:radius)*.6f);if(_effectsWarmed)_impactPlayer.Play();else _impactPlayer.PlayOnStart=true;
    }
    private float BossTempo=>GameObject.GetComponent<CombatArmour3D>() is {IsEnraged:true} a?Math.Clamp(a.EnrageAttackSpeed,1,1.5f):1;
    private void Recover(bool expose=true)
    {
        AttackPhase="Recover"; _phaseTime=Math.Max(.05f,RecoverySeconds);_cooldownRemaining=Math.Max(0,AttackCooldown)/BossTempo;
        GameObject.GetComponent<CombatArmour3D>()?.SetExposed(expose);
        if(AttackStyle==EnemyAttackStyle.Charger)
        {
            if(GameObject.GetComponent<HealthComponent>() is {} h)h.IncomingDamageMultiplier=Math.Max(1,RecoveryDamageMultiplier);
            if(GameObject.GetComponent<CombatArmour3D>()==null)Warning(Transform.WorldPosition,Math.Max(.5f,CollisionRadius),new(1,.8f,.08f,1),RecoverySeconds);
        }
    }
}
