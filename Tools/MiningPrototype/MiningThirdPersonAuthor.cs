using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningThirdPersonAuthor
{
 public static void Build(Scene scene,GameProjectRuntime runtime)
 {
  string backup=Program.Root+"/.byteengine/Backups/third-person-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(backup);File.Copy(Program.Root+"/Scenes/Main.bytescene",backup+"/Main.bytescene");
  var op=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningOperation3D>().Single();var player=scene.FindGameObject(op.PlayerObjectId)!;var equipment=player.GetComponent<FpsGunLab3D>()!;
  var camera=scene.FindGameObject(equipment.CameraObjectId)??scene.ActiveCamera!.GameObject;
  foreach(var o in scene.GameObjects)
  {if(o.GetComponent<Camera3D>() is {} cameraComponent)cameraComponent.ActiveGameCamera=false;foreach(var driver in o.Components.Where(c=>c is TopDownCamera3D or ThirdPersonCamera3D or FirstPersonCamera3D))driver.Enabled=false;}
  camera.SetParent(player,true);camera.Name="Third Person Gameplay Camera";var view=camera.GetComponent<Camera3D>()!;view.Enabled=true;view.ActiveGameCamera=true;view.Orthographic=false;view.FieldOfView=65;view.NearClip=.08f;view.FarClip=180;
  if(player.GetComponent<TopDownController3D>() is {} top)top.Enabled=false;
  var controller=player.GetComponent<PlayerController3D>()??player.AddComponent(new PlayerController3D());controller.Enabled=true;controller.AcceptLookInput=true;controller.UseLocalOrientation=false;controller.CharacterRotation=CharacterRotationMode.FaceCamera;controller.TurnSpeed=900;controller.SetControlRotation(0,8);
  controller.JumpAction=new(Guid.Empty,string.Empty); // Space remains the existing dash binding.
  var boom=player.GetComponent<CameraBoom3D>()??player.AddComponent(new CameraBoom3D());boom.Enabled=true;boom.CameraObjectId=camera.Id;boom.FirstPerson=false;boom.UseControlRotation=true;boom.ArmLength=3.8f;boom.PivotHeight=1.45f;boom.ShoulderOffset=.65f;boom.Pitch=8;boom.Yaw=0;boom.MinPitch=-40;boom.MaxPitch=65;boom.MouseSensitivityX=.12f;boom.MouseSensitivityY=.096f;boom.CameraLagEnabled=false;boom.RotationLagEnabled=false;boom.EnableCameraCollision=true;boom.CollisionRadius=.2f;
  boom.InvertVerticalLook=false;boom.InvertHorizontalLook=false;
  boom.MinPitch=-35;boom.MaxPitch=55;
  equipment.CameraObjectId=camera.Id;equipment.AimMode=WeaponAimMode.ThirdPerson;boom.SnapToSocket();
  var hud=scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningBusinessUi3D>().Single();
  var combat=player.GetComponent<MiningThirdPerson3D>()??player.AddComponent(new MiningThirdPerson3D());combat.InterfaceObjectId=hud.GameObject.Id;combat.NormalArmLength=2.8f;combat.AimArmLength=2.1f;combat.NormalFieldOfView=60;combat.AimFieldOfView=48;
  var defaults=System.Text.Json.JsonSerializer.Deserialize<CompanyUpgrade[]>(new MiningTrade3D().ResearchDefinitionsJson)!;
  foreach(var trade in scene.GameObjects.SelectMany(o=>o.Components).OfType<MiningTrade3D>())
  {
   var nodes=System.Text.Json.JsonSerializer.Deserialize<CompanyUpgrade[]>(trade.ResearchDefinitionsJson)??[];
   foreach(var node in nodes)if(defaults.FirstOrDefault(n=>n.Id==node.Id) is {} updated)
   {node.Name=updated.Name;node.Description=updated.Description;node.ReloadSpeedBonus=updated.ReloadSpeedBonus;node.MagazineBonus=updated.MagazineBonus;node.DashCooldownReduction=updated.DashCooldownReduction;node.AutoRefine=updated.AutoRefine;node.AutoCollectAmmo=updated.AutoCollectAmmo;}
   trade.ResearchDefinitionsJson=System.Text.Json.JsonSerializer.Serialize(nodes);trade.EfficiencyPerLevel=1;
  }
  if(player.GetComponent<DashAbility3D>() is {} dash)dash.DirectionMode=DashDirectionMode.MovementOrFacing;
  const string profile="Assets/Animations/SyntyDirectionalLocomotion.byteanim";
  File.Copy(Program.Root+"/"+profile,backup+"/SyntyDirectionalLocomotion.byteanim");
  var animation=AnimationProfileSerializer.Load(Program.Root+"/"+profile);animation.Procedural.AimEnabled=true;animation.Procedural.AimInModelSpace=true;animation.Procedural.AimBone="Spine_03";animation.Procedural.AimWeight=1;animation.Procedural.AimPitchLimit=60;animation.Procedural.AimSmoothing=14;AnimationProfileSerializer.Save(Program.Root+"/"+profile,animation);
  runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");Console.WriteLine("THIRD PERSON INSTALLED. Backup "+backup);
 }
}
