namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 void DrawDeliveryContracts(BusinessPainter p,MiningTrade3D trade)
 {
  if(trade.SaleInProgress)
  {
   p.T(trade.ShippingGuaranteedDelivery?"CONTRACT DELIVERY":"LIVE SALE IN TRANSIT",28,205,24);
   p.T($"{trade.SaleQuantity} ore / {trade.LockedSaleCredits:N0} cr locked",28,255,21);
   p.T("Keep drilling — payment arrives automatically.",28,301,16,MenuMuted);
   p.Bar(28,366,504,9,trade.SaleProgress);p.T($"{trade.SaleSecondsRemaining:0}s remaining",28,391,18);
   p.Button(PrimaryButtonObjectId,"DELIVERY IN PROGRESS",28,558,504,46,true,18);return;
  }
  if(trade.ActiveDeliveryContract is {} order)
  {
   int have=Math.Min(trade.Stock(true),order.Quantity);
   p.T("ACCEPTED ORDER",28,201,15,Accent);p.T(order.Buyer,28,234,24);
   p.T($"Deliver {order.Quantity} refined ore",28,283,21);
   p.T($"FIXED PAYMENT   {order.Payout:N0} cr",28,330,25,Accent);
   p.T($"READY TO DELIVER   {have} / {order.Quantity}",28,386,18);
   p.Bar(28,422,504,8,have/(float)order.Quantity);
   p.T("Gather ore across operations. No deadline or price risk.",28,457,15,MenuMuted);
   if(Feedback.Length>0)p.Wrap(Feedback,28,500,55,15);
   p.Button(PrimaryButtonObjectId,have>=order.Quantity?$"DELIVER ORDER / {order.Payout:N0} cr":$"NEED {order.Quantity-have} MORE REFINED ORE",28,558,504,46,true,17);return;
  }
  p.T("CHOOSE A BUYER ORDER",28,182,17,MenuMuted);
  var orders=trade.DeliveryOrders;
  for(int i=0;i<orders.Count;i++)
  {
   var offer=orders[i];float y=212+i*88;
   p.Button(ContractButton(i),"",28,y,504,76,false,18);
   p.T(offer.Buyer,42,y+9,20);p.T($"{offer.Quantity} refined ore",42,y+42,17,MenuMuted);
   p.T($"{offer.Payout:N0} cr",516,y+9,18,Accent,ByteEngine.Core.Graphics.UiAnchor.TopRight);
   if(offer.Quantity>trade.DeliveryCapacity)p.T("FREIGHT REQUIRED",516,y+44,13,MenuMuted,ByteEngine.Core.Graphics.UiAnchor.TopRight);
  }
  p.T("Select an order. Gather its ore. Deliver for a fixed payout.",28,497,15,MenuMuted);
  if(trade.LastSaleWasGuaranteed&&trade.LastSaleQuantity>0)p.T($"LAST CONTRACT PAID  {trade.LastSaleTotal:N0} cr",28,525,16,Accent);
  p.Button(PrimaryButtonObjectId,"SELECT AN ORDER ABOVE",28,558,504,46,false,17);
 }
}
