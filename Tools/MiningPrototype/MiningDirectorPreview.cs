using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningDirectorPreview
{
 static void Set(object o,string key,object value)=>o.GetType().GetProperty(key)!.SetValue(o,value);
 public static void Apply(Scene scene,MiningOperation3D op,bool account,bool contracts,bool threat=false,bool research=false,bool drone=false,bool kit=false)
 {
  op.PersistProgress=false;op.TutorialEnabled=false;var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().Single();var player=scene.FindGameObject(op.PlayerObjectId)!;
  if(research||drone||kit){Set(op,"CompletedRounds",1);Set(op,"Credits",1240);Set(op,"RefinedOre",80);var trade=op.GameObject.GetComponent<MiningTrade3D>()!;if(!trade.HasResearch("charter"))trade.BuyResearch("charter");trade.BuyResearch("rd");var shop=op.GameObject.GetComponent<MiningResearchStation3D>()!;typeof(MiningResearchStation3D).GetMethod("OnUpdate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(shop,null);shop.PlaceStation(shop.StationPosition);var station=shop.DeployedStation!;player.Transform.WorldPosition=station.Transform.WorldPosition+new Vector3(0,0,-1.5f);if(kit){shop.Buy(1);shop.Close();}else if(!drone)shop.Open();else{shop.Buy(0);var defender=scene.GetComponentsSnapshot<MiningSecurityDrone3D>().Single();defender.RestoreBattery(30);defender.Transform.WorldPosition=player.Transform.WorldPosition+new Vector3(1,1.7f,-2);var attacker=scene.CreateGameObject("Drone practice target");attacker.Transform.WorldPosition=player.Transform.WorldPosition+new Vector3(4,0,-6);attacker.AddComponent(new HealthComponent());var body=attacker.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,Material=new(){BaseColor=new(.7f,.1f,.8f,1)}});attacker.Transform.LocalScale=new(.8f);var list=(List<GameObject>)typeof(MiningOperation3D).GetField("_enemies",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(op)!;list.Add(attacker);Set(op,"Phase",MiningPhase.Drilling);Set(op,"EnemiesScheduled",1);Set(op,"EnemiesSpawned",1);op.Enabled=false;}}
  else if(threat){
   var enemy=scene.CreateGameObject("Last enemy marker preview");enemy.Transform.WorldPosition=player.Transform.WorldPosition+new Vector3(0,0,-9);enemy.AddComponent(new HealthComponent());var body=scene.CreateGameObject("Threat preview body");body.SetParent(enemy,false);body.Transform.LocalPosition=new(0,.7f,0);body.Transform.LocalScale=new(1,.8f,1);body.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,Material=new(){BaseColor=new(.65f,.12f,.8f,1)}});
   var list=(List<GameObject>)typeof(MiningOperation3D).GetField("_enemies",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(op)!;list.Add(enemy);Set(op,"EnemiesScheduled",1);Set(op,"EnemiesSpawned",1);Set(op,"Phase",MiningPhase.Drilling);typeof(MiningOperation3D).GetField("finalEnemyClock",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(op,6f);op.Enabled=false;
  }
  else if(account){player.Transform.WorldPosition=scene.FindGameObject(ui.TerminalObjectId)!.Transform.WorldPosition;Set(op,"CompanyAccountCredits",4500);op.SetCompanyDepositPercent(50,false);ui.Open(MiningScreen.Account);}
  else if(contracts){player.Transform.WorldPosition=scene.FindGameObject(op.DrillObjectId)!.Transform.WorldPosition;ui.Open(MiningScreen.Drill);}
  else{
   Set(op,"ActiveWave",op.SelectedContract!);Set(op,"RoundQuota",18);Set(op,"Phase",MiningPhase.Drilling);Set(op,"EnemiesScheduled",1);Set(op,"EnemiesSpawned",1);Set(op,"ExtractionProgress",1f);op.Tick(.01f);op.Enabled=false;
  }
 }
}
