using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 void DrawSupportBuildTray(RenderContext c,float width,float height,float scale,MiningDefensePlanner3D planner)
 {
  var kinds=Enum.GetValues<MiningDefenseKind>().Where(k=>planner.Available(k)>0).ToArray();float size=88,gap=6,total=kinds.Length*(size+gap)-gap,left=(width-total)/2;
  SupportUiArt.Panel(c,scale,20,20,320,54);SupportUiArt.T(c,scale,"BUILD SUPPORT",34,28,20,true);SupportUiArt.T(c,scale,"Choose a kit, then position the preview",34,52,13);
  for(int i=0;i<kinds.Length;i++){var kind=kinds[i];float x=left+i*(size+gap),y=height-138;bool chosen=kind==planner.SelectedKind;SupportUiArt.Panel(c,scale,x,y,size,82,SupportUiArt.Dark,chosen?SupportUiArt.Gold:SupportUiArt.Edge);SupportUiArt.Image(c,scale,SupportUiArt.KindIcon(kind),x+27,y+6,34,32);SupportUiArt.T(c,scale,SupportUiArt.KindLabel(kind),x+size/2,y+43,13,true,anchor:UiAnchor.TopCenter);SupportUiArt.T(c,scale,planner.ReplacementCost(kind)>0?$"{planner.ReplacementCost(kind)} refined":"x"+planner.Available(kind),x+size/2,y+62,13,true,chosen?SupportUiArt.Gold:SupportUiArt.White,UiAnchor.TopCenter);}
  if(planner.Status.Length>0)SupportUiArt.T(c,scale,planner.Status.Length>90?planner.Status[..87]+"...":planner.Status,width/2,height-165,14,false,planner.PlacementValid?SupportUiArt.White:SupportUiArt.Gold,UiAnchor.TopCenter);
  SupportUiArt.Panel(c,scale,width/2-370,height-47,740,32);SupportUiArt.T(c,scale,"[LMB] Place     [Q/E] Rotate     [RMB] Cancel     [B] Next kit",width/2,height-39,15,true,anchor:UiAnchor.TopCenter);
 }
}
