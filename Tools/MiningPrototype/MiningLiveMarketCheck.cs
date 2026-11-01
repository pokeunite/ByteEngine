using System.Reflection;
using System.Text.Json;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningLiveMarketCheck
{
 public static void Run(Scene scene,MiningOperation3D op,MiningTrade3D trade,Action<bool,string> check)
 {
  void Set(object o,string p,object v)=>o.GetType().GetProperty(p)!.SetValue(o,v);
  Set(op,"RefinedOre",100);Set(trade,"PriceMultiplier",1.2f);trade.SaleSeconds=30;
  check(Math.Abs(trade.LiveQuote(true)-5.82f)<.001f&&Math.Abs(trade.GuaranteedQuote(true)-4.5f)<.001f,"live net and guaranteed quotes are explicit and include fees");
  int cash=op.Credits;check(!trade.BeginMarketSale(true,41,true)&&op.RefinedOre==100,"base delivery capacity rejects oversized dispatch without consuming cargo");
  check(trade.AcceptDeliveryContract(0)&&trade.BeginMarketSale(true,20,true)&&op.RefinedOre==80&&op.Credits==cash&&trade.LockedSaleCredits==90,"accepted delivery reserves the whole batch and locks a guaranteed payout");
  check(!trade.BeginSale(true,10)&&op.RefinedOre==80,"an accepted batch cannot be double-sold");
  Set(op,"Phase",MiningPhase.Drilling);Set(trade,"PriceMultiplier",.65f);trade.Tick(15);
  check(trade.SaleInProgress&&Math.Abs(trade.SaleProgress-.5f)<.001f&&op.Credits==cash,"shipping continues during drilling and pays only on completion");
  var saved=JsonSerializer.Deserialize<MiningOperation3D.MiningSave>(JsonSerializer.Serialize(new MiningOperation3D.MiningSave{Credits=op.Credits,Refined=op.RefinedOre,PendingMarketSale=op.PendingMarketSale}))!;
  var root=scene.CreateGameObject("Market persistence fixture");var restored=root.AddComponent(new MiningOperation3D{PersistProgress=false,TargetEnabled=false});restored.Initialize();Set(restored,"Credits",saved.Credits);Set(restored,"RefinedOre",saved.Refined);Set(restored,"PendingMarketSale",saved.PendingMarketSale!);restored.AdvanceMarketSale(15);int settled=restored.Credits;restored.AdvanceMarketSale(100);
  check(settled==cash+90&&restored.Credits==settled&&restored.PendingMarketSale==null&&restored.RefinedOre==80,"saved in-flight sale resumes and settles exactly once with no duplicate ore");scene.DestroyGameObject(root);
  trade.Tick(15);check(op.Credits==cash+90&&trade.LastSaleWasGuaranteed&&trade.LastSaleAdvantage==0,"guaranteed delivery ignores a market slump and records its receipt");
  Set(op,"Phase",MiningPhase.Preparation);Set(trade,"PriceMultiplier",1.2f);cash=op.Credits;
  check(trade.BeginSale(true,20),"whole-batch live sale accepts the displayed offer");Set(trade,"PriceMultiplier",.65f);trade.Tick(30);
  check(op.Credits==cash+116&&trade.LastSaleAdvantage==26&&!trade.LastSaleWasGuaranteed,"live price locks and receipt shows the gain versus delivery");
  Set(trade,"PriceMultiplier",.65f);check(trade.BeginSale(true,20),"low market remains an explicit player choice");trade.Tick(30);
  check(trade.LastSaleTotal==63&&trade.LastSaleAdvantage==-27,"low-price receipt reports the exact shortfall versus delivery");
  check(trade.BuyResearch("freight")&&trade.DeliveryCapacity==80,"freight research increases guaranteed dispatch capacity from 40 to 80");
  check(trade.BuyResearch("scanner")&&trade.HasDemandScanner,"demand scanner unlocks published market-cycle information");
  Set(trade,"PriceMultiplier",1.2f);float before=trade.LiveQuote(true);check(trade.BuyResearch("broker")&&trade.ExchangeFeePercent==1&&trade.LiveQuote(true)>before,"broker research lowers the fee and improves the actual live payout");
  Set(trade,"DemandClock",0f);trade.Tick(90);check(trade.DemandDescription=="Strong demand"&&trade.NextDemandDescription=="Quiet demand","published demand cycle changes and scanner context matches its real schedule");
  Set(op,"RawOre",40);Set(op,"RefiningAmount",10);check(!trade.BeginMarketSale(false,31,false)&&trade.BeginMarketSale(false,5,false)&&op.RawOre==35,"raw sale cannot reserve ore already being refined");trade.Tick(30);Set(op,"RefiningAmount",0);
  check(!trade.BeginMarketSale(true,0,false)&&!op.ReserveMarketSale(true,1,float.NaN,4.5f,false,30),"market rejects zero quantities and invalid quotes");
  Set(op,"RawOre",0);Set(op,"RefinedOre",100);Set(trade,"PriceMultiplier",1f);
  var ui=scene.GameObjects.Select(o=>o.GetComponent<MiningBusinessUi3D>()).OfType<MiningBusinessUi3D>().Single();Set(ui,"Screen",MiningScreen.Market);ui.Purchase(1);ui.SetQuantity(100);
  check(ui.GuaranteedSelected&&ui.Quantity==80&&ui.MarketQuantityLimit==80,"native delivery button and quantity slider respect upgraded dispatch capacity");
  ui.Purchase(0);ui.SetQuantity(20);check(!ui.GuaranteedSelected&&ui.MarketQuantityLimit==100,"native live-market button restores the full-stock selling choice");ui.Primary();
  check(trade.SaleInProgress&&trade.SaleQuantity==20,"native Sell button submits the selected whole-batch sale");trade.Tick(30);Set(ui,"Screen",MiningScreen.None);Set(op,"RefinedOre",0);
 }
}
