using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Text.Json.Nodes;

using ByteEngine.Core.Scene;

using ByteEngine.Core.Serialization;

using ByteEngine.Core.Serialization.SerializationModels;



namespace GoblinScrapper.Construction;



internal sealed class VehicleBuilder3DCodec : IComponentCodec

{

    public string TypeName => "bytebard.goblinscrapper.VehicleBuilder3D";

    public Type ComponentType => typeof(VehicleBuilder3D);

    public ComponentData Serialize(Component component, ComponentSerializationContext context)

    {

        var builder=(VehicleBuilder3D)component;

        return new ComponentData { Type=TypeName, Properties=new JsonObject

        { ["useBuiltInToolbarActions"]=builder.UseBuiltInToolbarActions,["useAuthoredScene"]=builder.UseAuthoredScene,["battleDuration"]=builder.BattleDuration,["enemyMoveSpeed"]=builder.EnemyMoveSpeed,["enemyAttackDamage"]=builder.EnemyAttackDamage,["enemyAttackInterval"]=builder.EnemyAttackInterval,["enemyModel"]=builder.EnemyModel,["jamLandPalette"]=builder.JamLandPalette,["battlefieldEnabled"]=builder.BattlefieldEnabled,["battlefieldEnemyCount"]=builder.BattlefieldEnemyCount,["partsDirectory"]=builder.PartsDirectory, ["maximumSpeed"]=builder.MaximumSpeed, ["roadGrip"]=builder.RoadGrip, ["driftGrip"]=builder.DriftGrip, ["freeBuilding"]=builder.FreeBuilding, ["useBuiltInControls"]=builder.UseBuiltInControls, ["useBuiltInPointerControls"]=builder.UseBuiltInPointerControls, ["automaticCamera"]=builder.AutomaticCamera, ["showWorkshopHud"]=builder.ShowWorkshopHud } };

    }

    public Component Deserialize(ComponentData data, ComponentSerializationContext context) => new VehicleBuilder3D

    {

        UseBuiltInToolbarActions=data.Properties["useBuiltInToolbarActions"]?.GetValue<bool>()??true,
        UseAuthoredScene=data.Properties["useAuthoredScene"]?.GetValue<bool>()??false,
        BattleDuration=data.Properties["battleDuration"]?.GetValue<float>()??120,
        EnemyMoveSpeed=data.Properties["enemyMoveSpeed"]?.GetValue<float>()??1.5f,
        EnemyAttackDamage=data.Properties["enemyAttackDamage"]?.GetValue<float>()??5,
        EnemyAttackInterval=data.Properties["enemyAttackInterval"]?.GetValue<float>()??1.5f,
        EnemyModel=data.Properties["enemyModel"]?.GetValue<string>()??"Assets/Battlefield/red-goblin.glb",
        JamLandPalette=data.Properties["jamLandPalette"]?.GetValue<bool>()??false,
        BattlefieldEnabled=data.Properties["battlefieldEnabled"]?.GetValue<bool>()??false,
        BattlefieldEnemyCount=data.Properties["battlefieldEnemyCount"]?.GetValue<int>()??18,
        UseBuiltInControls=data.Properties["useBuiltInControls"]?.GetValue<bool>() ?? true,
        UseBuiltInPointerControls=data.Properties["useBuiltInPointerControls"]?.GetValue<bool>() ?? true,
        AutomaticCamera=data.Properties["automaticCamera"]?.GetValue<bool>() ?? true,
        ShowWorkshopHud=data.Properties["showWorkshopHud"]?.GetValue<bool>() ?? true,
        FreeBuilding=data.Properties["freeBuilding"]?.GetValue<bool>() ?? true,

        Assets=context.Assets, ProjectRoot=context.ProjectRoot,

        PartsDirectory=data.Properties["partsDirectory"]?.GetValue<string>() ?? "Assets/refinded parts",

        MaximumSpeed=data.Properties["maximumSpeed"]?.GetValue<float>() ?? 12,

        RoadGrip=data.Properties["roadGrip"]?.GetValue<float>() ?? 12,

        DriftGrip=data.Properties["driftGrip"]?.GetValue<float>() ?? .9f

    };

}
