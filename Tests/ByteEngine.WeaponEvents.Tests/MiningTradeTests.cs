using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
static class MiningTradeTests
{
 static void Check(bool b,string message){if(!b)throw new Exception("Workshop: "+message);Console.WriteLine("PASS workshop: "+message);}
 static void Set(object o,string p,object v)=>o.GetType().GetProperty(p)!.SetValue(o,v);
 public static void Run()
 {
  string root=Path.Combine(Path.GetTempPath(),"MiningWorkshop-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var scene=new Scene("Workshop tests");typeof(Scene).GetProperty("ProjectRoot")!.SetValue(scene,root);
  var company=scene.CreateGameObject("Company");var player=scene.CreateGameObject("Player");player.AddComponent(new HealthComponent());var move=player.AddComponent(new TopDownController3D());
  var op=company.AddComponent(new MiningOperation3D{StartingCredits=1000,PersistProgress=false,PlayerObjectId=player.Id});op.Initialize();
  var trade=company.AddComponent(new MiningTrade3D{OperationObjectId=company.Id,PersistProgress=false});trade.Initialize();
  Check(MiningOperation3D.SaleTotal(20,5.45f)==109,"two-decimal market quotes cannot lose a credit to float rounding");trade.Tick(100);Check(trade.StoredAmmo==100,"production stops exactly at storage capacity");Check(trade.History.Count>1,"market keeps updating in preparation without starting a wave");
  int credits=op.Credits,cost=trade.Cost(0);Check(trade.Buy(0)&&op.Credits==credits-cost&&trade.Output==4,"output purchase uses displayed cost and changes production");
  Check(trade.Buy(1)&&trade.Capacity==150,"storage purchase increases actual capacity");Check(trade.Buy(2)&&Math.Abs(trade.Output-4.4f)<.001f,"efficiency changes actual production rate");
  trade.MaximumLevel=1;credits=op.Credits;Check(!trade.Buy(0)&&op.Credits==credits,"max level does not spend credits");Check(!trade.Buy(-1)&&!trade.Buy(3),"invalid upgrade indices rejected");
  Check(!op.SpendCredits(-1)&&!op.SpendCredits(int.MaxValue),"invalid or unaffordable spending rejected");
  Set(op,"RefinedOre",80);float quote=trade.Quote(true);credits=op.Credits;Check(trade.Sell(true,20)&&op.RefinedOre==60&&op.Credits==credits+MiningOperation3D.SaleTotal(20,quote),"partial sale uses current quote and selected quantity");
  credits=op.Credits;Check(!trade.Sell(true,61)&&!trade.Sell(true,0)&&op.Credits==credits,"overselling and zero sale cannot generate credits");
  Set(op,"RawOre",10);Set(op,"RefiningAmount",8);Check(!trade.Sell(false,3)&&trade.Sell(false,2)&&op.RawOre==8,"refinery-reserved ore cannot be sold");
  Set(op,"Phase",MiningPhase.Drilling);credits=op.Credits;Check(!trade.Buy(0)&&!trade.Sell(true,1)&&!op.SpendCredits(1)&&op.Credits==credits,"transactions blocked during combat");
  for(int i=0;i<200;i++)trade.Tick(6);Check(trade.History.Count==80&&trade.History.All(x=>x>=trade.MinimumMultiplier&&x<=trade.MaximumMultiplier),"bounded price history respects bounds across 200 changes");
  trade.Tick(.2f);float price=trade.PriceMultiplier;trade.PersistProgress=true;trade.Save();
  var other=scene.CreateGameObject("Reloaded trade").AddComponent(new MiningTrade3D{OperationObjectId=company.Id});other.Initialize();Check(other.PriceMultiplier==price&&other.History.SequenceEqual(trade.History)&&other.MarketSeed==trade.MarketSeed&&other.PriceClock==trade.PriceClock&&other.OutputLevel==1,"save restores history, seed, update clock and upgrades");
  trade.Tick(4);other.Tick(4);Check(trade.PriceMultiplier==other.PriceMultiplier,"reloading cannot reroll the next price");
  var ui=scene.CreateGameObject("UI").AddComponent(new MiningBusinessUi3D{OperationObjectId=company.Id,WorkshopObjectId=company.Id});Set(op,"Phase",MiningPhase.Preparation);ui.Open(MiningScreen.AmmoPress);Check(ui.Screen==MiningScreen.AmmoPress&&!move.InputEnabled,"modal disables movement");ui.Close();Check(move.InputEnabled,"closing restores previous movement state");move.InputEnabled=false;ui.Open(MiningScreen.Market);ui.Close();Check(!move.InputEnabled,"closing preserves an initially disabled controller");
  Set(op,"Phase",MiningPhase.Drilling);ui.Open(MiningScreen.Company);Check(ui.Screen==MiningScreen.None,"combat cannot open the shop");
 }
}
