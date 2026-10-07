using System.Numerics;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

public static class GraphicsDiagnostics
{
    public static double PostCpuMs { get; internal set; }
    public static double? PostGpuMs { get; internal set; }
    public static int ExtraPasses { get; internal set; }
    public static int Width { get; internal set; }
    public static int Height { get; internal set; }
}

/// <summary>Reusable half-resolution AO and quarter-resolution separable bloom. No frame allocation.</summary>
internal sealed class PostEffects3D : IDisposable
{
    private sealed class Target : IDisposable
    {
        public int Texture,Fbo,Width,Height;
        public void Resize(int width,int height,bool ao)
        {
            width=Math.Max(1,width); height=Math.Max(1,height);
            if(Texture!=0 && Width==width && Height==height) return;
            Dispose(); Width=width; Height=height; Texture=GL.GenTexture(); Fbo=GL.GenFramebuffer();
            GL.BindTexture(TextureTarget.Texture2D,Texture);
            GL.TexImage2D(TextureTarget.Texture2D,0,ao?PixelInternalFormat.R8:PixelInternalFormat.Rgba16f,width,height,0,ao?PixelFormat.Red:PixelFormat.Rgba,PixelType.Float,IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMinFilter,(int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureMagFilter,(int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapS,(int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D,TextureParameterName.TextureWrapT,(int)TextureWrapMode.ClampToEdge);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer,Fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,FramebufferAttachment.ColorAttachment0,TextureTarget.Texture2D,Texture,0);
            if(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)!=FramebufferErrorCode.FramebufferComplete) { Dispose(); throw new InvalidOperationException("Post effect target is incomplete."); }
        }
        public void Dispose() { if(Texture!=0)GL.DeleteTexture(Texture); if(Fbo!=0)GL.DeleteFramebuffer(Fbo); Texture=Fbo=0; }
    }
    private readonly Target _ao=new(),_aoBlur=new(),_bloom=new(),_blur=new();
    private Shader? _shader;
    private int _vao;
    private readonly int[] _queries=new int[3];
    private readonly bool[] _pending=new bool[3];
    private int _queryIndex,_activeQuery=-1;
    private long _cpuStart;
    public int AoTexture { get; private set; }
    public int BloomTexture { get; private set; }
    public void Begin(bool profile)
    {
        _cpuStart=Stopwatch.GetTimestamp(); _activeQuery=-1;
        if(!profile) { GraphicsDiagnostics.PostGpuMs=null; return; }
        for(int i=0;i<3;i++)
        {
            if(!_pending[i]) continue;
            GL.GetQueryObject(_queries[i],GetQueryObjectParam.QueryResultAvailable,out int available);
            if(available!=0) { GL.GetQueryObject(_queries[i],GetQueryObjectParam.QueryResult,out long nanos); GraphicsDiagnostics.PostGpuMs=nanos/1e6; _pending[i]=false; }
        }
        int slot=_queryIndex++%3;
        if(_pending[slot]) return;
        if(_queries[slot]==0) _queries[slot]=GL.GenQuery();
        GL.BeginQuery(QueryTarget.TimeElapsed,_queries[slot]); _activeQuery=slot;
    }
    public void End()
    {
        if(_activeQuery>=0) { GL.EndQuery(QueryTarget.TimeElapsed); _pending[_activeQuery]=true; _activeQuery=-1; }
        GraphicsDiagnostics.PostCpuMs=Stopwatch.GetElapsedTime(_cpuStart).TotalMilliseconds;
    }
    public void Prepare(int color,int depth,int width,int height,Matrix4x4 projection,GraphicsLook look)
    {
        AoTexture=BloomTexture=0; GraphicsDiagnostics.ExtraPasses=0; GraphicsDiagnostics.Width=width; GraphicsDiagnostics.Height=height;
        if(look.Occlusion<=0 && look.Bloom<=0) { _ao.Dispose(); _aoBlur.Dispose(); _bloom.Dispose(); _blur.Dispose(); return; }
        _shader??=new Shader(Vertex,Fragment); if(_vao==0)_vao=GL.GenVertexArray();
        GL.Disable(EnableCap.DepthTest); GL.DepthMask(false); GL.Disable(EnableCap.Blend); GL.Disable(EnableCap.CullFace);
        _shader.Use(); _shader.SetInt("uColor",0); _shader.SetInt("uDepth",1); _shader.SetInt("uInput",2);
        Matrix4x4.Invert(projection,out var inverse); _shader.SetMatrix4("uInverseProjection",new OpenTK.Mathematics.Matrix4(inverse.M11,inverse.M12,inverse.M13,inverse.M14,inverse.M21,inverse.M22,inverse.M23,inverse.M24,inverse.M31,inverse.M32,inverse.M33,inverse.M34,inverse.M41,inverse.M42,inverse.M43,inverse.M44)); _shader.SetFloat("uProjectionY",Math.Abs(projection.M22));
        _shader.SetFloat("uRadius",look.Radius); _shader.SetInt("uSamples",look.AoSamples); _shader.SetFloat("uThreshold",look.Threshold);
        Bind(color,0); Bind(depth,1); GL.BindVertexArray(_vao);
        if(look.Occlusion>0 && depth!=0 && projection!=Matrix4x4.Identity)
        {
            _ao.Resize(width/2,height/2,true); _aoBlur.Resize(width/2,height/2,true);
            Draw(_ao,0,0); Draw(_aoBlur,1,_ao.Texture); AoTexture=_aoBlur.Texture;
        }
        else { _ao.Dispose(); _aoBlur.Dispose(); }
        if(look.Bloom>0)
        {
            _bloom.Resize(width/4,height/4,false); _blur.Resize(width/4,height/4,false);
            Draw(_bloom,2,0); Draw(_blur,3,_bloom.Texture); Draw(_bloom,4,_blur.Texture); BloomTexture=_bloom.Texture;
        }
        else { _bloom.Dispose(); _blur.Dispose(); }
        GL.BindVertexArray(0);
    }
    private static void Bind(int texture,int unit) { GL.ActiveTexture(TextureUnit.Texture0+unit); GL.BindTexture(TextureTarget.Texture2D,texture); }
    private void Draw(Target target,int pass,int input)
    { GL.BindFramebuffer(FramebufferTarget.Framebuffer,target.Fbo); GL.Viewport(0,0,target.Width,target.Height); Bind(input,2); _shader!.SetInt("uPass",pass); GL.DrawArrays(PrimitiveType.Triangles,0,3); GraphicsDiagnostics.ExtraPasses++; }
    public void Dispose() { _ao.Dispose(); _aoBlur.Dispose(); _bloom.Dispose(); _blur.Dispose(); _shader?.Dispose(); if(_vao!=0)GL.DeleteVertexArray(_vao); foreach(var q in _queries)if(q!=0)GL.DeleteQuery(q); }
    private const string Vertex="""
        #version 330 core
        out vec2 uv;
        void main(){ vec2 p=gl_VertexID==0?vec2(-1,-1):gl_VertexID==1?vec2(3,-1):vec2(-1,3); uv=p*.5+.5; gl_Position=vec4(p,0,1); }
        """;
    private const string Fragment="""
        #version 330 core
        in vec2 uv; out vec4 outputColor;
        uniform sampler2D uColor,uDepth,uInput;
        uniform mat4 uInverseProjection;
        uniform float uProjectionY,uRadius,uThreshold;
        uniform int uPass,uSamples;
        vec3 position(vec2 p){ vec4 q=uInverseProjection*vec4(p*2-1,texture(uDepth,p).r*2-1,1); return q.xyz/q.w; }
        void main(){
            vec2 texel=1.0/vec2(textureSize(uInput,0));
            if(uPass==0){
                float depth=texture(uDepth,uv).r; if(depth>=.999999){outputColor=vec4(1);return;}
                vec3 p=position(uv), dx=dFdx(p),dy=dFdy(p); vec3 n=cross(dx,dy);
                if(dot(n,n)<1e-12){outputColor=vec4(1);return;} n=normalize(n); if(dot(n,p)>0)n=-n;
                float radius=min(.15,uRadius*uProjectionY/max(.1,-p.z)*.5); float result=0;
                for(int i=0;i<12;i++){if(i>=uSamples)break; float angle=float(i)*2.399963;
                    vec2 offset=vec2(cos(angle),sin(angle))*radius*sqrt((float(i)+.5)/float(uSamples));
                    vec2 sampleUv=uv+offset; if(any(lessThan(sampleUv,vec2(0)))||any(greaterThan(sampleUv,vec2(1))))continue;
                    if(texture(uDepth,sampleUv).r>=.999999)continue;
                    vec3 delta=position(sampleUv)-p; float distance=length(delta);
                    result+=max(0,dot(n,delta)/max(.001,distance)-.08)*(1-smoothstep(0,uRadius,distance));
                }
                outputColor=vec4(clamp(1-result/float(uSamples)*2,0,1));return;
            }
            if(uPass==1){
                float center=position(uv).z; float total=0,weight=0;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
                    vec2 p=uv+vec2(x,y)*texel; float w=exp(-abs(position(p).z-center)/max(.02,uRadius*.25));
                    total+=texture(uInput,p).r*w; weight+=w;
                }
                outputColor=vec4(total/max(.001,weight));return;
            }
            if(uPass==2){
                vec2 pixel=1.0/vec2(textureSize(uColor,0)); vec3 color=vec3(0);
                for(int y=-1;y<=1;y+=2)for(int x=-1;x<=1;x+=2){vec3 c=max(texture(uColor,uv+vec2(x,y)*pixel).rgb,vec3(0)); float b=max(c.r,max(c.g,c.b)); color+=c*max(0,b-uThreshold)/max(.001,b);}
                outputColor=vec4(color*.25,1);return;
            }
            vec2 d=uPass==3?vec2(texel.x,0):vec2(0,texel.y);
            vec3 color=texture(uInput,uv).rgb*.227027;
            color+=(texture(uInput,uv+d*1.384615).rgb+texture(uInput,uv-d*1.384615).rgb)*.316216;
            color+=(texture(uInput,uv+d*3.230769).rgb+texture(uInput,uv-d*3.230769).rgb)*.070270;
            outputColor=vec4(color,1);
        }
        """;
}
