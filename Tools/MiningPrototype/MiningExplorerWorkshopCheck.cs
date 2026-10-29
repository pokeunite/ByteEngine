using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using KarlsonDemo.Plugin;
internal static class MiningExplorerWorkshopCheck
{
 public static void Run(Scene scene,MiningOperation3D op)
 {
  void Check(bool pass,string label){if(!pass)throw new Exception(label);Console.WriteLine("PASS explorer workshop: "+label);}
  var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  var host=new SceneManager();host.LoadScene(scene);var trade=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>().Single();
  var player=scene.FindGameObject(op.PlayerObjectId)!;var gun=player.GetComponent<FpsGunLab3D>()!;
  var update=typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!;
  void Frame(){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.05d]);update.Invoke(scene,null);}
  for(int i=0;i<4;i++)Frame();
  bool Descendant(GameObject o){for(var p=o;p!=null;p=p.Parent)if(p==player)return true;return false;}
  var renderers=scene.GameObjects.Where(Descendant).SelectMany(o=>o.Components).OfType<SkeletalMeshRenderer>().ToArray();
  Check(renderers.Any(r=>r.ResolvedModel?.Skeleton?.Bones.Count==51&&r.ResolvedModel.Animations.Count==76),"new explorer is instantiated on the existing player with source animations");
  var controller=player.GetComponent<AnimationController>()!;Check(!controller.AnimationProfile.IsEmpty,"existing locomotion profile remains assigned");
  Check(gun.SelectedWeapon?.Definition!=null,"default pistol is still carried after replacing the model");
  Check(op.RefineryBatch==3&&op.QueueRefineryBatches&&op.RefineryDuration==3,"authored refinery uses the new small-batch queue");Check(trade.UseProductionCycles&&trade.CycleDuration==5&&trade.Capacity==35,"authored press uses five-second cycles and 35 storage");
  var bucket=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningAmmoBucket3D>().Single();
  trade.Tick(10);var weapon=gun.SelectedWeapon!;weapon.SetAmmo(weapon.CurrentAmmo,0);var machine=bucket.GameObject.GetComponent<SkeletalMeshRenderer>()!;
  player.Transform.WorldPosition=machine.TryGetBoneWorldMatrix(bucket.BucketMarker,out var matrix)?matrix.Translation:Vector3.Transform(bucket.BucketOffset,bucket.Transform.WorldMatrix);
  Frame();Check(bucket.LastCollected>0&&weapon.ReserveAmmo>0&&bucket.CollectingRounds>0,"walk-up collection credits real reserve and launches bounded bullet flights");
  for(int i=0;i<12;i++)Frame();Check(bucket.CollectingRounds==0,"pickup flights expire instead of accumulating scene objects");
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"verification preserves the authored scene");host.UnloadScene();
 }
}
