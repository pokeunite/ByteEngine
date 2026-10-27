namespace KarlsonDemo.Plugin;
public sealed partial class MiningTrade3D
{
 public float BaseSaleUnitsPerSecond{get;set;}=5;
 public int SaleQuantity{get;private set;}
 public int SaleProcessed{get;private set;}
 public bool SaleRefined{get;private set;}
 public float SaleQuote{get;private set;}
 public bool SaleInProgress=>SaleProcessed<SaleQuantity;
 public float SaleProgress=>SaleQuantity>0?SaleProcessed/(float)SaleQuantity:0;
 public float SaleUnitsPerSecond=>Math.Max(.1f,BaseSaleUnitsPerSecond+Stat("OilSellRate"))*Math.Clamp(PriceMultiplier,.65f,1.6f);
 float saleAccumulator;
 public bool BeginSale(bool refined,int quantity)
 {
  Initialize();if(SaleInProgress||Operation?.Phase!=MiningPhase.Preparation||quantity<=0||Stock(refined)<quantity)return false;
  SaleQuantity=quantity;SaleProcessed=0;SaleRefined=refined;SaleQuote=Quote(refined);saleAccumulator=0;LastSaleTotal=0;LastSaleQuote=SaleQuote;return true;
 }
 void TickShipment(float delta)
 {
  if(!SaleInProgress||Operation==null)return;
  saleAccumulator=Math.Min(SaleQuantity,saleAccumulator+delta*SaleUnitsPerSecond);int count=Math.Min(SaleQuantity-SaleProcessed,(int)saleAccumulator);if(count==0)return;
  if(Operation.ProcessShipmentUnits(SaleRefined,count,SaleQuote,SaleProcessed)){SaleProcessed+=count;saleAccumulator-=count;LastSaleTotal=MiningOperation3D.SaleTotal(SaleProcessed,SaleQuote);}
  else{SaleQuantity=SaleProcessed;LastError="Shipment stopped: cargo changed. Unsold cargo remains in storage.";}
 }
}
