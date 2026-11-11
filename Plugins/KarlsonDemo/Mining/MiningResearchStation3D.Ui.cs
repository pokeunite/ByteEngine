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
 static Vector2 ShopOrigin(float width,float height)=>new(width-488,(height-480)/2);
 static Vector4 OrderButton(int item)=>new(342,77+Array.IndexOf(ShopOrder,item)*67,116,34);
 static bool Inside(Vector2 p,Vector4 r)=>p.X>=r.X&&p.Y>=r.Y&&p.X<=r.X+r.Z&&p.Y<=r.Y+r.W;
 bool DroneActive=>ActiveDroneCount>=MaximumDrones;
 bool OrderAvailable(int item)=>Op is {} op&&item switch{0=>ActiveDroneCount<MaximumDrones&&op.Credits>=DroneCost,1=>(MaximumBarricades<=0||BarricadesOwned<MaximumBarricades)&&op.RefinedOre>=BarricadeOreCost,4=>op.RefinedOre>=MineOreCost,3=>op.Credits>=ExtraRefineryCost,_=>op.Credits>=op.CargoUpgradeCost&&op.CargoCapacity<1000000};
 void SubmitOrder(int item){selected=item;if(!OrderAvailable(item)){Status=item==0&&DroneActive?"All drone slots are in use.":item is 1 or 4?"Not enough refined ore.":"Not enough camp credits.";MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Error);return;}Buy(item);}
 void TickShopInput()
 {
  int position=Array.IndexOf(ShopOrder,selected);if(Input.IsKeyPressed(Key.Up))selected=ShopOrder[Math.Max(0,position-1)];if(Input.IsKeyPressed(Key.Down))selected=ShopOrder[Math.Min(ShopOrder.Length-1,position+1)];if(Input.IsKeyPressed(Key.Enter))SubmitOrder(selected);
  float scale=Math.Max(.01f,Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720));var p=Input.GameViewMousePosition/scale-ShopOrigin(Input.GameViewSize.X/scale,Input.GameViewSize.Y/scale);
  int previous=hovered;hovered=-1;foreach(int item in ShopOrder)if(Inside(p,OrderButton(item)))hovered=item;
  closeHovered=Inside(p,new(432,12,32,32));if(previous!=hovered&&hovered>=0)MiningAudio3D.Find(GameObject)?.PlayHoverCue(.6f);
  if(!Input.IsMouseButtonPressedForUi(MouseButton.Left))return;if(closeHovered){Close();return;}if(Inside(p,new(80,230,252,30))&&Op?.DefencePlanner is {DepletedMineCount:>0} planner){int count=planner.RechargeAllMines();Status=count>0?$"Recharged {count} mines in place.":$"Need {planner.MineReplacementOreCost} refined ore per depleted mine.";MiningAudio3D.Find(GameObject)?.PlayCue(count>0?MiningSoundCue.Confirm:MiningSoundCue.Error);return;}
  foreach(int item in ShopOrder)if(Inside(p,OrderButton(item))){SubmitOrder(item);return;}
 }
 void DrawShop(RenderContext c,MiningOperation3D op,float s,float width,float height)
 {
  var o=ShopOrigin(width,height);float x=o.X,y=o.Y;
  void T(string v,float xx,float yy,int size=17,bool bold=false,Vector4? color=null,UiAnchor anchor=UiAnchor.TopLeft)=>SupportUiArt.T(c,s,v,x+xx,y+yy,size,bold,color,anchor);
  SupportUiArt.Panel(c,s,x,y,468,480,accent:true);T("FIELD R&D",20,15,25,true);T($"{op.Credits:N0} cr",418,21,13,true,SupportUiArt.Gold,UiAnchor.TopRight);SupportUiArt.L(c,s,x+12,y+54,x+456,y+54);T("X",443,17,19);
  for(int row=0;row<5;row++){
   int item=ShopOrder[row];float yy=65+row*67;bool enabled=OrderAvailable(item);
   string title=item switch{0=>"Security drone",1=>"Field barricade",4=>"Proximity mine",3=>"Additional refinery",_=>"Cargo expansion"};
   string detail=item switch{0=>$"{ActiveDroneCount} / {MaximumDrones} active / {DroneBatterySeconds:0}s",1=>$"{BarricadeStock} ready",4=>$"{MineStock} ready / recharge 10 refined",3=>$"{RefineryStock} ready / 50 raw per load",_=>$"{op.CargoCapacity:N0} > {Math.Min(1000000,op.CargoCapacity+op.CargoCapacityPerUpgrade):N0}"};
   string icon=item switch{0=>"support-drone",1=>"support-barricade",4=>"support-mine",3=>"support-refinery",_=>"cargo"};SupportUiArt.Image(c,s,icon,x+20,y+yy+9,46,42);T(title,82,yy+7,17,true);if(item==4&&op.DefencePlanner is {DepletedMineCount:>0} mines)T($"Recharge {mines.DepletedMineCount} / {mines.DepletedMineCount*mines.MineReplacementOreCost} refined [click]",82,yy+34,13,color:SupportUiArt.Gold);else T(detail,82,yy+34,13);var b=OrderButton(item);
   SupportUiArt.Panel(c,s,x+b.X,y+b.Y,b.Z,b.W,enabled?hovered==item?new(1,.87f,.38f,1):SupportUiArt.Gold:new(.23f,.24f,.23f,1),enabled?SupportUiArt.Gold:SupportUiArt.Muted);
   string cost=item switch{0=>DroneActive?"Slots full":$"{DroneCost:N0} cr",1=>$"{BarricadeOreCost} refined",4=>$"{MineOreCost} refined",3=>$"{ExtraRefineryCost:N0} cr",_=>$"{op.CargoUpgradeCost:N0} cr"};T(cost,b.X+b.Z/2,b.Y+9,15,true,enabled?new(.05f,.055f,.05f,1):SupportUiArt.Muted,UiAnchor.TopCenter);
   SupportUiArt.L(c,s,x+12,y+yy+64,x+456,y+yy+64,new(.38f,.39f,.36f,1));
  }
  SupportUiArt.Image(c,s,"info",x+20,y+421,20,20);T("Purchase kits, then place with [B]",52,422,16);T(Status.StartsWith("Choose support.")?$"{op.Credits:N0} cr / {op.RefinedOre:N0} refined":Status.Length>53?Status[..50]+"...":Status,20,453,13,color:SupportUiArt.Gold);T("Esc Close",448,453,13,anchor:UiAnchor.TopRight);
 }
}
