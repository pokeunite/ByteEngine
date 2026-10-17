using System.Numerics;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Staggered height/velocity grid for small-amplitude shallow-water motion. Reflecting boundaries and zero-mean displacement preserve the basin's authoritative volume.</summary>
internal sealed class ShallowWaterGridReference
{
 public int Columns {get;} public int Rows {get;} public float Width {get;} public float Length {get;}
 readonly float[] height,next,u,w;readonly float dx,dz;
 public float MeanOffset=>height.Average();
 public float MaximumDisplacement=>height.Max(v=>Math.Abs(v));
 public float Energy=>height.Sum(v=>v*v)/height.Length;
 public ShallowWaterGridReference(int columns,int rows,float width,float length)
 {Columns=Math.Clamp(columns,8,96);Rows=Math.Clamp(rows,8,96);Width=Math.Max(.1f,width);Length=Math.Max(.1f,length);dx=Width/Columns;dz=Length/Rows;height=new float[Columns*Rows];next=new float[height.Length];u=new float[(Columns+1)*Rows];w=new float[Columns*(Rows+1)];}
 public void Seed(float amplitude){for(int z=0;z<Rows;z++)for(int x=0;x<Columns;x++){float a=(x+.5f)/Columns,b=(z+.5f)/Rows;height[z*Columns+x]=amplitude*(MathF.Cos(a*MathF.PI*3)*MathF.Cos(b*MathF.PI*2)+.5f*MathF.Cos(a*MathF.PI*7)*MathF.Cos(b*MathF.PI*5));}Normalize(amplitude*3);}
 public void Clear(){Array.Clear(height);Array.Clear(u);Array.Clear(w);}
 public float Sample(float x,float z)=>SampleArray(height,Columns,Rows,x/Width*Columns-.5f,z/Length*Rows-.5f);
 static float SampleArray(float[] data,int nx,int nz,float x,float z){x=Math.Clamp(x,0,nx-1);z=Math.Clamp(z,0,nz-1);int a=(int)x,b=(int)z,c=Math.Min(a+1,nx-1),d=Math.Min(b+1,nz-1);return float.Lerp(float.Lerp(data[b*nx+a],data[b*nx+c],x-a),float.Lerp(data[d*nx+a],data[d*nx+c],x-a),z-b);}
 public void Impulse(float x,float z,float radius,float amount)
 {radius=Math.Max(.05f,radius);int xa=Math.Clamp((int)((x-radius*3)/dx),0,Columns-1),xb=Math.Clamp((int)((x+radius*3)/dx),0,Columns-1),za=Math.Clamp((int)((z-radius*3)/dz),0,Rows-1),zb=Math.Clamp((int)((z+radius*3)/dz),0,Rows-1);for(int j=za;j<=zb;j++)for(int i=xa;i<=xb;i++){float ax=(i+.5f)*dx-x,az=(j+.5f)*dz-z;height[j*Columns+i]+=amount*MathF.Exp(-(ax*ax+az*az)/(radius*radius));}}
 public void Step(float dt,float depth,Vector2 drift,float damping,float limit)
 {
  if(dt<=0)return;if(depth<.004f){Clear();return;}float c=MathF.Sqrt(9.81f*Math.Min(depth,5));int steps=Math.Max(1,(int)MathF.Ceiling(dt*(c+drift.Length())/Math.Min(dx,dz)/.40f));float h=dt/steps,decay=MathF.Exp(-Math.Max(0,damping)*h);
  float heightDecay=MathF.Exp(-.08f*h);
  for(int sub=0;sub<steps;sub++){
   for(int z=0;z<Rows;z++)for(int x=1;x<Columns;x++){int i=z*(Columns+1)+x;u[i]=(u[i]-9.81f*(height[z*Columns+x]-height[z*Columns+x-1])/dx*h)*decay;}
   for(int z=1;z<Rows;z++)for(int x=0;x<Columns;x++){int i=z*Columns+x;w[i]=(w[i]-9.81f*(height[i]-height[i-Columns])/dz*h)*decay;}
   for(int z=0;z<Rows;z++)for(int x=0;x<Columns;x++){int i=z*Columns+x;float divergence=(u[z*(Columns+1)+x+1]-u[z*(Columns+1)+x])/dx+(w[(z+1)*Columns+x]-w[z*Columns+x])/dz;float advected=SampleArray(height,Columns,Rows,x-drift.X*h/dx,z-drift.Y*h/dz);next[i]=(advected-Math.Min(depth,5)*divergence*h)*heightDecay;}
   Array.Copy(next,height,height.Length);Normalize(limit);
  }
 }
 void Normalize(float limit){float mean=height.Average();for(int i=0;i<height.Length;i++)height[i]-=mean;float max=MaximumDisplacement;if(max>limit&&max>.00001f){float scale=limit/max;for(int i=0;i<height.Length;i++)height[i]*=scale;}}
}

