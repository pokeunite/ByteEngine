using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public int SurveySector{get;private set;}
 public int SurveySectorCount=>Math.Max(1,((Op?.Contracts.Count??0)+5)/6);
 static readonly Vector2[] SiteMarkers=[new(50,421),new(401,421),new(113,275),new(448,230),new(150,103),new(500,72)];
 public bool SelectSurveySector(int sector){if(Screen!=MiningScreen.Drill||sector<0||sector>=SurveySectorCount)return false;SurveySector=sector;SelectContract(sector*6);return true;}
 void UpdateDrillMapInput()
 {
  if(Screen!=MiningScreen.Drill||Op is not {} op)return;
  if(op.ExpeditionActive){if(Input.IsKeyPressed(Key.Enter))Primary();return;}
  int selected=0;for(int i=0;i<op.Contracts.Count;i++)if(op.Contracts[i].Id==op.SelectedContractId){selected=i;break;}
  SurveySector=selected/6;
  if(Input.IsKeyPressed(Key.PageDown)){SelectSurveySector((SurveySector+1)%SurveySectorCount);return;}
  if(Input.IsKeyPressed(Key.PageUp)){SelectSurveySector((SurveySector+SurveySectorCount-1)%SurveySectorCount);return;}
  if(Input.IsKeyPressed(Key.Right)){SelectContract((selected+1)%Math.Max(1,op.Contracts.Count));return;}
  if(Input.IsKeyPressed(Key.Left)){SelectContract((selected+op.Contracts.Count-1)%Math.Max(1,op.Contracts.Count));return;}
  if(Input.IsKeyPressed(Key.Enter)){Primary();return;}
  if(!Input.IsMouseButtonPressedForUi(MouseButton.Left))return;
  var size=Input.GameViewSize;float s=Math.Max(.01f,Math.Min(size.X/1280,size.Y/720));var b=CompactBounds(size.X/s,size.Y/s);var m=Input.GameViewMousePosition/s-new Vector2(b.X,b.Y);
  for(int i=0;i<6;i++){int index=SurveySector*6+i;if(index>=op.Contracts.Count)break;var point=SiteMarkers[i]+new Vector2(0,84);if(m.X>=point.X-25&&m.X<=point.X+232&&m.Y>=point.Y-32&&m.Y<=point.Y+44){SelectContract(index);return;}}
  int first=Math.Max(0,Math.Min(SurveySector-2,SurveySectorCount-5));int shown=Math.Min(5,SurveySectorCount);float tabWidth=710f/shown;
  for(int i=0;i<shown;i++)if(m.X>=18+i*tabWidth&&m.X<=18+(i+1)*tabWidth-5&&m.Y>=602&&m.Y<=632){SelectSurveySector(first+i);return;}
 }
 void RenderDrillSiteMap(RenderContext c,float width,float height,float scale)
 {
  if(Op is not {} op)return;var b=CompactBounds(width,height);
  void Image(string name,float x,float y,float w,float h)=>FieldImage(c,name,b.X+x,b.Y+y,w,h,scale);
  void T(string value,float x,float y,int size=17,bool bold=false,UiAnchor anchor=UiAnchor.TopLeft,Vector4? color=null)=>FieldText(c,value,b.X+x,b.Y+y,size,scale,bold,anchor,color);
  void Q(float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new((b.X+x)*scale,(b.Y+y)*scale),new(w*scale,h*scale),color);
  void L(float x,float y,float xx,float yy,Vector4? color=null,float thick=1)=>c.DrawUiLine(new((b.X+x)*scale,(b.Y+y)*scale),new((b.X+xx)*scale,(b.Y+yy)*scale),scale*thick,color??new(.65f,.64f,.59f,1));
  void Ring(float x,float y,float radius,Vector4 color,float thick=2){for(int j=0;j<40;j++){float a=j*MathF.Tau/40,z=(j+1)*MathF.Tau/40;L(x+MathF.Cos(a)*radius,y+MathF.Sin(a)*radius,x+MathF.Cos(z)*radius,y+MathF.Sin(z)*radius,color,thick);}}
  void Pips(int risk,float x,float y,float unit=15){for(int n=0;n<risk;n++)Q(x+n*(unit+4),y,unit,7,risk==5?new(1,.47f,.12f,1):Accent);}
  c.DrawUiQuad(Vector2.Zero,new(c.TargetWidth,c.TargetHeight),new(0,0,0,.22f));Image("survey-frame-border",0,0,1120,640);
  Image("drill-survey-map",2,84,748,466);Q(2,84,748,466,new(0,0,0,.25f));Q(751,84,367,554,new(.065f,.07f,.07f,.95f));
  Q(26,20,7,33,Accent);Q(37,20,7,33,Accent);T(op.ExpeditionActive?"CONTRACT IN PROGRESS":"SELECT DRILL SITE",60,13,32,true);T(op.ExpeditionsEnabled?$"{op.WavesPerExpedition} automatic waves / {op.PreparationSeconds:0}s preparation / machines usable in combat":"Choose your risk. Secure your reward.",60,55,17);
  L(1075,20,1093,38,TextColor,2);L(1093,20,1075,38,TextColor,2);L(2,83,1118,83);L(750,83,750,638);
  for(int i=0;i<6;i++)
  {
   int index=SurveySector*6+i;if(index>=op.Contracts.Count)break;var site=op.Contracts[index];var preview=op.PreviewContract(index);if(preview==null)continue;
   var point=SiteMarkers[i]+new Vector2(0,84);bool selected=site.Id==op.SelectedContractId;var color=selected?Accent:TextColor;
   float px=point.X,py=point.Y;Image(selected?"site-chip-selected":"site-chip-border",px+28,py-32,205,76);Ring(px,py,22,color,selected?3:2);if(selected)Ring(px,py,29,Accent,2);
   Image("drill",px-14,py-14,28,28);L(px,py+23,px,py+37,color,2);Q(px-4,py+36,8,8,color);
   T(site.Name,px+41,py-24,18,true);Pips(site.RiskLevel,px+41,py+2,site.RiskLevel>3?10:14);T(site.Difficulty,px+229,py,12,true,UiAnchor.TopRight);
   T($"{preview.Ore:N0} RAW ORE",px+41,py+21,15,true);
  }
  L(2,551,750,551);var wave=op.ExpeditionActive?(op.Phase==MiningPhase.Drilling?op.ActiveWave:op.NextExpeditionWave):op.SelectedContract;MiningContract? chosen=op.Contracts.FirstOrDefault(site=>site.Id==op.SelectedContractId);
  if(wave!=null&&chosen!=null)
  {
   T(wave.Name.ToUpperInvariant(),772,99,24,true);Pips(chosen.RiskLevel,772,139);T(wave.Difficulty,888,134,16,true);
   T(op.ExpeditionsEnabled?"ORE PER WAVE":"REWARD",772,171,15,true);T($"{wave.Ore:N0} RAW ORE",772,193,30,true,color:Accent);L(772,235,1100,235);
   T("EXPECTED HOSTILES",772,249,16,true);Image("hunter",775,280,32,32);T($"{wave.Hunters} Hunters",824,285,21,true);Image("rig-raider",775,324,32,32);T($"{wave.Raiders} Rig raiders",824,330,21,true);T(wave.Disruptors>0?$"{wave.Disruptors} raiders are Disruptors":$"{wave.Total} total / staggered arrivals",772,370,15);L(772,399,1100,399);
   T("SITE CONDITIONS",772,411,16,true);Image("info",775,441,18,18);T(op.ExpeditionActive?$"Cleared {op.ExpeditionWave}/{op.WavesPerExpedition} / next {op.PreparationRemaining:0}s":chosen.ConditionA,805,441,14);Image("info",775,467,18,18);T(op.ExpeditionsEnabled?"Cyan disruptors arrive from wave 3":chosen.ConditionB,805,467,14);
   T($"Cargo {op.CargoUsed:N0} / {op.CargoCapacity:N0}",772,502,16);Q(772,526,328,5,new(.25f,.27f,.27f,1));Q(772,526,328*Math.Clamp(op.CargoUsed/(float)Math.Max(1,op.CargoCapacity),0,1),5,Accent);
  }
  var start=W(PrimaryButtonObjectId);bool canStart=start?.Interactable==true;Q(772,544,328,48,canStart?Accent:new(.20f,.21f,.19f,1));T(op.ExpeditionActive?$"START EARLY +{op.PendingEarlyBonus}":op.ExpeditionsEnabled?"ACCEPT CONTRACT":"START OPERATION",936,557,21,true,UiAnchor.TopCenter,canStart?new(.05f,.05f,.04f,1):MenuMuted);
  string warning=Feedback.Length>0?Feedback:wave==null?"Choose a valid site":(long)op.CargoUsed+wave.Ore>op.CargoCapacity?"Not enough cargo space for this reward":!op.ExpeditionsEnabled&&op.RefiningAmount>0&&!op.QueueRefineryBatches?"Finish refining before launching":op.ExpeditionActive?"Countdown continues while this panel is open":"Esc Back / Enter Accept";T(warning.Length>45?warning[..42]+"...":warning,936,606,13,false,UiAnchor.TopCenter,canStart?TextColor:Accent);
  T($"DRILL OUTPUT +{op.DrillLevel*op.OrePerDrillLevel} ORE",22,570,13);var upgrade=W(Row0ButtonObjectId);bool buy=upgrade?.Interactable==true;Q(210,562,160,32,buy?new(.23f,.23f,.17f,1):new(.1f,.11f,.1f,1));T(op.IsUpgradeMaxed(MiningUpgrade.Drill)?"MAX LEVEL":$"UPGRADE {op.Cost(MiningUpgrade.Drill):N0} cr",290,569,13,true,UiAnchor.TopCenter,buy?Accent:MenuMuted);T($"{op.Contracts.Count} SITES / SELECT A SURVEY SECTOR",728,570,12,false,UiAnchor.TopRight);
  int first=Math.Max(0,Math.Min(SurveySector-2,SurveySectorCount-5));int shown=Math.Min(5,SurveySectorCount);float tabWidth=710f/shown;
  for(int i=0;i<shown;i++){int sector=first+i;bool active=sector==SurveySector;Q(18+i*tabWidth,602,tabWidth-5,30,active?Accent:new(.13f,.14f,.13f,1));string name=sector<MiningSiteCatalog.Sectors.Length?MiningSiteCatalog.Sectors[sector]:$"Sector {sector+1}";T(name.ToUpperInvariant(),18+i*tabWidth+(tabWidth-5)/2,610,12,true,UiAnchor.TopCenter,active?new(.06f,.06f,.05f,1):TextColor);}
 }
}
