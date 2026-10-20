using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

internal static class PushAudit
{
 internal static void Align(){
  using var runtime=new GameProjectRuntime(Program.Project,Console.WriteLine);var target=runtime.Assets.LoadModel(runtime.Database.ResolveReference("Assets/Animations/Longboard/neutral ride.fbx"));var sourceRef=runtime.Database.ResolveReference("Assets/Animations/Longboard/push.fbx");var source=runtime.Assets.LoadModel(sourceRef);var clip=source.Animations.First();
  string settingsPath=Program.Root+"/ArtSource/Animation/PushAlignment.json";Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
  if(!File.Exists(settingsPath))File.WriteAllText(settingsPath,"{\"rootNode\":\"RootNode\",\"yawDegrees\":130.68,\"loopBlendFraction\":0.18}");
  var settings=JsonDocument.Parse(File.ReadAllText(settingsPath)).RootElement;string rootName=settings.GetProperty("rootNode").GetString()!;float yaw=settings.GetProperty("yawDegrees").GetSingle()*MathF.PI/180;float seam=Math.Clamp(settings.GetProperty("loopBlendFraction").GetSingle(),0,.4f);
  if(target.Nodes.All(n=>n.Name!=rootName)||clip.Channels.Any(c=>c.NodeName==rootName))throw new Exception("Alignment expects a static common model root.");
  float Weight(float time){float t=Math.Clamp((time/clip.Duration-(1-seam))/Math.Max(.001f,seam),0,1);return t*t*(3-2*t);}
  ImportedVectorTrack? V(ImportedVectorTrack? track)=>track==null||track.Keys.Count==0?track:new(){Interpolation=ImportedAnimationInterpolation.Linear,Keys=track.Keys.Select(k=>new ImportedVectorKey(k.Time,Vector3.Lerp(k.Value,track.Keys[0].Value,Weight(k.Time)),Vector3.Zero,Vector3.Zero)).ToList()};
  ImportedQuaternionTrack? Q(ImportedQuaternionTrack? track)=>track==null||track.Keys.Count==0?track:new(){Interpolation=ImportedAnimationInterpolation.Linear,Keys=track.Keys.Select(k=>new ImportedQuaternionKey(k.Time,Quaternion.Slerp(k.Value,track.Keys[0].Value,Weight(k.Time)),Quaternion.Identity,Quaternion.Identity)).ToList()};
  var channels=clip.Channels.Select(c=>new ImportedAnimationChannel{NodeName=c.NodeName,Translation=V(c.Translation),Rotation=Q(c.Rotation),Scale=V(c.Scale)}).ToList();var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);channels.Add(new(){NodeName=rootName,Rotation=new(){Keys=[new(0,rotation,Quaternion.Identity,Quaternion.Identity),new(clip.Duration,rotation,Quaternion.Identity,Quaternion.Identity)]}});
  var aligned=new ImportedAnimation{Key="downhill:push-aligned",Name="Push Aligned",Duration=clip.Duration,Channels=channels};ModelOwnedAnimationStore.Bake(Program.Root,target,aligned,"Push Aligned",source.Guid,sourceRef.CachedProjectPath,clip.Key,clip.Name,true);
  string scenePath=Program.Root+"/Scenes/Main.bytescene";var data=JsonNode.Parse(File.ReadAllText(scenePath))!;foreach(var obj in data["gameObjects"]!.AsArray())foreach(var component in obj!["components"]!.AsArray())if(component!["type"]!.GetValue<string>()=="bytebard.downhill.BoardRider3D")component["properties"]!["pushClip"]="Push Aligned";File.WriteAllText(scenePath,data.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Baked aligned, loop-closed push through native model-owned animation storage; updated only the rider's push clip reference.");
 }
 internal static void Run(){
  using var runtime=new GameProjectRuntime(Program.Project,Console.WriteLine);
  var scene=new Scene("Animation audit");var obj=scene.CreateGameObject("Raw rider");var renderer=obj.AddComponent(new SkeletalMeshRenderer{Model=runtime.Database.ResolveReference("Assets/Animations/Longboard/neutral ride.fbx")});renderer.ResolveRuntimeResources();
  foreach(var n in renderer.ResolvedModel!.Nodes.Take(12)){Matrix4x4.Decompose(n.LocalTransform,out var s,out var q,out var p);Console.WriteLine($"NODE {n.Name} parent={n.ParentKey} s={s} r={q} p={p}");}
  Console.WriteLine("ROOT CHANNELS "+string.Join(" / ",renderer.ResolvedModel.Animations.First(a=>a.Name=="Push").Channels.Take(7).Select(c=>c.NodeName)));
  var rows=new List<object>();
  foreach(string clip in new[]{"Neutral","Push","Push Aligned","Foot Brake"}.Where(renderer.HasAnimation)){
   if(!renderer.Play(clip,true,0))throw new Exception("Missing "+clip);
   Console.WriteLine("CLIP "+clip+" duration="+renderer.Duration);
   var left=new List<Vector3>();var right=new List<Vector3>();
   for(int i=0;i<=40;i++){
    renderer.Seek(renderer.Duration*i/40f);var bones=new Dictionary<string,float[]>();
    foreach(string bone in new[]{"LeftFoot","RightFoot","LeftToeBase","RightToeBase","Hips","Head","LeftArm","RightArm","LeftUpLeg","RightUpLeg"})if(renderer.TryGetBoneModelMatrix("mixamorig:"+bone,out var m)){var p=m.Translation;bones[bone]=[p.X,p.Y,p.Z];if(bone=="LeftFoot")left.Add(p);if(bone=="RightFoot")right.Add(p);}
    rows.Add(new{clip,phase=i/40f,bones});if(i%10==0)Console.WriteLine($"{i/40f:0.00} L={left.Last()} R={right.Last()} shoulders={string.Join(',',bones["LeftArm"])} / {string.Join(',',bones["RightArm"])}");
   }
   Console.WriteLine("RANGE Left="+(left.Aggregate(Vector3.Max)-left.Aggregate(Vector3.Min))+" Right="+(right.Aggregate(Vector3.Max)-right.Aggregate(Vector3.Min)));
  }
  File.WriteAllText(Program.Root+"/Research/Push-motion-audit.json",JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
 }
}
