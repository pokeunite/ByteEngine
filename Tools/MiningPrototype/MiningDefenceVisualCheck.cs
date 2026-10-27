using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningDefenceVisualCheck
{
 public static void Preview(Scene scene,MiningOperation3D op)
 {
  var planner=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningDefensePlanner3D>()).Single();
  int i=0;foreach(var kind in Enum.GetValues<MiningDefenseKind>()){var p=i<3?new Vector3(12+i*3.5f,0,-25):new Vector3(14+(i-3)*4,0,-20);typeof(MiningDefensePlanner3D).GetMethod("Spawn",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(planner,[kind,p,0f]);i++;}
  var player=scene.FindGameObject(op.PlayerObjectId)!;player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;player.GetComponent<CameraBoom3D>()!.Enabled=false;
  var cam=scene.ActiveCamera!;cam.GameObject.SetParent(null,true);var eye=new Vector3(28,10,-10);cam.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,new Vector3(16,.8f,-24),Vector3.UnitY),out var pose);cam.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);cam.Orthographic=true;cam.OrthographicWidth=17;
  foreach(var name in new[]{"HUD canvas","Mining HUD","Mining business interface"})if(scene.FindGameObject(name) is {} hud)hud.Active=false;
 }
 public static void Author(Scene scene,GameProjectRuntime runtime)
 {
  string dir=Program.Root+"/.byteengine/Backups/animated-defences-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(dir);File.Copy(Program.Root+"/Scenes/Main.bytescene",dir+"/Main.bytescene");var planner=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningDefensePlanner3D>()).Single();
  AssetReference Ref(string name){string path="Assets/Models/Mining/Defences/"+name+".glb";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));return new(meta.RootElement.GetProperty("guid").GetGuid(),path);}
  planner.SentryModel=Ref("Sentry");planner.RepeaterModel=Ref("Repeater");planner.MortarModel=Ref("Mortar");planner.MineModel=Ref("Mine");planner.SupplyCacheModel=Ref("SupplyCache");planner.DefenceModelScale=.65f;planner.MinimumSpacing=2;
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("ANIMATED DEFENCES assigned to all five planner slots. "+dir);
 }
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS DEFENCE VISUAL "+text);}
 public static void Verify(Scene scene,MiningOperation3D op)
 {
  var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));var host=new SceneManager();host.LoadScene(scene);var clock=typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!;var update=typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!;
  void Frame(int count=1){for(int i=0;i<count;i++){clock.Invoke(null,[.05d]);update.Invoke(scene,null);}}
  Frame(4);var planner=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningDefensePlanner3D>()).Single();var player=scene.FindGameObject(op.PlayerObjectId)!;
  var all=new List<MiningDefense3D>();int n=0;
  foreach(var kind in Enum.GetValues<MiningDefenseKind>())
  {
   typeof(MiningDefensePlanner3D).GetMethod("Spawn",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(planner,[kind,new Vector3(15+n++*4,0,-30),0f]);var d=scene.GameObjects.Last(o=>o.GetComponent<MiningDefense3D>()!=null).GetComponent<MiningDefense3D>()!;d.BaseDamage=.001f;all.Add(d);
  }
  Frame(40);
  foreach(var d in all){Check(d.VisualReady&&d.VisualError.Length==0,d.Kind+" uses the new rigged asset");var child=scene.GameObjects.Single(o=>o.Parent==d.GameObject&&o.GetComponent<SkeletalMeshRenderer>()!=null);var renderer=child.GetComponent<SkeletalMeshRenderer>()!;Check(renderer.ModelLoaded&&renderer.SkinnedMeshCount>0,d.Kind+" imports and skins in live scene");Check(!scene.GameObjects.Any(o=>o.Parent==d.GameObject&&new[]{"Defence foundation","Tracking receiver","Proximity charge","Incoming supply crate"}.Contains(o.Name)),d.Kind+" replaces old procedural geometry");}
  var target=scene.CreateGameObject("Native defence target");target.Transform.WorldPosition=new(19,0,-36);target.AddComponent(new CapsuleCollider3D{Radius=.45f,Height=1.6f,Center=new(0,.8f,0)});var health=target.AddComponent(new HealthComponent{MaxHealth=10000,CurrentHealth=10000,DestroyOnDeath=false});op.TrackRoamingEnemy(target);
  Frame(100);foreach(var d in all.Take(3)){Check(d.ShotsFired>0,d.Kind+" fires using actual combat and animated muzzle");Check(d.VisualError.Length==0,d.Kind+" animation clips resolve through fire/idle transitions");}
  Check(health.CurrentHealth<10000,"new visuals retain damage delivery");
  foreach(var d in all.Take(2)){var r=scene.GameObjects.Single(o=>o.Parent==d.GameObject&&o.GetComponent<SkeletalMeshRenderer>()!=null).GetComponent<SkeletalMeshRenderer>()!;r.TryGetBoneWorldMatrix(d.Kind==MiningDefenseKind.Repeater?"MuzzleLeft":"Muzzle",out var muzzle);r.TryGetBoneWorldMatrix("Recoil",out var recoil);var barrel=Vector2.Normalize(new Vector2(muzzle.Translation.X-recoil.Translation.X,muzzle.Translation.Z-recoil.Translation.Z));var aim=Vector2.Normalize(new Vector2(target.Transform.WorldPosition.X-recoil.Translation.X,target.Transform.WorldPosition.Z-recoil.Translation.Z));Console.WriteLine($"BARREL ALIGN {d.Kind}: {Vector2.Dot(barrel,aim)} muzzle={muzzle.Translation} recoil={recoil.Translation}");Check(Vector2.Dot(barrel,aim)>.95f,d.Kind+" barrel actually faces the damage target");}
  var sentry=all[0];var sk=scene.GameObjects.Single(o=>o.Parent==sentry.GameObject&&o.GetComponent<SkeletalMeshRenderer>()!=null).GetComponent<SkeletalMeshRenderer>()!;bool rootOk=sk.TryGetBoneWorldMatrix("Root",out var rootBefore);bool yawOk=sk.TryGetBoneWorldMatrix("Yaw",out var yawBefore);Check(rootOk&&yawOk,"sentry pivots resolve");target.Transform.WorldPosition=new(21,0,-28);Frame(30);sk.TryGetBoneWorldMatrix("Root",out var rootAfter);sk.TryGetBoneWorldMatrix("Yaw",out var yawAfter);Check(rootBefore==rootAfter&&yawBefore!=yawAfter,"turret tracks target while support feet remain fixed");
  var mine=all.Single(d=>d.Kind==MiningDefenseKind.Mine);target.Transform.WorldPosition=mine.Transform.WorldPosition;Frame(2);Check(mine.Spent&&mine.GameObject.Scene==scene&&mine.VisualState==mine.TriggerClip,"mine trigger animates before removal");Frame(20);Check(!scene.GameObjects.Any(o=>o.GetComponent<MiningDefense3D>()==mine),"spent mine removes its model after activation");
  var cache=all.Single(d=>d.Kind==MiningDefenseKind.SupplyCache);var equipment=player.GetComponent<FpsGunLab3D>()!;equipment.SelectedWeapon!.SetAmmo(1,0);player.Transform.WorldPosition=cache.Transform.WorldPosition;Frame(2);Check(cache.Spent&&cache.VisualState==cache.OpenClip&&equipment.ReserveAmmo>0,"supply cache opens and awards stock once");Frame(40);Check(!scene.GameObjects.Any(o=>o.GetComponent<MiningDefense3D>()==cache),"opened cache cleans up after its lid animation");
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"checks preserve scene and business saves");host.UnloadScene();Console.WriteLine("DEFENCE VISUAL NATIVE VERIFICATION COMPLETE");
 }
}
