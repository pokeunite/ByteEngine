using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics;
/// <summary>Clipped vertical viewport. Supports wheel scrolling and reveals keyboard/controller focus.</summary>
public sealed class UiScrollContainer : Component
{
    public float ScrollY { get; set; }
    public float ContentHeight { get; set; }
    public float WheelSpeed { get; set; } = 48;
    public float MaximumScroll(Vector2 viewport)
    {
        var widget=GameObject.GetComponent<UiWidget>();if(widget==null)return 0;
        var rect=UiLayout.Resolve(GameObject,widget.Anchor,widget.Offset,widget.Size,viewport);
        float height=Math.Max(0,ContentHeight);
        if(height==0)foreach(var child in GameObject.Children)
            if(child.ActiveInHierarchy && child.GetComponent<UiWidget>() is {Visible:true} item)
            {
                var childRect=UiLayout.Resolve(child,item.Anchor,item.Offset,item.Size,viewport);
                height=Math.Max(height,(childRect.Position.Y+childRect.Size.Y-rect.Position.Y)/rect.Scale+Math.Max(0,ScrollY));
            }
        return Math.Max(0,height-rect.Size.Y/rect.Scale);
    }
    protected override void OnUpdate()
    {
        if(!UiLayout.IsVisible(GameObject)||GameObject.GetComponent<UiWidget>() is not { } widget)return;
        var viewport=Input.GameViewSize;
        if(Input.IsGameViewHovered && widget.Contains(Input.GameViewMousePosition,viewport))
        {
            bool nested=GameObject.Scene?.GameObjects.Any(o=>o.IsDescendantOf(GameObject)&&o.GetComponent<UiScrollContainer>() is {Enabled:true} && o.GetComponent<UiWidget>() is {} w && w.Contains(Input.GameViewMousePosition,viewport))==true;
            if(!nested)ScrollY-=Input.Snapshot.MouseWheel*Math.Max(0,WheelSpeed);
        }
        ScrollY=Math.Clamp(float.IsFinite(ScrollY)?ScrollY:0,0,MaximumScroll(viewport));
    }
    public void Reveal(UiWidget target,Vector2 viewport)
    {
        if(GameObject.GetComponent<UiWidget>() is not {} widget)return;
        var outer=UiLayout.Resolve(GameObject,widget.Anchor,widget.Offset,widget.Size,viewport);
        var inner=UiLayout.Resolve(target.GameObject,target.Anchor,target.Offset,target.Size,viewport);
        if(inner.Position.Y<outer.Position.Y)ScrollY-=(outer.Position.Y-inner.Position.Y)/outer.Scale;
        else if(inner.Position.Y+inner.Size.Y>outer.Position.Y+outer.Size.Y)ScrollY+=(inner.Position.Y+inner.Size.Y-outer.Position.Y-outer.Size.Y)/outer.Scale;
        ScrollY=Math.Clamp(ScrollY,0,MaximumScroll(viewport));
    }
}
