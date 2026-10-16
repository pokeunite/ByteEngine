using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Reusable open-water renderer. Analytic gravity waves supply geometry and normals;
/// the shared native water shader handles depth extinction, refraction and shoreline foam.
/// This decorative sea is deliberately separate from finite hydraulic puzzle networks.</summary>
public sealed class WaterOcean3D : Component
{
    public string DepthFieldName {get;set;}="";
    public float WaveMaskDepth {get;set;}=2.0f;
    WaterDepthField3D? depthField;
    protected override void OnStart()=>depthField=GameObject.Scene!.FindGameObject(DepthFieldName)?.GetComponent<WaterDepthField3D>();
    public float WaveAttenuation(Vector2 point){if(depthField==null)return 1;float t=Math.Clamp((SeaLevel-depthField.SampleHeight(point))/Math.Max(.01f,WaveMaskDepth),0,1);return t*t*(3-2*t);}
    public Vector2 Size { get; set; } = new(180,180);
    public int Segments { get; set; } = 192;
    public float SeaLevel { get; set; } = -.73f;
    public float SeabedHeight { get; set; } = -5.2f;
    public float WaveAmplitude { get; set; } = .105f;
    public float Wavelength { get; set; } = 6.3f;
    public Vector2 WindDirection { get; set; } = new(1,.4f);
    public float Steepness { get; set; } = .32f;
    public Vector3 Absorption { get; set; } = new(.53f,.115f,.065f);
    public Vector3 Scattering { get; set; } = new(.012f,.17f,.19f);
    public float ScatteringAnisotropy { get; set; } = .25f;
    public float CausticStrength { get; set; } = .65f;
    public float FoamStrength { get; set; } = 1;
    public float ShoreFoamWidth { get; set; } = .55f;
    public float Roughness { get; set; } = .12f;
    public float NormalStrength { get; set; } = .8f;
    public bool ScreenSpaceReflections { get; set; } = true;
    Mesh? mesh;
    float[]? vertices;
    int resolution;
    Vector2 builtSize;Vector3 builtCenter;float builtSeaLevel=float.NaN;
    readonly Material material=new(){WaterSurface=true,BlendMode=BlendMode3D.AlphaBlend,CullMode=CullMode3D.None};

    /// <summary>World-space analytic sample, also useful to floating scenery. Independent of rendering.</summary>
    public (Vector3 Displacement,Vector3 Normal) Sample(Vector2 worldXZ,float time)
    {
        var d=WindDirection.LengthSquared()>.0001f?Vector2.Normalize(WindDirection):Vector2.UnitX;
        float attenuation=WaveAttenuation(worldXZ);Vector3 displacement=Vector3.Zero,tangentX=Vector3.UnitX,tangentZ=Vector3.UnitZ;
        void Wave(Vector2 direction,float amplitude,float length,float phase)
        {
            float k=MathF.Tau/Math.Max(.1f,length),a=Math.Clamp(amplitude,0,.35f/k)*attenuation;
            float f=k*Vector2.Dot(direction,worldXZ)-MathF.Sqrt(9.81f*k)*time+phase;
            float sin=MathF.Sin(f),cos=MathF.Cos(f),q=Math.Clamp(Steepness,0,.8f);
            displacement+=new Vector3(direction.X*a*q*cos,a*sin,direction.Y*a*q*cos);
            float slope=a*k*cos,horizontal=a*k*q*sin;
            tangentX+=new Vector3(-direction.X*direction.X*horizontal,direction.X*slope,-direction.X*direction.Y*horizontal);
            tangentZ+=new Vector3(-direction.X*direction.Y*horizontal,direction.Y*slope,-direction.Y*direction.Y*horizontal);
        }
        Wave(d,WaveAmplitude,Wavelength,0);
        Wave(new(-d.Y,d.X),WaveAmplitude*.52f,Wavelength*.492f,1.7f);
        Wave(Vector2.Normalize(new Vector2(d.X*.8f+d.Y*.6f,d.Y*.8f-d.X*.6f)),WaveAmplitude*.21f,Wavelength*.1984f,3.1f);
        return(displacement,Vector3.Normalize(Vector3.Cross(tangentZ,tangentX)));
    }
    protected override void OnRender(RenderContext context)
    {
        if(!context.Has3DCamera)return;
        int desired=Math.Clamp(Segments,16,context.CaptureRenderEnvironment3D().Look.Quality switch{GraphicsQuality.Fast=>96,GraphicsQuality.Balanced=>128,_=>256});
        if(mesh!=null&&desired!=resolution){mesh.Dispose();mesh=null;vertices=null;builtSeaLevel=float.NaN;}
        if(mesh==null){resolution=desired;vertices=new float[(resolution+1)*(resolution+1)*8];uint[] ix=new uint[resolution*resolution*6];int k=0;for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++){uint a=(uint)(z*(resolution+1)+x),b=a+1,c=a+(uint)resolution+1,d=c+1;foreach(uint i in new[]{a,c,b,b,c,d})ix[k++]=i;}mesh=new(vertices,ix,true);}
        float time=(float)GameObject.Scene!.SimulationTime;var center=Transform.WorldPosition;
        if(builtSize!=Size||builtCenter!=center||builtSeaLevel!=SeaLevel){for(int z=0;z<=resolution;z++)for(int x=0;x<=resolution;x++){float u=(float)x/resolution,v=(float)z/resolution;int k=(z*(resolution+1)+x)*8;vertices![k]=(u-.5f)*Size.X+center.X;vertices[k+1]=SeaLevel;vertices[k+2]=(v-.5f)*Size.Y+center.Z;vertices[k+3]=0;vertices[k+4]=1;vertices[k+5]=0;vertices[k+6]=u;vertices[k+7]=v;}mesh.UpdateVertices(vertices!);builtSize=Size;builtCenter=center;builtSeaLevel=SeaLevel;}
        if(depthField!=null){material.WaterDepthTexture=depthField.Upload();material.WaterDepthRect=new(depthField.Origin,depthField.Size.X,depthField.Size.Y);material.WaterDepthSettings=new(depthField.MinimumHeight,depthField.MaximumHeight,SeaLevel,WaveMaskDepth);}
        material.WaterSize=Size;material.WaterColumn=new(SeaLevel,SeabedHeight,0,0);material.WaterMotion=new(time,0,0,FoamStrength);material.WaterFlow=new(0,0,Steepness,0);material.WaterOceanWaves=new(WaveAmplitude,Wavelength,WindDirection.X,WindDirection.Y);
        material.WaterAbsorption=new(Absorption,CausticStrength);material.WaterScattering=new(Scattering,ScatteringAnisotropy);material.WaterStyle=new(NormalStrength,Math.Clamp(Roughness,.045f,1),Math.Max(.01f,ShoreFoamWidth),1);material.WaterReflections=ScreenSpaceReflections;
        context.RenderWorld.Submit(mesh,material,Matrix4x4.Identity,RenderQueue3D.Transparent,false,true,true);
    }
    protected override void OnDestroy(){mesh?.Dispose();mesh=null;vertices=null;}
}
