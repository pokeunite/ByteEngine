using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly Dictionary<int,GameObject> _contactShadows=[];
 Texture2D? _contactShadowTexture;
 void RefreshContactShadows(){
  // The lightweight WebGL renderer has no native shadow maps. These bounded
  // contact decals ground vehicles without adding physics bodies or shadow passes.
  if(!OperatingSystem.IsBrowser()||Assets==null||_sand==null)return;
  _contactShadowTexture??=Assets.LoadTexture(new("Assets/Presentation/contact-shadow.png"));
  void Shadow(int id,Vector3 point,Vector2 size,float strength){
   if(!_contactShadows.TryGetValue(id,out var obj)){obj=GameObject.Scene!.CreateGameObject("Vehicle contact shade "+id);obj.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Plane,CastShadows=false,ReceiveShadows=false,Material=new(){MainTexture=_contactShadowTexture,BaseColor=new(0,0,0,strength),BlendMode=BlendMode3D.AlphaBlend,Shading=MaterialShadingMode.Unlit,DepthWriteMode=DepthWriteMode3D.Disabled}});_contactShadows[id]=obj;}
   if(!_sand.Sample(point,out var ground,out var normal,out _,out _)){obj.Active=false;return;}
   obj.Active=true;obj.Transform.WorldPosition=ground+normal*.018f;obj.Transform.WorldRotation=Quaternion.Normalize(FromTo(Vector3.UnitY,normal)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,_yaw));obj.Transform.LocalScale=new(size.X,1,size.Y);
   if(Building)obj.Transform.WorldPosition=new(ground.X,.062f,ground.Z);
   obj.GetComponent<MeshRenderer>()!.Material.BaseColor=new(0,0,0,strength*Math.Clamp(1-Math.Max(0,point.Y-ground.Y)*.12f,0,1));
  }
  foreach(var wheel in _blocks.Where(b=>_catalog[b.Type].Wheel).Take(8))if(_visuals.TryGetValue(wheel.Id,out var visual))Shadow(wheel.Id,visual.Transform.WorldPosition,new(_catalog[wheel.Type].Radius*2.2f),Building?.10f:.24f);
  Shadow(-1,Transform.WorldPosition+Vector3.Transform(Vector3.UnitZ,Transform.WorldRotation),new(2.1f,3.8f),Building?.12f:.20f);
  if(!Building&&_physics?.HasRecovery==true)Shadow(-2,RecoveryPosition,new(NoBrakesMission?2.3f:1.7f,NoBrakesMission?5.7f:2.7f),.22f);else if(_contactShadows.TryGetValue(-2,out var recovery))recovery.Active=false;
  if(!Building&&SprintMission&&_opponentParts.Count>0&&_physics!=null)Shadow(-3,_physics.AssemblyFrame(_opponentRoot).Position,new(2.1f,3.8f),.20f);else if(_contactShadows.TryGetValue(-3,out var rival))rival.Active=false;
  foreach(var id in _contactShadows.Keys.Where(id=>id>=0&&!_blocks.Any(b=>b.Id==id&&_catalog[b.Type].Wheel)).ToArray()){GameObject.Scene!.DestroyGameObject(_contactShadows[id]);_contactShadows.Remove(id);}
 }
 void ClearContactShadows(){foreach(var obj in _contactShadows.Values)if(obj.Scene is {} scene)scene.DestroyGameObject(obj);_contactShadows.Clear();_contactShadowTexture=null;}
}
