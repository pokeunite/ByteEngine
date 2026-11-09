using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public void RenderIndependentRefinery(RenderContext c,MiningAuxiliaryRefinery3D local)
 {
  if(Op is not {} op)return;float scale=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),width=c.TargetWidth/scale,height=c.TargetHeight/scale;
  float x=width-548,y=(height-640)/2;var p=new BusinessPainter(this,c,scale,x,y);var mouse=Input.GameViewMousePosition/scale-new Vector2(x,y);
  void Button(string label,float bx,float by,float bw,float bh,bool enabled){bool hover=mouse.X>=bx&&mouse.X<=bx+bw&&mouse.Y>=by&&mouse.Y<=by+bh;p.Q(bx,by,bw,bh,enabled?hover?new(.95f,.82f,.32f,1):Accent:MenuInset);if(!enabled)p.Border(bx,by,bw,bh,MenuEdge);p.T(label,bx+bw/2,by+12,18,enabled?new(.045f,.05f,.06f,1):MenuMuted,UiAnchor.TopCenter);}
  c.DrawUiQuad(Vector2.Zero,new(c.TargetWidth,c.TargetHeight),new(0,0,0,.28f));p.Q(0,0,520,640,Surface);p.Border(0,0,520,640,MenuEdge);p.Q(28,27,6,23,Accent);p.T("REFINERY",46,23,25);p.T($"{op.Credits:N0} cr",445,29,19,Accent,UiAnchor.TopRight);p.L(477,28,491,42,TextColor,1.5f);p.L(491,28,477,42,TextColor,1.5f);p.Rule(66,520);
  p.IconFrame("ore",28,88,48);p.T("RAW LOADED",92,88,15,MenuMuted);p.T(local.LoadedRaw.ToString("N0"),92,111,29);p.L(256,87,256,144);p.IconFrame("ingot",282,88,48);p.T("REFINED",346,88,15,MenuMuted);p.T(op.RefinedOre.ToString("N0"),346,111,29);p.Rule(164,520);
  bool active=local.LoadedRaw>0;p.T(active?"REFINING":"READY TO REFINE",28,183,19,active?Accent:TextColor);p.T(active?$"{local.Progress:P0}":"0%",492,183,19,MenuMuted,UiAnchor.TopRight);p.Bar(28,219,464,12,local.Progress);p.T($"{local.BatchSize} ore per batch",28,248,16,MenuMuted);p.T(active?$"{local.Remaining:0.0}s remaining":$"{local.BatchDuration:0.00}s per batch",492,248,16,MenuMuted,UiAnchor.TopRight);p.T($"LOAD LIMIT {local.LoadCapacity} RAW / {op.RawOre} AVAILABLE",28,275,13,MenuMuted);p.Rule(295,520);
  for(int row=0;row<3;row++){
   float yy=305+row*74;int level=row==0?local.BatchLevel:row==1?local.SpeedLevel:local.YieldLevel;
   string label=row==0?"Batch size":row==1?"Processing time":"Recovery yield",current=row==0?$"{local.BatchSize}":row==1?$"{local.BatchDuration:0.00}s":$"{local.YieldMultiplier:0.00}x",next=row==0?$"{Math.Min(local.LoadCapacity,local.BatchSize+2)}":row==1?$"{Math.Max(local.MinimumBatchSeconds,local.BatchDuration-.25f):0.00}s":$"{local.YieldMultiplier+.1f:0.00}x";
   p.IconFrame(row==0?"crate":row==1?"clock":"gear",28,yy+2,46);p.T(label,90,yy+2,18);p.T(current+"  >",90,yy+28,18,MenuMuted);p.T(level>=5?"MAX":next,236,yy+28,18,Accent);p.Pips(level,5,90,yy+55);Button(level>=5?"MAX":$"{local.Cost(row):N0} cr",384,yy+7,108,42,level<5&&op.Credits>=local.Cost(row));if(row<2)p.Rule(yy+68,520);
  }
  p.Rule(539,520);Button(active?"REFINING...":"REFINE & RETURN",28,552,464,44,!active&&op.RawOre>0);p.T("ESC  CLOSE",492,598,15,MenuMuted,UiAnchor.TopRight);
 }
}
