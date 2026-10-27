using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
static class MiningProgressionTests
{
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);Console.WriteLine("PASS progression: "+s);}
 static void Set(object o,string p,object value)=>o.GetType().GetProperty(p)!.SetValue(o,value);
 public static void Run()
 {
  var scene=new Scene("Ranked progression");var player=scene.CreateGameObject("Player");player.AddComponent(new HealthComponent());var gun=scene.CreateGameObject("Pistol").AddComponent(new WeaponDefinition3D{ReserveCapacity=72,DamagePerPellet=24});var company=scene.CreateGameObject("Company");var op=company.AddComponent(new MiningOperation3D{PlayerObjectId=player.Id,StartingCredits=1000000,PersistProgress=false});op.Initialize();var trade=company.AddComponent(new MiningTrade3D{OperationObjectId=company.Id,PersistProgress=false});trade.Initialize();
  Check(trade.LastError==""&&trade.Research.Count>=73,"full renamed research catalog validates");
  for(int pass=0;pass<100;pass++)foreach(var n in trade.Research)if(trade.CanBuyResearch(n.Id))Check(trade.BuyResearch(n.Id),"rank purchase commits "+n.Id);
  Check(trade.Research.All(n=>trade.ResearchRank(n.Id)==n.MaxRank),"every applicable upgrade rank is reachable and purchasable");
  Check(gun.DamagePerPellet>24&&gun.ReserveCapacity>72&&gun.PelletsPerShot>1,"pistol branches change actual damage, reserve and pellet count");
  Check(trade.Stat("AutogunGrant")>0&&trade.Stat("LandMineGrant")>0&&trade.Stat("MortarStrikeGrant")>0,"defence paths grant actual placement budgets");
  Set(op,"RefinedOre",40);float quote=trade.Quote(true);int before=op.Credits;Check(trade.BeginSale(true,20)&&trade.Stock(true)==20,"shipment reserves its selected cargo");Check(!trade.BeginSale(true,1),"overlapping shipment rejected");trade.Tick(.01f);Check(trade.SaleProgress<1,"shipment exposes intermediate progress");for(int i=0;i<20;i++)trade.Tick(.5f);Check(!trade.SaleInProgress&&op.RefinedOre==20&&op.Credits-before==MiningOperation3D.SaleTotal(20,quote),"chunked shipment exactly conserves cargo and quoted credits");
  Set(op,"RawOre",20);Check(op.BeginRefining(),"refining starts");Check(op.CargoUsed==40&&op.CargoReservedSpace==Math.Max(0,op.RefiningOutput-op.RefiningAmount),"cargo reservation counts only extra refinery yield without double-counting raw input");op.Tick(op.RefineryDuration/2);Check(op.RefiningProgress>.45f&&op.RefiningProgress<.55f,"refinery progress tracks real elapsed duration");op.Tick(10);
  var planner=player.AddComponent(new MiningDefensePlanner3D{WorkshopObjectId=company.Id,PersistProgress=false});Set(planner,"SelectedKind",MiningDefenseKind.Mine);Check(planner.TryPlace(new(2,0,0)),"researched mine can be placed");
  var list=(List<GameObject>)typeof(MiningOperation3D).GetField("_enemies",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(op)!;
  GameObject Enemy(Vector3 p){var e=scene.CreateGameObject("Alien");e.Transform.WorldPosition=p;e.AddComponent(new BoxCollider3D{Size=new(.8f,1.5f,.8f),Center=new(0,.75f,0)});e.AddComponent(new HealthComponent{MaxHealth=1000,CurrentHealth=1000});list.Add(e);return e;}
  var target=Enemy(new(2.5f,0,0));Set(op,"Phase",MiningPhase.Drilling);var mine=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningDefense3D>().Single();mine.Tick(.1f);Check(mine.Spent&&target.GetComponent<HealthComponent>()!.CurrentHealth<1000,"mine actually detects and damages an enemy once");
  target.GetComponent<HealthComponent>()!.Kill();
  Set(planner,"SelectedKind",MiningDefenseKind.Sentry);Check(planner.TryPlace(new(-2,0,0)),"researched sentry can be placed");var ranged=Enemy(new(-2,0,-5));var sentry=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningDefense3D>().Single(d=>d.Kind==MiningDefenseKind.Sentry);sentry.Tick(.1f);Check(sentry.ShotsFired==1&&ranged.GetComponent<HealthComponent>()!.CurrentHealth==1000,"deployed sentry waits for projectile impact");foreach(var bullet in scene.GameObjects.Select(o=>o.GetComponent<Projectile3D>()).OfType<Projectile3D>().ToArray())typeof(Projectile3D).GetMethod("Simulate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(bullet,[.1f]);Check(ranged.GetComponent<HealthComponent>()!.CurrentHealth<1000,"deployed sentry projectile damages approaching enemies");
  var wall=scene.CreateGameObject("Cover");wall.Transform.WorldPosition=new(-2,1,-2);wall.AddComponent(new BoxCollider3D{Size=new(2,2,.2f)});int shots=sentry.ShotsFired;sentry.Tick(10);Check(sentry.ShotsFired==shots,"sentry cannot shoot through cover");
  Set(planner,"SelectedKind",MiningDefenseKind.Mortar);Check(planner.TryPlace(new(0,0,3)),"researched mortar can be placed");var mortar=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningDefense3D>().Single(d=>d.Kind==MiningDefenseKind.Mortar);float hp=ranged.GetComponent<HealthComponent>()!.CurrentHealth;mortar.Tick(.1f);mortar.Tick(1);Check(mortar.ShotsFired>1&&ranged.GetComponent<HealthComponent>()!.CurrentHealth<hp,"mortar impact and researched salvo cause real damage");
  Set(planner,"SelectedKind",MiningDefenseKind.SupplyCache);Check(planner.TryPlace(new(4,0,2)),"researched supply crate can be requested");player.Transform.WorldPosition=new(4,0,2);var playerHp=player.GetComponent<HealthComponent>()!;playerHp.CurrentHealth=50;var cache=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningDefense3D>().Single(d=>d.Kind==MiningDefenseKind.SupplyCache);cache.Tick(2);Check(cache.Spent&&playerHp.CurrentHealth==65,"delivered supply crate restores real health and is consumed");
  string folder=Path.Combine(Path.GetTempPath(),"CompanyRanks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);typeof(Scene).GetProperty("ProjectRoot")!.SetValue(scene,folder);trade.PersistProgress=true;trade.Save();trade.PersistProgress=false;var fresh=new Scene("Reload ranks");typeof(Scene).GetProperty("ProjectRoot")!.SetValue(fresh,folder);var co=fresh.CreateGameObject("Company");co.AddComponent(new MiningOperation3D{PersistProgress=false});var loaded=co.AddComponent(new MiningTrade3D{OperationObjectId=co.Id});loaded.Initialize();Check(loaded.Research.All(n=>loaded.ResearchRank(n.Id)==n.MaxRank),"all ranked purchases survive a fresh scene load");
 }
}
