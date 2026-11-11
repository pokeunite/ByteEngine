using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public bool BuyingOre{get;private set;}
 public void ToggleMarketBuying(){if(Trade?.SaleInProgress==true||GuaranteedSelected)return;BuyingOre=!BuyingOre;SetQuantity(Math.Min(20,MarketQuantityLimit));}
 [Browsable(false)]public bool GuaranteedSelected{get;private set;}
 public int MarketQuantityLimit=>Trade is {} trade?BuyingOre&&!GuaranteedSelected?trade.MaximumBuyQuantity(RefinedSelected):GuaranteedSelected?Math.Min(trade.Stock(RefinedSelected),trade.DeliveryCapacity):trade.Stock(RefinedSelected):0;
 public void SelectMarketMethod(bool guaranteed)
 {
  if(Trade?.SaleInProgress==true)return;
  BuyingOre=false;GuaranteedSelected=guaranteed;if(guaranteed)RefinedSelected=true;SetQuantity(Math.Min(20,MarketQuantityLimit));MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalSelect);
 }
 public void StartSelectedMarketSale()
 {
  if(Trade is not {} trade)return;
  if(GuaranteedSelected&&trade.ActiveDeliveryContract==null){TransactionFeedback(trade.AcceptDeliveryContract(selectedDelivery),"CONTRACT ACCEPTED — gather refined ore.","Contract unavailable or freight capacity too low.");return;}
  if(BuyingOre&&!GuaranteedSelected){bool ok=trade.BuyOre(RefinedSelected,Quantity);TransactionFeedback(ok,$"BOUGHT {Quantity} {(RefinedSelected?"REFINED":"RAW")} ORE","Check credits and cargo space.");SetQuantity(Math.Min(20,MarketQuantityLimit));return;}
  if(GuaranteedSelected?trade.DeliverContract():trade.BeginSale(RefinedSelected,Quantity))
  {MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalPurchase);Notice($"{trade.SaleQuantity} ORE ACCEPTED - {trade.LockedSaleCredits:N0} cr LOCKED");SetQuantity(Math.Min(20,MarketQuantityLimit));}
  else TransactionFeedback(false,"","Check available cargo, dispatch limit and current sale.");
 }
}
