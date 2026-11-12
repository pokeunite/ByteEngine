using System.Numerics;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 void DrawCompanyOverview(BusinessPainter p,MiningOperation3D op,MiningTrade3D trade,float width,float height)
 {
  float card=(width-84)/2;
  void Offer(float x,bool delivery){p.Card(x,154,card,214);p.I(delivery?"company-delivery-truck":"company-exchange-ingots",x+24,178,74,74);p.S(delivery?"DELIVERY CONTRACTS":"LIVE EXCHANGE",x+24,270,25);p.T(delivery?"Agree a fixed payout, gather ore, then deliver.":"Buy or sell ore at the current market price.",x+24,310,17);p.T(delivery?"OPEN CONTRACTS  >":"OPEN EXCHANGE  >",x+24,340,15,Accent);}
  Offer(28,true);Offer(56+card,false);
  p.S("BUSINESS OVERVIEW",28,408,19);
  float cell=(width-84)/3;
  p.Card(28,448,cell,150);p.I("company-camp",46,467,32,32);p.T("AVAILABLE CAMP FUNDS",92,475,14,MenuMuted);p.S($"{op.Credits:N0} cr",48,522,30);p.T("Used for equipment and research",48,565,16,MenuMuted);
  float x=42+cell;p.Card(x,448,cell,150);p.I("company-company-bank",x+18,467,32,32);p.T("COMPANY ACCOUNT",x+64,475,14,MenuMuted);p.S($"{op.CompanyAccountCredits:N0} / {op.TargetCredits:N0} cr",x+20,522,26);p.Bar(x+20,568,cell-40,7,op.TargetProgress);
  x=56+cell*2;p.Card(x,448,cell,150);p.I("cargo",x+18,467,32,32);p.T("ORE IN STORAGE",x+64,475,14,MenuMuted);p.S($"{op.RefinedOre:N0} refined / {op.RawOre:N0} raw",x+20,522,24);p.T($"Cargo {op.CargoUsed:N0} / {op.CargoCapacity:N0}",x+20,565,16,MenuMuted);
  p.T(trade.SaleInProgress?$"Shipment in transit / {trade.SaleSecondsRemaining:0}s remaining":$"{op.CompanyDepositPercent}% of sale income goes to the company account",28,height-58,16,Accent);
 }
 void DrawCompanyFreight(BusinessPainter p,MiningOperation3D op,MiningTrade3D trade,float width,float height)
 {
  float x=600,w=width-x-28;p.L(572,140,572,height-54,MenuEdge);p.S("CAMP & FREIGHT",x,150,22);
  p.Card(x,198,w,94);p.I("raw-ore",x+16,218,32,32);p.T("RAW ORE",x+64,210,14,MenuMuted);p.S(op.RawOre.ToString("N0"),x+64,238,24);p.I("refined-ore",x+w/2,218,32,32);p.T("REFINED",x+w/2+48,210,14,MenuMuted);p.S(op.RefinedOre.ToString("N0"),x+w/2+48,238,24);
  p.T($"CARGO  {op.CargoUsed:N0} / {op.CargoCapacity:N0}",x,314,17);p.Bar(x,348,w,8,op.CargoFillFraction);
  p.Card(x,388,w,170);p.S(trade.SaleInProgress?"SHIPMENT IN TRANSIT":"READY FOR YOUR NEXT ORDER",x+18,408,20);
  if(trade.SaleInProgress){p.S($"{trade.SaleProcessed} / {trade.SaleQuantity} ore",x+18,450,23);p.T($"{trade.SaleSecondsRemaining:0}s remaining",x+w-18,455,17,Accent,UiAnchor.TopRight);p.Bar(x+18,490,w-36,8,trade.SaleProgress);p.T($"Locked total {trade.LockedSaleCredits:N0} cr",x+18,520,17);}
  else{p.T("Delivery contracts lock in a price.",x+18,454,18);p.T("Exchange orders use the live quote.",x+18,484,18);p.T("Payouts arrive during combat too.",x+18,524,16,Accent);}
  p.T($"Company deposit: {op.CompanyDepositPercent}% / change in Account",x,590,16,MenuMuted);
 }
 void DrawFullscreenResearch(RenderContext c,BusinessPainter p,MiningOperation3D op,MiningTrade3D trade,float ox,float oy,float width,float height,float scale)
 {
  mapWidth=width-360;mapHeight=height;float x=width-332;
  if(Feedback.Length==0)p.T("Asset credits",28,height-26,13,MenuMuted);
  p.Card(20,132,mapWidth-28,height-206);DrawResearchMap(c,ox,oy,scale,trade);p.L(mapWidth+8,132,mapWidth+8,height-50,MenuEdge);
  p.Card(28,height-62,130,28);p.T("CENTRE MAP",42,height-56,13,Accent);p.T("RMB drag to pan / Wheel to zoom",178,height-55,14,MenuMuted);p.T("Gold: owned   /   Outline: available   /   Dim: locked",28,142,13,MenuMuted);
  p.S("UPGRADE DETAILS",x,145,20);
  if(trade.Research.FirstOrDefault(n=>n.Id==SelectedResearchId) is not {} chosen){p.T("Select an upgrade on the map.",x,200,18);return;}
  p.IconFrame(ResearchIcon(chosen),x,188,58);int rank=trade.ResearchRank(chosen.Id);p.T($"RANK {rank} / {chosen.MaxRank}",x+78,207,16,Accent);
  var names=ResearchDescriptionLines(chosen.Name);for(int i=0;i<Math.Min(2,names.Length);i++)p.S(names[i],x,266+i*25,21);
  p.L(x,320,x+304,320,MenuEdge);
  var lines=ResearchDescriptionLines(trade.ResearchBenefit(chosen));int rows=ResearchDetailRows;descriptionScroll=Math.Clamp(descriptionScroll,0,Math.Max(0,lines.Length-rows));for(int i=0;i<rows&&i+descriptionScroll<lines.Length;i++)p.T(lines[i+descriptionScroll],x,340+i*23,17);
  string requirement=trade.ResearchRequirement(chosen.Id);if(requirement.Length>0){var requirements=ResearchDescriptionLines(requirement);for(int i=0;i<Math.Min(2,requirements.Length);i++)p.T(requirements[i],x,height-170+i*19,14,Accent);}
  if(lines.Length>rows)p.T("Scroll over details for more",x,height-194,13,MenuMuted);
  p.S(rank>=chosen.MaxRank?"FULLY RESEARCHED":$"{trade.ResearchCost(chosen.Id):N0} cr",x,height-126,22);
  p.Button(PrimaryButtonObjectId,!chosen.Available?"COMING SOON":rank>=chosen.MaxRank?"MAX RANK":!trade.ResearchUnlocked(chosen.Id)?"LOCKED":"UNLOCK UPGRADE",x,height-100,304,44,true,18);
 }
}
