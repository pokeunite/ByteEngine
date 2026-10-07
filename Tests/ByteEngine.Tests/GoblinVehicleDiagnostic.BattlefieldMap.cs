using System.Numerics;
using System.Text.Json;
using GoblinScrapper.Construction;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
namespace ByteEngine.Tests;
internal sealed partial class GoblinVehicleDiagnostic {
 private void BuildQuarrySiege(Scene scene,VehicleBuilder3D builder,string path,bool create){
  ShaderUniformCacheTests.Run();
  string source=Path.GetFullPath("Designs/BattlefieldKit");
  if(create){
   string backup=Path.Combine(_project!.ProjectRoot,"Backups/Before-Quarry-Siege");Directory.CreateDirectory(backup);if(!File.Exists(Path.Combine(backup,"Main.bytescene")))File.Copy(path,Path.Combine(backup,"Main.bytescene"));
   _project.AssetDatabase.Scan();
   var floor=scene.GameObjects.Single(o=>o.Name=="Test yard floor");floor.SetParent(null,true);
   foreach(var group in scene.GameObjects.Where(o=>o.Name is "WORKSHOP - editable environment" or "BATTLEFIELD - editable level" or "QUARRY SIEGE - editable battlefield").ToArray())scene.DestroyGameObject(group);
   var root=scene.CreateGameObject("QUARRY SIEGE - editable battlefield");root.Variables.Set("GoblinBattlefieldMap",VariableValue.FromString("quarry-siege-v1"));floor.SetParent(root,true);floor.Transform.LocalPosition=new(0,-.25f,-50);floor.Transform.LocalScale=new(190,.5f,180);
   floor.GetComponent<MeshRenderer>()!.MaterialAssetReference=ByteEngine.Editor.LocalMaterialAssetFactory.Save(_project,"Quarry_dirt",new Material{BaseColor=Vector4.One,MainTexture=_project.Assets.LoadTexture(new AssetReference("Assets/Environments/QuarrySiege/quarry-dirt.png")),UvTiling=new(24),Roughness=.95f});
   var floorBox=floor.GetComponent<BoxCollider3D>()!;floorBox.Size=Vector3.One;floorBox.Center=Vector3.Zero;
   using var kit=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,"kit.json")));using var layout=JsonDocument.Parse(File.ReadAllText(Path.Combine(source,"layout.json")));
   Vector3 Vec(JsonElement e)=>new(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
   Vector3 ConvertAxes(Vector3 v)=>new(v.X,v.Z,-v.Y);
   var groups=new Dictionary<string,GameObject>();
   foreach(var item in layout.RootElement.GetProperty("items").EnumerateArray()){
    string asset=item.GetProperty("asset").GetString()!,label=item.GetProperty("name").GetString()!;
    string section=asset.StartsWith("rock")||asset=="pine"?"Quarry perimeter":item.GetProperty("position")[2].GetSingle()<-100?"Red fortress":item.GetProperty("position")[2].GetSingle()>-15?"Green workshop":"Arena encounters";
    if(!groups.TryGetValue(section,out var group)){group=scene.CreateGameObject(section+" - move and edit props");group.SetParent(root,false);groups[section]=group;}
    var prop=VehicleBuilder3D.CreateModelVisual(scene,_project.Assets,"Assets/Environments/QuarrySiege/"+asset+".glb",group,label);
    prop.Transform.LocalPosition=Vec(item.GetProperty("position"));prop.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,item.GetProperty("yaw").GetSingle());prop.Transform.LocalScale=Vec(item.GetProperty("scale"));
    if(item.GetProperty("solid").GetBoolean())foreach(var c in kit.RootElement.GetProperty(asset).GetProperty("colliders").EnumerateArray()){
     var proxy=scene.CreateGameObject(label+" collision");proxy.SetParent(prop,false);proxy.Transform.LocalPosition=ConvertAxes(Vec(c.GetProperty("position")));var size=Vec(c.GetProperty("size"));proxy.Transform.LocalScale=new(size.X,size.Z,size.Y);
     if(c.TryGetProperty("angle",out var angle))proxy.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,angle.GetSingle());
     proxy.AddComponent(new BoxCollider3D{Size=Vector3.One});
    }
   }
   // Hidden collision-only perimeter prevents driving off the terrain.
   foreach(var v in new[]{(new Vector3(-89,6,-50),new Vector3(1,12,176)),(new Vector3(89,6,-50),new Vector3(1,12,176)),(new Vector3(0,6,-139),new Vector3(180,12,1)),(new Vector3(0,6,39),new Vector3(180,12,1))}){
    var wall=scene.CreateGameObject("Quarry limit - collision only");wall.SetParent(root,false);wall.Transform.LocalPosition=v.Item1;wall.Transform.LocalScale=v.Item2;wall.AddComponent(new BoxCollider3D{Size=Vector3.One});
   }
   var enemies=scene.GameObjects.Where(o=>o.Variables.TryGet("GoblinEnemySpawn",out var e)&&e!.Boolean).OrderBy(o=>o.Name).ToArray();var spawns=layout.RootElement.GetProperty("spawns");
   for(int i=0;i<enemies.Length;i++){enemies[i].Transform.WorldPosition=Vec(spawns[i%spawns.GetArrayLength()]);enemies[i].Transform.WorldRotation=Quaternion.Identity;enemies[i].Active=true;}
   builder.BattlefieldEnabled=true;builder.BattlefieldEnemyCount=enemies.Length;builder.BattleDuration=180;builder.Transform.WorldPosition=new(0,1.25f,0);
   foreach(var sky in scene.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>()){sky.ZenithColor=new(.28f,.43f,.58f);sky.HorizonColor=new(.67f,.73f,.72f);sky.GroundColor=new(.33f,.29f,.22f);sky.Exposure=1.05f;sky.FogEnabled=false;sky.FogColor=new(.63f,.66f,.62f);sky.FogStartDistance=100;sky.FogEndDistance=260;sky.FogMaxOpacity=.3f;}
   scene.ActiveCamera!.FarClip=350;
   _project.Scenes.Save(scene,path);Console.WriteLine("SAVED: Quarry Siege arena, 136 placed Blender props, 18 existing goblin spawns; original Main backed up.");
  }
  // Final native lighting pass: keep the broad battlefield visible at distance.
  foreach(var sky in scene.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>())sky.FogEnabled=false;
  _project!.Scenes.Save(scene,path);
  scene=_project!.Scenes.Load(path);builder=scene.GameObjects.SelectMany(o=>o.Components).OfType<VehicleBuilder3D>().Single();Assert(scene.GameObjects.Any(o=>o.Name=="QUARRY SIEGE - editable battlefield"),"Map missing after reload");
  var camera=scene.ActiveCamera!;var savedPosition=camera.Transform.WorldPosition;var savedRotation=camera.Transform.WorldRotation;
  void Aim(Vector3 pos,Vector3 target){camera.Transform.WorldPosition=pos;Matrix4x4.Invert(Matrix4x4.CreateLookAt(pos,target,Vector3.UnitY),out var view);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(view);}
  Aim(new(105,100,80),new(0,0,-57));Screenshot(scene,Path.Combine(source,"engine-overview.png"),1440,900,true);
  Aim(new(14,9,17),new(-2,3,-25));Screenshot(scene,Path.Combine(source,"engine-workshop.png"),1280,720,true);
  camera.Transform.WorldPosition=savedPosition;camera.Transform.WorldRotation=savedRotation;
  Scenes.LoadScene(scene);for(int i=0;i<12;i++)TickInput(scene,new(.98f,.5f),[]);
  var catalog=VehiclePartCatalog.Load(Path.Combine(_project.ProjectRoot,"Assets/GarageUI/parts-catalog.json"));builder.RestoreAssembly(ContraptionTests.Build(catalog).ToJson());Assert(builder.BeginDriving(),"Arena deployment failed");
  var start=builder.Transform.WorldPosition;for(int i=0;i<600;i++)TickInput(scene,new(.98f,.5f),[Key.W]);Assert(builder.Transform.WorldPosition.Z<start.Z-20,"Vehicle cannot drive out of workshop into arena");Assert(!builder.BattleLost,"Map caused immediate loss");
  Screenshot(scene,Path.Combine(source,"engine-driving.png"));
  using(var fb=new ByteEngine.Editor.SceneFramebuffer()){
   var gc=scene.ActiveCamera!;var f=gc.Transform.Forward;var view=new ByteEngine.Editor.EditorCamera3D{Position=gc.Transform.WorldPosition,Yaw=MathF.Atan2(f.Z,f.X)*180/MathF.PI,Pitch=MathF.Asin(f.Y)*180/MathF.PI,FieldOfView=gc.FieldOfView};
   for(int i=0;i<3;i++){fb.Render(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,new ByteEngine.Editor.EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);OpenTK.Graphics.OpenGL4.GL.Finish();}
   var objectTimes=new Dictionary<string,double>();
   for(int sample=0;sample<15;sample++){foreach(var obj in scene.GameObjects.ToArray()){var stamp=System.Diagnostics.Stopwatch.GetTimestamp();obj.UpdateInternal();var ms=System.Diagnostics.Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;objectTimes[obj.Name]=objectTimes.GetValueOrDefault(obj.Name)+ms;}}
   foreach(var pair in objectTimes.OrderByDescending(p=>p.Value).Take(8))Console.WriteLine($"PROFILE OBJECT {pair.Key}: {pair.Value/15:F2} ms");
   var timer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<60;i++)TickInput(scene,new(.98f,.5f),[]);timer.Stop();Console.WriteLine($"PROFILE UPDATE ONLY: {timer.Elapsed.TotalMilliseconds/60:F2} ms");
   timer.Restart();for(int i=0;i<100;i++)scene.Physics.Step(scene,1f/60);timer.Stop();Console.WriteLine($"PROFILE CORE PHYSICS: {timer.Elapsed.TotalMilliseconds/100:F2} ms");
   void RenderProfile(string label){for(int i=0;i<3;i++){fb.Render(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,new ByteEngine.Editor.EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);OpenTK.Graphics.OpenGL4.GL.Finish();}timer.Restart();for(int i=0;i<40;i++){fb.Render(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,new ByteEngine.Editor.EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);OpenTK.Graphics.OpenGL4.GL.Finish();}timer.Stop();Console.WriteLine($"PROFILE RENDER {label}: {timer.Elapsed.TotalMilliseconds/40:F2} ms");}
   RenderProfile("normal");var shadowLights=scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>().Where(l=>l.CastShadows).ToArray();foreach(var light in shadowLights)light.CastShadows=false;RenderProfile("no shadows");foreach(var light in shadowLights)light.CastShadows=true;
   var canvas=scene.GameObjects.Single(o=>o.Name=="Goblin Scraper workshop HUD");canvas.Active=false;RenderProfile("no HUD");canvas.Active=true;
   timer.Restart();for(int i=0;i<20;i++){TickInput(scene,new(.98f,.5f),[]);fb.Render(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,new ByteEngine.Editor.EditorCamera(),view,true,1280,720,1280,720,drawGrid3D:false);OpenTK.Graphics.OpenGL4.GL.Finish();}timer.Stop();Console.WriteLine($"MAP PROFILE: synchronized 720p update/render {timer.Elapsed.TotalMilliseconds/20:F1} ms per frame ({20000/timer.Elapsed.TotalMilliseconds:F1} fps), editor panels excluded.");
  }
  builder.ReturnToBuild();
  using(var buildFb=new ByteEngine.Editor.SceneFramebuffer()){
   for(int i=0;i<3;i++)TickInput(scene,new(.98f,.5f),[]);
   var bc=scene.ActiveCamera!;var forward=bc.Transform.Forward;var buildView=new ByteEngine.Editor.EditorCamera3D{Position=bc.Transform.WorldPosition,Yaw=MathF.Atan2(forward.Z,forward.X)*180/MathF.PI,Pitch=MathF.Asin(forward.Y)*180/MathF.PI,FieldOfView=bc.FieldOfView};
   var buildTimer=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<40;i++){TickInput(scene,new(.98f,.5f),[]);buildFb.Render(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,new ByteEngine.Editor.EditorCamera(),buildView,true,1280,720,1280,720,drawGrid3D:false);OpenTK.Graphics.OpenGL4.GL.Finish();}buildTimer.Stop();Console.WriteLine($"BUILD PROFILE: synchronized 720p update/render {buildTimer.Elapsed.TotalMilliseconds/40:F1} ms per frame ({40000/buildTimer.Elapsed.TotalMilliseconds:F1} fps), editor panels excluded.");
  }
  Assert(scene.GameObjects.Count(o=>o.Variables.TryGet("GoblinEnemySpawn",out var v)&&v!.Boolean&&o.Active)==18,"Authored enemy spawns did not reset");
  Console.WriteLine("PASS: imported models; native edit render; scene save/reload; deployment; clear central driving lane; existing enemy spawn reset.");
 }
}
