using System.Numerics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Vfx;

/// <summary>Bakes lifetime color/fade and sprite animation once, not once per particle/frame.</summary>
internal static class VfxAtlas
{
    internal const int Tile=32, Ages=32;
    private readonly record struct Key(Texture2D? Source,int Revision,VfxSprite Sprite,Vector4 Start,Vector4 End,int Columns,int Rows,string Curves);
    private sealed class Entry { public required Texture2D Texture; public int Users; }
    private static readonly Dictionary<Key,Entry> Cache=new();
    internal sealed class Lease : IDisposable
    {
        private Action? _release;
        public Texture2D Texture { get; }
        internal Lease(Texture2D texture,Action release) { Texture=texture; _release=release; }
        public void Dispose() { _release?.Invoke(); _release=null; }
    }
    internal static Lease Acquire(VfxLayer layer,Texture2D? source)
    {
        string curves=System.Text.Json.JsonSerializer.Serialize(new[] { layer.OpacityOverLife,layer.ColorBlendOverLife },VfxEffectSerializer.Options);
        var key=new Key(source,source?.ContentVersion??0,layer.Sprite,layer.StartColor,layer.EndColor,layer.FlipbookColumns,layer.FlipbookRows,curves);
        if(!Cache.TryGetValue(key,out var entry)) Cache[key]=entry=new Entry { Texture=Build(layer,source) };
        entry.Users++;
        return new Lease(entry.Texture,()=> { if(--entry.Users==0) { Cache.Remove(key); entry.Texture.Dispose(); } });
    }
    internal static Texture2D Build(VfxLayer layer,Texture2D? source)
    {
        var pixels=GeneratePixels(layer,source,out int width,out int height);
        return Texture2D.FromPixels(width,height,pixels,TextureFilter.Linear);
    }
    internal static byte[] GeneratePixels(VfxLayer layer,Texture2D? source,out int width,out int height)
    {
        int frames=source==null ? 1 : layer.FlipbookColumns*layer.FlipbookRows;
        width=Tile*frames; height=Tile*Ages;
        var pixels=new byte[width*height*4];
        ReadOnlySpan<byte> image=source==null ? ReadOnlySpan<byte>.Empty : source.PixelData.Span;
        byte[]? decoded=null;
        if(source!=null && image.IsEmpty)
        {
            if(source.IsHdr) throw new InvalidDataException("VFX sprites require an LDR image with alpha, such as PNG.");
            if(string.IsNullOrWhiteSpace(source.FilePath)) throw new InvalidDataException("VFX sprite pixels are unavailable.");
            using var stream=File.OpenRead(source.FilePath);
            var result=StbImageSharp.ImageResult.FromStream(stream,StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            decoded=result.Data; image=decoded;
        }
        for(int age=0;age<Ages;age++)
        {
            float lifetime=(float)age/(Ages-1);
            Vector4 color=Vector4.Lerp(layer.StartColor,layer.EndColor,layer.ColorBlendOverLife?.Evaluate(lifetime)??lifetime);
            color.W*=layer.OpacityOverLife?.Evaluate(lifetime)??1;
            for(int frame=0;frame<frames;frame++)
            for(int y=0;y<Tile;y++)
            for(int x=0;x<Tile;x++)
            {
                float u=(x+.5f)/Tile,v=(y+.5f)/Tile;
                Vector4 texel=Vector4.One;
                if(source!=null && image.Length>=source.Width*source.Height*4)
                {
                    int sx=Math.Clamp((int)(((frame%layer.FlipbookColumns)+u)/layer.FlipbookColumns*source.Width),0,source.Width-1);
                    int sy=Math.Clamp((int)(((frame/layer.FlipbookColumns)+v)/layer.FlipbookRows*source.Height),0,source.Height-1);
                    int n=(sy*source.Width+sx)*4;
                    texel=new Vector4(image[n],image[n+1],image[n+2],image[n+3])/255f;
                }
                else
                {
                    float r=Vector2.Distance(new(u,v),new(.5f)) * 2;
                    texel.W=layer.Sprite switch
                    {
                        VfxSprite.Solid=>1,
                        VfxSprite.Ring=>Math.Clamp(1-MathF.Abs(r-.72f)*9,0,1),
                        VfxSprite.Spark=>MathF.Pow(Math.Clamp(1-r,0,1),.65f),
                        _=>MathF.Pow(Math.Clamp(1-r,0,1),1.6f)
                    };
                }
                texel*=color;
                int index=((age*Tile+y)*width+frame*Tile+x)*4;
                pixels[index]=(byte)(texel.X*255); pixels[index+1]=(byte)(texel.Y*255); pixels[index+2]=(byte)(texel.Z*255); pixels[index+3]=(byte)(texel.W*255);
            }
        }
        return pixels;
    }
}
