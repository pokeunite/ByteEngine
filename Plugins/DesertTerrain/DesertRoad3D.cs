using System.Numerics;
using System.Globalization;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace DesertTerrain;
/// <summary>An editable, texture-feathered ribbon. Collision stays on the same sculptable heightfield.</summary>
public sealed class DesertRoad3D : Component
{
 public string Points {get;set;}="0,0;0,-50";
 public float Width {get;set;}=18;
 public float EndTaperLength {get;set;}=12;
 public string AlbedoPath {get;set;}="Assets/DesertSlice/haul-road.png";
 public string NormalPath {get;set;}="Assets/DesertSlice/brown_mud_dry_normal.jpg";
 public string RoughnessPath {get;set;}="Assets/DesertSlice/brown_mud_dry_rough.jpg";
 readonly List<Mesh> _meshes=[];readonly Material _material=new(){BaseColor=new(1,.96f,.88f,1),Roughness=.96f,DecodeColorTexturesSrgb=true,NormalStrength=.18f,BlendMode=BlendMode3D.AlphaBlend};
 Texture2D? _diff,_normal,_rough;string _key="";int _revision=-1;long _nextRefresh;DesertTerrain3D? _terrain;
 public int MeshCount=>_meshes.Count;
 protected override void OnRender(RenderContext context){if(!context.Has3DCamera)return;_terrain??=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().FirstOrDefault();if(_terrain==null)return;
  var key=Points+Width+EndTaperLength+Transform.WorldPosition+AlbedoPath+NormalPath+RoughnessPath;
  if(key!=_key||_meshes.Count==0||(_revision!=_terrain.DeformationRevision&&Environment.TickCount64>_nextRefresh)){Rebuild(key);_nextRefresh=Environment.TickCount64+750;}
  foreach(var mesh in _meshes)context.RenderWorld.Submit(mesh,_material,Matrix4x4.Identity,castShadows:false,receiveShadows:true);
 }
 void Rebuild(string key){foreach(var m in _meshes)m.Dispose();_meshes.Clear();
  if(_key!=key){_diff?.Dispose();_normal?.Dispose();_rough?.Dispose();Texture2D? Load(string p){string f=Path.IsPathRooted(p)?p:Path.Combine(_terrain!.ProjectRoot??Environment.CurrentDirectory,p);return File.Exists(f)?new Texture2D(f,TextureFilter.Linear):null;}_diff=Load(AlbedoPath);_normal=Load(NormalPath);_rough=Load(RoughnessPath);_material.MainTexture=_diff;_material.NormalTexture=_normal;_material.RoughnessTexture=_rough;}
  var points=Points.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Split(',')).Select(v=>new Vector2(float.Parse(v[0],CultureInfo.InvariantCulture),float.Parse(v[1],CultureInfo.InvariantCulture))).ToArray();if(points.Length<2)return;
  Width=Math.Clamp(float.IsFinite(Width)?Width:18,2,128);int across=Math.Clamp((int)MathF.Ceiling(Width/2),12,64);float travelled=0;float total=0;for(int i=1;i<points.Length;i++)total+=Vector2.Distance(points[i],points[i-1]);
  for(int first=0;first<points.Length-1;first+=24){int last=Math.Min(first+24,points.Length-1);int count=last-first+1;var vertices=new float[count*(across+1)*8];var indices=new uint[(count-1)*across*6];int vi=0,ix=0;
   for(int row=first;row<=last;row++){if(row>first)travelled+=Vector2.Distance(points[row],points[row-1]);var direction=Vector2.Normalize(points[Math.Min(row+1,points.Length-1)]-points[Math.Max(0,row-1)]);var side=new Vector2(-direction.Y,direction.X);float endDistance=Math.Min(travelled,total-travelled);float taper=Math.Clamp(endDistance/Math.Max(.1f,EndTaperLength),.02f,1);taper=taper*taper*(3-2*taper);
    for(int col=0;col<=across;col++){float v=col/(float)across;var p=points[row]+side*((v-.5f)*Width*taper);var world=Transform.WorldPosition+new Vector3(p.X,0,p.Y);_terrain!.TryGetSand(world,out var sand);var position=sand.Position+Vector3.UnitY*.07f;var normal=sand.Normal;
     vertices[vi++]=position.X;vertices[vi++]=position.Y;vertices[vi++]=position.Z;vertices[vi++]=normal.X;vertices[vi++]=normal.Y;vertices[vi++]=normal.Z;vertices[vi++]=travelled/Width;vertices[vi++]=v;
    }
   }
   for(int row=0;row<count-1;row++)for(int col=0;col<across;col++){uint a=(uint)(row*(across+1)+col),b=a+1,c=a+(uint)across+1,d=c+1;indices[ix++]=a;indices[ix++]=c;indices[ix++]=b;indices[ix++]=b;indices[ix++]=c;indices[ix++]=d;}
   _meshes.Add(new Mesh(vertices,indices));
  }
  _key=key;_revision=_terrain!.DeformationRevision;
 }
 protected override void OnStop(){foreach(var m in _meshes)m.Dispose();_meshes.Clear();_diff?.Dispose();_normal?.Dispose();_rough?.Dispose();_diff=_normal=_rough=null;_key="";}
 protected override void OnDestroy(){foreach(var m in _meshes)m.Dispose();_diff?.Dispose();_normal?.Dispose();_rough?.Dispose();}
}
