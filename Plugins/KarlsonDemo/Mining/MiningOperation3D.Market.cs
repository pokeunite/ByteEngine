using System.ComponentModel;
namespace KarlsonDemo.Plugin;

public sealed class MiningSale
{
 public string Id{get;set;}=Guid.NewGuid().ToString("N");
 public bool Refined{get;set;}public bool Guaranteed{get;set;}
 public int Quantity{get;set;}public int Credits{get;set;}public int BaselineCredits{get;set;}
 public float Quote{get;set;}public float Seconds{get;set;}=30;public float Age{get;set;}
 public int Advantage=>Credits-BaselineCredits;
 public bool Valid=>Id is {Length:32}&&Quantity>0&&Quantity<=1000000&&Credits>=0&&Credits<=1000000000&&BaselineCredits>=0&&BaselineCredits<=1000000000&&float.IsFinite(Quote)&&Quote>=0&&Quote<=1000000&&float.IsFinite(Seconds)&&Seconds>=.1f&&float.IsFinite(Age)&&Age>=0;
}
public sealed partial class MiningOperation3D
{
 [Browsable(false)]public MiningSale? PendingMarketSale{get;private set;}
 [Browsable(false)]public MiningSale? LastMarketSale{get;private set;}
 public bool ReserveMarketSale(bool refined,int quantity,float quote,float guaranteed,bool delivery,float seconds)
 {
  Initialize();if(PendingMarketSale!=null||Phase!=MiningPhase.Preparation||TargetWon||TargetExpired||quantity<=0||quantity>1000000||!float.IsFinite(quote)||quote<0||quote>1000000||!float.IsFinite(guaranteed)||guaranteed<0||guaranteed>1000000||!float.IsFinite(seconds))return false;
  int available=refined?RefinedOre:Math.Max(0,RawOre-RefiningAmount);if(quantity>available)return false;
  if(delivery&&(ActiveDeliveryContract is not {} order||!refined||quantity!=order.Quantity||Math.Abs(quote-order.UnitPrice)>.001f))return false;
  var sale=new MiningSale{Refined=refined,Guaranteed=delivery,Quantity=quantity,Quote=quote,Credits=SaleTotal(quantity,quote),BaselineCredits=SaleTotal(quantity,guaranteed),Seconds=Math.Clamp(seconds,.1f,3600)};
  if(refined)RefinedOre-=quantity;else RawOre-=quantity;if(delivery)ActiveDeliveryContract=null;PendingMarketSale=sale;SaveProgress();return true;
 }
 public void AdvanceMarketSale(float delta)
 {
  if(PendingMarketSale is not {} sale||!float.IsFinite(delta)||delta<=0)return;
  sale.Age=Math.Min(sale.Seconds,sale.Age+delta);
  if(sale.Age<sale.Seconds)return;
  Credits=Add(Credits,sale.Credits);LastMarketSale=sale;PendingMarketSale=null;if(sale.Guaranteed)DeliveryContractsCompleted++;
  Message=$"Sold {sale.Quantity} ore for {sale.Credits:N0} credits.";SaveProgress();
 }
}
