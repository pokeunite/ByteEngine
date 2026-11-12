using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Characters;
using ByteEngine.Core;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningDirectorCheck
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static void Set(object o,string name,object value)=>o.GetType().GetProperty(name)!.SetValue(o,value);
 static object? Call(object o,string name,params object[] values)=>o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(o,values);
 public static void Run()
 {
  var scene=new Scene("Director regressions");var player=scene.CreateGameObject("Player");var hp=player.AddComponent(new HealthComponent());var drill=scene.CreateGameObject("Drill");var rig=drill.AddComponent(new HealthComponent());drill.Transform.WorldPosition=new(0,0,5);
  var owner=scene.CreateGameObject("Operation");var op=owner.AddComponent(new MiningOperation3D{PersistProgress=false,PlayerObjectId=player.Id,DrillObjectId=drill.Id,TutorialEnabled=false,BaseCargoCapacity=10000,TargetEnabled=false});op.Initialize();
  Set(op,"RawOre",1000);op.SetCompanyDepositPercent(50,false);
  int credits=op.Credits;
  for(int i=0;i<10;i++){Check(op.ReserveMarketSale(false,1,1,1,false,.1f),"reserve one-credit sale");op.AdvanceMarketSale(.1f);}
  Check(op.CompanyAccountCredits==5&&op.Credits==credits+5,"split small shipments conserves money and fractional deposits");
  op.SetCompanyDepositPercent(25,false);Check(op.ReserveMarketSale(false,40,10,10,false,1),"reserve locked allocation shipment");op.SetCompanyDepositPercent(100,false);op.AdvanceMarketSale(1);
  Check(op.CompanyAccountCredits==105&&op.LastMarketSale is {CompanyDeposit:100,CampPayout:300},"in-flight shipment keeps its original allocation");
  op.SetCompanyDepositPercent(0,false);op.ReserveMarketSale(false,1,10,10,false,1);op.AdvanceMarketSale(1);Check(op.LastMarketSale is {CompanyDeposit:0,CampPayout:10},"zero percent pays camp only");
  op.SetCompanyDepositPercent(100,false);op.ReserveMarketSale(false,1,10,10,false,1);op.AdvanceMarketSale(1);Check(op.LastMarketSale is {CompanyDeposit:10,CampPayout:0},"100 percent pays protected account only");
  int bank=op.CompanyAccountCredits;Check(op.SpendCredits(10)&&op.CompanyAccountCredits==bank,"camp purchases cannot reduce company progress");
  var oldSave=JsonSerializer.Deserialize<MiningOperation3D.MiningSave>("{\"Version\":1,\"Credits\":1234}")!;Check(oldSave.Credits==1234&&oldSave.DepositPercent==null&&oldSave.CompanyAccount==0,"legacy save defaults preserve spendable money");
  var save=JsonSerializer.Deserialize<MiningOperation3D.MiningSave>(JsonSerializer.Serialize(new MiningOperation3D.MiningSave{CompanyAccount=115,DepositPercent=75,DepositRemainder=50}))!;Check(save.CompanyAccount==115&&save.DepositPercent==75&&save.DepositRemainder==50,"company balance, allocation and rounding survive save roundtrip");
  var list=(List<GameObject>)typeof(MiningOperation3D).GetField("_enemies",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(op)!;
  Set(op,"EnemiesScheduled",24);Set(op,"EnemiesSpawned",16);Call(op,"ResetDirector");
  for(int i=0;i<16;i++){var e=scene.CreateGameObject("Enemy "+i);e.AddComponent(new HealthComponent());list.Add(e);}
  Check(op.ReleasedEnemies==16,"24-enemy wave initially releases 16");
  foreach(var e in list.Take(3))e.GetComponent<HealthComponent>()!.Kill();Call(op,"TickDirector",3f);Check(op.ReleasedEnemies==20,"three quick kills release four reinforcements");
  foreach(var e in list.Skip(3).Take(3))e.GetComponent<HealthComponent>()!.Kill();Call(op,"TickDirector",.1f);Check(op.ReleasedEnemies==20,"reinforcement minimum gap prevents an instant refill");Call(op,"TickDirector",3f);Check(op.ReleasedEnemies==24,"final batch is bounded by the selected total");
  hp.CurrentHealth=10;Set(op,"EnemiesScheduled",24);Call(op,"ResetDirector");Call(op,"TickDirector",3f);Check(op.ReleasedEnemies==16,"low player health gives breathing room before reinforcements");hp.CurrentHealth=100;
  var point=scene.CreateGameObject("Front spawn");point.Transform.WorldPosition=new(0,0,-35);point.AddComponent(new MiningSpawnPoint3D{OperationObjectId=owner.Id});
  var floor=scene.CreateGameObject("Recovery floor");floor.Transform.WorldPosition=new(0,-.5f,-35);floor.AddComponent(new BoxCollider3D{Size=new(30,1,30)});
  var stranded=list.Last();stranded.Transform.WorldPosition=new(0,-40,-20);stranded.Active=false;float health=stranded.GetComponent<HealthComponent>()!.CurrentHealth;Call(op,"TickDirector",1f);
  Check(stranded.ActiveInHierarchy&&Vector3.Distance(stranded.Transform.WorldPosition,point.Transform.WorldPosition+Vector3.UnitY*.04f)<.01f&&stranded.GetComponent<HealthComponent>()!.CurrentHealth==health&&op.StrandedEnemiesRecovered>0,"inactive fallen enemy returns at a safe front spawn without free damage");
  stranded.Transform.WorldPosition=new(0,-.2f,-35);Call(op,"TickDirector",1f);Check(Math.Abs(stranded.Transform.WorldPosition.Y-.04f)<.01f,"near-surface buried spider projects onto actual floor");
  op.SetCompanyDepositPercent(0,false);Set(op,"Phase",MiningPhase.Drilling);Set(op,"RefinedOre",5);Check(op.ReserveMarketSale(true,5,2,2,false,1),"new exchange can be placed during combat");op.AdvanceMarketSale(1);Check(op.PendingMarketSale==null,"combat sale completes");Set(op,"Phase",MiningPhase.Preparation);
  Set(op,"RawOre",op.CargoCapacity);Set(op,"RefinedOre",0);Set(op,"RefineryYieldLevel",10);Check(op.BeginRefining(),"full cargo can begin yield-enhanced refining");
  op.Tick(op.RefiningRemaining+.01f);Check(op.CargoUsed>op.CargoCapacity&&op.RefinedOre>0,"full cargo refinery completes without losing bonus yield");Check((bool)Call(op,"CompleteAuxiliaryBatch",5)!,"auxiliary refinery also releases output when cargo is full");
  Set(op,"RefiningAmount",0);Set(op,"RefiningOutput",0);Set(op,"RefiningRemaining",0f);
  foreach(var e in list.ToArray())scene.DestroyGameObject(e);list.Clear();
  Set(op,"ActiveWave",op.PreviewContract(0)!);Set(op,"RoundQuota",18);Set(op,"RawOre",0);Set(op,"Phase",MiningPhase.Drilling);Set(op,"EnemiesScheduled",1);Set(op,"EnemiesSpawned",1);Set(op,"ExtractionProgress",1f);hp.CurrentHealth=20;rig.CurrentHealth=50;op.Tick(.01f);
  Check(op.CompletedRounds==1&&op.RawOre==18&&op.OverclockAvailable,"base operation secures ore and opens optional overclock");
  op.HoldOverclock(true,1);Check(!op.IsOverclockActive,"held interaction at wave end cannot accidentally accept overclock");op.HoldOverclock(false,.01f);op.HoldOverclock(true,1);
  Check(op.IsOverclockActive&&hp.CurrentHealth==20&&rig.CurrentHealth==50,"deliberate hold starts bonus wave without health reset");
  Set(op,"EnemiesSpawned",op.EnemiesScheduled);Set(op,"ExtractionProgress",1f);op.Tick(.01f);
  Check(!op.IsOverclockActive&&!op.OverclockAvailable&&op.RawOre==27&&op.CompletedRounds==1&&hp.HealthPercent==1,"bonus clear awards once, heals and cannot chain indefinitely");
  Set(op,"Phase",MiningPhase.Drilling);Set(op,"RoundQuota",18);Set(op,"EnemiesScheduled",1);Set(op,"EnemiesSpawned",1);Set(op,"ExtractionProgress",1f);op.Tick(.01f);Check(op.OverclockAvailable,"next regular operation may offer another overclock");op.Tick(20);Check(!op.OverclockAvailable&&op.Phase==MiningPhase.Preparation,"ignoring the offer automatically returns to preparation");
  var siege=op.PreviewContract(0)!;var hunters=op.PreviewContract(1)!;var swarm=op.PreviewContract(2)!;Check(siege.Raiders>siege.Hunters&&hunters.Hunters>hunters.Raiders&&swarm.Total>=siege.Total&&swarm.HunterHealth<siege.HunterHealth,"operation previews expose distinct siege, hunter and fragile swarm tactics");
  Set(op,"Phase",MiningPhase.Drilling);Set(op,"RoundQuota",18);Set(op,"EnemiesScheduled",1);Set(op,"EnemiesSpawned",1);Set(op,"ExtractionProgress",1f);op.Tick(.01f);int secured=op.RawOre;op.StartOverclock();hp.Kill();op.Tick(.01f);
  Check(op.Phase==MiningPhase.Failed&&op.RawOre==secured&&op.CompanyAccountCredits==bank,"failed overclock loses bonus but preserves secured base haul and company deposits");
  var goal=scene.CreateGameObject("Goal test").AddComponent(new MiningOperation3D{PersistProgress=false,PlayerObjectId=player.Id,DrillObjectId=drill.Id,TutorialEnabled=false});goal.Initialize();Set(goal,"Credits",50000);Set(goal,"TargetStarted",true);goal.Tick(.01f);Check(!goal.TargetWon,"large camp wallet alone cannot complete company target");Set(goal,"CompanyAccountCredits",30000);goal.Tick(.01f);Check(goal.TargetWon,"protected company deposits complete the level target");
  // Company-screen interactions have a dedicated current-layout suite: MiningCompanySystemsCheck.
 }
}
