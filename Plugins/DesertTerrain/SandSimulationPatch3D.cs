using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DesertTerrain;
/// <summary>Moving 32m soil domain, sampled at 25cm. Modified tiles remain cached as the vehicle travels.</summary>
public sealed class SandSimulationPatch3D:HeightfieldCollider3D
{
 float _debugTime;int _stamps,_changed;float _maxDepth;
 internal DesertTerrain3D Terrain=null!;public const int TileCells=32;const float Step=.25f;
 readonly Dictionary<(int,int),Tile> _tiles=[];Vector2 _minimum;public int WindowRevision{get;private set;}
 sealed class Tile{public readonly float[] Base=new float[1089],Delta=new float[1089],Packed=new float[1089];public readonly float[] Vertices=new float[1089*8];public readonly uint[] Indices=CreateIndices();public readonly byte[] ImprintPixels=new byte[33*33*4];public Texture2D? Imprint;public Material? Material;public Mesh? Mesh;public bool Dirty=true;}
 public override int Columns=>129;public override int Rows=>129;public override float CellSpacing=>Step;public override Vector2 GridMinimum=>_minimum;
 public override Vector3 Size{get=>new(32,80,32);set{}}
 public override BoundingBox3D LocalTerrainBounds=>new(new(_minimum.X,-32,_minimum.Y),new(_minimum.X+32,128,_minimum.Y+32));
 public bool ContainsLocal(float x,float z)=>x>=_minimum.X&&x<=_minimum.X+32&&z>=_minimum.Y&&z<=_minimum.Y+32;
 public void Focus(Vector3 world){Matrix4x4.Invert(Terrain.SurfaceMatrix,out var inverse);var p=Vector3.Transform(world,inverse);var min=new Vector2(MathF.Floor(p.X/8)*8-16,MathF.Floor(p.Z/8)*8-16);if(WindowRevision>0){
 // Keep a two-metre dead band around each eight-metre movement boundary.
 // This prevents resting suspension motion from rebuilding collision and GPU tiles.
 if(p.X>=_minimum.X+14&&p.X<_minimum.X+26)min.X=_minimum.X;
 if(p.Z>=_minimum.Y+14&&p.Z<_minimum.Y+26)min.Y=_minimum.Y;
 if(min==_minimum)return;
 }_minimum=min;WindowRevision++;foreach(var pair in _tiles)if(!Active(pair.Key)){pair.Value.Mesh?.Dispose();pair.Value.Mesh=null;pair.Value.Imprint?.Dispose();pair.Value.Imprint=null;}}
 bool Active((int x,int z) t)=>t.x*8>=_minimum.X&&t.x*8<_minimum.X+32&&t.z*8>=_minimum.Y&&t.z*8<_minimum.Y+32;
 Tile Get(int tx,int tz){if(_tiles.TryGetValue((tx,tz),out var t))return t;t=new();for(int z=0;z<=32;z++)for(int x=0;x<=32;x++){var p=new Vector3(tx*8+x*Step,0,tz*8+z*Step);Terrain.TrySampleLocal(p.X,p.Z,out float h,out _);t.Base[z*33+x]=h;t.Packed[z*33+x]=Terrain.SampleLocalCompaction(p.X,p.Z);}if(_tiles.Count>=512){var key=_tiles.Keys.FirstOrDefault(k=>!Active(k));if(_tiles.TryGetValue(key,out var old)&&!Active(key)){old.Mesh?.Dispose();old.Imprint?.Dispose();_tiles.Remove(key);}}_tiles[(tx,tz)]=t;return t;}
 (Tile tile,int index) Node(int gx,int gz){int tx=(int)MathF.Floor(gx/32f),tz=(int)MathF.Floor(gz/32f);return(Get(tx,tz),(gz-tz*32)*33+gx-tx*32);}
 public override float HeightAt(int x,int z){var n=Node((int)MathF.Round(_minimum.X/Step)+x,(int)MathF.Round(_minimum.Y/Step)+z);return n.tile.Base[n.index]+n.tile.Delta[n.index];}
 public bool Sample(Vector3 world,out SandSurface sand){sand=default;Matrix4x4.Invert(Terrain.SurfaceMatrix,out var inverse);var p=Vector3.Transform(world,inverse);if(!ContainsLocal(p.X,p.Z)||!TrySampleWorld(world,out var surface,out var normal))return false;var node=Node((int)MathF.Round(p.X/Step),(int)MathF.Round(p.Z/Step));float pack=node.tile.Packed[node.index];sand=new(surface,normal,pack,Terrain.LooseGrip+(Terrain.PackedGrip-Terrain.LooseGrip)*pack,Terrain.LooseResistance*(1-pack*.7f));return true;}
 void Set(int gx,int gz,float delta,float packed){var n=Node(gx,gz);n.tile.Delta[n.index]=delta;_maxDepth=Math.Max(_maxDepth,-delta);n.tile.Packed[n.index]=packed;n.tile.Dirty=true;
  // Shared boundary vertices must be identical in neighbouring render tiles.
  if(gx%32==0)Mirror(gx/32-1,(int)MathF.Floor(gz/32f),32,gz-(int)MathF.Floor(gz/32f)*32,delta,packed);
  if(gz%32==0)Mirror((int)MathF.Floor(gx/32f),gz/32-1,gx-(int)MathF.Floor(gx/32f)*32,32,delta,packed);
  if(gx%32==0&&gz%32==0)Mirror(gx/32-1,gz/32-1,32,32,delta,packed);
 }
 void Mirror(int tx,int tz,int x,int z,float delta,float packed){if(!_tiles.TryGetValue((tx,tz),out var tile))return;tile.Delta[z*33+x]=delta;tile.Packed[z*33+x]=packed;tile.Dirty=true;}
 public int Stamp(Vector3 from,Vector3 to,float width,float load,float slip,float dt){if(!float.IsFinite(width)||width<=0||!float.IsFinite(load)||!float.IsFinite(slip)||!float.IsFinite(dt)||dt<=0)return 0;if(!Sample(to,out var sand)||Math.Abs(to.Y-sand.Position.Y)>.35f)return 0;float distance=Vector3.Distance(from,to);if(distance>20||!float.IsFinite(distance)||(distance<.005f&&slip<.1f))return 0;Matrix4x4.Invert(Terrain.SurfaceMatrix,out var inverse);float radius=Math.Clamp(width*.5f,.15f,1);float pressure=Math.Clamp(load/(Math.Max(.07f,width*.3f)*22000),.05f,2);int steps=Math.Clamp((int)MathF.Ceiling(distance/.125f),1,64),changed=0;float shear=Math.Clamp(slip/8,0,1);
  for(int k=1;k<=steps;k++){var p=Vector3.Transform(Vector3.Lerp(from,to,k/(float)steps),inverse);int cx=(int)MathF.Round(p.X/Step),cz=(int)MathF.Round(p.Z/Step),range=(int)MathF.Ceiling(radius/Step)+1;float amount=pressure*(distance*.11f+dt*.016f+shear*dt*.10f)/steps;
   for(int z=cz-range;z<=cz+range;z++)for(int x=cx-range;x<=cx+range;x++){float r=new Vector2(x*Step-p.X,z*Step-p.Z).Length()/radius;if(r>1.7f)continue;var n=Node(x,z);float delta=n.tile.Delta[n.index],pack=n.tile.Packed[n.index];if(r<1){float w=(1-r*r)*(1-r*r);float target=-Math.Min(Terrain.MaximumRutDepth,Math.Clamp(Terrain.ContactRutDepth,.01f,.15f)*pressure*(1-pack*.65f)*(1+shear*.5f));float response=1-MathF.Exp(-(distance/steps+shear*dt/steps)*14);delta=Math.Min(delta,delta+(target*w-delta)*response);pack=Math.Min(1,pack+amount*w*2);}else delta+=amount*(.45f+shear*.25f)*(1-pack)*(1-(r-1)/.7f);Set(x,z,Math.Clamp(delta,-Math.Min(.18f,Terrain.MaximumRutDepth),.04f),pack);changed++;}
  }_stamps++;_changed+=changed;return changed;
 }
 static uint[] CreateIndices(){var indices=new uint[32*32*6];int n=0;for(int z=0;z<32;z++)for(int x=0;x<32;x++){uint a=(uint)(z*33+x),b=a+1,c=a+33,d=c+1;indices[n++]=a;indices[n++]=c;indices[n++]=b;indices[n++]=b;indices[n++]=c;indices[n++]=d;}return indices;}
 internal void RenderPatch(RenderContext context,Material material){
 _debugTime+=(float)ByteEngine.Core.Time.DeltaTime;if(_debugTime>=.5f){_debugTime=0;if(ByteEngine.Core.Diagnostics.SurfacePerformanceDiagnostics.SandEnabled)ByteEngine.Core.Diagnostics.SurfacePerformanceDiagnostics.RecordSand($"window={_minimum} revision={WindowRevision} cachedTiles={_tiles.Count} stamps={_stamps} modifiedNodes={_changed} deepestRut={_maxDepth:0.000}m shader={material.SandSurface} imprintTiles={_tiles.Count(t=>t.Value.Imprint!=null)}");_stamps=_changed=0;}
for(int tz=(int)(_minimum.Y/8);tz<(int)(_minimum.Y/8)+4;tz++)for(int tx=(int)(_minimum.X/8);tx<(int)(_minimum.X/8)+4;tx++){var tile=Get(tx,tz);if(tile.Mesh==null||tile.Dirty){var v=tile.Vertices;var indices=tile.Indices;int n=0;for(int z=0;z<=32;z++)for(int x=0;x<=32;x++){int i=z*33+x,j=i*8;float Height(int xx,int zz){if(xx>=0&&xx<=32&&zz>=0&&zz<=32){int node=zz*33+xx;return tile.Base[node]+tile.Delta[node];}var a=Node(tx*32+xx,tz*32+zz);return a.tile.Base[a.index]+a.tile.Delta[a.index];}var normal=Vector3.Normalize(new Vector3((Height(x-1,z)-Height(x+1,z))/.5f,1,(Height(x,z-1)-Height(x,z+1))/.5f));v[j]=tx*8+x*Step;v[j+1]=Height(x,z);v[j+2]=tz*8+z*Step;v[j+3]=normal.X;v[j+4]=normal.Y;v[j+5]=normal.Z;v[j+6]=v[j]/8;v[j+7]=v[j+2]/8;}
   for(int z=0;z<32;z++)for(int x=0;x<32;x++){uint a=(uint)(z*33+x),b=a+1,c=a+33,d=c+1;indices[n++]=a;indices[n++]=c;indices[n++]=b;indices[n++]=b;indices[n++]=c;indices[n++]=d;}if(tile.Mesh==null)tile.Mesh=new(v,indices,dynamicVertices:true);else tile.Mesh.UpdateVertices(v);
   for(int z=0;z<=32;z++)for(int x=0;x<=32;x++){int i=z*33+x,j=i*4;float Depth(int xx,int zz){var node=Node(tx*32+xx,tz*32+zz);return node.tile.Delta[node.index];}tile.ImprintPixels[j]=(byte)Math.Clamp(128+(Depth(x-1,z)-Depth(x+1,z))*384,0,255);tile.ImprintPixels[j+1]=(byte)Math.Clamp(128+(Depth(x,z-1)-Depth(x,z+1))*384,0,255);tile.ImprintPixels[j+2]=(byte)Math.Clamp(-tile.Delta[i]*255/.10f,0,255);tile.ImprintPixels[j+3]=(byte)Math.Clamp(Math.Abs(tile.Delta[i])*255/.018f,0,255);}
   if(tile.Imprint==null)tile.Imprint=Texture2D.FromPixels(33,33,tile.ImprintPixels);else tile.Imprint.UpdatePixels(tile.ImprintPixels);tile.Dirty=false;}tile.Material??=new Material();tile.Material.CopyFrom(material);tile.Material.SandImprintTexture=tile.Imprint;var origin=Vector3.Transform(new Vector3(tx*8,0,tz*8),Terrain.SurfaceMatrix);tile.Material.SandImprintOrigin=new(origin.X,origin.Z);context.RenderWorld.Submit(tile.Mesh,tile.Material,Terrain.SurfaceMatrix,castShadows:false,receiveShadows:true);}}
 internal void Write(BinaryWriter w){var values=new List<(int x,int z,float d,float p)>();foreach(var pair in _tiles)for(int z=0;z<32;z++)for(int x=0;x<32;x++){int i=z*33+x;if(Math.Abs(pair.Value.Delta[i])>.000001f)values.Add((pair.Key.Item1*32+x,pair.Key.Item2*32+z,pair.Value.Delta[i],pair.Value.Packed[i]));}w.Write(values.Count);foreach(var v in values){w.Write(v.x);w.Write(v.z);w.Write(v.d);w.Write(v.p);}}
 internal void Read(BinaryReader r){int count=r.ReadInt32();if(count<0||count>500000)throw new InvalidDataException("Fine sand state is too large");for(int i=0;i<count;i++){int x=r.ReadInt32(),z=r.ReadInt32();float d=r.ReadSingle(),p=r.ReadSingle();if(Math.Abs(x)>8192||Math.Abs(z)>8192||!float.IsFinite(d)||!float.IsFinite(p))throw new InvalidDataException("Invalid fine sand node");Set(x,z,Math.Clamp(d,-.18f,.04f),Math.Clamp(p,0,1));}}
 public void Reset(){foreach(var t in _tiles.Values){t.Mesh?.Dispose();t.Imprint?.Dispose();}_tiles.Clear();WindowRevision++;}
 protected override void OnDestroy(){Reset();}
}
