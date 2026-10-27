using System.Numerics;
using System.Reflection;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
static class MiningBusinessTests
{
 static void Check(bool ok,string message){if(!ok)throw new Exception("Mining: "+message);Console.WriteLine("PASS mining: "+message);}
 static (Scene Scene,MiningOperation3D Op,HealthComponent Player,HealthComponent Drill) Setup(bool persist=false,string? root=null)
 {
  var s=new Scene("Mining tests");if(root!=null)typeof(Scene).GetProperty("ProjectRoot")!.SetValue(s,root);
  var player=s.CreateGameObject("Operator");var hp=player.AddComponent(new HealthComponent());var drill=s.CreateGameObject("Machine");var dh=drill.AddComponent(new HealthComponent());var o=s.CreateGameObject("Business");
  var op=o.AddComponent(new MiningOperation3D{PlayerObjectId=player.Id,DrillObjectId=drill.Id,PersistProgress=persist,BaseEnemies=2,SpawnInterval=.05f,ExtractionSeconds=1,RaiderBlueprint=new(Guid.NewGuid(),"Raider.byteblueprint"),HunterBlueprint=new(Guid.NewGuid(),"Hunter.byteblueprint")});
  s.CreateGameObject("Entrance").AddComponent(new MiningSpawnPoint3D{OperationObjectId=o.Id});return(s,op,hp,dh);
 }
 static Guid Spawner()=>RuntimeSpawnService.ConfigureBlueprintSpawner((s,a,p)=>{var e=s.CreateGameObject("Spawned");e.Transform.WorldPosition=p;e.AddComponent(new HealthComponent());e.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true});e.AddComponent(new AIBehavior3D());return e;});
 static void Finish(MiningOperation3D op){for(int i=0;i<100&&op.Phase==MiningPhase.Drilling;i++){op.Tick(.1f);foreach(var e in op.SpawnedEnemies.ToArray())if(e.Scene!=null)e.GetComponent<HealthComponent>()!.Kill();}op.Tick(.1f);}
 public static void Run()
 {
  var registration=Spawner();try
  {
   var t=Setup();Check(t.Op.StartOperation(),"valid setup starts one operation");Check(!t.Op.StartOperation()&&!t.Op.Buy(MiningUpgrade.Drill),"cannot restart or purchase during a wave");
   for(int i=0;i<100;i++)t.Op.Tick(.1f);Check(t.Op.ExtractionProgress==1&&t.Op.RawOre==0&&t.Op.AliveEnemies==2&&t.Op.EnemiesSpawned==2,"last enemy cannot farm infinite resources or extra spawns");
   Finish(t.Op);Check(t.Op.Phase==MiningPhase.Preparation&&t.Op.RawOre==18&&t.Op.CompletedRounds==1,"fixed quota awarded once after extraction and all enemies end");t.Op.Tick(100);Check(t.Op.RawOre==18,"idle time cannot repeat payout");
   Check(t.Op.BeginRefining()&&!t.Op.BeginRefining()&&!t.Op.StartOperation()&&!t.Op.SellCargo(),"refinery reserves one batch and blocks duplicate processing or selling it");t.Op.Tick(4);Check(t.Op.RawOre==0&&t.Op.RefinedOre==18,"refining conserves resource units");
   Check(t.Op.SellCargo()&&t.Op.Credits==110&&!t.Op.SellCargo(),"sale uses explicit refined price exactly once");
   Check(t.Op.Buy(MiningUpgrade.Drill)&&t.Op.Credits==70&&t.Op.DrillLevel==1,"purchase deducts the displayed cost");Check(t.Op.StartOperation()&&t.Op.RoundQuota==25&&t.Drill.MaxHealth==210&&t.Op.EnemiesScheduled==4,"next operation reflects drill yield, durability and wave escalation");t.Drill.Kill();t.Op.Tick(.1f);Check(t.Op.Phase==MiningPhase.Failed&&t.Op.RawOre==0&&t.Op.AliveEnemies==0,"destroying the drill fails operation without unbanked reward");t.Op.Retry();Check(t.Op.Phase==MiningPhase.Preparation&&!t.Player.IsDead&&!t.Drill.IsDead&&t.Op.DrillLevel==1&&t.Op.Credits==70,"retry restores actors and keeps purchased progress");
   var limit=Setup();limit.Op.MaximumAlive=1;limit.Op.BaseEnemies=6;limit.Op.StartOperation();for(int i=0;i<20;i++)limit.Op.Tick(.1f);Check(limit.Op.EnemiesSpawned==1&&limit.Op.AliveEnemies==1,"spawn concurrency budget enforced");Finish(limit.Op);Check(limit.Op.EnemiesSpawned==6&&limit.Op.RawOre==18,"budgeted wave still finishes and pays once");
   var death=Setup();death.Op.StartOperation();death.Player.Kill();death.Op.Tick(.1f);Check(death.Op.Phase==MiningPhase.Failed&&death.Op.RawOre==0,"operator death fails operation");
   var research=Setup();research.Op.StartingCredits=300;var gun=research.Scene.CreateGameObject("Gun");var def=gun.AddComponent(new WeaponDefinition3D{DamagePerPellet=20});research.Op.Initialize();Check(research.Op.Buy(MiningUpgrade.CompanyResearch)&&Math.Abs(def.DamagePerPellet-22)<.001f,"company research changes actual gun damage");research.Op.MaximumUpgradeLevel=1;Check(!research.Op.Buy(MiningUpgrade.CompanyResearch),"max level prevents overspending");
   string path=Path.Combine(Path.GetTempPath(),"Mining-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(path);
   var save=Setup(true,path);save.Op.StartingCredits=100;save.Op.Initialize();save.Op.Buy(MiningUpgrade.Drill);save.Op.SaveProgress();var reload=Setup(true,path);reload.Op.Initialize();Check(reload.Op.DrillLevel==1&&reload.Op.Credits==60,"company save round-trips across a fresh component");
   RuntimeSpawnService.ClearBlueprintSpawner(registration);var missing=Setup();Check(!missing.Op.StartOperation(),"missing native spawner rejects operation before extraction");registration=RuntimeSpawnService.ConfigureBlueprintSpawner((s,a,p)=>null);var failed=Setup();failed.Op.StartOperation();failed.Op.Tick(.1f);Check(failed.Op.Phase==MiningPhase.Failed&&failed.Op.RawOre==0,"failed blueprint spawn never creates a free payout");
  }finally{RuntimeSpawnService.ClearBlueprintSpawner(registration);}
 }
}
