using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 bool IsCompanyScreen=>Screen is MiningScreen.Company or MiningScreen.Market or MiningScreen.Research or MiningScreen.Account;
 int selectedDelivery;
 void LayoutCompanySystems(Action<Guid,float,float,float,float> place)
 {
  float width=Screen==MiningScreen.Research?940:560;
  place(CloseXButtonObjectId,width-46,18,28,28);
  foreach(var id in new[]{CloseButtonObjectId,SecondaryButtonObjectId,Row0ButtonObjectId,Row1ButtonObjectId,Row2ButtonObjectId})if(W(id) is {} widget)widget.Visible=false;
  if(Screen==MiningScreen.Company||Screen==MiningScreen.Account){if(W(PrimaryButtonObjectId) is {} primary)primary.Visible=false;return;}
  if(Screen==MiningScreen.Research){place(PrimaryButtonObjectId,430,532,156,40);place(CreditsButtonObjectId,28,628,150,20);if(Trade is {} trade)for(int i=0;i<Math.Min(ResearchButtonObjectIds.Count,trade.Research.Count);i++){var n=trade.Research[i];var p=NodePoint(n);float r=NodeRadius;place(ResearchButtonObjectIds[i],p.X-r,p.Y-r,r*2,r*2);if(W(ResearchButtonObjectIds[i]) is {} node)node.Visible=mapShown.Contains(n.Id)&&NodeVisible(p);}return;}
  if(!GuaranteedSelected){place(RefinedButtonObjectId,28,132,252,34);place(RawButtonObjectId,280,132,252,34);place(QuantitySliderObjectId,30,520,500,28);place(PrimaryButtonObjectId,300,580,222,36);return;}
  for(int i=0;i<3;i++){place(ContractButton(i),28,148+i*66,504,56);if(W(ContractButton(i)) is {} row)row.Interactable=Trade?.ActiveDeliveryContract==null&&Trade?.SaleInProgress!=true;}
  bool active=Trade?.ActiveDeliveryContract!=null;place(PrimaryButtonObjectId,active?344:322,active?565:446,active?176:198,40);
  if(!active&&W(PrimaryButtonObjectId) is {} accept)accept.Interactable=Trade is {} t&&!t.SaleInProgress&&selectedDelivery<t.DeliveryOrders.Count&&t.DeliveryOrders[selectedDelivery].Quantity<=t.DeliveryCapacity;
 }
 void TickCompanySystemsInput(MiningOperation3D op)
 {
  if(!IsCompanyScreen){accountDragging=false;return;}
  var size=Input.GameViewSize;float s=Math.Max(.01f,Math.Min(size.X/1280,size.Y/720));var b=CompactBounds(size.X/s,size.Y/s);var m=Input.GameViewMousePosition/s-new Vector2(b.X,b.Y);
  bool pressed=Input.IsMouseButtonPressedForUi(MouseButton.Left),down=Input.IsMouseButtonDownForUi(MouseButton.Left);
  bool Hit(float x,float y,float w,float h)=>m.X>=x&&m.Y>=y&&m.X<x+w&&m.Y<y+h;
  if(pressed&&m.Y>=80&&m.Y<116){if(Hit(28,80,168,36))Open(MiningScreen.Company);else if(Hit(196,80,168,36))Open(MiningScreen.Research);else if(Hit(364,80,168,36))Open(MiningScreen.Account);return;}
  if(Screen==MiningScreen.Company&&pressed){if(Hit(28,154,504,144)){Open(MiningScreen.Market);SelectMarketMethod(true);}else if(Hit(28,326,504,144)){Open(MiningScreen.Market);SelectMarketMethod(false);}return;}
  if(Screen==MiningScreen.Market&&pressed&&!GuaranteedSelected&&Hit(388,440,144,32)&&(m.X<460)!=BuyingOre)ToggleMarketBuying();
  if(Screen is not (MiningScreen.Research or MiningScreen.Account))return;
  float x=Screen==MiningScreen.Research?638:28,w=Screen==MiningScreen.Research?278:504,top=Screen==MiningScreen.Research?180:148;
  if(pressed&&Hit(x,top+374,w,38)){bool ok=op.UpgradeCargo();Notice(ok?$"CARGO EXPANDED TO {op.CargoCapacity}":"Not enough camp credits for cargo expansion.");}
  if(pressed&&Hit(x,top+190,w,40))accountDragging=true;
  if(accountDragging&&down)op.SetCompanyDepositPercent((int)MathF.Round(Math.Clamp((m.X-x-12)/(w-24),0,1)*20)*5,false);
  if(accountDragging&&!down){accountDragging=false;op.SaveProgress();MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Confirm);}
  if(Input.IsKeyPressed(Key.Left))op.SetCompanyDepositPercent(op.CompanyDepositPercent-5);
  if(Input.IsKeyPressed(Key.Right))op.SetCompanyDepositPercent(op.CompanyDepositPercent+5);
 }
 void DrawCompanyTabs(BusinessPainter p)
 {
  for(int i=0;i<3;i++){float x=28+i*168;bool active=i==0?Screen is MiningScreen.Company or MiningScreen.Market:i==1?Screen==MiningScreen.Research:Screen==MiningScreen.Account;p.Card(x,80,168,36,active);if(active){p.Q(x+8,82,152,32,Accent);p.Q(x+2,88,164,20,Accent);}p.S(i==0?"Trade":i==1?"Research":"Account",x+84,88,17,UiAnchor.TopCenter,active?new(.06f,.06f,.05f,1):TextColor);}
 }
 void RenderCompanySystems(RenderContext c,float width,float height,float scale)
 {
  if(Op is not {} op||Trade is not {} trade)return;
  var b=CompactBounds(width,height);var p=new BusinessPainter(this,c,scale,b.X,b.Y);
  c.DrawUiQuad(Vector2.Zero,new(c.TargetWidth,c.TargetHeight),new(0,0,0,.12f));p.Card(0,0,b.Width,b.Height);p.Q(12,10,b.Width-24,58,new(.065f,.065f,.063f,1));
  string title=Screen==MiningScreen.Company?"Company terminal":Screen==MiningScreen.Research?"Research":Screen==MiningScreen.Account?"Account":GuaranteedSelected?"Delivery contracts":"Live exchange";
  p.S(title,24,22,27);p.S($"{op.Credits:N0} cr",b.Width-68,28,18,UiAnchor.TopRight);p.L(b.Width-37,26,b.Width-25,38,TextColor,1.5f);p.L(b.Width-25,26,b.Width-37,38,TextColor,1.5f);DrawCompanyTabs(p);
  if(Screen==MiningScreen.Company){p.Card(28,154,504,144);p.I("company-delivery-truck",50,192,84,84);p.L(152,176,152,276,TextColor);p.S("Delivery contracts",176,187,25);p.T("Fixed payout. Gather ore. Deliver.",176,228,17);p.Card(28,326,504,144);p.I("company-exchange-ingots",50,364,84,84);p.L(152,348,152,448,TextColor);p.S("Live exchange",176,359,25);p.T("Sell or buy at the current price.",176,400,17);p.T("Choose how your next shipment earns.",28,506,16,MenuMuted);}
  else if(Screen==MiningScreen.Account)DrawCompanyAccountCard(p,op,28,148,504);
  else if(Screen==MiningScreen.Research){DrawResearchMap(c,b.X,b.Y,scale,trade);p.Card(18,492,586,138);if(trade.Research.FirstOrDefault(n=>n.Id==SelectedResearchId) is {} chosen){p.IconFrame(ResearchIcon(chosen),30,510,50);p.S(chosen.Name.Length>28?chosen.Name[..25]+"...":chosen.Name,96,505,19);int rank=trade.ResearchRank(chosen.Id);p.T(trade.ResearchRequirement(chosen.Id).Length>0?(trade.ResearchRequirement(chosen.Id).Length>46?trade.ResearchRequirement(chosen.Id)[..43]+"...":trade.ResearchRequirement(chosen.Id)):$"Rank {rank} / {chosen.MaxRank}",96,536,12,MenuMuted);var lines=ResearchDescriptionLines(trade.ResearchBenefit(chosen));descriptionScroll=Math.Clamp(descriptionScroll,0,Math.Max(0,lines.Length-3));for(int i=0;i<3&&i+descriptionScroll<lines.Length;i++)p.T(lines[i+descriptionScroll],30,575+i*18,14);p.S($"{trade.ResearchCost(chosen.Id):N0} cr",586,505,16,UiAnchor.TopRight);p.Button(PrimaryButtonObjectId,!chosen.Available?"COMING SOON":rank>=chosen.MaxRank?"MAX RANK":"UNLOCK",430,532,156,40,true,17);}p.T("Drag RMB to pan / Scroll to zoom",28,458,13,MenuMuted);p.L(620,132,620,620,TextColor);p.S("Account",638,142,23);DrawCompanyAccountCard(p,op,638,180,278);p.T("Asset credits",28,630,13,MenuMuted);}
  else if(GuaranteedSelected)DrawCompanyContracts(p,trade);
  else DrawCompanyExchange(p,trade);
  if(Feedback.Length>0)p.T(Feedback.Length>66?Feedback[..63]+"...":Feedback,28,626,13,Accent);
  p.T("ESC  Close",b.Width-24,638,12,MenuMuted,UiAnchor.TopRight);
 }
 void DrawCompanyAccountCard(BusinessPainter p,MiningOperation3D op,float x,float top,float w)
 {
  p.S("Company target",x,top,18);p.S($"{op.CompanyAccountCredits:N0} / {op.TargetCredits:N0} cr",x,top+32,w<300?21:29);p.Bar(x,top+78,w,16,op.TargetProgress);p.I("clock",x,top+106,22,22);p.T($"{op.DaysRemaining:0.00} days remaining",x+32,top+108,16);p.L(x,top+146,x+w,top+146,TextColor);
  // Shared absolute slider coordinates keep standalone Account and the Research sidebar identical to their hitboxes.
  p.S($"Deposit {op.CompanyDepositPercent}% of sales",x,top+168,w<300?16:20);p.Bar(x+12,top+210,w-24,7,op.CompanyDepositPercent/100f);p.I("support-knob",x+12+(w-24)*op.CompanyDepositPercent/100f-9,top+204,18,18);p.T("0%",x,top+230,14);p.T("100%",x+w,top+230,14,TextColor,UiAnchor.TopRight);
  p.Card(x,top+262,w,102);p.S("Example (1,000 cr in sales)",x+10,top+274,w<300?14:18);int deposit=1000*op.CompanyDepositPercent/100;p.I("company-camp",x+10,top+304,22,22);p.T($"Camp: {1000-deposit:N0} cr",x+44,top+306,w<300?14:18);p.I("company-company-bank",x+10,top+332,22,22);p.T($"Company: {deposit:N0} cr",x+44,top+334,w<300?14:18,TextColor);
  p.Card(x,top+374,w,38);p.S($"Cargo {op.CargoCapacity}   +{op.CargoCapacityPerUpgrade}",x+10,top+384,w<300?13:18);p.T($"Upgrade: {op.CargoUpgradeCost:N0} cr",x,top+422,15,Accent);p.T("Uses camp credits.",x,top+446,14,MenuMuted);
 }
 int CompanyMarketTotal(MiningTrade3D trade)=>BuyingOre?(int)Math.Clamp(Math.Ceiling(Quantity*(double)trade.BuyQuote(RefinedSelected)-.00001),0,int.MaxValue):MiningOperation3D.SaleTotal(Quantity,trade.LiveQuote(RefinedSelected));
 void DrawCompanyExchange(BusinessPainter p,MiningTrade3D trade)
 {
  for(int i=0;i<2;i++){bool selected=i==0?RefinedSelected:!RefinedSelected;float x=28+i*252;p.Card(x,132,252,34,selected);if(selected){p.Q(x+8,134,236,30,Accent);p.Q(x+2,140,248,18,Accent);}p.S(i==0?"Refined":"Raw",x+126,140,17,UiAnchor.TopCenter,selected?new(.06f,.06f,.05f,1):TextColor);}
  float live=trade.LiveQuote(RefinedSelected),reference=trade.GuaranteedQuote(RefinedSelected),offer=BuyingOre?trade.BuyQuote(RefinedSelected):live;float premium=reference>0?(live/reference-1)*100:0;
  p.S($"{offer:0.00} cr / ore",28,184,32);p.Card(356,181,176,52,true);p.S(BuyingOre?$"Buy fee {Math.Clamp(trade.BuyFeePercent,0,25):0.#}%":$"Live pays {premium:+0;-0;0}%",444,189,17,UiAnchor.TopCenter,Accent);p.T(BuyingOre?"included in quote":"vs reference",444,212,12,TextColor,UiAnchor.TopCenter);
  float gx=70,gy=258,gw=462,gh=156,price=(RefinedSelected?Op!.RefinedOrePrice:Op!.RawOrePrice)*Math.Clamp(trade.LiveMarketPriceBonus,.1f,5);var history=trade.History;float factor=BuyingOre?1+Math.Clamp(trade.BuyFeePercent,0,25)/100:1-trade.ExchangeFeePercent/100;var chartValues=history.Select(v=>v*price*factor).Append(reference).Append(offer).ToArray();float padding=Math.Max(.1f,(chartValues.Max()-chartValues.Min())*.12f),low=Math.Max(0,chartValues.Min()-padding),high=chartValues.Max()+padding;
  float Y(float value)=>gy+gh*(1-Math.Clamp((value-low)/Math.Max(.001f,high-low),0,1));
  for(int i=0;i<=4;i++){float yy=gy+i*gh/4;p.L(gx,yy,gx+gw,yy,new(.23f,.23f,.22f,1));p.T((high-(high-low)*i/4).ToString("0.00"),gx-8,yy-7,12,TextColor,UiAnchor.TopRight);}
  for(int i=0;i<=8;i++)p.L(gx+i*gw/8,gy,gx+i*gw/8,gy+gh,new(.16f,.16f,.15f,1));
  for(float xx=gx;xx<gx+gw;xx+=16)p.L(xx,Y(reference),Math.Min(xx+10,gx+gw),Y(reference),TextColor,1.3f);p.T($"Contract reference {reference:0.00}",gx+gw-4,Y(reference)-19,12,TextColor,UiAnchor.TopRight);
  for(int i=1;i<history.Count;i++){float a=gx+(i-1)*gw/Math.Max(1,history.Count-1),b=gx+i*gw/Math.Max(1,history.Count-1);p.L(a,Y(history[i-1]*price*factor),b,Y(history[i]*price*factor),Accent,2);p.Q(b-2,Y(history[i]*price*factor)-2,4,4,Accent);}
  p.L(28,430,532,430,TextColor);p.S($"Owned ({(RefinedSelected?"refined":"raw")} ore): {trade.Stock(RefinedSelected):N0}",28,446,16);p.Card(388,440,144,32);p.Q(BuyingOre?390:460,442,70,28,Accent);p.S("Buy",424,448,15,UiAnchor.TopCenter,BuyingOre?new(.06f,.06f,.05f,1):TextColor);p.S("Sell",496,448,15,UiAnchor.TopCenter,!BuyingOre?new(.06f,.06f,.05f,1):TextColor);
  p.S($"Quantity: {Quantity}",28,490,17);p.Bar(40,534,480,6,MarketQuantityLimit>0?Quantity/(float)MarketQuantityLimit:0);p.I("support-knob",40+(MarketQuantityLimit>0?Quantity/(float)MarketQuantityLimit:0)*480-8,529,16,16);p.T("0",28,548,12);p.T(MarketQuantityLimit.ToString(),532,548,12,TextColor,UiAnchor.TopRight);
  int total=trade.SaleInProgress?trade.LockedSaleCredits:CompanyMarketTotal(trade);p.Card(28,574,504,48);p.S(trade.SaleInProgress?$"Locked payout {total:N0} cr":BuyingOre?$"You pay {total:N0} cr":$"You receive {total:N0} cr",40,588,18);p.Button(PrimaryButtonObjectId,trade.SaleInProgress?"IN TRANSIT":$"{(BuyingOre?"BUY":"SELL")} {Quantity} ORE",300,580,222,36,true,18);
  if(trade.SaleInProgress){p.Bar(28,562,504,5,trade.SaleProgress);p.T($"{trade.SaleQuantity} ore / {trade.SaleSecondsRemaining:0}s remaining",28,632,12,MenuMuted);}else if(Feedback.Length==0&&trade.LastSaleQuantity>0)p.T(Op!.LastMarketSale is {} receipt?$"Last: {trade.LastSaleQuantity} ore / {trade.LastSaleTotal:N0} cr (Camp {receipt.CampPayout} / Company {receipt.CompanyDeposit})":$"Last sale: {trade.LastSaleQuantity} ore / {trade.LastSaleTotal:N0} cr",28,632,12,MenuMuted);
 }
 void DrawCompanyContracts(BusinessPainter p,MiningTrade3D trade)
 {
  var orders=trade.DeliveryOrders;selectedDelivery=Math.Clamp(selectedDelivery,0,Math.Max(0,orders.Count-1));
  for(int i=0;i<orders.Count;i++){var o=orders[i];float y=148+i*66;p.Card(28,y,504,56,selectedDelivery==i);p.I("company-delivery-truck",40,y+15,30,30);p.S($"{o.Quantity} ore",82,y+12,20);p.L(215,y+10,215,y+46,TextColor);p.S($"{o.Payout:N0} cr",232,y+9,19);p.T("Guaranteed total",232,y+34,12,MenuMuted);p.I("clock",402,y+18,20,20);p.T(o.Quantity>trade.DeliveryCapacity?"Freight needed":"No deadline",430,y+22,12,MenuMuted);}
  p.Card(28,358,504,140);p.S("Contract details",40,370,18);p.L(40,399,520,399,TextColor);
  if(orders.Count>0){var chosen=orders[selectedDelivery];p.T(chosen.Buyer,40,410,15);p.T($"{chosen.Quantity} refined ore / {chosen.Payout:N0} cr",40,446,16);if(trade.ActiveDeliveryContract==null)p.Button(PrimaryButtonObjectId,"ACCEPT CONTRACT",322,446,198,40,true,16);else p.T("An order is already accepted.",40,476,12,MenuMuted);}else p.T("No delivery orders are configured.",40,416,16);
  p.Card(28,514,504,106);p.S(trade.SaleInProgress?"Delivery in transit":"Active contract",40,526,18);
  if(trade.ActiveDeliveryContract is {} active){int have=Math.Min(trade.Stock(true),active.Quantity);p.I("company-delivery-truck",40,558,32,32);p.S($"{have} / {active.Quantity} refined ore",88,558,16);p.Bar(88,590,230,9,trade.SaleInProgress?trade.SaleProgress:have/(float)active.Quantity);p.Button(PrimaryButtonObjectId,trade.SaleInProgress?"IN TRANSIT":$"DELIVER {active.Quantity} ORE",344,565,176,40,true,15);}else if(trade.SaleInProgress){p.T($"{trade.SaleQuantity} ore / {trade.LockedSaleCredits:N0} cr",40,564,18);p.Bar(40,596,480,8,trade.SaleProgress);}else p.T("Select an offer, then accept its fixed payout.",40,566,16,MenuMuted);
 }
}
