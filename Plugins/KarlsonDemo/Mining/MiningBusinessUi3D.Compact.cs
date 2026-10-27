using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 (float X,float Y,float Width,float Height) CompactBounds(float width,float height)=>Screen==MiningScreen.AudioCredits?((width-740)/2,(height-450)/2,740,450):Screen==MiningScreen.Drill?(Math.Min(width-960,(width-940)/2+90),(height-560)/2,940,560):Screen==MiningScreen.Research?(width-848,(height-660)/2,820,660):Screen==MiningScreen.Market?(width-588,(height-660)/2,560,660):(width-548,(height-640)/2,520,640);
 void LayoutCompact()
 {
  var size=Input.GameViewSize;float scale=Math.Max(.01f,Math.Min(size.X/1280,size.Y/720));float width=size.X/scale,height=size.Y/scale;var b=CompactBounds(width,height);
  void Place(Guid id,float x,float y,float w,float h){if(W(id) is {} widget){widget.Anchor=UiAnchor.Center;widget.Offset=new(b.X+x+w/2-width/2,b.Y+y+h/2-height/2);widget.Size=new(w,h);}}
  Place(CloseXButtonObjectId,b.Width-52,17,36,36);Place(CloseButtonObjectId,b.Width-132,b.Height-48,104,30);
  if(Screen==MiningScreen.AudioCredits){Place(CloseXButtonObjectId,690,10,36,36);Place(CloseButtonObjectId,570,385,140,40);return;}
  if(Screen==MiningScreen.Refinery||Screen==MiningScreen.AmmoPress){for(int i=0;i<3;i++)Place(i==0?Row0ButtonObjectId:i==1?Row1ButtonObjectId:Row2ButtonObjectId,384,312+i*74,108,42);Place(PrimaryButtonObjectId,28,552,464,44);return;}
  if(Screen==MiningScreen.Market){Place(SecondaryButtonObjectId,28,80,242,36);Place(RawButtonObjectId,28,140,98,28);Place(RefinedButtonObjectId,136,140,130,28);Place(QuantitySliderObjectId,30,454,500,32);Place(PrimaryButtonObjectId,28,558,504,46);return;}
  if(Screen==MiningScreen.Research){Place(SecondaryButtonObjectId,280,80,242,36);Place(CreditsButtonObjectId,28,612,150,25);Place(PrimaryButtonObjectId,584,541,208,45);
   if(Trade is {} trade)for(int i=0;i<Math.Min(ResearchButtonObjectIds.Count,trade.Research.Count);i++){var n=trade.Research[i];var p=NodePoint(n);float r=NodeRadius;Place(ResearchButtonObjectIds[i],p.X-r,p.Y-r,r*2,r*2);if(W(ResearchButtonObjectIds[i]) is {} w)w.Visible=mapShown.Contains(n.Id)&&NodeVisible(p);}
   return;
  }
  if(Screen==MiningScreen.Drill){for(int i=0;i<3;i++)Place(ContractButton(i),28+i*298,86,286,288);Place(Row0ButtonObjectId,252,407,158,40);Place(PrimaryButtonObjectId,28,476,730,44);Place(CloseButtonObjectId,790,483,122,32);}
 }
}
