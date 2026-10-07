using ByteEngine.Core.Gameplay;
using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace DesertTerrain;
public sealed class DesertTerrainPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterComponent(new TerrainCodec(),new("Desert Terrain 3D","World","Heightmap landscape with persistent wheel ruts and sand compaction.","desert sand terrain heightfield dunes heightmap png"));
        context.RegisterSimpleComponent<DesertRoad3D>("DesertRoad3D",new("Desert Road 3D","World","Editable haul road conforming to the desert heightfield.","road route desert path"));
        context.RegisterSimpleComponent<SandContact3D>("SandContact3D",new("Sand Contact 3D","Physics","Attach to a tyre's ground contact point to emit ruts.","wheel tyre track sand"));
        context.RegisterSimpleComponent<DesertSandbox3D>("DesertSandbox3D",new("Desert Sandbox 3D","Gameplay","Test rover for heightmap terrain and sand traction.","sand demo rover"));
        context.RegisterAction(new(){Id="bytebard.desertterrain.regenerate",Category="Desert Terrain",DisplayName="Reload Terrain Source",Execute=(_,e)=>e.Self.GetComponent<DesertTerrain3D>()?.Regenerate()});
        context.RegisterAction(new(){Id="bytebard.desertterrain.reset",Category="Desert Terrain",DisplayName="Reset Wheel Tracks",Execute=(_,e)=>e.Self.GetComponent<DesertTerrain3D>()?.ResetSand()});
        context.RegisterAction(new(){Id="bytebard.desertterrain.save",Category="Desert Terrain",DisplayName="Save Wheel Tracks",Execute=(_,e)=>e.Self.GetComponent<DesertTerrain3D>()?.SaveSandToDisk()});
        context.RegisterAction(new(){Id="bytebard.desertterrain.load",Category="Desert Terrain",DisplayName="Load Wheel Tracks",Execute=(_,e)=>e.Self.GetComponent<DesertTerrain3D>()?.LoadSandFromDisk()});

    }
}
internal sealed class TerrainCodec : IComponentCodec
{
    public string TypeName=>"bytebard.desertterrain.DesertTerrain3D";
    public Type ComponentType=>typeof(DesertTerrain3D);
    private static readonly PropertyInfo[] Settings=typeof(DesertTerrain3D).GetProperties().Where(p=>p.DeclaringType==typeof(DesertTerrain3D)&&p.Name is not ("ResetDeformationRequested" or "ReloadHeightmapRequested")&&p.CanRead&&p.SetMethod?.IsPublic==true&&(p.PropertyType==typeof(int)||p.PropertyType==typeof(float)||p.PropertyType==typeof(bool))).ToArray();
    public ComponentData Serialize(Component component,ComponentSerializationContext context)
    {
        var terrain=(DesertTerrain3D)component;terrain.ProjectRoot=context.ProjectRoot;terrain.EnsureGenerated();var p=new JsonObject();
        foreach(var prop in Settings){var value=prop.GetValue(terrain);p[char.ToLowerInvariant(prop.Name[0])+prop.Name[1..]]=value switch{int i=>JsonValue.Create(i),float f=>JsonValue.Create(f),bool b=>JsonValue.Create(b),_=>null};}
        p["packedSurfaceMaskPath"]=terrain.PackedSurfaceMaskPath;p["sandAlbedoPath"]=terrain.SandAlbedoPath;p["sandNormalPath"]=terrain.SandNormalPath;p["sandRoughnessPath"]=terrain.SandRoughnessPath;p["heightmapPath"]=terrain.HeightmapPath;p["heightmapHash"]=terrain.HeightmapContentHash;
        p["sandColor"]=new JsonArray(terrain.SandColor.X,terrain.SandColor.Y,terrain.SandColor.Z,terrain.SandColor.W);
        p["autoLoadSavedSand"]=terrain.AutoLoadSavedSand;p["useProjectMatrix"]=terrain.UseProjectMatrix;
        var layers=new JsonArray();for(int layer=0;layer<32;layer++)if(terrain.CollisionMask.Contains(layer))layers.Add(layer);p["collisionLayers"]=layers;
        p["center"]=new JsonArray(terrain.Center.X,terrain.Center.Y,terrain.Center.Z);p["isTrigger"]=terrain.IsTrigger;p["sandState"]=terrain.ExportSand();p["sculptState"]=terrain.ExportSculpt();
        return new(){Type=TypeName,Properties=p};
    }
    public Component Deserialize(ComponentData data,ComponentSerializationContext context)
    {
        var t=new DesertTerrain3D{ProjectRoot=context.ProjectRoot,PackedSurfaceMaskPath=data.Properties["packedSurfaceMaskPath"]?.GetValue<string>()??"",SandAlbedoPath=data.Properties["sandAlbedoPath"]?.GetValue<string>()??"",SandNormalPath=data.Properties["sandNormalPath"]?.GetValue<string>()??"",SandRoughnessPath=data.Properties["sandRoughnessPath"]?.GetValue<string>()??"",HeightmapPath=data.Properties["heightmapPath"]?.GetValue<string>()??""};
        foreach(var prop in Settings)if(data.Properties[char.ToLowerInvariant(prop.Name[0])+prop.Name[1..]] is JsonNode node)prop.SetValue(t,prop.PropertyType==typeof(int)?(object)node.GetValue<int>():prop.PropertyType==typeof(bool)?(object)node.GetValue<bool>():node.GetValue<float>());
        if(data.Properties["sandColor"] is JsonArray c&&c.Count==4)t.SandColor=new(c[0]!.GetValue<float>(),c[1]!.GetValue<float>(),c[2]!.GetValue<float>(),c[3]!.GetValue<float>());
        if(data.Properties["center"] is JsonArray a&&a.Count==3)t.Center=new(a[0]!.GetValue<float>(),a[1]!.GetValue<float>(),a[2]!.GetValue<float>());
        t.AutoLoadSavedSand=data.Properties["autoLoadSavedSand"]?.GetValue<bool>()??true;
        t.UseProjectMatrix=data.Properties["useProjectMatrix"]?.GetValue<bool>()??true;
        if(data.Properties["collisionLayers"] is JsonArray layers)t.CollisionMask=ByteEngine.Core.Classification.LayerMask.FromLayers(layers.Select(n=>n!.GetValue<int>()).ToArray());
        t.IsTrigger=data.Properties["isTrigger"]?.GetValue<bool>()??false;
        t.EnsureGenerated();
        try{t.ImportSculpt(data.Properties["sculptState"]?.GetValue<string>()??"");}catch(InvalidDataException e){context.WarningSink?.Invoke("Authored terrain layer skipped: "+e.Message);}
        string? expectedHash=data.Properties["heightmapHash"]?.GetValue<string>();
        if(expectedHash==null||expectedHash==t.HeightmapContentHash)t.ImportSand(data.Properties["sandState"]?.GetValue<string>()??"");
        else context.WarningSink?.Invoke("Heightmap changed; old wheel ruts were discarded for the new landscape.");
        return t;
    }
}
