using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningThirdPersonVerify
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS third person: "+message);}
 public static void Run(Scene scene)
 {
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();op.PersistProgress=false;foreach(var t in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>())t.PersistProgress=false;
  var hash=System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene"));
  var host=new SceneManager();host.LoadScene(scene);var player=scene.FindGameObject(op.PlayerObjectId)!;var control=player.GetComponent<PlayerController3D>()!;var boom=player.GetComponent<CameraBoom3D>()!;var equipment=player.GetComponent<FpsGunLab3D>()!;var ui=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();var combat=player.GetComponent<MiningThirdPerson3D>()!;
  void Frame(Key? key=null,Vector2 look=default,MouseButton? mouse=null,Key? secondary=null)
  {var raw=new RawInputSnapshot{MouseDelta=look};if(key is {} k)raw.KeysDown.Add(k);if(secondary is {} second)raw.KeysDown.Add(second);if(mouse is {} m)raw.MouseButtonsDown.Add(m);typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);Input.SetGameViewPointer(new(.5f),new(1280,720),true);InputActions.Update(raw,true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[.05d]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);}
  Frame();var camera=scene.ActiveCamera!;Check(!camera.Orthographic&&camera.GameObject.Id==equipment.CameraObjectId,"one perspective gameplay camera is bound to the weapon");Check(control.Enabled&&player.GetComponent<TopDownController3D>()?.Enabled==false,"third-person controller owns movement, top-down input is disabled");Check(boom.Enabled&&boom.EnableCameraCollision&&!boom.FirstPerson,"collision-aware third-person boom is active");
  var start=player.Transform.WorldPosition;for(int i=0;i<12;i++)Frame(Key.W);Check(player.Transform.WorldPosition.Z<start.Z-1,"W moves forward in camera space");Frame();player.Transform.WorldPosition=new(0,0,4);control.SetControlRotation(90,8);start=player.Transform.WorldPosition;for(int i=0;i<12;i++)Frame(Key.W);Check(player.Transform.WorldPosition.X>start.X+1,"W follows a rotated camera rather than fixed map north");
  for(int i=0;i<10;i++)Frame();float yaw=control.ControlYaw;Frame(look:new(25,-10));Check(Math.Abs(PlayerController3D.DeltaAngle(yaw,control.ControlYaw))>1,"mouse movement changes camera/control yaw");Check(Vector3.Dot(camera.Transform.Forward,CameraBoom3D.CalculateViewForward(control.ControlYaw,control.ControlPitch))>.999f,"rendered camera follows current control rotation");
  for(int i=0;i<25;i++)Frame(mouse:MouseButton.Right);Check(combat.Aiming&&Math.Abs(camera.FieldOfView-combat.AimFieldOfView)<.1f&&Math.Abs(boom.ArmLength-combat.AimArmLength)<.1f,"right mouse aims with tighter field of view and shoulder distance");for(int i=0;i<25;i++)Frame();Check(!combat.Aiming&&Math.Abs(camera.FieldOfView-combat.NormalFieldOfView)<.1f,"releasing aim restores normal framing");
  player.Transform.WorldPosition=new(0,0,4);control.SetControlRotation(90,0);for(int i=0;i<10;i++)Frame();start=player.Transform.WorldPosition;Frame(Key.D,secondary:Key.Space);for(int i=0;i<5;i++)Frame();Check(!player.GetComponent<DashAbility3D>()!.Enabled&&player.GetComponent<DashAbility3D>()!.ActivationSequence==0,"Space no longer activates a dash");Check(player.Transform.WorldPosition.Y<start.Y+.3f,"Space keeps ground dash behavior instead of also jumping");
  player.Transform.WorldPosition=scene.FindGameObject(ui.TerminalObjectId)!.Transform.WorldPosition+new Vector3(0,0,-1);Frame();ui.Open(MiningScreen.Company);Check(!control.Enabled&&!player.GetComponent<DashAbility3D>()!.Enabled,"opening terminal disables third-person movement and dash");start=player.Transform.WorldPosition;yaw=control.ControlYaw;for(int i=0;i<12;i++)Frame(Key.W,new(20,5));Check(Vector3.Distance(start,player.Transform.WorldPosition)<.05f&&control.ControlYaw==yaw,"UI pointer movement and WASD do not move player or rotate camera");Check(Input.KeepGameViewPointerFree,"machine panels request a free mouse pointer");ui.Close();Frame();Check(control.Enabled&&!player.GetComponent<DashAbility3D>()!.Enabled,"closing terminal restores movement while leaving dash disabled");
  player.Transform.WorldPosition=new(0,0,4);control.SetControlRotation(0,0);for(int i=0;i<10;i++)Frame();
  var wall=scene.CreateGameObject("Verification camera obstacle");var pivot=player.Transform.WorldPosition+Vector3.UnitY*boom.PivotHeight;wall.Transform.WorldPosition=Vector3.Lerp(pivot,camera.Transform.WorldPosition,.5f);wall.AddComponent(new BoxCollider3D{Size=new(3,3,.3f)});boom.SnapToSocket();Check(boom.ActualArmLength<boom.ArmLength-.4f,"nearby solid wall pulls the shoulder camera forward");scene.DestroyGameObject(wall);Frame();boom.SnapToSocket();
  var target=scene.CreateGameObject("Verification crosshair target");target.Transform.WorldPosition=camera.Transform.WorldPosition+camera.Transform.Forward*12;target.AddComponent(new BoxCollider3D{Size=new(1.8f)});var health=target.AddComponent(new HealthComponent{MaxHealth=1000,CurrentHealth=1000});var weapon=equipment.SelectedWeapon!;var def=weapon.Definition!;def.SpreadDegrees=0;def.MovingInaccuracyDegrees=0;def.AccuracyDegrees=0;def.BloomPerShotDegrees=0;def.RecoilPitchPerShot=0;def.RecoilYawPerShot=0;def.ShotMode=WeaponShotMode.Hitscan;
  Check(equipment.AimMode==WeaponAimMode.ThirdPerson&&equipment.Fire()&&health.CurrentHealth<1000,"muzzle-converged hitscan hits the centre-camera target");float before=health.CurrentHealth;for(int i=0;i<20;i++)Frame();def.ShotMode=WeaponShotMode.Projectile;Check(equipment.Fire(),"third-person fire launches the configured projectile from the weapon");for(int i=0;i<50;i++)Frame();Check(health.CurrentHealth<before,"projectile reaches the crosshair target");
  scene.DestroyGameObject(target);
  Check(!boom.InvertVerticalLook,"authored TPS uses conventional vertical mouse look");
  float previousPitch=control.ControlPitch;Frame(look:new(0,-20));Check(control.ControlPitch<previousPitch,"mouse up looks up, rather than inherited inverted look");
  Quaternion? levelTorso=null;bool checkedTorso=false;
  foreach(float pitch in new[]{-35f,0f,35f,55f})
  {
   player.Transform.WorldPosition=new(12,0,10);control.SetControlRotation(35,pitch);for(int i=0;i<5;i++)Frame();boom.SnapToSocket();
   var point=camera.Transform.WorldPosition+camera.Transform.Forward*2;
   var clip=Vector4.Transform(new Vector4(point,1),camera.GetRenderViewMatrix()*camera.GetProjectionMatrix(1280f/720));
   Check(clip.W>0&&Math.Abs(clip.X/clip.W)<.001f&&Math.Abs(clip.Y/clip.W)<.001f,$"actual rendered projection keeps aim on crosshair at pitch {pitch}");
   foreach(var mesh in scene.GameObjects.Where(o=>o==player||o.IsDescendantOf(player)).SelectMany(o=>o.Components).OfType<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>())
   if(mesh.TryGetBoneWorldMatrix("Spine_03",out var torso))
   {
    Matrix4x4.Decompose(torso,out _,out var rotation,out _);
    if(pitch==0)levelTorso=rotation;
    if(pitch==35&&levelTorso is {} level){Check(Math.Abs(Quaternion.Dot(level,rotation))<.98f,"actual imported torso responds to vertical aiming");checkedTorso=true;}
   }
  }
  Check(checkedTorso,"Synty aim bone was resolved and evaluated");
  Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Program.Root+"/Scenes/Main.bytescene")).SequenceEqual(hash),"verification preserves the authored scene and company saves");host.UnloadScene();Console.WriteLine("THIRD PERSON NATIVE VERIFICATION COMPLETE");
 }
}
