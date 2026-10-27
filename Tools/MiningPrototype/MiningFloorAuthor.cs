using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
internal static class MiningFloorAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/full-sand-floor-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  string folder="Assets/Environment/MiningSandbox",parentPath=folder+"/OrangeSand.bmat";using var source=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+parentPath+".meta"));var parent=new AssetReference(source.RootElement.GetProperty("guid").GetGuid(),parentPath);
  foreach(var name in new[]{"Alien worksite floor","Ground"})
  {
   var floor=scene.FindGameObject(name)??throw new Exception("Missing floor: "+name);var renderer=floor.GetComponent<MeshRenderer>()??throw new Exception("Missing floor mesh: "+name);Matrix4x4.Decompose(floor.Transform.WorldMatrix,out var size,out _,out var centre);
   // Cube top U follows +X, V follows -Z. Preserve the approved sample's
   // twelve/fourteen-metre texture scale and one continuous world-space phase.
   var tile=new Vector2(Math.Abs(size.X)/12,-Math.Abs(size.Z)/14);var offset=new Vector2((centre.X-Math.Abs(size.X)/2-4)/12,(centre.Z+Math.Abs(size.Z)/2+11)/14);
   var material=new MaterialAsset{Kind=MaterialAssetKind.Instance,Name="Orange sand - "+name,ParentMaterial=parent};material.SetOverride(nameof(MaterialParameters.UvTiling),tile);material.SetOverride(nameof(MaterialParameters.UvOffset),offset);
   string file=folder+"/OrangeSand-"+(name=="Ground"?"OuterGround":"Worksite")+".bmat";MaterialAssetSerializer.Save(Program.Root+"/"+file,material);string meta=Program.Root+"/"+file+".meta";if(!File.Exists(meta))File.WriteAllText(meta,JsonSerializer.Serialize(new{guid=Guid.NewGuid(),type="Material"}));using var id=JsonDocument.Parse(File.ReadAllText(meta));renderer.MaterialAssetReference=new(id.RootElement.GetProperty("guid").GetGuid(),file);renderer.MaterialOverrides.Reset();Console.WriteLine("SAND FLOOR "+name+" scale "+tile+" offset "+offset);
  }
  if(scene.FindGameObject("Orange sand ground sample") is {} sample)sample.Active=false;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("FULL SAND FLOOR INSTALLED; collision, navigation, camp positions and accepted sky retained. Backup "+backup);
 }
}
