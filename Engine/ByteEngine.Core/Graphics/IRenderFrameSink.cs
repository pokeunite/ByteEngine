using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Graphics;

/// <summary>
/// Render-pass boundary for non-desktop hosts. Geometry remains owned by Core.
/// Consume submissions synchronously; the list is reused.
/// UI coordinates are top-left pixel coordinates. Does not initialize desktop GL.
/// </summary>
public interface IRenderFrameSink
{
    void Draw3D(RenderView3D? view, RenderLighting3D lighting,
        RenderEnvironment3D environment, IReadOnlyList<RenderSubmission> submissions);
    void SetUiClip(Vector4? rectangle) { }
    Vector2 MeasureText(string text,string? fontPath,int size) => new(text.Length*size*.6f,size*1.2f);
    void DrawText(string value, string? fontPath, int size, Vector2 position,
        Vector4 color, float wrapWidth, UiAnchor anchor);
    void DrawQuad(Vector2 position, Vector2 size, Vector4 color);
    void DrawLine(Vector2 from, Vector2 to, float thickness, Vector4 color)
    {
        int steps = Math.Clamp((int)(Vector2.Distance(from,to)/Math.Max(thickness,1)),1,512);
        for(int i=0;i<=steps;i++) DrawQuad(Vector2.Lerp(from,to,i/(float)steps)-new Vector2(thickness*.5f),new(thickness),color);
    }
    void DrawImage(Texture2D texture, Vector2 position, Vector2 size, Vector4 color);
}
