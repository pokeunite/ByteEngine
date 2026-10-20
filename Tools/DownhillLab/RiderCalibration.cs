using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

internal static class RiderCalibration
{
 internal static void ImportHands(){
  using(var db=new ByteEngine.Core.Assets.AssetDatabase(Program.Root,["Assets","Scenes"]))db.Scan();
  using var runtime=new GameProjectRuntime(Program.Project,Console.WriteLine);runtime.Database.Scan();var target=runtime.Assets.LoadModel(runtime.Database.ResolveReference("Assets/Animations/Longboard/neutral ride.fbx"));
  foreach(var pair in new[]{("Hands Down Heel","17_Heelside_Hand_Drag_Carve.fbx"),("Hands Down Toe","18_Toeside_Hand_Drag_Carve.fbx")}){var reference=runtime.Database.ResolveReference("Assets/Animations/Longboard/"+pair.Item2);var source=runtime.Assets.LoadModel(reference);var clip=source.Animations.First();ModelOwnedAnimationStore.Bake(Program.Root,target,clip,pair.Item1,source.Guid,reference.CachedProjectPath,clip.Key,clip.Name,true);Console.WriteLine("Imported "+pair.Item1);}
 }
 internal static void Audit(){
  using var runtime=new GameProjectRuntime(Program.Project,Console.WriteLine);var reference=runtime.Database.ResolveReference("Assets/Animations/Longboard/neutral ride.fbx");var model=runtime.Assets.LoadModel(reference);var scene=new Scene("Source pose audit");var r=scene.CreateGameObject("Raw").AddComponent(new SkeletalMeshRenderer{Model=reference});r.ResolveRuntimeResources();
  Vector3 B(string n){r.TryGetBoneModelMatrix("mixamorig:"+n,out var m);return Vector3.Transform(m.Translation,Quaternion.CreateFromAxisAngle(Vector3.UnitY,-1.4869888f));}
  foreach(var clip in model.Animations.Where(a=>a.Name.EndsWith(" Board"))){r.Play(clip.Name,true,0);for(int i=1;i<=4;i++){r.Seek(clip.Duration*i/5);Console.WriteLine($"AUDIT {clip.Name} phase={i/5f} left={B("LeftFoot")} right={B("RightFoot")} shoulders={B("RightArm")-B("LeftArm")} hips={B("Hips")} head={B("Head")} hands={(B("LeftHand")+B("RightHand"))*.5f} Lhand={B("LeftHand")} Rhand={B("RightHand")} Larm={B("LeftArm")} Rarm={B("RightArm")}");}}
 }
 internal static void Run(){
  using var runtime=new GameProjectRuntime(Program.Project,Console.WriteLine);var reference=runtime.Database.ResolveReference("Assets/Animations/Longboard/neutral ride.fbx");var model=runtime.Assets.LoadModel(reference);
  var scene=new Scene("Rider calibration");var renderer=scene.CreateGameObject("Raw motion").AddComponent(new SkeletalMeshRenderer{Model=reference});renderer.ResolveRuntimeResources();
  Vector3 Bone(string name){if(!renderer.TryGetBoneModelMatrix("mixamorig:"+name,out var matrix))throw new Exception("Missing bone "+name);return matrix.Translation;}
  renderer.Play("Neutral",true,0);renderer.Seek(renderer.Duration*.6f);Vector3 left=Bone("LeftFoot"),right=Bone("RightFoot"),across=right-left;
  float ownerYaw=MathF.Atan2(-across.X,across.Z);var ownerRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,ownerYaw);var center=Vector3.Transform((left+right)*.5f,ownerRotation);Vector3 ownerOffset=new(-center.X,.024f,-center.Z);
  var bind=new Dictionary<string,Matrix4x4>();foreach(var node in model.Nodes)bind[node.Key]=node.LocalTransform*(node.ParentKey!=null?bind[node.ParentKey]:Matrix4x4.Identity);
  Matrix4x4 Bind(string name)=>bind[model.Nodes.First(n=>n.Name=="mixamorig:"+name).Key];
  Quaternion FootRotation(string side,Vector3 desired){var foot=Bind(side+"Foot");var toe=Bind(side+"ToeBase");Matrix4x4.Decompose(foot,out _,out var q,out _);Vector3 direction=toe.Translation-foot.Translation;direction.Y=0;direction=Vector3.Normalize(direction);var turn=AnimationPoseMath.FromTo(direction,Vector3.Normalize(desired));return Quaternion.Normalize(turn*q);}
  var leftRotation=FootRotation("Left",new(1,0,-.35f));var rightRotation=FootRotation("Right",new(1,0,-.08f));var brakeRotation=FootRotation("Right",-Vector3.UnitZ);
  var leftForward=FootRotation("Left",-Vector3.UnitZ);
  float leftY=Bind("LeftFoot").Translation.Y+.024f,rightY=Bind("RightFoot").Translation.Y+.024f;
  Console.WriteLine($"CALIBRATION yaw={ownerYaw*180/MathF.PI} offset={ownerOffset} ankleHeights={leftY}/{rightY}");
  var specs=new[]{("Neutral","Neutral Board",.45f,1f,false),("Soft Heel","Soft Heel Board",.4f,.9f,false),("Soft Toe","Soft Toe Board",.4f,.9f,false),("Hard Heel","Hard Heel Board",.4f,.9f,false),("Hard Toe","Hard Toe Board",.4f,.9f,false),("Tuck","Tuck Board",.4f,.9f,false),("Tuck Heel","Tuck Heel Board",.4f,.9f,false),("Tuck Toe","Tuck Toe Board",.15f,.4f,false),("Heelside Slide","Heelside Slide Board",.35f,.75f,false),("Toeside Slide","Toeside Slide Board",.35f,.75f,false),("Hands Down Heel","Hands Down Heel Board",.35f,.75f,false),("Hands Down Toe","Hands Down Toe Board",.35f,.75f,false),("Foot Brake","Foot Brake Board",.45f,.75f,true),("Push","Push Board",0f,1f,true)};
  var summary=new List<object>();
  foreach(var spec in specs){var source=model.Animations.First(a=>a.Name==spec.Item1);renderer.Play(spec.Item1,true,0);Vector3 feetAxis=Vector3.Zero,shoulders=Vector3.Zero,midpoint=Vector3.Zero;
   Vector3 facing=Vector3.Zero;
   for(int i=0;i<5;i++){renderer.Seek(source.Duration*float.Lerp(spec.Item3,spec.Item4,.3f+i*.1f));var l=Bone("LeftFoot");var r=Bone("RightFoot");feetAxis+=r-l;shoulders+=Bone("RightArm")-Bone("LeftArm");facing+=Bone("Head")-Bone("Hips");midpoint+=(l+r)*.5f;}
   midpoint/=5;float worldYaw=spec.Item5?MathF.Atan2(shoulders.Z,shoulders.X):MathF.Atan2(-feetAxis.X,feetAxis.Z);
   if(spec.Item1=="Push")worldYaw=.052f;
   if(spec.Item1.StartsWith("Tuck"))worldYaw=MathF.Atan2(facing.X,-facing.Z);
   float correction=worldYaw-ownerYaw;var worldCenter=Vector3.Transform(midpoint,Quaternion.CreateFromAxisAngle(Vector3.UnitY,worldYaw));Vector3 worldShift=new(-worldCenter.X-ownerOffset.X,0,-worldCenter.Z-ownerOffset.Z);
   Vector3 modelShift=Vector3.Transform(worldShift,Quaternion.Inverse(ownerRotation));var import=model.Nodes.First(n=>n.ParentKey==null).LocalTransform;Matrix4x4.Invert(import,out var inverseImport);Vector3 rootShift=Vector3.TransformNormal(modelShift,inverseImport);
   // Push has a deliberately free right foot; center its supporting foot rather than the moving pair.
   if(spec.Item1 is "Push" or "Foot Brake"){renderer.Seek(source.Duration*.6f);var support=Vector3.Transform(Bone("LeftFoot"),Quaternion.CreateFromAxisAngle(Vector3.UnitY,worldYaw));worldShift=new(-.025f-support.X-ownerOffset.X,0,-.24f-support.Z-ownerOffset.Z);rootShift=Vector3.TransformNormal(Vector3.Transform(worldShift,Quaternion.Inverse(ownerRotation)),inverseImport);}
   float start=source.Duration*spec.Item3,end=source.Duration*spec.Item4,duration=end-start;int frames=Math.Max(2,(int)Math.Ceiling(duration*30));float Seam(float t){float x=Math.Clamp((t/duration-.82f)/.18f,0,1);return x*x*(3-2*x);}
   Vector3 SampleV(ImportedVectorTrack track,float t){var k=track.Keys;int i=0;while(i+1<k.Count&&k[i+1].Time<t)i++;if(i+1==k.Count)return k[i].Value;return Vector3.Lerp(k[i].Value,k[i+1].Value,Math.Clamp((t-k[i].Time)/Math.Max(1e-6f,k[i+1].Time-k[i].Time),0,1));}
   Quaternion SampleQ(ImportedQuaternionTrack track,float t){var k=track.Keys;int i=0;while(i+1<k.Count&&k[i+1].Time<t)i++;if(i+1==k.Count)return k[i].Value;return Quaternion.Slerp(k[i].Value,k[i+1].Value,Math.Clamp((t-k[i].Time)/Math.Max(1e-6f,k[i+1].Time-k[i].Time),0,1));}
   ImportedVectorTrack? V(ImportedVectorTrack? track)=>track==null||track.Keys.Count==0?null:track.Keys.All(k=>Vector3.DistanceSquared(k.Value,track.Keys[0].Value)<1e-10f)?new(){Keys=[new(0,track.Keys[0].Value,Vector3.Zero,Vector3.Zero)]}:new(){Keys=Enumerable.Range(0,frames+1).Select(i=>{float t=duration*i/frames;return new ImportedVectorKey(t,Vector3.Lerp(SampleV(track,start+t),SampleV(track,start),Seam(t)),Vector3.Zero,Vector3.Zero);}).ToList()};
   ImportedQuaternionTrack? Q(ImportedQuaternionTrack? track)=>track==null||track.Keys.Count==0?null:track.Keys.All(k=>Math.Abs(Quaternion.Dot(k.Value,track.Keys[0].Value))>.999999f)?new(){Keys=[new(0,track.Keys[0].Value,Quaternion.Identity,Quaternion.Identity)]}:new(){Keys=Enumerable.Range(0,frames+1).Select(i=>{float t=duration*i/frames;return new ImportedQuaternionKey(t,Quaternion.Slerp(SampleQ(track,start+t),SampleQ(track,start),Seam(t)),Quaternion.Identity,Quaternion.Identity);}).ToList()};
   var channels=source.Channels.Where(c=>c.NodeName!="RootNode").Select(c=>new ImportedAnimationChannel{NodeName=c.NodeName,Translation=V(c.Translation),Rotation=Q(c.Rotation),Scale=V(c.Scale)}).ToList();var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,correction);channels.Add(new(){NodeName="RootNode",Rotation=new(){Keys=[new(0,rotation,Quaternion.Identity,Quaternion.Identity),new(duration,rotation,Quaternion.Identity,Quaternion.Identity)]},Translation=new(){Keys=[new(0,rootShift,Vector3.Zero,Vector3.Zero),new(duration,rootShift,Vector3.Zero,Vector3.Zero)]}});
   ModelOwnedAnimationStore.Bake(Program.Root,model,new(){Key="downhill:"+spec.Item2,Name=spec.Item2,Duration=duration,Channels=channels},spec.Item2,model.Guid,reference.CachedProjectPath,source.Key,source.Name,true);summary.Add(new{source=spec.Item1,result=spec.Item2,start=spec.Item3,end=spec.Item4,yawDegrees=worldYaw*180/MathF.PI,rootShift});Console.WriteLine($"CLIP {spec.Item2} world yaw={worldYaw*180/MathF.PI:0.0}");
  }
  var profilePath=Program.Root+"/Assets/Animations/Longboard/Riding.byteanim";var profile=AnimationProfileSerializer.Load(profilePath);foreach(var space in profile.BlendSpaces)foreach(var sample in space.Samples)if(!sample.Clip.EndsWith(" Board"))sample.Clip+=" Board";
  foreach(string side in new[]{"Left","Right"})if(!profile.Procedural.IkChains.Any(c=>c.Name==side+"Hand"))profile.Procedural.IkChains.Add(new(){Name=side+"Hand",RootBone="mixamorig:"+side+"Arm",MidBone="mixamorig:"+side+"ForeArm",EndBone="mixamorig:"+side+"Hand",Weight=1,FootGrounding=false});
  AnimationProfileSerializer.Save(profilePath,profile);
  string scenePath=Program.Root+"/Scenes/Main.bytescene";var data=JsonNode.Parse(File.ReadAllText(scenePath))!;
  JsonObject Vec(Vector3 v)=>new(){["x"]=v.X,["y"]=v.Y,["z"]=v.Z};JsonObject Quat(Quaternion q)=>new(){["x"]=q.X,["y"]=q.Y,["z"]=q.Z,["w"]=q.W};
  JsonArray V3(Vector3 v)=>new(v.X,v.Y,v.Z);JsonArray Q4(Quaternion q)=>new(q.X,q.Y,q.Z,q.W);
  foreach(var obj in data["gameObjects"]!.AsArray()){
   if(obj!["name"]!.GetValue<string>()=="Rider"){obj["transform"]!["localRotation"]=Quat(ownerRotation);obj["transform"]!["localPosition"]=Vec(ownerOffset);}
   foreach(var component in obj["components"]!.AsArray()){
    var type=component!["type"]!.GetValue<string>();var p=component["properties"]!;
    if(type=="bytebard.downhill.BoardRider3D"){p["pushClip"]="Push Board";p["brakeClip"]="Foot Brake Board";p["heelSlideClip"]="Heelside Slide Board";p["toeSlideClip"]="Toeside Slide Board";p["leftFootAnchor"]=V3(new(-.025f,leftY,-.24f));p["rightFootAnchor"]=V3(new(.025f,rightY,.24f));p["leftFootRotation"]=Q4(leftRotation);p["leftForwardFootRotation"]=Q4(leftForward);p["rightFootRotation"]=Q4(rightRotation);p["rightBrakeFootRotation"]=Q4(brakeRotation);p["footAnkleHeight"]=rightY-.024f;p["footReleaseBlendSeconds"]=.14f;p["pushFootOutsideX"]=.38f;}
    if(type=="SkeletalMeshRenderer")p["defaultAnimation"]="Neutral Board";
    if(type=="bytebard.downhill.BoardController3D"){p["steeringResponse"]=7;p["brakeDeceleration"]=5;}
   }
  }
  File.WriteAllText(scenePath,data.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));File.WriteAllText(Program.Root+"/Research/Rider-calibration.json",JsonSerializer.Serialize(new{ownerYaw,ownerOffset,leftRotation,rightRotation,brakeRotation,summary},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
 }
}







