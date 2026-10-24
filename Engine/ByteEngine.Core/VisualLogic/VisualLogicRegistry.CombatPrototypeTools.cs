using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterCombatPrototypeTools(VisualLogicRegistry r)
    {
        var target=ObjectArg();
        Test(r,"combat.coreExposed","Combat / Armour","Weak point is exposed",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<CombatArmour3D>()?.IsExposed==true,target);
        Add(r,"combat.exposeCore","Combat / Armour","Set weak point exposure",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<CombatArmour3D>()?.SetExposed(B(i,c,"enabled",true)),target,BoolArg("enabled","Exposed",true));
        Add(r,"enemyAI.dodge","AI / Tactics","Request enemy dodge",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<SimpleEnemyAI3D>()?.RequestDodge(),target);
        Test(r,"enemyAI.adapted","AI / Tactics","Boss has learned from evaded attacks",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<SimpleEnemyAI3D>() is {} ai&&ai.EvadedCharges+ai.EvadedSlams>=N(i,c,"count",1),target,NumberArg("count","Evaded attacks",1));
        Add(r,"navigation.rebuild","Navigation","Rebuild navigation around current obstacles",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<ByteEngine.Core.Navigation.NavigationRegion3D>()?.Bake(),target);
        Add(r,"combat.configureFeedback","Combat / Feedback","Configure damage numbers and hit knockback",(i,c)=>{if(ResolveObjectTarget(i,c)?.GetComponent<CombatFeedback3D>() is {} feedback){feedback.DamageNumbers=B(i,c,"numbers",true);feedback.Knockback=B(i,c,"knockback",true);feedback.KnockbackSpeed=(float)Math.Clamp(N(i,c,"speed",4),0,20);}},target,BoolArg("numbers","Show damage numbers",true),BoolArg("knockback","Enable knockback",true),NumberArg("speed","Knockback speed",4));
        Add(r,"character.stopMotor","Character Controller 3D","Stop character velocity",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<ByteEngine.Core.Characters.CharacterController3D>()?.SetVelocity(Vector3.Zero),target);
        Test(r,"enemyAI.phase","AI / Attacks","Enemy attack phase is",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<SimpleEnemyAI3D>()?.AttackPhase==S(i,c,"phase","Windup"),target,TextArg("phase","Phase (Ready, Windup, Charge, Recover)","Windup"));
        Add(r,"enemyAI.resetAttack","AI / Attacks","Reset enemy attack",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<SimpleEnemyAI3D>()?.ResetAttackPattern(),target);
        Test(r,"combat.groupDefeated","Combat / Encounters","All enemies in group are defeated",(i,c)=>{
            var root=ResolveObjectTarget(i,c);if(root==null)return false;
            var enemies=c.Scene.GameObjects.Where(o=>{for(var p=o;p!=null;p=p.Parent)if(p==root)return true;return false;}).Select(o=>o.GetComponent<HealthComponent>()).Where(h=>h!=null).ToArray();
            return enemies.Length>0&&enemies.All(h=>h!.IsDead);
        },target);

        Add(r,"character.configureArenaMovement","Character Controller 3D","Configure arena movement",(i,c)=>{
            if(ResolveObjectTarget(i,c)?.GetComponent<ByteEngine.Core.Characters.CharacterController3D>() is {} motor){
                motor.ArenaMovement=B(i,c,"enabled",true);motor.Acceleration=(float)Math.Clamp(N(i,c,"acceleration",95),0,500);
                motor.GroundFriction=(float)Math.Clamp(N(i,c,"friction",8),0,30);motor.StopSpeed=(float)Math.Clamp(N(i,c,"stopSpeed",4),0,30);
                motor.AirAcceleration=(float)Math.Clamp(N(i,c,"airAcceleration",22),0,200);motor.MaximumAirSpeed=(float)Math.Clamp(N(i,c,"airCap",13),0,100);
            }
        },target,BoolArg("enabled","Arena movement",true),NumberArg("acceleration","Ground acceleration",95),NumberArg("friction","Ground friction",8),NumberArg("stopSpeed","Stopping speed",4),NumberArg("airAcceleration","Air steering acceleration",22),NumberArg("airCap","Air speed limit",13));
        Add(r,"combat.damageInRange","Combat / Damage","Damage nearby target at contact",(i,c)=>{
            var attacker=ResolveObjectTarget(i,c);var victim=ResolveObjectArgument(i,"other",c);
            if(attacker==null||victim==null||attacker.GetComponent<HealthComponent>()?.IsDead==true)return;
            if(Vector3.Distance(attacker.Transform.WorldPosition,victim.Transform.WorldPosition)<=N(i,c,"range",2))victim.GetComponent<HealthComponent>()?.Damage((float)N(i,c,"amount",10));
        },target,ObjectArg("other","Damage target"),NumberArg("range","Contact range",2),NumberArg("amount","Damage",10));
        Add(r,"animation.setBaseClip","Animation","Set event-driven base animation",(i,c)=>{if(ResolveObjectTarget(i,c)?.GetComponent<ByteEngine.Core.Animation.AnimationController>() is {} controller){controller.DriveLocomotion=false;controller.ManualBaseClip=S(i,c,"clip","Idle");}},target,TextArg("clip","Looping base clip","Idle"));
        Add(r,"object.setSimulationRate","Object / Time","Set local simulation rate",(i,c)=>{
            if(ResolveObjectTarget(i,c) is {} obj)(obj.GetComponent<SimulationRate3D>()??obj.AddComponent(new SimulationRate3D())).Rate=(float)N(i,c,"rate",1);
        },target,NumberArg("rate","Gameplay and animation rate (0 to 4)",1));
        Add(r,"screen.setStrength","Screen Effects","Set screen overlay strength",(i,c)=>{if(ResolveObjectTarget(i,c)?.GetComponent<ScreenOverlay>() is {} fx)fx.Strength=(float)Math.Clamp(N(i,c,"strength"),0,1);},target,NumberArg("strength","Effect strength (0 to 1)"));
        Add(r,"resource.changePerSecond","Gameplay / Resources","Change resource per second",(i,c)=>{
            if(ResolveObjectTarget(i,c)?.GetComponent<ResourceMeter3D>() is {} meter){double dt=B(i,c,"unscaled",true)?Time.UnscaledDeltaTime:Time.DeltaTime;meter.SetAmount(meter.Current+(float)(N(i,c,"rate")*Math.Max(0,dt)));}
        },target,NumberArg("rate","Units per second (negative drains)",10),BoolArg("unscaled","Use real time",true));
        Test(r,"resource.atLeast","Gameplay / Resources","Resource is at least",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<ResourceMeter3D>()?.Current>=N(i,c,"amount",1),target,NumberArg("amount","Amount",1));
        Add(r,"ui.setBarFromResource","UI","Set bar from resource",(i,c)=>{if(ResolveObjectTarget(i,c)?.GetComponent<UiWidget>() is {Kind:UiWidgetKind.ProgressBar} bar&&ResolveObjectArgument(i,"source",c)?.GetComponent<ResourceMeter3D>() is {} meter){bar.Maximum=meter.Capacity;bar.Value=meter.Current;}},target,ObjectArg("source","Resource owner"));
        Add(r,"projectile.fireAtObject","Gameplay / Weapons","Fire projectile at object",(i,c)=>{
            var obj=ResolveObjectTarget(i,c);var other=ResolveObjectArgument(i,"other",c);if(obj?.GetComponent<ProjectileLauncher3D>() is not {} launcher||other==null||obj.GetComponent<HealthComponent>()?.IsDead==true)return;
            var offset=EventValueResolver.GetVector3(i,"offset",c,new(0,1,0));var muzzle=Vector3.Transform(launcher.MuzzleOffset,obj.Transform.WorldMatrix);
            var projectile=launcher.Fire(other.Transform.WorldPosition+offset-muzzle);
            var clock=ResolveObjectArgument(i,"clock",c,false);if(projectile!=null&&clock?.GetComponent<SimulationRate3D>()!=null)projectile.AddComponent(new SimulationRate3D{SourceObjectId=clock.Id});
        },target,ObjectArg("other","Aim target"),new("offset","Aim offset",VariableValue.FromVector3(new(0,1,0))),ObjectArg("clock","Projectile clock object (optional)"));
        Test(r,"object.hasLineOfSight","Object / Spatial","Has clear line of sight",(i,c)=>{
            var a=ResolveObjectTarget(i,c);var b=ResolveObjectArgument(i,"other",c);if(a==null||b==null)return false;
            var from=a.Transform.WorldPosition+EventValueResolver.GetVector3(i,"fromOffset",c,new(0,1,0));var to=b.Transform.WorldPosition+EventValueResolver.GetVector3(i,"toOffset",c,new(0,1,0));var delta=to-from;float distance=delta.Length();if(distance<.001f)return true;
            return !GameplayQuery3D.Raycast(c.Scene,from,delta/distance,out var hit,distance,a,includeTriggers:false)||hit.GameObject==b;
        },target,ObjectArg("other","Target object"),new("fromOffset","Eye offset",VariableValue.FromVector3(new(0,1,0))),new("toOffset","Target offset",VariableValue.FromVector3(new(0,1,0))));
    }
}
