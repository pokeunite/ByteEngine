using ByteEngine.Core.Plugins;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
namespace Karlson.Plugin;

public sealed class KarlsonPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterSimpleComponent<ParkourPlayer3D>("ParkourPlayer3D",new("Parkour Player","Character","Fixed-step parkour intent over the native character motor; slide, wall run, grapple and physics grab.","karlson parkour wall run grapple"));
        context.RegisterComponent(new KarlsonDirectorCodec(),new("Karlson Course Director","Gameplay","Course/menu/results state and native HUD bindings.","karlson course milk timer"));
        context.RegisterSimpleComponent<KarlsonWeapon3D>("KarlsonWeapon3D",new("Physical Weapon","Gameplay","Pickup, equip, native hitscan and thrown weapon impacts.","karlson gun weapon throw"));
        context.RegisterSimpleComponent<KarlsonEnemy3D>("KarlsonEnemy3D",new("Course Enemy","AI","Budgeted line-of-sight sentry or pursuit; physical knockdown on death.","karlson enemy sentry"));
        context.RegisterSimpleComponent<KarlsonProp3D>("KarlsonProp3D",new("Physics Course Prop","Gameplay","Throwable props, breakable glass and explosive barrels.","karlson barrel glass prop"));
        context.RegisterSimpleComponent<MilkGoal3D>("MilkGoal3D",new("Milk Goal","Gameplay","Goal detection exposed to ByteGraph; completion is authored in events.","karlson milk goal"));
        context.RegisterSimpleComponent<CourseGate3D>("CourseGate3D",new("Course Gate","Gameplay","ByteGraph-driven gate opening.","karlson gate door"));
        context.RegisterCondition(new(){Id="bytebard.karlson.goalReached",Category="Karlson",DisplayName="Player Reached Milk",Evaluate=(_,e)=>e.Self.GetComponent<MilkGoal3D>()?.Reached==true});
        context.RegisterAction(new(){Id="bytebard.karlson.completeCourse",Category="Karlson",DisplayName="Complete Course",Execute=(_,e)=>e.Self.Scene?.FindComponent<KarlsonDirector3D>()?.Complete()});
        context.RegisterCondition(new(){Id="bytebard.karlson.enemiesCleared",Category="Karlson",DisplayName="All Course Enemies Cleared",Evaluate=(_,e)=>e.Self.Scene?.FindComponent<KarlsonDirector3D>()?.EnemiesCleared==true});
        context.RegisterAction(new(){Id="bytebard.karlson.openGate",Category="Karlson",DisplayName="Open Course Gate",Execute=(_,e)=>e.Self.GetComponent<CourseGate3D>()?.Open()});
        context.RegisterAction(new(){Id="bytebard.karlson.restart",Category="Karlson",DisplayName="Restart Course",Execute=(_,e)=>e.Self.Scene?.RequestRestart()});
    }
}
