using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>Authored scene-transition presentation. Progress is supplied by the scene loader.</summary>
public sealed class LoadingScreenStyle : Component
{
    public AssetReference FontReference { get; set; } = AssetReference.Empty;
    public string DestinationTitles { get; set; } = "";
    public Vector4 Background { get; set; } = new(.015f,.075f,.105f,1);
    public Vector4 Accent { get; set; } = new(.96f,.71f,.29f,1);
    internal string Title(string path) => DestinationTitles.Split(';',StringSplitOptions.RemoveEmptyEntries)
        .Select(s=>s.Split('=',2)).FirstOrDefault(s=>s.Length==2&&s[0].Trim().Equals(path,StringComparison.OrdinalIgnoreCase))?.Last().Trim()
        ?? Path.GetFileNameWithoutExtension(path).Replace('_',' ').ToUpperInvariant();
    internal void Draw(RenderContext context,string title,float progress)
    {
        float w=context.TargetWidth,h=context.TargetHeight,scale=MathF.Min(w/1280,h/720);
        float left=40*scale,right=w-40*scale,y=h-70*scale,length=right-left;
        var cream=new Vector4(.97f,.95f,.88f,1);string? font=FontRuntime.ResolvePath(FontReference);
        context.QueueUiClip(null);context.QueueUiQuad(Vector2.Zero,new(w,h),Background);
        // A deliberately quiet vertical tint, with no imagery behind the controls.
        for(int row=0;row<48;row++){float a=row/47f;context.QueueUiQuad(new(0,h*row/48),new(w,h/48+1),Background+new Vector4(0,.006f*a,.01f*a,0));}
        int size=Math.Max(24,(int)(72*scale));
        while(size>24*scale&&context.MeasureText(title,font,size).X>length)size--;
        context.QueueUiText(title,font,size,new(left,8*scale),cream,length,UiAnchor.TopLeft);
        float p=Math.Clamp(progress,0,1),x=left+length*p;
        context.QueueUiQuad(new(left,y),new(length,4*scale),Accent*new Vector4(.35f,.35f,.35f,1));
        context.QueueUiQuad(new(left,y),new(length*p,4*scale),Accent);
        // Small vector boat; its centre follows exactly the completed-work endpoint.
        for(int row=0;row<8;row++){float half=(24-row*1.2f)*scale;context.QueueUiQuad(new(x-half,y-(31-row)*scale),new(half*2,scale+1),cream);}
        for(int line=0;line<3;line++)for(int i=0;i<42;i++){
            float dx=(i-21)*scale,dy=MathF.Sin(i*.39f)*1.5f*scale;
            context.QueueUiQuad(new(x+dx,y-(17-line*4)*scale+dy),new(scale+1,1.2f*scale),cream);
        }
        var label="L O A D I N G";int labelSize=Math.Max(18,(int)(26*scale));
        context.QueueUiText(label,font,labelSize,new(right-context.MeasureText(label,font,labelSize).X,y+12*scale),cream,length,UiAnchor.TopLeft);
    }
}
