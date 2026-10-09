using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core;
using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using DuneCompany;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunWorkshopV3(Scene scene,DuneWorkshop3D workshop,string root){
  void Click(string name){var w=scene.FindGameObject(name)!.GetComponent<UiWidget>()!;if(!w.GameObject.ActiveInHierarchy)throw new Exception("Inactive button: "+name);var r=UiLayout.Resolve(w.GameObject,w.Anchor,w.Offset,w.Size,new(1280,720));var p=(r.Position+r.Size*.5f)/new Vector2(1280,720);Tick(scene,p,false);Tick(scene,p,true);Tick(scene,p,false);}
  if(scene.FindGameObject("Parts catalogue")!.ActiveInHierarchy||scene.FindGameObject("Garage inspector")!.ActiveInHierarchy)throw new Exception("Home still shows build panels");
  Capture(scene,Path.Combine(root,"Preview/workshop-v3-tutorial.png"),false);
  if(scene.FindGameObject("Workshop tutorial")!.ActiveInHierarchy){
   Click("Tutorial next");
   // Start with the authored root; the exact reported one-added-rail case must advance.
   foreach(var block in workshop.Blocks.Where(b=>b.Id!=0).Reverse().ToArray())workshop.RemoveBlock(block.Id);
   Tick(scene,new(.5f),false);
   if(scene.FindGameObject("Tutorial next")!.GetComponent<UiWidget>()!.Interactable)throw new Exception("Frame step accepted a lone root");
   if(!scene.FindGameObject("Tutorial part guide")!.ActiveInHierarchy)throw new Exception("Required frame not highlighted");
   if(!workshop.AddBlock(3,0,new(0,0,2),Quaternion.Identity))throw new Exception("Frame add failed");Tick(scene,new(.5f),false);
   if(!scene.FindGameObject("Tutorial next")!.GetComponent<UiWidget>()!.Interactable)throw new Exception("One added frame didn't unlock next");
   Capture(scene,Path.Combine(root,"Preview/tutorial-frame-ready.png"),false);Click("Tutorial next");
   if(!scene.FindGameObject("Tutorial part guide")!.ActiveInHierarchy||!scene.FindGameObject("Tutorial heading")!.GetComponent<UiText>()!.Text.Contains("SEAT"))throw new Exception("Seat guidance missing");
   workshop.AddBlock(25,1,new(0,.6f,2),Quaternion.Identity);Tick(scene,new(.5f),false);Click("Tutorial next");
   if(scene.FindGameObject("Tutorial next")!.GetComponent<UiWidget>()!.Interactable)throw new Exception("Drive step accepted missing engine/wheels");
   workshop.AddBlock(23,0,new(0,.5f,0),Quaternion.Identity);Tick(scene,new(.5f),false);
   Capture(scene,Path.Combine(root,"Preview/tutorial-wheel-guide.png"),false);
   workshop.AddBlock(13,0,new(-.6f,.25f,0),Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2));workshop.AddBlock(13,1,new(.6f,.25f,2),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-MathF.PI/2));Tick(scene,new(.5f),false);
   Click("Tutorial next");Click("Tutorial next");if(!scene.FindGameObject("Contracts overlay")!.ActiveInHierarchy)throw new Exception("Tutorial didn't open contracts");Click("Tutorial next");Click("Close garage overlay");
   if(scene.FindGameObject("Tutorial part guide")!.ActiveInHierarchy)throw new Exception("Guide remains after completing tutorial");
   Console.WriteLine("PASS one-added-frame tutorial progression, seat/engine/wheel category guidance, required-part highlight, disabled invalid Next, all six steps");
  }
  workshop.Command("Load vehicle");Tick(scene,new(.5f),false);Capture(scene,Path.Combine(root,"Preview/workshop-v3-home.png"),false);
  foreach(var viewport in new[]{new Vector2(960,540),new Vector2(1600,1000),new Vector2(1920,720)}){var nav=scene.FindGameObject("Garage home navigation")!.GetComponent<UiWidget>()!;var rect=UiLayout.Resolve(nav.GameObject,nav.Anchor,nav.Offset,nav.Size,viewport);if(Math.Abs(rect.Size.Y-viewport.Y)>.01)throw new Exception("Navigation does not fill viewport");foreach(string item in new[]{"Run / build","Home readiness","Garage wallet"}){var obj=scene.FindGameObject(item)!;var widget=obj.GetComponent<UiWidget>();var text=obj.GetComponent<UiText>();var bounds=UiLayout.Resolve(obj,widget?.Anchor??text!.Anchor,widget?.Offset??text!.Offset,widget?.Size??new Vector2(45,25),viewport);if(bounds.Position.X<0||bounds.Position.Y<0||bounds.Position.X+bounds.Size.X>viewport.X+1||bounds.Position.Y+bounds.Size.Y>viewport.Y+1)throw new Exception("Responsive bounds: "+item);}}
  int count=workshop.Blocks.Count;Tick(scene,new(.55f,.45f),true);Tick(scene,new(.55f,.45f),false);if(workshop.Blocks.Count!=count)throw new Exception("Garage home places blocks");
  Click("Workshop settings");if(!scene.FindGameObject("Workshop settings panel")!.ActiveInHierarchy)throw new Exception("Settings panel");float before=ByteEngine.Core.Audio.AudioMixer.GetVolume(ByteEngine.Core.Audio.AudioBus.Music);Capture(scene,Path.Combine(root,"Preview/workshop-v3-settings.png"),false);Click("Settings Music -");if(Math.Abs(ByteEngine.Core.Audio.AudioMixer.GetVolume(ByteEngine.Core.Audio.AudioBus.Music)-Math.Max(0,before-.1f))>.001f)throw new Exception("Music bus not applied");Click("Workshop settings close");
  Click("Build vehicle");if(!scene.FindGameObject("Parts catalogue")!.ActiveInHierarchy||scene.FindGameObject("Garage home navigation")!.ActiveInHierarchy)throw new Exception("Build mode visibility invalid");Capture(scene,Path.Combine(root,"Preview/workshop-v3-build.png"),false);
  foreach(var size in new[]{new Vector2(1280,720),new Vector2(1600,1000),new Vector2(1920,720)}){
   Input.SetGameViewPointer(new(.5f),size,true);Time.Update(1f/60);scene.UpdateInternal();
   var catalogue=scene.FindGameObject("Parts catalogue")!.GetComponent<UiWidget>()!;var strip=scene.FindGameObject("Garage command strip")!.GetComponent<UiWidget>()!;
   var a=UiLayout.Resolve(catalogue.GameObject,catalogue.Anchor,catalogue.Offset,catalogue.Size,size);var b=UiLayout.Resolve(strip.GameObject,strip.Anchor,strip.Offset,strip.Size,size);
   if(Math.Abs(a.Position.Y+a.Size.Y-b.Position.Y)>1||Math.Abs(b.Position.X)>1||Math.Abs(b.Size.X-size.X)>1)throw new Exception("Bottom-left gap or incomplete toolbar");
   var commands=new[]{"Build home","Place blocks","Tune blocks","Move branch","Rotate mount","Change mount face","Copy part","Erase blocks","Undo","Redo","Build test","Build save"};
   Vector2 previous=Vector2.Zero;foreach(string n in commands){var w=scene.FindGameObject(n)!.GetComponent<UiWidget>()!;var r=UiLayout.Resolve(w.GameObject,w.Anchor,w.Offset,w.Size,size);if(r.Position.X<previous.X-1||r.Position.Y<b.Position.Y||r.Position.Y+r.Size.Y>size.Y)throw new Exception("Toolbar overlap/bounds: "+n);previous=new(r.Position.X+r.Size.X,0);}
  }Tick(scene,new(.5f),false);
  var surfaceFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  var cutaway=typeof(DuneWorkshop3D).GetMethod("UpdateUndersideVisibility",surfaceFlags)!;
  var restore=typeof(DuneWorkshop3D).GetMethod("RestoreGroundVisibility",surfaceFlags)!;
  string SurfaceName(MeshRenderer r){string n=r.MaterialReference?.SubAssetKey??"";for(var o=r.GameObject;o!=null;o=o.Parent)n+=" "+o.Name;return n;}
  var floors=scene.GameObjects.SelectMany(o=>o.Components).OfType<MeshRenderer>().Where(r=>SurfaceName(r).Contains("Concrete floor")).ToArray();
  if(floors.Length==0)throw new Exception("No garage floors in camera test");
  var enabled=scene.GameObjects.SelectMany(o=>o.Components).Where(c=>c is ByteEngine.Core.Characters.HeightfieldCollider3D).ToDictionary(c=>c,c=>c.Enabled);
  var camera=scene.ActiveCamera!;var cameraPosition=camera.Transform.WorldPosition;var cameraRotation=camera.Transform.WorldRotation;var look=typeof(DuneWorkshop3D).GetMethod("LookCamera",surfaceFlags)!;
  look.Invoke(workshop,[new Vector3(0,-5,6),new Vector3(0,1,0)]);cutaway.Invoke(workshop,[new Vector3(0,-5,6)]);Capture(scene,Path.Combine(root,"Preview/camera-under-floor.png"),false);if(floors.Any(r=>r.Visible))throw new Exception("Garage floor blocks underground camera");
  if(enabled.Any(p=>p.Key.Enabled!=p.Value))throw new Exception("Camera cutaway disabled terrain physics");
  restore.Invoke(workshop,[]);if(floors.Any(r=>!r.Visible))throw new Exception("Garage floor visibility wasn't restored");
  var roofs=scene.GameObjects.SelectMany(o=>o.Components).OfType<MeshRenderer>().Where(r=>SurfaceName(r).Contains("Corrugated walls")||SurfaceName(r).Contains("Structural frame")||SurfaceName(r).Contains("Workshop storage shelving")).ToArray();var originals=roofs.ToDictionary(r=>r,r=>r.Mesh);
  look.Invoke(workshop,[new Vector3(0,15,6),new Vector3(0,1,0)]);cutaway.Invoke(workshop,[new Vector3(0,15,6)]);Capture(scene,Path.Combine(root,"Preview/camera-above-roof.png"),false);if(!roofs.Any(r=>r.Mesh!=originals[r]||!r.Visible))throw new Exception("Roof still blocks ceiling camera");
  restore.Invoke(workshop,[]);if(roofs.Any(r=>r.Mesh!=originals[r]||!r.Visible))throw new Exception("Roof mesh wasn't restored");camera.Transform.WorldPosition=cameraPosition;camera.Transform.WorldRotation=cameraRotation;
  Click("Build home");Click("Blueprints tab");if(!scene.FindGameObject("Blueprints overlay")!.ActiveInHierarchy)throw new Exception("Blueprint navigation");Capture(scene,Path.Combine(root,"Preview/workshop-v3-blueprints.png"),false);Click("Close garage overlay");
  Click("Contracts tab");Click("Contract row tow");Capture(scene,Path.Combine(root,"Preview/workshop-v3-contracts.png"),false);Click("Close garage overlay");
  Click("Tutorial replay");Click("Tutorial next");if(!scene.FindGameObject("Parts catalogue")!.ActiveInHierarchy)throw new Exception("Tutorial did not open Build");Click("Tutorial skip");Click("Build home");
  Click("Run / build");if(workshop.Building||scene.FindGameObject("Garage home navigation")!.ActiveInHierarchy)throw new Exception("Drive didn't hide navigation");for(int i=0;i<60;i++)Tick(scene,new(.5f),false,ByteEngine.Core.Key.W);Capture(scene,Path.Combine(root,"Preview/mission-hud-free-drive.png"),false);
  var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  typeof(DuneWorkshop3D).GetField("_contractRun",flags)!.SetValue(workshop,true);typeof(DuneWorkshop3D).GetField("_contractComplete",flags)!.SetValue(workshop,true);typeof(DuneWorkshop3D).GetField("_credits",flags)!.SetValue(workshop,250);Tick(scene,new(.5f),false);
  if(!scene.FindGameObject("Contract result")!.ActiveInHierarchy)throw new Exception("Result banner missing");
  var victory=scene.FindGameObject("Job sound - contract-victory")?.GetComponent<ByteEngine.Core.Audio.AudioSource3D>();if(victory==null)throw new Exception("Victory cue wasn't dispatched");int voices=scene.GameObjects.Count(o=>o.Name=="Job sound - contract-victory");for(int i=0;i<6;i++)Tick(scene,new(.5f),false);if(scene.GameObjects.Count(o=>o.Name=="Job sound - contract-victory")!=voices)throw new Exception("Duplicate victory sounds");
  Capture(scene,Path.Combine(root,"Preview/mission-hud-result.png"),false);
  for(int i=0;i<180;i++)Tick(scene,new(.5f),false);if(scene.FindGameObject("Contract result")!.ActiveInHierarchy)throw new Exception("Result banner did not expire after 2.5 seconds");
  Click("Return workshop");if(!workshop.Building||!scene.FindGameObject("Garage home navigation")!.ActiveInHierarchy)throw new Exception("Garage return");
  Scenes.UnloadScene();scene=_project!.Scenes.Load(Path.Combine(root,"Scenes/Workshop.bytescene"));Scenes.LoadScene(scene);Tick(scene,new(.5f),false);if(scene.FindGameObject("Workshop tutorial")!.ActiveInHierarchy)throw new Exception("Skipped guide reappeared");
  Scenes.UnloadScene();_project.Dispose();Console.WriteLine("PASS Workshop V3 home/build isolation, blueprint/contracts navigation, tutorial replay/skip persistence, drive/return, actual game captures.");Close();
 }
}
