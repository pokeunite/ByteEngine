using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Security.Cryptography;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;

internal static class MiningExplorerCharacterCheck
{
 public static void Run(Scene scene,MiningOperation3D op,GameProjectRuntime runtime,bool fitSocket=false)
 {
  var checks=new List<string>();
  void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS EXPLORER "+message);}
  var hash=SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  var host=new SceneManager();host.LoadScene(scene);
  void Frame(){typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[.02d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);}
  for(int i=0;i<3;i++)Frame();
  var player=scene.FindGameObject(op.PlayerObjectId)!;
  bool Under(GameObject o,GameObject root){for(GameObject? p=o;p!=null;p=p.Parent)if(p==root)return true;return false;}
  var body=scene.GameObjects.Where(o=>Under(o,player)).SelectMany(o=>o.Components.OfType<SkeletalMeshRenderer>()).Single(r=>r.ResolvedModel?.Name=="Explorer"||r.ResolvedModel?.Meshes.Any(m=>m.Name.Contains("Explorer"))==true);
  var model=body.ResolvedModel!;
  Check(model.Skeleton?.Bones.Count==51&&model.Animations.Count==76,"fitted 51-bone rig and 76 animations load in the real runtime");
  var gun=player.GetComponent<FpsGunLab3D>()!;
  Check(gun.SelectedWeapon?.Definition?.DisplayName=="Pistol","default pistol remains carried");
  Check(gun.RefreshWeaponAttachment()&&gun.WeaponAttachmentError.Length==0,"PistolGrip resolves on the new explorer");
  var weapon=gun.SelectedWeapon!;
  var controller=player.GetComponent<AnimationController>()!;controller.Enabled=false;
  player.GetComponent<PlayerController3D>()!.Enabled=false;player.GetComponent<CharacterController3D>()!.Enabled=false;player.GetComponent<MiningThirdPerson3D>()!.Enabled=false;
  player.Transform.WorldPosition=Vector3.Zero;player.Transform.WorldRotation=Quaternion.Identity;
  body.ConfigurePoseProfile(null);body.SetBlendSpace(null,0,0,0);body.ClearActionOverlay();body.SetAim(0,0);
  if(fitSocket)
  {
   body.Play("PolygonSyntyCharacter_SHStandIdle",true,0);body.Seek(0);
   body.TryGetBoneModelMatrix("Hand_R",out var handFrame);
   body.TryGetBoneModelMatrix("IndexFinger_01_1",out var indexFrame);
   Check(SkeletalSocketResolver.TryExtractStableBoneFrame(handFrame,out var origin,out var rotation,out _),"stable hand frame is available for fitting");
   var socket=model.FindSocket("PistolGrip")!;
   var offsetRotation=Quaternion.CreateFromYawPitchRoll(socket.RotationOffsetDegrees.Y*MathF.PI/180,socket.RotationOffsetDegrees.X*MathF.PI/180,socket.RotationOffsetDegrees.Z*MathF.PI/180);
   var palm=Vector3.Lerp(origin,indexFrame.Translation,.60f);
   var modelGrip=new Vector3(0,-.020f,.035f);
   socket.Scale=Vector3.One;
   socket.PositionOffset=Vector3.Transform(palm-origin,Quaternion.Inverse(rotation))-Vector3.Transform(modelGrip,offsetRotation);
   ModelSocketMetadataStore.Save(Program.Root,model.Guid,model.Sockets);
   Console.WriteLine($"FITTED PISTOL {socket.PositionOffset} scale {socket.Scale}");
   gun.RefreshWeaponAttachment();
  }
  var output=new List<object>();
  object Geometry(Mesh mesh,Matrix4x4 matrix,Vector4 color)
  {
   var data=mesh.VertexData.Span;var pts=new float[data.Length/8*3];
   for(int i=0;i<data.Length/8;i++){var p=Vector3.Transform(new(data[i*8],data[i*8+1],data[i*8+2]),matrix);pts[i*3]=p.X;pts[i*3+1]=p.Y;pts[i*3+2]=p.Z;}
   return new{positions=pts,indices=mesh.IndexData.ToArray(),color=new[]{color.X,color.Y,color.Z,color.W}};
  }
  foreach(string suffix in new[]{"SHStandIdle","SHWalk","SHRun","SHStandShoot","SHStandReload","DHStandIdle","DHStandShoot"})
  {
   string clip="PolygonSyntyCharacter_"+suffix;var animation=model.Animations.Single(a=>a.Name==clip);
   Check(body.Play(clip,true,0),"clip resolves: "+suffix);
   foreach(float phase in new[]{0f,.25f,.5f,.75f})
   {
    body.Seek(animation.Duration*phase);
    Check(body.TryGetCurrentModelBounds(out var bounds)&&bounds.Maximum.Y-bounds.Minimum.Y>.5f&&bounds.Maximum.Y-bounds.Minimum.Y<3,"finite posed bounds: "+suffix+" "+phase);
    Check(SkeletalSocketResolver.TryGetSocketModelTransform(body,"PistolGrip",out var socket),"animated grip resolves: "+suffix+" "+phase);
    Check(body.TryGetBoneModelMatrix("Hand_R",out var hand)&&Vector3.Distance(socket.Position,hand.Translation)<.5f,"pistol anchor remains near its animated hand");
    gun.RefreshWeaponAttachment();
    Check(weapon.TryGetMuzzleWorldPosition(out var muzzle)&&float.IsFinite(muzzle.X+muzzle.Y+muzzle.Z),"muzzle remains usable: "+suffix+" "+phase);
   }
   body.Seek(animation.Duration*(suffix=="SHStandReload"?.55f:.25f));gun.RefreshWeaponAttachment();
   var parts=new List<object>();int materialIndex=0;
   foreach(var posed in body.CapturePoseMeshes())
   {
    Vector4 color=model.Materials[Math.Min(materialIndex++,model.Materials.Count-1)].BaseColor;
    parts.Add(Geometry(posed.Mesh,posed.World,color));posed.Mesh.Dispose();
   }
   var gunParts=new List<object>();
   foreach(var r in scene.GameObjects.Where(o=>Under(o,weapon.GameObject)).SelectMany(o=>o.Components.OfType<MeshRenderer>()))
   {
    var mesh=r.Mesh??(r.MeshReference is {} mr?runtime.Assets.GetModelMesh(mr.Model,mr.SubAssetKey):null);
    if(mesh!=null)gunParts.Add(Geometry(mesh,r.Transform.WorldMatrix,new(.07f,.08f,.09f,1)));
   }
   body.TryGetBoneModelMatrix("Hand_R",out var bone);
   output.Add(new{clip=suffix,parts,gunParts,hand=new[]{bone.Translation.X,bone.Translation.Y,bone.Translation.Z}});
  }
  Directory.CreateDirectory("artifacts/explorer-native");
  File.WriteAllText("artifacts/explorer-native/poses.json",JsonSerializer.Serialize(output));
  File.WriteAllText("artifacts/explorer-native/checks.json",JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
  Check(SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"verification preserves the authored scene and saved progress");
  host.UnloadScene();Console.WriteLine("EXPLORER NATIVE CHECKS COMPLETE "+checks.Count);
 }
}
