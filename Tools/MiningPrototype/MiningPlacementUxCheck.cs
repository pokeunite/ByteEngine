using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningPlacementUxCheck
{
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS PLACEMENT "+label);}
 static void Set(object o,string key,object value)=>o.GetType().GetProperty(key)!.SetValue(o,value);
 static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(o,args);
 static void Frame(Scene scene,int count){for(int i=0;i<count;i++){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[1d/60]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}}
 public static void Run(Scene scene,MiningOperation3D op)
 {
  var host=new SceneManager();host.LoadScene(scene);op.Initialize();op.TutorialEnabled=false;Set(op,"CompletedRounds",2);Set(op,"Credits",100000);Set(op,"RefinedOre",10000);Set(op,"Phase",MiningPhase.Preparation);var player=scene.FindGameObject(op.PlayerObjectId)!;player.Transform.WorldPosition=new(0,.15f,-24);
  var trade=op.GameObject.GetComponent<MiningTrade3D>()!;trade.Initialize();if(!trade.HasResearch("charter"))trade.BuyResearch("charter");if(!trade.HasResearch("rd"))trade.BuyResearch("rd");var shop=op.GameObject.GetComponent<MiningResearchStation3D>()!;Call(shop,"OnUpdate");var planner=player.GetComponent<MiningDefensePlanner3D>()!;
  var ranks=(Dictionary<string,int>)typeof(MiningTrade3D).GetField("_ranks",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(trade)!;
  // Supply caches are optional in the current map; grant test-only stock to cover the reusable placement path.
  if(!trade.Research.Any(n=>n.Stat=="UnlockAirdrop"))((List<CompanyUpgrade>)typeof(MiningTrade3D).GetField("_research",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(trade)!).Add(new(){Id="test-supply-stock",Stat="UnlockAirdrop",Amount=1});
  foreach(var n in trade.Research.Where(n=>n.Stat is "AutogunGrant" or "UnlockMachineGun" or "MortarStrikeGrant" or "LandMineGrant" or "UnlockAirdrop"))ranks[n.Id]=2;
  void Preview(MiningDefenseKind kind){Check(planner.SelectBuild(kind),"select "+kind);Set(planner,"PlacementPoint",new Vector3(8,0,-30));Set(planner,"PlacementValid",true);Call(planner,"UpdateBarricadePreview");var ghost=scene.FindGameObject(kind+" hologram preview")!;Check(ghost!=null&&ghost.Children.Single().GetComponent<SkeletalMeshRenderer>()?.ResolvedModel!=null,"model-only hologram loads for "+kind);Check(!ghost.Components.OfType<Collider3D>().Any()&&!ghost.Components.OfType<HealthComponent>().Any(),"preview has no gameplay components: "+kind);planner.Cancel();}
  Preview(MiningDefenseKind.ResearchStation);Check(shop.PlaceStation(new(-10,0,-30)),"deploy R&D kit");Check(shop.DroneCost==108,"drone cost is 20 percent higher");shop.Buy(1);shop.Buy(3);shop.Buy(3);
  foreach(var kind in Enum.GetValues<MiningDefenseKind>().Where(k=>k!=MiningDefenseKind.ResearchStation))Preview(kind);
  foreach(var kind in new[]{MiningDefenseKind.Sentry,MiningDefenseKind.Repeater,MiningDefenseKind.Mortar,MiningDefenseKind.Mine,MiningDefenseKind.SupplyCache,MiningDefenseKind.ExtraRefinery}){
   Check(planner.SelectBuild(kind),"select repeated placement: "+kind);bool placed=false;for(int i=0;i<20&&!placed;i++){float angle=i*MathF.Tau/20;placed=planner.TryPlace(player.Transform.WorldPosition+new Vector3(MathF.Cos(angle)*24,0,MathF.Sin(angle)*24));}Check(placed&&planner.BuildMode,"placement stays active with more stock: "+kind);planner.Cancel();
  }
  // Actual projectile test: low friendly cover is cleared; tall walls still block.
  op.Enabled=false;Set(op,"Phase",MiningPhase.Drilling);var enemies=(List<GameObject>)typeof(MiningOperation3D).GetField("_enemies",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(op)!;enemies.Clear();
  var gunObject=scene.CreateGameObject("Cover firing test sentry");gunObject.Transform.WorldPosition=new(0,0,-55);var gun=gunObject.AddComponent(new MiningDefense3D{Kind=MiningDefenseKind.Sentry,OperationObjectId=op.GameObject.Id,WorkshopObjectId=op.GameObject.Id,VisualModel=planner.SentryModel,VisualScale=planner.DefenceModelScale,BaseInterval=.2f});
  GameObject Victim(Vector3 p){var enemy=scene.CreateGameObject("Cover firing target");enemy.Transform.WorldPosition=p;enemy.AddComponent(new HealthComponent{MaxHealth=1000,CurrentHealth=1000});enemy.AddComponent(new BoxCollider3D{Center=new(0,.7f,0),Size=new(1.2f,1.4f,1.2f)});enemies.Add(enemy);return enemy;}
  var target=Victim(new(0,0,-65));var cover=scene.CreateGameObject("Friendly low barricade");cover.Transform.WorldPosition=new(0,0,-57);cover.AddComponent(new BoxCollider3D{Center=new(0,.475f,0),Size=new(2.4f,.95f,.26f)});Frame(scene,100);Check(gun.ShotsFired>0&&target.GetComponent<HealthComponent>()!.CurrentHealth<1000,"sentry fires real projectiles over nearby barricades");
  var wall=scene.CreateGameObject("Tall cover test wall");wall.Transform.WorldPosition=new(0,0,-60);wall.AddComponent(new BoxCollider3D{Center=new(0,2,0),Size=new(4,4,.4f)});Frame(scene,20);int shots=gun.ShotsFired;Frame(scene,60);Check(gun.ShotsFired==shots,"tall world obstacles still block firing");var clear=Victim(new(-8,0,-66));Frame(scene,100);Check(clear.GetComponent<HealthComponent>()!.CurrentHealth<1000,"sentry switches from blocked nearest target to another clear target");
  host.UnloadScene();
 }
}
