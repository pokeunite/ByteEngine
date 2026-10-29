using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
using ByteEngine.Core.VisualLogic;
static class MiningContractTests
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS contracts: "+text);}
 public static void Run()
 {
  var scene=new Scene("Contract test");var player=scene.CreateGameObject("Operator");player.AddComponent(new HealthComponent());var drill=scene.CreateGameObject("Drill");drill.AddComponent(new HealthComponent());var op=scene.CreateGameObject("Company").AddComponent(new MiningOperation3D{PlayerObjectId=player.Id,DrillObjectId=drill.Id,PersistProgress=false,SpawnInterval=.05f,ExtractionSeconds=1,RaiderBlueprint=new("raider.byteblueprint"),HunterBlueprint=new("hunter.byteblueprint"),MaximumAlive=100});scene.CreateGameObject("Front entrance").AddComponent(new MiningSpawnPoint3D{OperationObjectId=op.GameObject.Id});
  var a=op.PreviewContract(0)!;var b=op.PreviewContract(1)!;var c=op.PreviewContract(2)!;
  var authoredContracts=op.ContractsJson;op.ContractsJson="{";
  Check(op.Contracts.Count==0,"editing contract JSON invalidates prior valid previews");
  op.ContractsJson=authoredContracts;Check(op.Contracts.Count==3,"repairing contract JSON restores previews immediately");
  Check(a.Total==6&&a.Raiders==4&&a.Hunters==2&&a.Ore==18,"shallow preview has explicit role counts and ore");Check(a.HunterHealth==50&&a.RaiderHealth==75&&a.HunterSpeed==5&&a.RaiderSpeed==3.8f,"preview exposes tougher health and faster approach speeds");Check(b.Total>a.Total&&c.Total>b.Total&&b.Ore>a.Ore&&c.Ore>b.Ore,"risk and reward increase together");
  foreach(var wave in new[]{a,b,c})Check(Enumerable.Range(0,wave.Total).Count(i=>MiningOperation3D.IsHunterAt(wave,i))==wave.Hunters,"spawn schedule exactly matches preview for "+wave.Id);
  op.MaximumEnemiesPerRound=7;Check(op.PreviewContract(2)!.Total==7,"preview respects hard enemy budget");op.MaximumEnemiesPerRound=24;
  var token=RuntimeSpawnService.ConfigureBlueprintSpawner((s,asset,p)=>{var e=s.CreateGameObject(asset.ProjectPath);e.Transform.WorldPosition=p;e.AddComponent(new HealthComponent());e.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,MoveSpeed=asset.ProjectPath.StartsWith("hunter")?3.1f:1.9f});e.AddComponent(new AIBehavior3D());e.AddComponent(new NavigationAgent3D());return e;});
  try
  {
   Check(op.SelectContract(2)&&op.StartOperation(),"selected overdrive launches");var wave=op.ActiveWave!;Check(!op.SelectContract(0),"selection cannot change during a live wave");op.ContractsJson="invalid";op.BaseEnemies=99;
   for(int i=0;i<300&&op.EnemiesSpawned<op.EnemiesScheduled;i++)op.Tick(.05f);
   Check(op.EnemiesSpawned==wave.Total&&op.RoundQuota==wave.Ore,"active wave snapshots counts and reward against mid-wave edits");
   Check(op.SpawnedEnemies.Count(e=>e.GetComponent<AIBehavior3D>()!.TargetObjectId==player.Id)==wave.Hunters&&op.SpawnedEnemies.Count(e=>e.GetComponent<AIBehavior3D>()!.TargetObjectId==drill.Id)==wave.Raiders,"spawned role totals and assigned targets match advertised counts");
   foreach(var e in op.SpawnedEnemies)
   {
    var ai=e.GetComponent<AIBehavior3D>()!;var combat=e.GetComponent<SimpleEnemyAI3D>()!;bool hunter=ai.TargetObjectId==player.Id;Check(e.GetComponent<HealthComponent>()!.MaxHealth==(hunter?wave.HunterHealth:wave.RaiderHealth),"actual enemy health matches the advertised contract");
    Check(combat.MoveSpeed> (hunter?4.4f:3.2f)&&ai.MovementSpeedMultiplier>1,"actual navigation pressure increases for "+(hunter?"hunter":"raider"));
    if(!hunter)Check(combat.DodgeEnabled&&e.GetComponent<MiningEnemyEvasion3D>() is {Cooldown:>=2,ReactionSeconds:>=.1f},"raider evasion is bounded and delayed");
   }
   foreach(var e in op.SpawnedEnemies)e.GetComponent<HealthComponent>()!.Kill();op.Tick(2);Check(op.RawOre==wave.Ore&&op.CompletedRounds==1,"completed contract awards exactly its previewed ore once");
   Check(op.Contracts.Count==0&&!op.StartOperation(),"malformed contracts reject launch without free rewards");
   op.ContractsJson=new MiningOperation3D().ContractsJson;op.BaseEnemies=6;typeof(MiningOperation3D).GetProperty("RefinedOre")!.SetValue(op,40);var trade=op.GameObject.AddComponent(new MiningTrade3D{OperationObjectId=op.GameObject.Id,PersistProgress=false});trade.Initialize();int credits=op.Credits;Check(trade.BeginSale(true,20),"shipment reserves old cargo before next wave");float lockedQuote=trade.SaleQuote;Check(op.SelectContract(0)&&op.StartOperation(),"drilling can start with an active shipment");Check(op.CargoUsed==op.RawOre+op.RefinedOre&&op.CargoReservedSpace==op.RoundQuota,"cargo HUD accounts for stored goods and incoming extraction separately");trade.Tick(1);Check(trade.SaleProcessed>0&&op.Phase==MiningPhase.Drilling,"shipment progresses and pays during live combat");for(int i=0;i<100&&trade.SaleInProgress;i++)trade.Tick(.5f);Check(!trade.SaleInProgress&&op.RefinedOre==20&&op.Credits==credits+MiningOperation3D.SaleTotal(20,lockedQuote),"background shipment consumes and pays its old cargo exactly once");int paid=op.Credits;trade.Tick(20);Check(op.Credits==paid,"finished shipment cannot pay twice during combat");Check(!trade.BeginSale(true,1)&&!op.SellUnits(true,1,99),"new instant trades still require the terminal between rounds");drill.GetComponent<HealthComponent>()!.Kill();op.Tick(.1f);Check(op.CargoReservedSpace==0,"failed extraction releases incoming cargo reservation");

  }finally{RuntimeSpawnService.ClearBlueprintSpawner(token);}
  Check(MiningEnemyEvasion3D.Threatens(Vector3.Zero,-Vector3.UnitZ,new(0,0,-8),24,1.6f),"near firing lane registers a threat");Check(!MiningEnemyEvasion3D.Threatens(Vector3.Zero,-Vector3.UnitZ,new(8,0,-8),24,1.6f)&&!MiningEnemyEvasion3D.Threatens(Vector3.Zero,-Vector3.UnitZ,new(0,0,8),24,1.6f)&&!MiningEnemyEvasion3D.Threatens(Vector3.Zero,-Vector3.UnitZ,new(0,0,-40),24,1.6f),"sideways, behind-player and distant enemies do not dodge unrelated shots");
  var obstacleScene=new Scene("Static machinery navigation");var machine=obstacleScene.CreateGameObject("Destructible drill");machine.AddComponent(new HealthComponent());machine.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D{Size=new(3,2,3),Center=new(0,1,0)});var regionObject=obstacleScene.CreateGameObject("Grid");regionObject.Transform.WorldPosition=new(-5,0,-5);var region=regionObject.AddComponent(new NavigationRegion3D{Width=20,Height=20,CellSize=.5f,ExcludeDynamicActors=true,EndpointSearchRadius=3});region.Bake();Check(region.Grid!=null&&!region.Grid.IsWalkable(10,10),"static drill remains a navigation obstacle despite having health");Check(region.FindPath(new(0,0,-4),new(0,0,4)).Count>0,"hunters can route around the drill to the operator behind it");Check(region.FindPath(new(0,0,-4),Vector3.Zero).Count>0,"raiders can find a reachable attack approach beside the drill");
  var dodger=scene.CreateGameObject("Dodger");dodger.Transform.WorldPosition=new(0,0,-5);var hp=dodger.AddComponent(new HealthComponent());var executor=dodger.AddComponent(new SimpleEnemyAI3D{BehaviorControlled=true,DodgeEnabled=true,DodgeSpeed=5,DodgeSeconds=.18f,DodgeCooldown=4.5f,TargetId=drill.Id});executor.RequestDodge();Check(executor.TickBehaviorBackground(.05f),"requested dodge enters native background tactic");var before=dodger.Transform.WorldPosition;executor.TickBehaviorBackground(.1f);Check(Vector3.Distance(before,dodger.Transform.WorldPosition)>.1f,"native dodge actually moves the raider");hp.Damage(5);Check(hp.CurrentHealth==95,"dodging never grants invulnerability");
 }
}
