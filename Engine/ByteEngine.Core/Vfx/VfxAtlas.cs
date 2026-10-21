using System.Numerics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Vfx;

/// <summary>Shared bounded timeline atlases: animation and lifetime are baked together, rather than a frame-by-age cross product.</summary>
internal static class VfxAtlas
{
    internal readonly record struct Layout(int Tile,int Columns,int Rows,int Frames)
    {
        public int Width=>Tile*Columns;
        public int Height=>Tile*Rows;
        public Vector4 Uv(float age)
        {
            int frame=Math.Clamp((int)(age*(Frames-1)),0,Frames-1);
            float x=frame%Columns,y=frame/Columns;
            return new((x+.5f/Tile)/Columns,(y+.5f/Tile)/Rows,(x+1-.5f/Tile)/Columns,(y+1-.5f/Tile)/Rows);
        }
    }
    internal static Layout GetLayout(VfxLayer layer)
    {
        int frames=Math.Clamp(Math.Max(32,layer.FlipbookColumns*layer.FlipbookRows*2),32,128);
        int tile=layer.SpriteResolution<=32?32:layer.SpriteResolution<=64?64:layer.SpriteResolution<=128?128:256;
        int columns=frames>64?16:8, rows=(frames+columns-1)/columns;
        // At most 8 MiB per cached layer and 4096 pixels on either axis (portable WebGL2).
        while((long)columns*rows*tile*tile*4>8*1024*1024 || columns*tile>4096 || rows*tile>4096) tile/=2;
        return new(tile,columns,rows,frames);
    }
    private readonly record struct Key(Texture2D? Source,int Revision,string Settings);
    private sealed class Entry { public required Texture2D Texture; public int Users; }
    private static readonly Dictionary<Key,Entry> Cache=new();
    internal sealed class Lease : IDisposable
    {
        private Action? _release;
        public Texture2D Texture { get; }
        public Layout Layout { get; }
        internal Lease(Texture2D texture,Layout layout,Action release) { Texture=texture;Layout=layout;_release=release; }
        public void Dispose() { _release?.Invoke();_release=null; }
    }
    internal static Lease Acquire(VfxLayer layer,Texture2D? source)
    {
        var key=new Key(source,source?.ContentVersion??0,System.Text.Json.JsonSerializer.Serialize(new {
            layer.Sprite,layer.StartColor,layer.EndColor,layer.FlipbookColumns,layer.FlipbookRows,layer.SpriteResolution,
            layer.OpacityOverLife,layer.ColorBlendOverLife,layer.Dissolve,layer.PatternStrength,layer.PatternScale
        },VfxEffectSerializer.Options));
        if(!Cache.TryGetValue(key,out var entry)) Cache[key]=entry=new Entry {Texture=Build(layer,source)};
        entry.Users++;
        return new(entry.Texture,GetLayout(layer),()=>{if(--entry.Users==0){Cache.Remove(key);entry.Texture.Dispose();}});
    }
    internal static Texture2D Build(VfxLayer layer,Texture2D? source)
    { var pixels=GeneratePixels(layer,source,out int width,out int height);return Texture2D.FromPixels(width,height,pixels,TextureFilter.Linear); }
    internal static byte[] GeneratePixels(VfxLayer layer,Texture2D? source,out int width,out int height)
    {
        var layout=GetLayout(layer);width=layout.Width;height=layout.Height;
        var pixels=new byte[width*height*4];
        ReadOnlySpan<byte> image=source==null?ReadOnlySpan<byte>.Empty:source.PixelData.Span;
        if(source!=null&&image.IsEmpty)
        {
            if(source.IsHdr)throw new InvalidDataException("VFX sprites require an LDR image with alpha, such as PNG.");
            if(string.IsNullOrWhiteSpace(source.FilePath))throw new InvalidDataException("VFX sprite pixels are unavailable.");
            using var stream=File.OpenRead(source.FilePath);
            image=StbImageSharp.ImageResult.FromStream(stream,StbImageSharp.ColorComponents.RedGreenBlueAlpha).Data;
        }
        int sourceFrames=layer.FlipbookColumns*layer.FlipbookRows;
        for(int frame=0;frame<layout.Frames;frame++)
        {
            float age=(float)frame/(layout.Frames-1);
            var color=Vector4.Lerp(layer.StartColor,layer.EndColor,layer.ColorBlendOverLife?.Evaluate(age)??age);
            color.W*=layer.OpacityOverLife?.Evaluate(age)??1;
            int sourceFrame=Math.Min(sourceFrames-1,(int)(age*sourceFrames));
            for(int y=0;y<layout.Tile;y++)for(int x=0;x<layout.Tile;x++)
            {
                float u=(x+.5f)/layout.Tile,v=(y+.5f)/layout.Tile;
                Vector4 texel=source!=null?Sample(image,source.Width,source.Height,sourceFrame,layer.FlipbookColumns,layer.FlipbookRows,u,v):new(1,1,1,SpriteAlpha(layer.Sprite,u,v,age));
                if(layer.PatternStrength>0||layer.Dissolve>0)
                {
                    float noise=Noise(u*layer.PatternScale+age*.8f,v*layer.PatternScale-age*.6f);
                    texel.W*=1-layer.PatternStrength+layer.PatternStrength*noise;
                    if(layer.Dissolve>0)texel.W*=Math.Clamp((noise-age*layer.Dissolve+.08f)/.12f,0,1);
                }
                texel=Vector4.Clamp(texel*color,Vector4.Zero,Vector4.One);
                int index=(((frame/layout.Columns)*layout.Tile+y)*width+(frame%layout.Columns)*layout.Tile+x)*4;
                pixels[index]=(byte)(texel.X*255);pixels[index+1]=(byte)(texel.Y*255);pixels[index+2]=(byte)(texel.Z*255);pixels[index+3]=(byte)(texel.W*255);
            }
        }
        return pixels;
    }
    private static Vector4 Sample(ReadOnlySpan<byte> image,int width,int height,int frame,int columns,int rows,float u,float v)
    {
        if(image.Length<(long)width*height*4)return Vector4.One;
        int left=frame%columns*width/columns,top=frame/columns*height/rows;
        int right=(frame%columns+1)*width/columns-1,bottom=(frame/columns+1)*height/rows-1;
        float px=Math.Clamp(left+u*(right-left),left,right),py=Math.Clamp(top+v*(bottom-top),top,bottom);
        int x=(int)px,y=(int)py,x1=Math.Min(x+1,right),y1=Math.Min(y+1,bottom);
        var a=Pixel(image,width,x,y);var b=Pixel(image,width,x1,y);var c=Pixel(image,width,x,y1);var d=Pixel(image,width,x1,y1);
        return Vector4.Lerp(Vector4.Lerp(a,b,px-x),Vector4.Lerp(c,d,px-x),py-y);
    }
    private static Vector4 Pixel(ReadOnlySpan<byte> image,int width,int x,int y)
    {int n=(y*width+x)*4;return new Vector4(image[n],image[n+1],image[n+2],image[n+3])/255f;}
    internal static float SpriteAlpha(VfxSprite sprite,float u,float v,float age)
    {
        float x=(u-.5f)*2,y=(v-.5f)*2,r=MathF.Sqrt(x*x+y*y),disc=Math.Clamp(1-r,0,1);
        return sprite switch {
            VfxSprite.Solid=>1,
            VfxSprite.Ring=>Math.Clamp(1-MathF.Abs(r-.72f)*9,0,1),
            VfxSprite.Spark=>MathF.Pow(disc,.65f),
            VfxSprite.Smoke=>Math.Clamp((1-r*r)*Noise(u*5+age*.7f,v*5-age)*2,0,1)*Math.Clamp((1-r)*6,0,1),
            VfxSprite.Flame=>Math.Clamp((1-MathF.Abs(x)*2/(.3f+(1-v)))*MathF.Sin(v*MathF.PI)*Noise(u*7,v*6-age*3)*3,0,1)*Math.Clamp(disc*5,0,1),
            VfxSprite.Star=>Math.Clamp(1-Math.Min(Math.Abs(x),Math.Abs(y))*18,0,1)*MathF.Pow(disc,.4f),
            VfxSprite.Arc=>Math.Clamp(1-MathF.Abs(r-.65f)*12,0,1)*Math.Clamp((x+.15f)*5,0,1)*Math.Clamp(disc*8,0,1),
            VfxSprite.Lightning=>Math.Clamp(1-MathF.Abs(x-(Noise(v*18,2)-.5f)*.7f)*22,0,1)*Math.Clamp((1-Math.Abs(y))*5,0,1),
            VfxSprite.Ripple=>Math.Clamp(1-MathF.Abs(r-(.2f+age*.65f))*20,0,1),
            _=>MathF.Pow(disc,1.6f)
        };
    }
    private static float Hash(float x,float y){float v=MathF.Sin(x*127.1f+y*311.7f)*43758.5453f;return v-MathF.Floor(v);}
    private static float Noise(float x,float y)
    {
        float ix=MathF.Floor(x),iy=MathF.Floor(y),fx=x-ix,fy=y-iy;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
        float a=Hash(ix,iy),b=Hash(ix+1,iy),c=Hash(ix,iy+1),d=Hash(ix+1,iy+1);
        return (a+(b-a)*fx)*(1-fy)+(c+(d-c)*fx)*fy;
    }
}
