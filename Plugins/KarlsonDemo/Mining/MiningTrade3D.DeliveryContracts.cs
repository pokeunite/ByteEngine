using System.ComponentModel;
using System.Text.Json;
namespace KarlsonDemo.Plugin;

public sealed class OreDeliveryContract
{
 public string Id{get;set;}="local";
 public string Buyer{get;set;}="Orbital repair depot";
 public int Quantity{get;set;}=20;
 public float UnitPrice{get;set;}=4.5f;
 public int Payout=>MiningOperation3D.SaleTotal(Quantity,UnitPrice);
 public bool Valid=>!string.IsNullOrWhiteSpace(Id)&&!string.IsNullOrWhiteSpace(Buyer)&&Quantity is >0 and <=1000000&&float.IsFinite(UnitPrice)&&UnitPrice is >0 and <=1000;
}
public sealed partial class MiningTrade3D
{
 [Category("Delivery contracts"),Description("Buyer orders: a fixed amount of refined ore for a locked payout. Larger orders require Freight capacity.")]
 public string DeliveryOrdersJson{get;set;}=JsonSerializer.Serialize(new OreDeliveryContract[]{new(),new(){Id="colony",Buyer="Colony engineering",Quantity=40,UnitPrice=5},new(){Id="shipyard",Buyer="Frontier shipyard",Quantity=80,UnitPrice=5.5f}});
 public IReadOnlyList<OreDeliveryContract> DeliveryOrders
 {
  get{try{return (JsonSerializer.Deserialize<OreDeliveryContract[]>(DeliveryOrdersJson)??[]).Where(o=>o.Valid).Take(3).ToArray();}catch(JsonException){return [];}}
 }
 public OreDeliveryContract? ActiveDeliveryContract=>Operation?.ActiveDeliveryContract;
 public bool AcceptDeliveryContract(int index)
 {
  var orders=DeliveryOrders;
  if(index<0||index>=orders.Count||orders[index].Quantity>DeliveryCapacity||SaleInProgress)return false;
  // Snapshot the order. Editing definitions or market prices cannot change an accepted job.
  return Operation?.AcceptOreContract(new(){Id=orders[index].Id,Buyer=orders[index].Buyer,Quantity=orders[index].Quantity,UnitPrice=orders[index].UnitPrice})==true;
 }
 public bool DeliverContract()
 {
  if(ActiveDeliveryContract is not {} order||order.Quantity>DeliveryCapacity)return false;
  return Operation?.ReserveMarketSale(true,order.Quantity,order.UnitPrice,order.UnitPrice,true,SaleSeconds)==true;
 }
}
public sealed partial class MiningOperation3D
{
 [Browsable(false)]public OreDeliveryContract? ActiveDeliveryContract{get;private set;}
 [Browsable(false)]public int DeliveryContractsCompleted{get;private set;}
 public bool AcceptOreContract(OreDeliveryContract order)
 {
  Initialize();if(!order.Valid||ActiveDeliveryContract!=null||PendingMarketSale!=null||Phase!=MiningPhase.Preparation||TargetWon||TargetExpired)return false;
  ActiveDeliveryContract=order;SaveProgress();return true;
 }
}
