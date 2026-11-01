using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningTrade3D
{
 [Category("Market / buying")]public float BuyFeePercent{get;set;}=5;
 public float BuyQuote(bool refined)=>MathF.Ceiling(Quote(refined)*(1+Math.Clamp(BuyFeePercent,0,25)/100)*100)/100;
 public int MaximumBuyQuantity(bool refined)
 {
  if(Operation is not {} op||SaleInProgress||op.Phase!=MiningPhase.Preparation||op.TargetWon||op.TargetExpired)return 0;
  float price=BuyQuote(refined);if(!float.IsFinite(price)||price<=0)return 0;
  return Math.Max(0,Math.Min(op.CargoCapacity-op.CargoUsed-op.CargoReservedSpace,(int)Math.Min(1000000,Math.Floor(op.Credits/price))));
 }
 public bool BuyOre(bool refined,int quantity){Initialize();return quantity>0&&quantity<=MaximumBuyQuantity(refined)&&Operation?.PurchaseOre(refined,quantity,BuyQuote(refined))==true;}
}
public sealed partial class MiningOperation3D
{
 public bool PurchaseOre(bool refined,int quantity,float quote)
 {
  Initialize();if(Phase!=MiningPhase.Preparation||TargetWon||TargetExpired||PendingMarketSale!=null||quantity<=0||quantity>1000000||!float.IsFinite(quote)||quote<=0||quote>1000000||quantity>(long)CargoCapacity-CargoUsed-CargoReservedSpace)return false;
  long cost=(long)Math.Ceiling(quantity*(double)quote-.00001);if(cost<=0||cost>Credits)return false;
  Credits-=(int)cost;if(refined)RefinedOre+=quantity;else RawOre+=quantity;Message=$"Bought {quantity} {(refined?"refined":"raw")} ore for {cost} credits.";SaveProgress();return true;
 }
}
