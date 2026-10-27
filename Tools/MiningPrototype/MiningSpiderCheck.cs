using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.AI;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
internal static class MiningSpiderCheck
{
 public static void Preview(Scene scene,MiningOperation3D op)
 {
  int index=0;foreach(var reference in new[]{op.HunterBlueprint,op.RaiderBlueprint}){
   var enemy=RuntimeSpawnService.SpawnBlueprint(scene,reference,new Vector3(12+index++*3.4f,0,-27))!;
   enemy.GetComponent<AIBehavior3D>()!.Enabled=false;enemy.GetComponent<SimpleEnemyAI3D>()!.Enabled=false;
  }
  var player=scene.FindGameObject(op.PlayerObjectId)!;player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;player.GetComponent<CameraBoom3D>()!.Enabled=false;
  var cam=scene.ActiveCamera!;cam.GameObject.SetParent(null,true);var eye=new Vector3(19,6,-35);cam.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,new Vector3(13.5f,.6f,-27),Vector3.UnitY),out var pose);cam.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);cam.Orthographic=true;cam.OrthographicWidth=9;
  foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;
 }
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS SPIDER "+text);}
 public static void Verify(Scene scene,MiningOperation3D op)
 {
  var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  var host=new SceneManager();host.LoadScene(scene);var clock=typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!;var update=typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!;
  void Frame(int n=1){for(int i=0;i<n;i++){clock.Invoke(null,[.05d]);update.Invoke(scene,null);}}
  foreach(var reference in new[]{op.HunterBlueprint,op.RaiderBlueprint})
  {
   var enemy=RuntimeSpawnService.SpawnBlueprint(scene,reference,new Vector3(14,0,-28))!;
   enemy.GetComponent<AIBehavior3D>()!.Enabled=false;enemy.GetComponent<SimpleEnemyAI3D>()!.Enabled=false;
   Frame(3);var driver=enemy.GetComponent<MiningSpiderAnimation3D>();Check(driver!=null,"blueprint loads editable animation component");
   Check(driver!.ModelReady&&driver.LastError.Length==0,"fitted model loads: "+driver.ClipPrefix);
   var renderer=driver.Renderer!;Check(renderer.SkinnedMeshCount>0&&renderer.ResolvedModel!.Skeleton!.Bones.Count>=56,"native skeletal skin and 56-bone rig");
   renderer.TryGetBoneModelMatrix("head",out var head);renderer.TryGetBoneModelMatrix("hips",out var hips);Console.WriteLine($"SPIDER FORWARD {driver.ClipPrefix}: head={head.Translation} hips={hips.Translation}");
   foreach(string clip in new[]{"Idle","Walk","Jump","Attack","Death"})Check(renderer.ResolvedModel!.Animations.Any(a=>a.Name==driver.ClipPrefix+"_"+clip),"clip imported: "+clip);
   enemy.Transform.WorldPosition+=new Vector3(.2f,0,0);Frame();Check(driver.State=="Walk","actual movement drives walk");Frame(3);Check(driver.State=="Idle","stopping returns to idle");
   Check(driver.PlayClip("Jump"),"jump exposed to event actions");Frame(5);Check(driver.State=="Jump"&&renderer.PlaybackTime>0,"jump advances without restarting every frame");Frame(35);
   Check(driver.PlayClip("Attack"),"attack clip resolves");Frame(3);Check(renderer.PlaybackTime>0,"attack advances");
   var visual=renderer.GameObject;enemy.GetComponent<HealthComponent>()!.Kill();Frame();
   Check(enemy.Scene==null&&visual.Scene==scene&&visual.Parent==null,"death visual survives gameplay-root destruction");
   Check(renderer.CurrentAnimation==driver.ClipPrefix+"_Death","death plays on detached corpse");Frame(10);Check(renderer.PlaybackTime>0,"death continues after kill");Frame(90);Check(visual.Scene==null,"corpse expires after death playback");
  }
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"test preserves scene");host.UnloadScene();Console.WriteLine("SPIDER NATIVE VERIFICATION COMPLETE");
 }
}
