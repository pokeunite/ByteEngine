using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public float ResearchZoom{get;set;}=.65f;
 public Vector2 ResearchPan{get;set;}
 Vector2 lastMapPointer;bool wasPanning;
 readonly Dictionary<string,Vector2> mapPositions=new();
 readonly HashSet<string> mapShown=new();
 float mapWidth=1280,mapHeight=720;
 // Reveal the next decision, rather than drawing every distant upgrade at once.
 void PrepareResearchMap(MiningTrade3D trade)
 {
  mapPositions.Clear();mapShown.Clear();var nodes=trade.Research;var root=nodes.FirstOrDefault(n=>n.Prerequisites.Length==0);if(root==null)return;
  var children=nodes.Where(n=>n.Prerequisites.Length>0).GroupBy(n=>n.Prerequisites[0]).ToDictionary(g=>g.Key,g=>g.ToArray());
  bool Expanded(CompanyUpgrade n)=>n.Id==root.Id||trade.HasResearch(n.Id);
  float Span(CompanyUpgrade n,int depth=0)=>depth>20||!Expanded(n)||!children.TryGetValue(n.Id,out var ch)?1:Math.Max(1,ch.Sum(c=>Span(c,depth+1)));
  void Visit(CompanyUpgrade n,float x,float y,int side,int depth)
  {
   if(depth>20||!mapShown.Add(n.Id))return;mapPositions[n.Id]=new(x,y);
   if(!Expanded(n)||!children.TryGetValue(n.Id,out var next))return;
   float cursor=y-next.Sum(c=>Span(c))*50;
   foreach(var child in next){float span=Span(child)*100;Visit(child,x+side*145,cursor+span/2,side,depth+1);cursor+=span;}
  }
  mapPositions[root.Id]=Vector2.Zero;mapShown.Add(root.Id);
  if(children.TryGetValue(root.Id,out var branches))foreach(int side in new[]{-1,1})
  {
   var group=branches.Where((n,i)=>(i%2==0?-1:1)==side).ToArray();float cursor=-group.Sum(n=>Span(n))*50;
   foreach(var branch in group){float span=Span(branch)*100;Visit(branch,side*175,cursor+span/2,side,1);cursor+=span;}
  }
 }
 Vector2 NodePoint(CompanyUpgrade n)=>new Vector2(mapWidth*.5f,300)+(mapPositions.GetValueOrDefault(n.Id))*ResearchZoom+ResearchPan;
 bool NodeVisible(Vector2 p)=>p.X>52&&p.X<mapWidth-52&&p.Y>158&&p.Y<442;
 float NodeRadius=>30*Math.Clamp(ResearchZoom,.7f,1.15f);
 void UpdateMapInput()
 {
  if(Screen!=MiningScreen.Research)return;
  var size=Input.GameViewSize;float s=Math.Max(.01f,Math.Min(size.X/1280,size.Y/720));var bounds=CompactBounds(size.X/s,size.Y/s);mapWidth=bounds.Width;mapHeight=bounds.Height;
  if(Trade is {} trade)PrepareResearchMap(trade);
  var mouse=Input.GameViewMousePosition/s-new Vector2(bounds.X,bounds.Y);bool over=mouse.X>28&&mouse.X<mapWidth-28&&mouse.Y>135&&mouse.Y<460;
  bool pan=over&&Input.IsMouseButtonDown(MouseButton.Right);
  if(pan&&wasPanning)ResearchPan+=mouse-lastMapPointer;
  if(over&&Math.Abs(Input.Snapshot.MouseWheel)>.001f)
  {float old=ResearchZoom;ResearchZoom=Math.Clamp(old+Input.Snapshot.MouseWheel*.08f,.45f,1.5f);var center=new Vector2(mapWidth*.5f,300);ResearchPan=mouse-center-(mouse-center-ResearchPan)*(ResearchZoom/old);}
  ResearchPan=Vector2.Clamp(ResearchPan,new(-5000,-5000),new(5000,5000));lastMapPointer=mouse;wasPanning=pan;
 }
 string ResearchIcon(CompanyUpgrade n)=>n.Id=="company"?"company":n.Id.StartsWith("arms")||n.Branch=="Pistol"?"pistol":n.Id.StartsWith("industry")||n.Branch=="Base"?"drill":n.Branch=="Health"||n.Id.StartsWith("survival")?"health":n.Branch=="Market"?"market":n.Branch=="Ammo"?"ammo":n.Branch=="Mines"?"mine":n.Branch=="Artillery"?"mortar":n.Branch=="Defence"?"turret":n.Stat.Contains("Speed")?"speed":n.Branch=="Supply"?"crate":"gear";
 void DrawResearchIcon(RenderContext c,CompanyUpgrade n,Vector2 p,float size,float scale,Vector4 color)
 {
  if(!AnimationRuntimeAssets.TryGet(out var assets)||assets==null)return;
  string icon=ResearchIcon(n);var asset=new AssetReference("Assets/UI/Mining/Artwork/"+(icon=="pistol"?"company-pistol":"research-"+icon)+".png");
  var dimensions=icon=="pistol"?new Vector2(size,size*.5f):new Vector2(size,size);
  c.DrawUiImage(assets.LoadTexture(asset),(p-dimensions*.5f)*scale,dimensions*scale,color);
 }
 void DrawResearchMap(RenderContext c,float ox,float oy,float scale,MiningTrade3D trade)
 {
  void Q(float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new((ox+x)*scale,(oy+y)*scale),new(w*scale,h*scale),color);
  void L(Vector2 a,Vector2 b,Vector4 color,float thickness=1)=>c.DrawUiLine((a+new Vector2(ox,oy))*scale,(b+new Vector2(ox,oy))*scale,thickness*scale,color);
  var muted=new Vector4(.28f,.31f,.34f,.85f);
  foreach(var n in trade.Research.Where(n=>mapShown.Contains(n.Id)&&n.Prerequisites.Length>0))
  {
   var parent=trade.Research.FirstOrDefault(p=>p.Id==n.Prerequisites[0]);if(parent==null)continue;
   var a=NodePoint(parent);var z=NodePoint(n);if(!NodeVisible(a)||!NodeVisible(z))continue;
   float side=z.X>=a.X?1:-1;var color=trade.HasResearch(parent.Id)?Accent:muted;
   float bend=a.X+side*(parent.Prerequisites.Length==0?60:55)*ResearchZoom;
   L(a+new Vector2(side*NodeRadius,0),new(bend,a.Y),color,1.2f);L(new(bend,a.Y),new(bend,z.Y),color,1.2f);L(new(bend,z.Y),z-new Vector2(side*NodeRadius,0),color,1.2f);
  }
  foreach(var n in trade.Research.Where(n=>mapShown.Contains(n.Id)))
  {
   var p=NodePoint(n);if(!NodeVisible(p))continue;int rank=trade.ResearchRank(n.Id);bool available=trade.ResearchUnlocked(n.Id),selected=n.Id==SelectedResearchId;float r=NodeRadius;
   var fill=rank>=n.MaxRank?Accent:available?new(.09f,.105f,.12f,1):new(.075f,.085f,.10f,1);
   var edge=selected?Accent:rank>=n.MaxRank?Accent:available?new(.53f,.48f,.26f,1):new(.25f,.28f,.31f,1);
   // Clipped-corner octagons with a fine border, no heavy square frame.
   for(int y=0;y<(int)(r*2);y++){float inset=Math.Max(0,8-Math.Min(y,r*2-1-y));Q(p.X-r+inset,p.Y-r+y,r*2-inset*2,1,edge);if(y>1&&y<r*2-2)Q(p.X-r+inset+1.5f,p.Y-r+y,r*2-inset*2-3,1,fill);}
   DrawResearchIcon(c,n,p+new Vector2(ox,oy-2),r*1.32f,scale,rank>=n.MaxRank?new(.05f,.06f,.07f,1):available?TextColor:new(.34f,.37f,.40f,1));
   int pips=Math.Min(n.MaxRank,5);float start=p.X-(pips*6-2)/2f;for(int i=0;i<pips;i++)Q(start+i*6,p.Y+r-8,4,3,i<rank?Accent:muted);
   if(!available&&rank==0){Q(p.X-3,p.Y-r+5,6,5,new(.7f,.72f,.65f,1));L(new(p.X-2,p.Y-r+5),new(p.X-2,p.Y-r+2),TextColor);L(new(p.X-2,p.Y-r+2),new(p.X+2,p.Y-r+2),TextColor);L(new(p.X+2,p.Y-r+2),new(p.X+2,p.Y-r+5),TextColor);}
   if(rank>=n.MaxRank){L(new(p.X+r-12,p.Y+r-14),new(p.X+r-8,p.Y+r-10),TextColor,2);L(new(p.X+r-8,p.Y+r-10),new(p.X+r-3,p.Y+r-17),TextColor,2);}
  }
 }
}
