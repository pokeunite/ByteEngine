using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningTrade3D
{
 [Category("Market / sales")]public float SaleSeconds{get;set;}=30;
 [Category("Market / sales")]public float GuaranteedPriceFactor{get;set;}=.9f;
 [Category("Market / sales")]public float BaseExchangeFeePercent{get;set;}=3;
 [Category("Market / sales")]public int BaseDeliveryCapacity{get;set;}=40;
 [Browsable(false)]public float BaseSaleUnitsPerSecond{get;set;}=5;
 public float ExchangeFeePercent=>Math.Clamp(BaseExchangeFeePercent-Stat("MarketFeeReductionPct"),0,25);
 public int DeliveryCapacity=>Math.Clamp(BaseDeliveryCapacity+(int)Stat("DeliveryCapacity"),1,1000000);
 public float GuaranteedQuote(bool refined)=>MathF.Round(Math.Max(0,refined?Operation?.RefinedOrePrice??5:Operation?.RawOrePrice??2)*Math.Clamp(GuaranteedPriceFactor,0,10),2);
 public float LiveQuote(bool refined)=>MathF.Floor(Quote(refined)*100*(1-ExchangeFeePercent/100)+.0001f)/100;
 public float SaleQuote=>Operation?.PendingMarketSale?.Quote??Operation?.LastMarketSale?.Quote??0;
 public int SaleQuantity=>Operation?.PendingMarketSale?.Quantity??Operation?.LastMarketSale?.Quantity??0;
 public int SaleProcessed=>Operation?.PendingMarketSale is {} s?(int)Math.Floor(s.Quantity*SaleProgress):Operation?.LastMarketSale?.Quantity??0;
 public bool SaleRefined=>Operation?.PendingMarketSale?.Refined??Operation?.LastMarketSale?.Refined??true;
 public bool SaleInProgress=>Operation?.PendingMarketSale!=null;
 public float SaleProgress=>Operation?.PendingMarketSale is {} s?Math.Clamp(s.Age/s.Seconds,0,1):Operation?.LastMarketSale!=null?1:0;
 public float SaleUnitsPerSecond=>Operation?.PendingMarketSale is {} s?s.Quantity/s.Seconds:0;
 public int LockedSaleCredits=>Operation?.PendingMarketSale?.Credits??0;
 public float SaleSecondsRemaining=>Operation?.PendingMarketSale is {} pending?Math.Max(0,pending.Seconds-pending.Age):0;
 public bool ShippingGuaranteedDelivery=>Operation?.PendingMarketSale?.Guaranteed==true;
 public int LastSaleAdvantage=>Operation?.LastMarketSale?.Advantage??0;
 public int LastSaleQuantity=>Operation?.LastMarketSale?.Quantity??0;
 public bool LastSaleWasGuaranteed=>Operation?.LastMarketSale?.Guaranteed??false;
 public bool BeginSale(bool refined,int quantity)
 {
  return BeginMarketSale(refined,quantity,false);
 }
 public bool BeginMarketSale(bool refined,int quantity,bool guaranteed)
 {
  Initialize();if(guaranteed)return refined&&ActiveDeliveryContract?.Quantity==quantity&&DeliverContract();
  return Operation?.ReserveMarketSale(refined,quantity,LiveQuote(refined),GuaranteedQuote(refined),false,SaleSeconds)==true;
 }
 void TickShipment(float delta)
 {
  bool wasShipping=SaleInProgress;
  Operation?.AdvanceMarketSale(delta);
  if(wasShipping&&!SaleInProgress)MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Notification);
 }
}
