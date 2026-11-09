using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningResearchStation3D
{
 public AssetReference ShopHeadingFont{get;set;}=new("Assets/UI/Mining/Fonts/BarlowCondensed-Bold.ttf");
 public AssetReference ShopBodyFont{get;set;}=new("Assets/UI/Mining/Fonts/BarlowCondensed-Medium.ttf");
 int hovered=-1;bool closeHovered;
 static readonly Vector4 ShopYellow=new(1,.79f,.12f,1),ShopWhite=new(.94f,.95f,.91f,1),ShopMuted=new(.64f,.69f,.71f,1),ShopEdge=new(.29f,.33f,.35f,1);
 static readonly int[] ShopOrder=[0,1,4,3,2];
 static Vector2 ShopOrigin(float width,float height)=>new(width-588,(height-600)/2);
 static Vector4 OrderButton(int item)=>new(380,112+Array.IndexOf(ShopOrder,item)*82,152,40);
 static bool Inside(Vector2 p,Vector4 r)=>p.X>=r.X&&p.Y>=r.Y&&p.X<=r.X+r.Z&&p.Y<=r.Y+r.W;
 bool DroneActive=>ActiveDroneCount>=MaximumDrones;
 bool OrderAvailable(int item)=>Op is {} op&&item switch{0=>ActiveDroneCount<MaximumDrones&&op.Credits>=DroneCost,1=>(MaximumBarricades<=0||BarricadesOwned<MaximumBarricades)&&op.RefinedOre>=BarricadeOreCost,4=>op.RefinedOre>=MineOreCost,3=>op.Credits>=ExtraRefineryCost,_=>op.Credits>=op.CargoUpgradeCost&&op.CargoCapacity<1000000};
 void SubmitOrder(int item){selected=item;if(!OrderAvailable(item)){Status=item==0&&DroneActive?"All drone slots are in use.":item is 1 or 4?"Not enough refined ore.":"Not enough camp credits.";MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Error);return;}Buy(item);}
 void TickShopInput()
 {
  int position=Array.IndexOf(ShopOrder,selected);if(Input.IsKeyPressed(Key.Up))selected=ShopOrder[Math.Max(0,position-1)];if(Input.IsKeyPressed(Key.Down))selected=ShopOrder[Math.Min(ShopOrder.Length-1,position+1)];if(Input.IsKeyPressed(Key.Enter))SubmitOrder(selected);
  float scale=Math.Max(.01f,Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720));var p=Input.GameViewMousePosition/scale-ShopOrigin(Input.GameViewSize.X/scale,Input.GameViewSize.Y/scale);
  int previous=hovered;hovered=-1;foreach(int item in ShopOrder)if(Inside(p,OrderButton(item)))hovered=item;
  closeHovered=Inside(p,new(504,16,36,36))||Inside(p,new(432,552,104,36));if(previous!=hovered&&hovered>=0)MiningAudio3D.Find(GameObject)?.PlayHoverCue(.6f);
  if(!Input.IsMouseButtonPressedForUi(MouseButton.Left))return;if(closeHovered){Close();return;}
  foreach(int item in ShopOrder)if(Inside(p,OrderButton(item))){SubmitOrder(item);return;}
 }
 void DrawShop(RenderContext c,MiningOperation3D op,float s,float width,float height)
 {
  var origin=ShopOrigin(width,height);float ox=origin.X,oy=origin.Y;
  void Q(float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new((ox+x)*s,(oy+y)*s),new(w*s,h*s),color);
  void L(float x,float y,float xx,float yy,Vector4 color)=>c.DrawUiLine(new((ox+x)*s,(oy+y)*s),new((ox+xx)*s,(oy+yy)*s),s,color);
  void T(string text,float x,float y,int size=20,Vector4? color=null,bool bold=false,UiAnchor anchor=UiAnchor.TopLeft)=>c.DrawUiText(text,bold?ShopHeadingFont:Ui?.BodyFont??ShopBodyFont,(int)(size*s),new((ox+x)*s,(oy+y)*s),color??ShopWhite,anchor);
  c.DrawUiQuad(Vector2.Zero,new(c.TargetWidth,c.TargetHeight),new(0,0,0,.12f));Q(0,0,560,600,new(.025f,.035f,.045f,.985f));Q(0,0,4,54,ShopYellow);
  T("FIELD R&D",26,23,28);T($"{op.Credits:N0} cr",356,22,21,ShopYellow);T($"{op.RefinedOre:N0} REFINED",356,50,14,ShopMuted);T("X",516,20,23,closeHovered?ShopYellow:ShopWhite);L(24,78,536,78,ShopEdge);
  for(int row=0;row<ShopOrder.Length;row++){
   int item=ShopOrder[row];float y=92+row*82;bool enabled=OrderAvailable(item);Q(24,y,512,72,selected==item?new(.10f,.105f,.075f,1):new(.055f,.07f,.085f,1));
   string title=item switch{0=>"Security drone",1=>"Field barricade",4=>"Proximity mine",3=>"Additional refinery",_=>"Cargo expansion"};
   string detail=item switch{0=>$"{ActiveDroneCount} / {MaximumDrones} active / {DroneBatterySeconds:0}s combat",1=>$"{BarricadeStock} kits ready",4=>$"{MineStock} kits / 10 refined ore to replace",3=>$"50 raw per load / {RefineryStock} ready",_=>$"{op.CargoCapacity:N0} > {Math.Min(1000000,op.CargoCapacity+op.CargoCapacityPerUpgrade):N0} CAPACITY"};
   string icon=item switch{1=>"shield",4=>"mine",2=>"crate",_=>"gear"};
   float ix=42,iy=y+20;
   if(item==0){Q(ix+11,iy+10,14,12,ShopWhite);foreach(var offset in new[]{new Vector2(3,3),new Vector2(33,3),new Vector2(3,29),new Vector2(33,29)}){L(ix+18,iy+16,ix+offset.X,iy+offset.Y,ShopWhite);for(int n=0;n<12;n++){float a=n*MathF.Tau/12,nextAngle=(n+1)*MathF.Tau/12;L(ix+offset.X+6*MathF.Cos(a),iy+offset.Y+4*MathF.Sin(a),ix+offset.X+6*MathF.Cos(nextAngle),iy+offset.Y+4*MathF.Sin(nextAngle),ShopWhite);}}}
   else if(item==3){Q(ix+4,iy+11,28,21,ShopWhite);Q(ix+7,iy+1,6,11,ShopWhite);Q(ix+23,iy+3,6,9,ShopWhite);Q(ix,iy+32,37,4,ShopWhite);Q(ix+11,iy+18,5,7,ShopEdge);Q(ix+22,iy+18,5,7,ShopEdge);}
   else if(AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null)c.DrawUiImage(assets.LoadTexture(new AssetReference("Assets/UI/Mining/Artwork/research-"+icon+".png")),new((ox+ix)*s,(oy+iy)*s),new(34*s),ShopWhite);
   T(title,96,y+11,20);T(detail,96,y+48,13,ShopMuted);
   var b=OrderButton(item);Q(b.X,b.Y,b.Z,b.W,enabled&&hovered==item?new(.29f,.24f,.08f,1):new(.04f,.055f,.065f,1));L(b.X,b.Y,b.X+b.Z,b.Y,enabled?ShopYellow:ShopEdge);
   string cost=item switch{0=>DroneActive?"SLOTS FULL":$"ORDER {DroneCost:N0} cr",1=>$"{BarricadeOreCost} REFINED ORE",4=>$"{MineOreCost} REFINED ORE",3=>$"ORDER {ExtraRefineryCost:N0} cr",_=>$"UPGRADE {op.CargoUpgradeCost:N0} cr"};
   T(cost,b.X+b.Z/2,b.Y+11,15,enabled?ShopYellow:ShopMuted,true,UiAnchor.TopCenter);
  }
  L(24,514,536,514,ShopEdge);string feedback=Status.StartsWith("Choose support.")?"Order support here. Place kits with B or their shortcuts.":Status;T(feedback.Length>66?feedback[..63]+"...":feedback,24,530,13,ShopMuted);T("Q / E rotates placement previews",24,568,13,ShopMuted);T("Esc  Close",532,568,16,closeHovered?ShopYellow:ShopWhite,false,UiAnchor.TopRight);
 }
}
