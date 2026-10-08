using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Core.Gameplay;
/// <summary>Fixed-domain persistent contact field. CPU sampling and GPU displacement share the same 12.5cm grid.</summary>
public sealed class InteractiveSand3D:HeightfieldCollider3D
{
 const float Step=.125f;
 int Cells=256,N=257;
 const int RenderTileCells=128;
 const int UploadTileSamples=64;
 readonly Dictionary<int,(int X0,int Z0,int X1,int Z1)> _dirtyTiles=[];readonly Queue<int> _uploadQueue=[];bool _fullUpload;
 readonly Dictionary<int,(float Touch,float Visit)> _weather=[];readonly List<int> _activeWeather=[];int _windCursor;float _windTime;
 public float WindDelaySeconds{get;set;}=12;
 public float WindFillSeconds{get;set;}=30;
 float _surfaceWidth=32;
 float Half=>_surfaceWidth*.5f;
 public float SurfaceWidth { get=>_surfaceWidth; set { float width=float.IsFinite(value)?Math.Clamp(MathF.Round(value/32)*32,32,256):32;if(width==_surfaceWidth)return;_surfaceWidth=width;Cells=(int)(width/Step);N=Cells+1;_ready=false;DisposeMeshes();_field?.Dispose();_field=null;_dirtyTiles.Clear();_uploadQueue.Clear(); } }
 float[] _height=[],_base=[],_mask=[];
 byte[] _pixels=[];
 readonly Dictionary<(int x,int z,int step),Mesh> _tiles=[];
 void DisposeMeshes(){foreach(var mesh in _tiles.Values)mesh.Dispose();_tiles.Clear();}
 float _debugTime;int _stampCount,_transferBytes;double _uploadMs;
 Texture2D? _field;Material? _material;bool _ready;
 public Vector2 AuthoredDepressionCenter{get;set;}=Vector2.Zero;
 public float AuthoredDepressionDepth{get;set;}
 public float AuthoredDepressionRadius{get;set;}=5;
 public float ContactDepth{get;set;}=.24f;
 public float ShoulderHeight{get;set;}=.075f;
 public float RippleHeight{get;set;}=.018f;
 public Vector4 SandColor{get;set;}=new(.79f,.62f,.39f,1);
 /// <summary>Borrow a terrain-owned material; displacement stays owned by this surface.</summary>
 public void SetSurfaceMaterial(Material material){_material=material;}
 public int LastUploadBytes{get;private set;}
 public int UploadCount{get;private set;}
 public override int Columns=>N;public override int Rows=>N;public override float CellSpacing=>Step;
 public override Vector2 GridMinimum=>new(-Half);
 public override Vector3 Size{get=>new(_surfaceWidth,1,_surfaceWidth);set{}}
 public override BoundingBox3D LocalTerrainBounds=>new(new(-Half,-3f,-Half),new(Half,.2f,Half));
 public override float HeightAt(int x,int z){Ensure();return _height[z*N+x];}
 void Ensure(){if(_ready)return;_ready=true;ResetTracks();}
 public void ResetTracks(){_weather.Clear();_activeWeather.Clear();_windCursor=0;_windTime=0;_fullUpload=true;_dirtyTiles.Clear();_uploadQueue.Clear();if(_height.Length!=N*N){_height=new float[N*N];_base=new float[N*N];_mask=new float[N*N];_pixels=new byte[N*N*4];}_ready=true;for(int z=0;z<N;z++)for(int x=0;x<N;x++){
  float px=x*Step-Half,pz=z*Step-Half;int i=z*N+x;
  _height[i]=_base[i]=Math.Clamp(RippleHeight,0,.035f)*MathF.Sin(px*10+pz*3+MathF.Sin(pz*.7f));float distance=Vector2.Distance(new(px,pz),AuthoredDepressionCenter);float bowl=Math.Clamp(1-distance/Math.Max(1,AuthoredDepressionRadius),0,1);_height[i]=_base[i]-Math.Clamp(AuthoredDepressionDepth,0,2.5f)*bowl*bowl*(3-2*bowl);_base[i]=_height[i];_mask[i]=0;
 }Dirty(0,0,Cells,Cells);}
 void Dirty(int x0,int z0,int x1,int z1){int stride=(N+UploadTileSamples-1)/UploadTileSamples;for(int z=z0/UploadTileSamples;z<=z1/UploadTileSamples;z++)for(int x=x0/UploadTileSamples;x<=x1/UploadTileSamples;x++){
  int key=z*stride+x;int lowX=Math.Max(x0,x*UploadTileSamples),lowZ=Math.Max(z0,z*UploadTileSamples),highX=Math.Min(x1,(x+1)*UploadTileSamples-1),highZ=Math.Min(z1,(z+1)*UploadTileSamples-1);
  if(_dirtyTiles.TryGetValue(key,out var previous))_dirtyTiles[key]=(Math.Min(lowX,previous.X0),Math.Min(lowZ,previous.Z0),Math.Max(highX,previous.X1),Math.Max(highZ,previous.Z1));
  else {_dirtyTiles[key]=(lowX,lowZ,highX,highZ);_uploadQueue.Enqueue(key);}
 }}
 protected override void OnUpdate()=>AdvanceWind(Math.Min((float)ByteEngine.Core.Time.DeltaTime,.1f));
 public void AdvanceWind(float dt)
 {
  if(!float.IsFinite(dt)||dt<=0||WindFillSeconds<=0)return;_windTime+=dt;if(_activeWeather.Count==0)return;
  int budget=Math.Min(1024,_activeWeather.Count);float hold=Math.Max(0,WindDelaySeconds),duration=Math.Max(.1f,WindFillSeconds);
  for(int visit=0;visit<budget&&_activeWeather.Count>0;visit++){
   if(_windCursor>=_activeWeather.Count)_windCursor=0;int i=_activeWeather[_windCursor];var age=_weather[i];
   float previous=Math.Clamp((age.Visit-age.Touch-hold)/duration,0,1),progress=Math.Clamp((_windTime-age.Touch-hold)/duration,0,1);
   if(progress>previous){float ratio=(1-progress)/Math.Max(1e-6f,1-previous);_height[i]=_base[i]+(_height[i]-_base[i])*ratio;_mask[i]*=ratio;Dirty(i%N,i/N,i%N,i/N);}
   if(progress>=1){_height[i]=_base[i];_mask[i]=0;_weather.Remove(i);_activeWeather[_windCursor]=_activeWeather[^1];_activeWeather.RemoveAt(_activeWeather.Count-1);}
   else {_weather[i]=(age.Touch,_windTime);_windCursor++;}
  }
 }
 public int Stamp(Vector3 from,Vector3 to,float radius,float pressure=1)
 {
  Ensure();if(!Matrix4x4.Invert(SurfaceMatrix,out var inv)||!float.IsFinite(radius)||radius<=0||!float.IsFinite(pressure)||pressure<=0)return 0;
  var a=Vector3.Transform(from,inv);var b=Vector3.Transform(to,inv);
  if(!float.IsFinite(a.X)||!float.IsFinite(a.Z)||!float.IsFinite(b.X)||!float.IsFinite(b.Z)||Vector3.DistanceSquared(a,b)>100)return 0;
  radius=Math.Clamp(radius,.12f,2);float outer=radius*1.65f;
  int x0=Math.Clamp((int)MathF.Floor((Math.Min(a.X,b.X)-outer+Half)/Step),0,Cells),x1=Math.Clamp((int)MathF.Ceiling((Math.Max(a.X,b.X)+outer+Half)/Step),0,Cells);
  int z0=Math.Clamp((int)MathF.Floor((Math.Min(a.Z,b.Z)-outer+Half)/Step),0,Cells),z1=Math.Clamp((int)MathF.Ceiling((Math.Max(a.Z,b.Z)+outer+Half)/Step),0,Cells);
  var av=new Vector2(a.X,a.Z);var ab=new Vector2(b.X-a.X,b.Z-a.Z);float length=ab.LengthSquared();int count=0;
  for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
   var p=new Vector2(x*Step-Half,z*Step-Half);float t=length>1e-8f?Math.Clamp(Vector2.Dot(p-av,ab)/length,0,1):0;
   float r=Vector2.Distance(p,av+ab*t)/radius;if(r>=1.65f)continue;int i=z*N+x;float old=_height[i];
   if(r<1){float shape=1-r*r;shape*=shape;float depth=Math.Clamp(ContactDepth*pressure,.01f,.32f);float target=-depth*shape;
    _height[i]=Math.Min(old,(_base[i]+(target-_base[i])*Math.Clamp(shape*2,0,1)));_mask[i]=Math.Max(_mask[i],Math.Clamp(shape*3,0,1));
   }else{float rim=MathF.Sin((r-1)/.65f*MathF.PI);float target=Math.Clamp(ShoulderHeight,0,.09f)*rim*rim;if(old>=_base[i]-.005f)_height[i]=Math.Max(old,_base[i]+target);_mask[i]=Math.Max(_mask[i],rim*.8f);}
   if(!_weather.ContainsKey(i))_activeWeather.Add(i);_weather[i]=(_windTime,_windTime);
   if(_height[i]!=old)count++;
  }
  if(count>0){_stampCount++;Dirty(x0,z0,x1,z1);}return count;
 }
 void Encode(int x0,int z0,int x1,int z1){for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){int i=z*N+x,j=i*4;int h=(int)Math.Clamp(MathF.Round(((_height[i]+3f)/4f)*65535),0,65535);_pixels[j]=(byte)(h>>8);_pixels[j+1]=(byte)h;_pixels[j+2]=(byte)Math.Clamp((_base[i]-_height[i])/.24f*255,0,255);_pixels[j+3]=(byte)(_mask[i]*255);}}
 void Upload(){LastUploadBytes=0;if(!_fullUpload&&_uploadQueue.Count==0)return;long started=System.Diagnostics.Stopwatch.GetTimestamp();
  if(_fullUpload||_field==null){Encode(0,0,Cells,Cells);if(_field==null)_field=Texture2D.FromPixels(N,N,_pixels);else _field.UpdateRegion(_pixels,0,0,N,N);LastUploadBytes=_pixels.Length;_fullUpload=false;_dirtyTiles.Clear();_uploadQueue.Clear();}
  else {int stride=(N+UploadTileSamples-1)/UploadTileSamples;for(int tile=0;tile<8&&_uploadQueue.Count>0;tile++){int key=_uploadQueue.Dequeue();var rect=_dirtyTiles[key];_dirtyTiles.Remove(key);int x0=rect.X0,z0=rect.Z0,x1=rect.X1,z1=rect.Z1;Encode(x0,z0,x1,z1);_field.UpdateRegion(_pixels,x0,z0,x1-x0+1,z1-z0+1);LastUploadBytes+=(x1-x0+1)*(z1-z0+1)*4;}}
  _transferBytes+=LastUploadBytes;_uploadMs=System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;UploadCount++;
 }
 Mesh Tile(int tx,int tz,int step){var key=(tx,tz,step);if(_tiles.TryGetValue(key,out var existing))return existing;
  const int tileCells=RenderTileCells;int cells=tileCells/step,n=cells+1;var v=new float[n*n*8];var ids=new uint[cells*cells*6];int k=0;
  for(int z=0;z<n;z++)for(int x=0;x<n;x++){int gx=tx*tileCells+x*step,gz=tz*tileCells+z*step,i=(z*n+x)*8;v[i]=gx*Step-Half;v[i+4]=1;v[i+2]=gz*Step-Half;v[i+6]=gx/(float)Cells;v[i+7]=gz/(float)Cells;}
  for(int z=0;z<cells;z++)for(int x=0;x<cells;x++){uint a=(uint)(z*n+x),b=a+1,d=a+(uint)n,e=d+1;ids[k++]=a;ids[k++]=d;ids[k++]=b;ids[k++]=b;ids[k++]=d;ids[k++]=e;}
  // Vertical skirts hide cracks where adjacent tiles use different displacement LODs.
  int original=v.Length/8;Array.Resize(ref v,v.Length+4*n*8);Array.Resize(ref ids,ids.Length+4*cells*12);
  for(int edge=0;edge<4;edge++)for(int j=0;j<n;j++){
   int top=edge==0?j:edge==1?cells*n+j:edge==2?j*n:j*n+cells,down=original+edge*n+j;
   Array.Copy(v,top*8,v,down*8,8);v[down*8+1]=-.4f;
   if(j==cells)continue;int next=edge<2?top+1:top+n;uint a=(uint)top,b=(uint)next,c=(uint)down,d=(uint)(down+1);
   ids[k++]=a;ids[k++]=c;ids[k++]=b;ids[k++]=b;ids[k++]=c;ids[k++]=d;
   ids[k++]=a;ids[k++]=b;ids[k++]=c;ids[k++]=b;ids[k++]=d;ids[k++]=c;
  }
  var mesh=new Mesh(v,ids);_tiles[key]=mesh;return mesh;
 }
 protected override void OnRender(RenderContext context){if(!context.Has3DCamera)return;Ensure();Upload();_material??=new Material{Roughness=.94f,SandSurface=true};_material.BaseColor=SandColor;_material.HeightFieldTexture=_field;_material.HeightFieldRange=4f;_material.HeightFieldBias=-3f;_material.HeightFieldWorldSize=new(_surfaceWidth+Step);_material.HeightFieldUvTransform=new(Cells/(float)N,Cells/(float)N,.5f/N,.5f/N);
  _debugTime+=(float)ByteEngine.Core.Time.DeltaTime;if(_debugTime>=.5f){_debugTime=0;if(ByteEngine.Core.Diagnostics.SurfacePerformanceDiagnostics.SandEnabled)ByteEngine.Core.Diagnostics.SurfacePerformanceDiagnostics.RecordSand($"mode=fixed-displacement field={N}x{N} spacing={Step:0.###}m contactStamps={_stampCount} uploadedBytes={_transferBytes} lastUploadCPU={_uploadMs:0.000}ms uploads={UploadCount} gpuHeightTexture={_field!=null}");_stampCount=_transferBytes=0;}
  var camera=context.RenderWorld.View?.CameraPosition??Vector3.Zero;
  float tileSize=RenderTileCells*Step;
  for(int tz=0;tz<Cells/RenderTileCells;tz++)for(int tx=0;tx<Cells/RenderTileCells;tx++){
   var localCenter=new Vector3(tx*tileSize-Half+tileSize*.5f,0,tz*tileSize-Half+tileSize*.5f);
   var bounds=new BoundingBox3D(localCenter-new Vector3(tileSize*.5f,3f,tileSize*.5f),localCenter+new Vector3(tileSize*.5f,.2f,tileSize*.5f)).Transform(SurfaceMatrix);
   if(context.RenderWorld.View is {} view&&!view.Frustum.Intersects(bounds))continue;
   var center=Vector3.Transform(localCenter,SurfaceMatrix);float distance=Vector2.Distance(new(camera.X,camera.Z),new(center.X,center.Z));
   int step=distance<21?(OperatingSystem.IsBrowser()?2:1):distance<65?4:16;
   context.RenderWorld.Submit(Tile(tx,tz,step),_material,SurfaceMatrix,castShadows:false);
  }
  // Bound dense mesh memory as the camera travels around the map.
  foreach(var key in _tiles.Keys.Where(k=>k.step==1&&Vector2.Distance(new(camera.X,camera.Z),new(k.x*tileSize-Half+tileSize*.5f,k.z*tileSize-Half+tileSize*.5f))>40).ToArray()){_tiles[key].Dispose();_tiles.Remove(key);}


 }
 protected override void OnDestroy(){DisposeMeshes();_field?.Dispose();}
}
