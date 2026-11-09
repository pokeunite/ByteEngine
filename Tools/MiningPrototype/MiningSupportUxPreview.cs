using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningSupportUxPreview
{
 static void Set(object obj,string key,object value)=>obj.GetType().GetProperty(key)!.SetValue(obj,value);
 public static void Apply(Scene scene,MiningOperation3D op,string kind)
 {
  if(kind=="--support-route-preview")return;
  op.TutorialEnabled=false;Set(op,"CompletedRounds",2);Set(op,"Credits",6800);Set(op,"RefinedOre",500);Set(op,"RawOre",100);
  var player=scene.FindGameObject(op.PlayerObjectId)!;var trade=op.GameObject.GetComponent<MiningTrade3D>()!;trade.Initialize();if(!trade.HasResearch("charter"))trade.BuyResearch("charter");if(!trade.HasResearch("rd"))trade.BuyResearch("rd");var shop=op.GameObject.GetComponent<MiningResearchStation3D>()!;
  typeof(MiningResearchStation3D).GetMethod("OnUpdate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(shop,null);shop.PlaceStation(new(-10,0,-26));
  if(kind=="--support-shop-preview"){player.Transform.WorldPosition=new(-10,.15f,-28);shop.Open();return;}
  if(kind=="--support-local-preview"){shop.Buy(3);shop.PlaceRefinery(new(0,0,-28));var local=scene.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Single();player.Transform.WorldPosition=new(0,.15f,-30);local.Open();return;}
  shop.Buy(1);if(kind=="--support-icons-preview"){for(int i=0;i<5;i++){shop.Buy(1);shop.PlaceBarricade(new(i*2,0,-24),i*15);}return;}
  player.Transform.WorldPosition=new(0,.15f,-24);player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;player.GetComponent<CameraBoom3D>()!.Enabled=false;player.GetComponent<PlayerController3D>()!.Enabled=false;
  var camera=scene.ActiveCamera!;camera.Transform.WorldPosition=new(-4,4,-19);Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,new(0,.2f,-28),Vector3.UnitY),out var pose);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);var planner=player.GetComponent<MiningDefensePlanner3D>()!;planner.SelectBuild(MiningDefenseKind.Barricade);planner.RotatePlacement(45);
 }
}
