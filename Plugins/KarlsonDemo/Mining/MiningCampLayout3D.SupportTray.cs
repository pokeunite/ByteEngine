using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningCampLayout3D
{
 readonly List<(Guid Id,Vector4 Rect)> campTrayHits=[];
 static readonly string[] campTrayKinds=["support-mine","support-barricade","support-sentry","support-mortar","support-refinery","support-station"];
 bool PickCampTray()
 {
  if(!Input.IsMouseButtonPressedForUi(MouseButton.Left))return false;float scale=Math.Max(.01f,Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720));var p=Input.GameViewMousePosition/scale;
  foreach(var tile in campTrayHits)if(p.X>=tile.Rect.X&&p.X<=tile.Rect.X+tile.Rect.Z&&p.Y>=tile.Rect.Y&&p.Y<=tile.Rect.Y+tile.Rect.W)return BeginMove(tile.Id);return false;
 }
 void DrawCampTray(RenderContext c,float s,float w,float h)
 {
  campTrayHits.Clear();var objects=Objects.ToArray();var kinds=campTrayKinds.Where(k=>objects.Any(o=>CampIcon(o)==k)).ToArray();float tile=90,gap=6,left=(w-kinds.Length*(tile+gap)+gap)/2;
  for(int i=0;i<kinds.Length;i++){
   var group=objects.Where(o=>CampIcon(o)==kinds[i]).ToArray();bool selected=group.Any(o=>o.Id==SelectedObjectId);float x=left+i*(tile+gap),y=h-138;SupportUiArt.Panel(c,s,x,y,tile,80,SupportUiArt.Dark,selected?SupportUiArt.Gold:SupportUiArt.Edge);SupportUiArt.Image(c,s,kinds[i],x+28,y+5,34,32);
   string name=kinds[i] switch{"support-mine"=>"Mine","support-barricade"=>"Barricade","support-sentry"=>"Sentry","support-mortar"=>"Mortar","support-refinery"=>"Refinery",_=>"Station"};SupportUiArt.T(c,s,name,x+tile/2,y+42,13,true,anchor:UiAnchor.TopCenter);SupportUiArt.T(c,s,"x"+group.Length,x+tile/2,y+60,13,true,selected?SupportUiArt.Gold:SupportUiArt.White,UiAnchor.TopCenter);campTrayHits.Add((group[0].Id,new(x,y,tile,80)));
  }
  SupportUiArt.Panel(c,s,w/2-470,h-47,940,32);SupportUiArt.T(c,s,"[LMB] Select / Place    [Q/E] Rotate    [RMB] Cancel    [Tab] Done    WASD / Wheel: Pan / Zoom",w/2,h-39,14,true,anchor:UiAnchor.TopCenter);
 }
}
