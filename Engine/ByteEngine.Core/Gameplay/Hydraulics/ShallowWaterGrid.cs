using System.Numerics;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Staggered height/velocity grid for small-amplitude shallow-water motion. Reflecting boundaries and zero-mean displacement preserve the basin's authoritative volume.</summary>
public sealed class ShallowWaterGrid
{
 public int Columns {get;} public int Rows {get;} public float Width {get;} public float Length {get;}
 float[] height,next;readonly float[] u,w;readonly float dx,dz;
 public float MeanOffset {get{double sum=0;foreach(float value in height)sum+=value;return (float)(sum/height.Length);}}
 public float MaximumDisplacement {get{float max=0;foreach(float value in height)max=Math.Max(max,Math.Abs(value));return max;}}
 public float Energy {get{double sum=0;foreach(float value in height)sum+=value*value;return (float)(sum/height.Length);}}
 sealed class ImpulseKernel {public int[] Indices=[];public float[] Weights=[];public long Used;}
 readonly Dictionary<(float X,float Z,float Radius),ImpulseKernel> impulses=new();long impulseSequence;
 public ShallowWaterGrid(int columns,int rows,float width,float length)
 {Columns=Math.Clamp(columns,8,96);Rows=Math.Clamp(rows,8,96);Width=Math.Max(.1f,width);Length=Math.Max(.1f,length);dx=Width/Columns;dz=Length/Rows;height=new float[Columns*Rows];next=new float[height.Length];u=new float[(Columns+1)*Rows];w=new float[Columns*(Rows+1)];}
 public void Seed(float amplitude){for(int z=0;z<Rows;z++)for(int x=0;x<Columns;x++){float a=(x+.5f)/Columns,b=(z+.5f)/Rows;height[z*Columns+x]=amplitude*(MathF.Cos(a*MathF.PI*3)*MathF.Cos(b*MathF.PI*2)+.5f*MathF.Cos(a*MathF.PI*7)*MathF.Cos(b*MathF.PI*5));}Normalize(amplitude*3);}
 public void Clear(){Array.Clear(height);Array.Clear(u);Array.Clear(w);}
 public float Sample(float x,float z)=>SampleArray(height,Columns,Rows,x/Width*Columns-.5f,z/Length*Rows-.5f);
 static float SampleArray(float[] data,int nx,int nz,float x,float z){x=Math.Clamp(x,0,nx-1);z=Math.Clamp(z,0,nz-1);int a=(int)x,b=(int)z,c=Math.Min(a+1,nx-1),d=Math.Min(b+1,nz-1);float tx=x-a,tz=z-b;float low=data[b*nx+a]*(1-tx)+data[b*nx+c]*tx,high=data[d*nx+a]*(1-tx)+data[d*nx+c]*tx;return low*(1-tz)+high*tz;}
 public void Impulse(float x,float z,float radius,float amount)
 {
  radius=Math.Max(.05f,radius);var key=(x,z,radius);
  if(!impulses.TryGetValue(key,out var kernel)){
   if(impulses.Count>=16){var oldest=impulses.First();foreach(var entry in impulses)if(entry.Value.Used<oldest.Value.Used)oldest=entry;impulses.Remove(oldest.Key);}
   int xa=Math.Clamp((int)((x-radius*3)/dx),0,Columns-1),xb=Math.Clamp((int)((x+radius*3)/dx),0,Columns-1),za=Math.Clamp((int)((z-radius*3)/dz),0,Rows-1),zb=Math.Clamp((int)((z+radius*3)/dz),0,Rows-1);
   int count=(xb-xa+1)*(zb-za+1);kernel=new(){Indices=new int[count],Weights=new float[count]};int at=0;
   for(int j=za;j<=zb;j++)for(int i=xa;i<=xb;i++){float ax=(i+.5f)*dx-x,az=(j+.5f)*dz-z;kernel.Indices[at]=j*Columns+i;kernel.Weights[at++]=MathF.Exp(-(ax*ax+az*az)/(radius*radius));}
   impulses.Add(key,kernel);
  }
  kernel.Used=++impulseSequence;for(int i=0;i<kernel.Indices.Length;i++)height[kernel.Indices[i]]+=amount*kernel.Weights[i];
 }
 public void Step(float dt,float depth,Vector2 drift,float damping,float limit)
 {
  if(dt<=0)return;if(depth<.004f){Clear();return;}float c=MathF.Sqrt(9.81f*Math.Min(depth,5));int steps=Math.Max(1,(int)MathF.Ceiling(dt*(c+drift.Length())/Math.Min(dx,dz)/.40f));float h=dt/steps,decay=MathF.Exp(-Math.Max(0,damping)*h);
  float heightDecay=MathF.Exp(-.08f*h),gravityX=9.81f/dx*h,gravityZ=9.81f/dz*h,depthTime=Math.Min(depth,5)*h;
  bool advect=drift!=Vector2.Zero;float offsetX=-drift.X*h/dx,offsetZ=-drift.Y*h/dz;
  int shiftX=(int)MathF.Floor(offsetX),shiftZ=(int)MathF.Floor(offsetZ);float tx=offsetX-shiftX,tz=offsetZ-shiftZ;
  for(int sub=0;sub<steps;sub++){
   for(int z=0;z<Rows;z++){int row=z*Columns,face=z*(Columns+1);for(int x=1;x<Columns;x++){int i=face+x;u[i]=(u[i]-(height[row+x]-height[row+x-1])*gravityX)*decay;}}
   for(int z=1;z<Rows;z++){int row=z*Columns;for(int x=0;x<Columns;x++){int i=row+x;w[i]=(w[i]-(height[i]-height[i-Columns])*gravityZ)*decay;}}
   for(int z=0;z<Rows;z++){
    int row=z*Columns,face=z*(Columns+1),za=Math.Clamp(z+shiftZ,0,Rows-1)*Columns,zb=Math.Clamp(z+shiftZ+1,0,Rows-1)*Columns;
    for(int x=0;x<Columns;x++){
     int i=row+x;float divergence=(u[face+x+1]-u[face+x])/dx+(w[i+Columns]-w[i])/dz;
     float advected=height[i];
     if(advect){int xa=Math.Clamp(x+shiftX,0,Columns-1),xb=Math.Clamp(x+shiftX+1,0,Columns-1);float low=height[za+xa]*(1-tx)+height[za+xb]*tx,high=height[zb+xa]*(1-tx)+height[zb+xb]*tx;advected=low*(1-tz)+high*tz;}
     next[i]=(advected-depthTime*divergence)*heightDecay;
    }
   }
   (height,next)=(next,height);Normalize(limit);
  }
 }
 void Normalize(float limit){float mean=MeanOffset,max=0;for(int i=0;i<height.Length;i++){height[i]-=mean;max=Math.Max(max,Math.Abs(height[i]));}if(max>limit&&max>.00001f){float scale=limit/max;for(int i=0;i<height.Length;i++)height[i]*=scale;}}
}
