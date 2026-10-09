using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics;
public enum UiContainerKind { Row, Column, Grid }
/// <summary>Responsive child placement computed at render/hit-test time without rewriting authored offsets.</summary>
public sealed class UiContainer : Component
{
 public UiContainerKind Kind {get;set;}=UiContainerKind.Column;
 public Vector4 Padding {get;set;}=new(12);
 public Vector2 Spacing {get;set;}=new(8);
 public Vector2 CellSize {get;set;}=new(120,80);
 public bool EqualWidth {get;set;}
 internal bool TryPlace(GameObject child,Vector2 parentSize,out Vector2 offset,out Vector2 size){
  offset=Vector2.Zero;size=child.GetComponent<UiWidget>()?.LayoutSize()??Vector2.Zero;int index=0;Vector2 cursor=new(Math.Max(0,Padding.X),Math.Max(0,Padding.Y));
  float usable=Math.Max(0,parentSize.X-Math.Max(0,Padding.X)-Math.Max(0,Padding.Z));
  int columns=Math.Max(1,(int)MathF.Floor((usable+Math.Max(0,Spacing.X))/Math.Max(1,CellSize.X+Math.Max(0,Spacing.X))));
  foreach(var item in GameObject.Children){
   if(!item.ActiveInHierarchy||item.GetComponent<UiWidget>() is not {Visible:true} widget)continue;
   if(ReferenceEquals(item,child)){
    offset=Kind==UiContainerKind.Grid?cursor+new Vector2(index%columns*(Math.Max(1,CellSize.X)+Math.Max(0,Spacing.X)),index/columns*(Math.Max(1,CellSize.Y)+Math.Max(0,Spacing.Y))):cursor;
    if(Kind==UiContainerKind.Grid)size=Vector2.Max(Vector2.One,CellSize);else if(EqualWidth&&Kind==UiContainerKind.Column)size=new(usable,size.Y);
    return true;
   }
   if(Kind==UiContainerKind.Column)cursor.Y+=Math.Max(0,widget.LayoutSize().Y)+Math.Max(0,Spacing.Y);
   if(Kind==UiContainerKind.Row)cursor.X+=Math.Max(0,widget.LayoutSize().X)+Math.Max(0,Spacing.X);
   index++;
  }
  return false;
 }
}
