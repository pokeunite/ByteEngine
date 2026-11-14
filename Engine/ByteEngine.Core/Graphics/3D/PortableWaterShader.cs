using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>Portable GLSL shared by desktop GL and browser GLES. No platform uniforms or sampling conventions.</summary>
public static class PortableWaterShader
{
 public const string Source="""
float byteWaterNoise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.-2.*f);vec4 h=fract(sin(vec4(dot(i,vec2(127.1,311.7)),dot(i+vec2(1,0),vec2(127.1,311.7)),dot(i+vec2(0,1),vec2(127.1,311.7)),dot(i+vec2(1),vec2(127.1,311.7))))*43758.5453);return mix(mix(h.x,h.y,f.x),mix(h.z,h.w,f.x),f.y);}
void byteGravityWave(inout vec3 disp,inout vec3 tx,inout vec3 tz,vec2 q,vec2 d,float amplitude,float wavelength,float phase,float time,float steepness){float k=6.2831853/max(.1,wavelength),a=clamp(amplitude,0.,.35/k),f=k*dot(d,q)-sqrt(9.81*k)*time+phase,s=sin(f),c=cos(f),steep=clamp(steepness,0.,.8);disp+=vec3(d.x*a*steep*c,a*s,d.y*a*steep*c);float slope=a*k*c,h=a*k*steep*s;tx+=vec3(-d.x*d.x*h,d.x*slope,-d.x*d.y*h);tz+=vec3(-d.x*d.y*h,d.y*slope,-d.y*d.y*h);}
vec3 byteWaterTransmittance(vec3 extinction,float distanceAlong){return exp(-max(extinction,vec3(.000001))*min(30.,max(0.,distanceAlong)));}
float byteWaterPhase(float g,float cosine){g=clamp(g,-.8,.8);return (1.-g*g)/(12.56637*pow(max(.05,1.+g*g-2.*g*cosine),1.5));}
""";
}

/// <summary>The authoritative water material transport layout. Shared with browser serialization and generated GLSL declarations.</summary>
public sealed class BrowserWaterParameters
{
 public bool surface {get;set;}
 public bool reflections {get;set;}
 public int depthTexture {get;set;}
 public float[] size {get;set;}=[];
 public float[] column {get;set;}=[];
 public float[] motion {get;set;}=[];
 public float[] flow {get;set;}=[];
 public float[] impact {get;set;}=[];
 public float[] wake {get;set;}=[];
 public float[] absorption {get;set;}=[];
 public float[] scattering {get;set;}=[];
 public float[] style {get;set;}=[];
 public float[] waves {get;set;}=[];
 public float[] depthRect {get;set;}=[];
 public float[] depthSettings {get;set;}=[];
 public static BrowserWaterParameters? Capture(Material m,int depthTexture){
  if(!m.WaterSurface&&!m.WaterVolume)return null;
  static float[] V(Vector4 v)=>[v.X,v.Y,v.Z,v.W];
  return new(){surface=m.WaterSurface,reflections=m.WaterReflections,depthTexture=depthTexture,size=[m.WaterSize.X,m.WaterSize.Y],column=V(m.WaterColumn),motion=V(m.WaterMotion),flow=V(m.WaterFlow),impact=V(m.WaterImpact),wake=V(m.WaterWake),absorption=V(m.WaterAbsorption),scattering=V(m.WaterScattering),style=V(m.WaterStyle),waves=V(m.WaterOceanWaves),depthRect=V(m.WaterDepthRect),depthSettings=V(m.WaterDepthSettings)};
 }
}
