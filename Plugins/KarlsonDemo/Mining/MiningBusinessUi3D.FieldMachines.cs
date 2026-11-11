using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 void RenderFieldMachine(RenderContext c,float width,float height,float scale,MiningAuxiliaryRefinery3D? local=null)
 {
  if(Op is not {} op)return;
  bool ammo=local==null&&Screen==MiningScreen.AmmoPress;
  var trade=Trade;float x=width-500,y=(height-580)/2;
  var mouse=Input.GameViewMousePosition/scale-new Vector2(x,y);
  void Image(string name,float px,float py,float w,float h)=>FieldImage(c,name,x+px,y+py,w,h,scale);
  void T(string value,float px,float py,int size=17,bool bold=false,UiAnchor anchor=UiAnchor.TopLeft,Vector4? color=null)=>FieldText(c,value,x+px,y+py,size,scale,bold,anchor,color);
  void Q(float px,float py,float w,float h,Vector4 color)=>c.DrawUiQuad(new((x+px)*scale,(y+py)*scale),new(w*scale,h*scale),color);
  void L(float px,float py,float xx,float yy,Vector4? color=null)=>c.DrawUiLine(new((x+px)*scale,(y+py)*scale),new((x+xx)*scale,(y+yy)*scale),scale,color??new(.60f,.60f,.55f,.7f));
  void Bar(float px,float py,float w,float fraction){Q(px,py,w,12,new(.32f,.33f,.32f,1));Q(px,py,w*Math.Clamp(fraction,0,1),12,Accent);}
  void Button(string label,float px,float py,float w,float h,bool enabled,bool primary=false,Guid id=default)
  {
   bool hover=local==null?W(id)?.IsHovered==true:mouse.X>=px&&mouse.X<=px+w&&mouse.Y>=py&&mouse.Y<=py+h;
   if(primary){Q(px,py,w,h,enabled?(hover?new(1,.85f,.35f,1):Accent):new(.18f,.19f,.17f,1));T(label,px+w/2,py+12,18,true,UiAnchor.TopCenter,enabled?new(.06f,.06f,.05f,1):MenuMuted);}
   else{Image("price-border",px,py,w,h);if(hover&&enabled)Q(px+2,py+2,w-4,h-4,new(.4f,.35f,.14f,.7f));Image("credits",px+7,py+11,18,18);T(label,px+w-8,py+11,16,true,UiAnchor.TopRight,enabled?TextColor:MenuMuted);}
  }
  c.DrawUiQuad(Vector2.Zero,new(c.TargetWidth,c.TargetHeight),new(0,0,0,.12f));
  Image("machine-border",0,0,480,580);Image(ammo?"ammo":"factory",22,18,30,30);
  T(ammo?"Ammo press":"Refinery",65,18,25,true);T(ammo?"Produces reserve ammunition.":"Refines raw ore into usable refined ore.",22,57,15);
  L(439,20,453,34);L(453,20,439,34);L(18,82,462,82);
  bool active=local!=null?local.LoadedRaw>0:op.RefiningAmount>0;
  float progress=local?.Progress??op.RefiningProgress;
  if(ammo&&trade!=null)
  {
   Image("cargo",24,99,24,24);T("Bucket",62,101);T($"{(int)trade.StoredAmmo} / {trade.Capacity}",453,101,18,true,UiAnchor.TopRight);
   L(18,134,462,134);Image("gear",24,149,24,24);bool full=trade.StoredAmmo>=trade.Capacity;
   T(full?"Bucket full":"Producing",62,152,16,true);float fraction=full?1:trade.UseProductionCycles?trade.ProductionCycleProgress:trade.StoredAmmo-MathF.Floor(trade.StoredAmmo);
   Bar(191,153,181,fraction);T(full?"Full":trade.UseProductionCycles?$"{trade.CycleDuration*(1-fraction):0.0}s":$"{fraction:P0}",453,152,16,false,UiAnchor.TopRight);
   T("Ammo ejects automatically. Walk near the bucket to collect.",22,191,14);
  }
  else
  {
   Image("raw-ore",24,97,24,24);T("Raw loaded",62,101);T($"{local?.LoadedRaw??op.RefiningAmount} / {local?.LoadCapacity??op.RefineryLoadCapacity}",453,101,18,true,UiAnchor.TopRight);
   Image("refined-ore",24,132,24,24);T("Refined stock",62,136);T(op.RefinedOre.ToString("N0"),453,136,18,true,UiAnchor.TopRight);
   Image("gear",24,169,24,24);T(active?"Processing":"Ready to load",62,172,16,true);Bar(191,174,181,progress);T(progress.ToString("P0"),453,172,16,false,UiAnchor.TopRight);
   T(active?$"Time remaining: {local?.Remaining??op.RefiningRemaining:0.0}s":$"{op.RawOre:N0} raw ore available",191,196,14);
  }
  L(18,222,462,222);T("Upgrades",22,230,18,true);
  for(int row=0;row<3;row++)
  {
   float yy=250+row*68;string name,sub,icon,current,next;int cost,level,max;
   if(ammo&&trade!=null)
   {
    level=trade.Level(row);max=trade.MaximumLevel;name=row==0?"Cycle time":row==1?"Bucket capacity":"Rounds per cycle";sub=row==0?"Time per production cycle":row==1?"Maximum rounds stored":"Rounds produced each cycle";icon=row==0?"clock":row==1?"cargo":"ammo";
    current=row==0?(trade.UseProductionCycles?$"{trade.CycleDuration:0.00}s":$"{trade.Output:0.#}/s"):row==1?$"{trade.Capacity}":trade.UseProductionCycles?$"{trade.RoundsPerCycle:0.#}":$"{1+trade.EfficiencyLevel*trade.EfficiencyPerLevel:0.##}x";
    next=row==0?(trade.UseProductionCycles?$"{trade.CycleDurationAt(trade.OutputLevel+1):0.00}s":$"{trade.OutputAt(trade.OutputLevel+1,trade.EfficiencyLevel):0.#}/s"):row==1?$"{trade.Capacity+trade.ExtraStoragePerLevel}":trade.UseProductionCycles?$"{trade.RoundsAt(trade.EfficiencyLevel+1):0.#}":$"{1+(trade.EfficiencyLevel+1)*trade.EfficiencyPerLevel:0.##}x";cost=trade.Cost(row);
   }
   else
   {
    var kind=RowUpgrade(row);level=local==null?op.Level(kind):row==0?local.BatchLevel:row==1?local.SpeedLevel:local.YieldLevel;max=local==null?op.MaximumUpgradeLevel:5;
    name=row==0?"Batch size":row==1?"Cycle time":"Recovery";sub=row==0?"Ore per cycle":row==1?"Processing duration":"Refined ore yield";icon=row==0?"cargo":row==1?"clock":"recycle";
    current=row==0?$"{local?.BatchSize??op.RefineryBatchSizeAt(level)}":row==1?$"{local?.BatchDuration??op.RefineryDuration:0.00}s":$"{local?.YieldMultiplier??op.RefineryYieldMultiplier:0.00}x";
    next=row==0?$"{(local!=null?Math.Min(local.LoadCapacity,local.BatchSize+2):op.RefineryBatchSizeAt(level+1))}":row==1?$"{(local!=null?Math.Max(local.MinimumBatchSeconds,local.BatchDuration-.25f):op.RefineryDurationAt(level+1)):0.00}s":$"{(local!=null?local.YieldMultiplier+.1f:op.RefineryYieldMultiplier+op.RefineryYieldPerLevel):0.00}x";cost=local?.Cost(row)??op.Cost(kind);
   }
   Image("upgrade-row-border",18,yy,444,62);Image(icon,27,yy+16,25,25);T(name,62,yy+7,16,true);T(sub,62,yy+34,12);
   T(current,224,yy+22,15,false,UiAnchor.TopRight);L(236,yy+29,245,yy+29);L(241,yy+25,245,yy+29);L(245,yy+29,241,yy+33);T((level>=max||!ammo&&local==null&&op.IsUpgradeMaxed(RowUpgrade(row)))?"MAX":next,253,yy+22,15);
   var id=row==0?Row0ButtonObjectId:row==1?Row1ButtonObjectId:Row2ButtonObjectId;
   Button((level>=max||!ammo&&local==null&&op.IsUpgradeMaxed(RowUpgrade(row)))?"MAX":cost.ToString("N0"),354,yy+10,98,40,level<max&&op.Credits>=cost,id:id);
  }
  L(18,461,462,461);Image("info",22,473,17,17);T("Upgrades affect this machine only.",47,474,13);T($"{op.Credits:N0} cr",458,474,14,true,UiAnchor.TopRight,Accent);
  if(!ammo)Button(active?"PROCESSING...":"LOAD ORE",18,500,444,44,!active&&op.RawOre>0,true,PrimaryButtonObjectId);
  else T("Production continues while you are away.",22,516,14);
  if(!ammo){bool auto=local?.AutoLoad??op.AutoRefineryEnabled;T((auto?"[ON] ":"[OFF] ")+"Auto load / 35% slower",22,554,13,true,color:auto?Accent:TextColor);T("Esc Close",458,554,12,anchor:UiAnchor.TopRight);}else T(Feedback.Length>0?Feedback:"Esc Close",22,554,13,color:MenuMuted);
 }
}
