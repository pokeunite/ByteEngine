using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Graphics;
/// <summary>Event-driven fullscreen tint and soft edges, behind the HUD.</summary>
public sealed class ScreenOverlay : Component
{
    public Vector4 Tint {get;set;}=new(.1f,.7f,1,.06f);
    public Vector4 EdgeColor {get;set;}=new(.05f,.8f,1,.35f);
    public float Strength {get;set;}
    public float EdgeWidth {get;set;}=.08f;
    public int OrderInLayer {get;set;}=-100;
    public override int? RenderOrder=>OrderInLayer;
    protected override void OnRender(RenderContext context)
    {
        float strength=float.IsFinite(Strength)?Math.Clamp(Strength,0,1):0;if(strength<=0)return;
        var size=new Vector2(context.TargetWidth,context.TargetHeight);var tint=Tint;tint.W*=strength;
        context.QueueUiQuad(Vector2.Zero,size,tint);
        float width=Math.Min(size.X,size.Y)*Math.Clamp(float.IsFinite(EdgeWidth)?EdgeWidth:.08f,0,.3f);
        const int strips=12;
        for(int n=0;n<strips;n++){
            float distance=width*n/strips,band=width/strips;var color=EdgeColor;color.W*=strength*MathF.Pow(1-n/(float)strips,2);
            context.QueueUiQuad(new(distance,distance),new(band,Math.Max(0,size.Y-distance*2)),color);
            context.QueueUiQuad(new(size.X-distance-band,distance),new(band,Math.Max(0,size.Y-distance*2)),color);
            context.QueueUiQuad(new(distance+band,distance),new(Math.Max(0,size.X-2*(distance+band)),band),color);
            context.QueueUiQuad(new(distance+band,size.Y-distance-band),new(Math.Max(0,size.X-2*(distance+band)),band),color);
        }
    }
}
