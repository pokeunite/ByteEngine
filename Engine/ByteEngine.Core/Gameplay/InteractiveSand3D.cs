using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Gameplay;
/// <summary>Fixed-domain persistent contact field. CPU sampling and GPU displacement share the same 12.5cm grid.</summary>
public sealed class InteractiveSand3D:HeightfieldCollider3D
{
 const int Cells=256,N=257;const float Step=.125f;
 readonly float[] _height=new float[N*N],_base=new float[N*N],_mask=new float[N*N];
 readonly byte[] _pixels=new byte[N*N*4];readonly List<Mesh> _meshes=[];readonly List<Mesh> _farMeshes=[];readonly List<Mesh> _midMeshes=[];
 float _debugTime;int _stampCount,_transferBytes;double _uploadMs;
 Texture2D? _field;Material? _material;bool _ready;int _x0=N,_z0=N,_x1=-1,_z1=-1;
 public float ContactDepth{get;set;}=.24f;
 public float ShoulderHeight{get;set;}=.075f;
 public float RippleHeight{get;set;}=.018f;
 public Vector4 SandColor{get;set;}=new(.79f,.62f,.39f,1);
 public int LastUploadBytes{get;private set;}
 public int UploadCount{get;private set;}
 public override int Columns=>N;public override int Rows=>N;public override float CellSpacing=>Step;
 public override Vector2 GridMinimum=>new(-16);
 public override Vector3 Size{get=>new(32,1,32);set{}}
 public override BoundingBox3D LocalTerrainBounds=>new(new(-16,-.4f,-16),new(16,.2f,16));
 public override float HeightAt(int x,int z){Ensure();return _height[z*N+x];}
 void Ensure(){if(_ready)return;_ready=true;ResetTracks();}
 public void ResetTracks(){_ready=true;for(int z=0;z<N;z++)for(int x=0;x<N;x++){
  float px=x*Step-16,pz=z*Step-16;int i=z*N+x;
  _height[i]=_base[i]=Math.Clamp(RippleHeight,0,.035f)*MathF.Sin(px*10+pz*3+MathF.Sin(pz*.7f));_mask[i]=0;
 }Dirty(0,0,Cells,Cells);}
 void Dirty(int x0,int z0,int x1,int z1){_x0=Math.Min(_x0,x0);_z0=Math.Min(_z0,z0);_x1=Math.Max(_x1,x1);_z1=Math.Max(_z1,z1);}
 public int Stamp(Vector3 from,Vector3 to,float radius,float pressure=1)
 {
  Ensure();if(!Matrix4x4.Invert(SurfaceMatrix,out var inv)||!float.IsFinite(radius)||radius<=0||!float.IsFinite(pressure)||pressure<=0)return 0;
  var a=Vector3.Transform(from,inv);var b=Vector3.Transform(to,inv);
  if(!float.IsFinite(a.X)||!float.IsFinite(a.Z)||!float.IsFinite(b.X)||!float.IsFinite(b.Z)||Vector3.DistanceSquared(a,b)>100)return 0;
  radius=Math.Clamp(radius,.12f,2);float outer=radius*1.65f;
  int x0=Math.Clamp((int)MathF.Floor((Math.Min(a.X,b.X)-outer+16)/Step),0,Cells),x1=Math.Clamp((int)MathF.Ceiling((Math.Max(a.X,b.X)+outer+16)/Step),0,Cells);
  int z0=Math.Clamp((int)MathF.Floor((Math.Min(a.Z,b.Z)-outer+16)/Step),0,Cells),z1=Math.Clamp((int)MathF.Ceiling((Math.Max(a.Z,b.Z)+outer+16)/Step),0,Cells);
  var av=new Vector2(a.X,a.Z);var ab=new Vector2(b.X-a.X,b.Z-a.Z);float length=ab.LengthSquared();int count=0;
  for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
   var p=new Vector2(x*Step-16,z*Step-16);float t=length>1e-8f?Math.Clamp(Vector2.Dot(p-av,ab)/length,0,1):0;
   float r=Vector2.Distance(p,av+ab*t)/radius;if(r>=1.65f)continue;int i=z*N+x;float old=_height[i];
   if(r<1){float shape=1-r*r;shape*=shape;float depth=Math.Clamp(ContactDepth*pressure,.01f,.32f);float target=-depth*shape;
    _height[i]=Math.Min(old,(_base[i]+(target-_base[i])*Math.Clamp(shape*2,0,1)));_mask[i]=Math.Max(_mask[i],Math.Clamp(shape*3,0,1));
   }else{float rim=MathF.Sin((r-1)/.65f*MathF.PI);float target=Math.Clamp(ShoulderHeight,0,.09f)*rim*rim;if(old>=_base[i]-.005f)_height[i]=Math.Max(old,_base[i]+target);_mask[i]=Math.Max(_mask[i],rim*.8f);}
   if(_height[i]!=old)count++;
  }
  if(count>0){_stampCount++;Dirty(x0,z0,x1,z1);}return count;
 }
 void Upload(){LastUploadBytes=0;if(_x1<_x0)return;long started=System.Diagnostics.Stopwatch.GetTimestamp();
  for(int z=_z0;z<=_z1;z++)for(int x=_x0;x<=_x1;x++){int i=z*N+x,j=i*4;int h=(int)Math.Clamp(MathF.Round((_height[i]+.4f)/.6f*65535),0,65535);_pixels[j]=(byte)(h>>8);_pixels[j+1]=(byte)h;_pixels[j+2]=(byte)Math.Clamp(-_height[i]/.24f*255,0,255);_pixels[j+3]=(byte)(_mask[i]*255);}
  if(_field==null){_field=Texture2D.FromPixels(N,N,_pixels);LastUploadBytes=_pixels.Length;}
  else{_field.UpdateRegion(_pixels,_x0,_z0,_x1-_x0+1,_z1-_z0+1);LastUploadBytes=(_x1-_x0+1)*(_z1-_z0+1)*4;}
  _transferBytes+=LastUploadBytes;_uploadMs=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;UploadCount++;_x0=_z0=N;_x1=_z1=-1;
 }
 void Build(){if(_meshes.Count>0)return;
  Mesh Tile(int tx,int tz,int step){int cells=64/step,n=cells+1;var v=new float[n*n*8];var ids=new uint[cells*cells*6];int k=0;
   for(int z=0;z<n;z++)for(int x=0;x<n;x++){int gx=tx*64+x*step,gz=tz*64+z*step,i=(z*n+x)*8;v[i]=gx*Step-16;v[i+4]=1;v[i+2]=gz*Step-16;v[i+6]=gx/(float)Cells;v[i+7]=gz/(float)Cells;}
   for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){uint a=(uint)(z*n+x),b=a+1,d=a+(uint)n,e=d+1;ids[k++]=a;ids[k++]=d;ids[k++]=b;ids[k++]=b;ids[k++]=d;ids[k++]=e;}
   return new Mesh(v,ids);
  }
  for(int tz=0;tz<4;tz++)for(int tx=0;tx<4;tx++){_meshes.Add(Tile(tx,tz,1));_midMeshes.Add(Tile(tx,tz,2));_farMeshes.Add(Tile(tx,tz,4));}
 }
 protected override void OnRender(RenderContext context){if(!context.Has3DCamera)return;Ensure();Build();Upload();_material??=new Material{Roughness=.94f,SandSurface=true};_material.BaseColor=SandColor;_material.HeightFieldTexture=_field;_material.HeightFieldRange=.6f;_material.HeightFieldBias=-.4f;_material.HeightFieldWorldSize=new(32.125f);_material.HeightFieldUvTransform=new(256f/257,256f/257,.5f/257,.5f/257);
  _debugTime+=(float)ByteEngine.Core.Time.DeltaTime;if(_debugTime>=.5f){_debugTime=0;if(ByteEngine.Core.Diagnostics.SurfacePerformanceDiagnostics.SandEnabled)ByteEngine.Core.Diagnostics.SurfacePerformanceDiagnostics.RecordSand($"mode=fixed-displacement field=257x257 spacing=0.125m contactStamps={_stampCount} uploadedBytes={_transferBytes} lastUploadCPU={_uploadMs:0.000}ms uploads={UploadCount} gpuHeightTexture={_field!=null}");_stampCount=_transferBytes=0;}
  var camera=context.RenderWorld.View?.CameraPosition??Vector3.Zero;
  for(int i=0;i<_meshes.Count;i++){var center=Vector3.Transform(_meshes[i].LocalBounds.Center,SurfaceMatrix);float distance=Vector3.Distance(camera,center);var mesh=distance>22?_farMeshes[i]:distance>13?_midMeshes[i]:_meshes[i];context.RenderWorld.Submit(mesh,_material,SurfaceMatrix,castShadows:false);}

 }
 protected override void OnDestroy(){foreach(var mesh in _meshes)mesh.Dispose();foreach(var mesh in _midMeshes)mesh.Dispose();foreach(var mesh in _farMeshes)mesh.Dispose();_field?.Dispose();}
}
