using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>Projects any world-space landmark into the native canvas. No game rules or labels are built in.</summary>
public sealed class UiWorldAnchor : Component
{
    public string TargetName { get; set; } = "";
    public Vector3 WorldOffset { get; set; }
    public Vector2 PixelOffset { get; set; }
    public override int? RenderOrder => -1000;
    protected override void OnLateUpdate()=>Project(Input.GameViewSize,GameObject.Scene?.ActiveCamera);
    protected override void OnRender(RenderContext context)=>Project(new(context.TargetWidth,context.TargetHeight),context.Camera3D??GameObject.Scene?.ActiveCamera);
    void Project(Vector2 viewport,Camera3D? camera)
    {
        var scene=GameObject.Scene;
        if(scene==null||camera==null||viewport.X<=0||viewport.Y<=0) return;
        Vector3 point=WorldOffset;
        if(TargetName.Length>0 && scene.FindGameObject(TargetName) is {} target) point+=target.Transform.WorldPosition;
        var clip=Vector4.Transform(new Vector4(point,1),camera.GetViewMatrix()*camera.GetProjectionMatrix(viewport.X/viewport.Y));
        if(clip.W<=0||!UiLayout.TryGetCanvas(GameObject,out var canvas)||canvas==null)return;
        var rect=UiLayout.ResolveCanvas(canvas,viewport);
        var offset=(new Vector2((clip.X/clip.W+1)*viewport.X/2,(1-clip.Y/clip.W)*viewport.Y/2)-rect.Origin)/rect.Scale+PixelOffset;
        if(GameObject.GetComponent<UiWidget>() is {} widget) widget.Offset=offset;
        else if(GameObject.GetComponent<UiText>() is {} text) text.Offset=offset;
    }
}
