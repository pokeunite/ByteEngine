using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 (float X,float Y,float Width,float Height) CompactBounds(float width,float height)=>IsCompanyScreen?(20,12,width-40,height-24):Screen==MiningScreen.Account?(width-588,(height-580)/2,560,580):Screen==MiningScreen.AudioCredits?((width-740)/2,(height-450)/2,740,450):Screen==MiningScreen.Drill?((width-1120)/2,(height-640)/2,1120,640):Screen==MiningScreen.Research?(width-848,(height-660)/2,820,660):Screen==MiningScreen.Market?(width-588,(height-660)/2,560,660):(width-500,(height-580)/2,480,580);
 void LayoutCompact()
 {
  var size=Input.GameViewSize;float scale=Math.Max(.01f,Math.Min(size.X/1280,size.Y/720));float width=size.X/scale,height=size.Y/scale;var b=CompactBounds(width,height);
  void Place(Guid id,float x,float y,float w,float h){if(W(id) is {} widget){widget.Anchor=UiAnchor.Center;widget.Offset=new(b.X+x+w/2-width/2,b.Y+y+h/2-height/2);widget.Size=new(w,h);}}
  Place(CloseXButtonObjectId,b.Width-52,17,36,36);Place(CloseButtonObjectId,b.Width-132,b.Height-48,104,30);
  if(IsCompanyScreen){LayoutCompanySystems(Place);return;}
  if(Screen==MiningScreen.AudioCredits){Place(CloseXButtonObjectId,690,10,36,36);Place(CloseButtonObjectId,570,385,140,40);return;}
  if(Screen==MiningScreen.Refinery||Screen==MiningScreen.AmmoPress){Place(CloseXButtonObjectId,434,12,32,32);if(W(CloseButtonObjectId) is {} close)close.Visible=false;for(int i=0;i<3;i++)Place(i==0?Row0ButtonObjectId:i==1?Row1ButtonObjectId:Row2ButtonObjectId,354,260+i*68,98,40);Place(PrimaryButtonObjectId,18,500,444,44);Place(SecondaryButtonObjectId,18,548,330,28);return;}
  if(Screen==MiningScreen.Market){Place(Row2ButtonObjectId,280,179,252,28);Place(SecondaryButtonObjectId,28,80,162,36);Place(Row0ButtonObjectId,28,132,242,36);Place(Row1ButtonObjectId,280,132,252,36);Place(RawButtonObjectId,28,179,98,28);Place(RefinedButtonObjectId,136,179,130,28);Place(QuantitySliderObjectId,30,394,500,32);Place(PrimaryButtonObjectId,28,558,504,46);for(int i=0;i<3;i++)Place(ContractButton(i),28,212+i*88,504,76);return;}
  if(Screen==MiningScreen.Research){Place(SecondaryButtonObjectId,200,80,162,36);Place(CreditsButtonObjectId,584,600,150,23);Place(PrimaryButtonObjectId,584,541,208,45);
   if(Trade is {} trade)for(int i=0;i<Math.Min(ResearchButtonObjectIds.Count,trade.Research.Count);i++){var n=trade.Research[i];var p=NodePoint(n);float r=NodeRadius;Place(ResearchButtonObjectIds[i],p.X-r,p.Y-r,r*2,r*2);if(W(ResearchButtonObjectIds[i]) is {} w)w.Visible=mapShown.Contains(n.Id)&&NodeVisible(p);}
   return;
  }
  if(Screen==MiningScreen.Drill){Place(CloseXButtonObjectId,1070,15,32,32);if(W(CloseButtonObjectId) is {} close)close.Visible=false;Place(Row0ButtonObjectId,210,562,160,32);Place(PrimaryButtonObjectId,772,544,328,48);}
 }
}
