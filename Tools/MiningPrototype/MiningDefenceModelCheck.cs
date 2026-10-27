using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Runtime;
internal static class MiningDefenceModelCheck
{
 public static void Run(GameProjectRuntime runtime)
 {
  foreach(string name in new[]{"Sentry","Repeater","Mortar","Mine","SupplyCache"})
  {
   string path="Assets/Models/Mining/Defences/"+name+".glb";using var doc=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));var reference=new AssetReference(doc.RootElement.GetProperty("guid").GetGuid(),path);var model=runtime.Assets.LoadModel(reference);
   if(model.Skeleton?.Bones.Count<5||model.Animations.Count!=5||model.Meshes.Count==0)throw new Exception("Incomplete rigged defence import: "+name);
   var nodes=model.Nodes.Select(n=>n.Name).ToHashSet();
   foreach(var clip in model.Animations)
   {
    if(!clip.Name.StartsWith(name+"_")||clip.Duration<=0||clip.Channels.Count==0||clip.Channels.Any(c=>!nodes.Contains(c.NodeName)))throw new Exception("Invalid animation binding: "+clip.Name);
   }
   Console.WriteLine($"PASS NATIVE DEFENCE {name}: {model.Meshes.Count} material sections, {model.Skeleton!.Bones.Count} bones, clips "+string.Join(", ",model.Animations.Select(a=>a.Name)));
  }
  Console.WriteLine("DEFENCE MODEL NATIVE IMPORT COMPLETE");
 }
}
