using System.Numerics;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;

// Test harness only; never installed in the game project.
internal sealed class PlayCheck:ByteEngineApplication
{
 const string Root="C:/Users/codex/Documents/ultra violence";
 GameProjectRuntime? runtime;int frame;string? capture;object? equipment;int ammo,shots,reserve;float health;Vector3 start;
 readonly List<string> checks=[];readonly string[] names=["Pistol","SPAS-12","AK-47"];
 public PlayCheck():base(1280,720,"Ultra Violence — Sketchfab verification"){}
 void Check(bool value,string message){if(!value)throw new Exception("FAIL "+message);checks.Add(message);Console.WriteLine("PASS "+message);}
 protected override void OnLoad(){base.OnLoad();IsVisible=false;runtime=new(Root+"/ultra violence.byteproject",Console.WriteLine);var scene=runtime.LoadStartupScene();foreach(var target in scene.GameObjects.Where(o=>o.Name.StartsWith("Practice target")))foreach(var module in target.Components.OfType<ByteEngine.Core.VisualLogic.EventModuleComponent>().ToArray())target.RemoveComponent(module);Scenes.LoadScene(scene);}
 object? Get(string name)=>equipment!.GetType().GetProperty(name)!.GetValue(equipment);
 protected override void OnEngineUpdate()
 {
  frame++;var scene=Scenes.ActiveScene!;
  equipment??=scene.GameObjects.SelectMany(o=>o.Components).Single(c=>c.GetType().Name=="FpsGunLab3D");
  var player=scene.GameObjects.Single(o=>o.Name=="FPS Character");
  int slot=Math.Min((frame-1)/600,2),tick=(frame-1)%600+1;
  var raw=new RawInputSnapshot();
  if(tick==5)raw.KeysDown.Add((Key)((int)Key.D1+slot));
  if(slot==2&&tick>=140&&tick<170||slot<2&&(tick==140||tick==155))raw.MouseButtonsDown.Add(MouseButton.Left);
  if(tick==210)raw.KeysDown.Add(Key.R);
  if(frame>=1760&&frame<1780)raw.KeysDown.Add(Key.W);
  if(frame==1790)raw.KeysDown.Add(Key.Space);
  if(frame>1800){raw=new();if(frame>=1805&&frame<1830)raw.MouseButtonsDown.Add(MouseButton.Left);if(frame==1850)raw.KeysDown.Add(Key.R);}
  typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);
  InputActions.Update(raw,true);Input.SetGameViewPointer(new(.5f),new(1280,720),true);
  typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1d/60]);
  var view=scene.FindGameObject(names[slot]+" view model")!.GetComponent<SkeletalMeshRenderer>()!;
  if(frame>1800)
  {
   if(frame==1801){shots=(int)Get("ShotsFired")!;foreach(var module in view.GameObject.Components.OfType<ByteEngine.Core.VisualLogic.EventModuleComponent>().ToArray())view.GameObject.RemoveComponent(module);view.GameObject.GetComponent<ByteEngine.Core.Animation.AnimationController>()!.CancelCurrentAction();}
   if(frame==1840)Check((int)Get("ShotsFired")!>shots&&view.CurrentAnimation.EndsWith("_Idle"),"Removing presentation events stops fire animation while shooting still works");
   if(frame==1860)Check((bool)Get("Reloading")!&&view.CurrentAnimation.EndsWith("_Idle"),"Removing presentation events stops reload animation while reload logic still works");
   if(frame==1870){File.WriteAllText(Root+"/Preview/Gameplay checks.txt",string.Join(Environment.NewLine,checks));Close();}
   return;
  }
  if(frame==40){Check(player.GetComponent<CharacterController3D>()!.IsGrounded,"Native FPS is grounded");Check(scene.GameObjects.Count(o=>o.Components.Any(c=>c.GetType().Name=="WeaponInstance3D"))==3,"Exactly three Sketchfab weapons");start=player.Transform.WorldPosition;}
  if(tick==120)
  {
   Check((int)Get("SelectedSlot")! ==slot,"Equips "+names[slot]);
   Check(view.ModelLoaded&&view.BoneNames.Count>100,"Both source skins loaded: "+names[slot]);
   Check(view.CurrentAnimation.EndsWith("_Idle"),"Returns to embedded idle: "+names[slot]);
   Check(view.TryGetCurrentModelBounds(out var b)&&b.IsValid&&b.Size.Length()<5,"Finite metre-scale skinned bounds: "+names[slot]);
   Console.WriteLine(names[slot]+" BOUNDS "+b);
   ammo=(int)Get("Ammo")!;shots=(int)Get("ShotsFired")!;reserve=(int)Get("ReserveAmmo")!;
   health=scene.FindGameObject("Practice target 2")!.GetComponent<ByteEngine.Core.Gameplay.HealthComponent>()!.CurrentHealth;
   capture=names[slot]+" Idle";
  }
  if(tick==145){Check(view.CurrentAnimation.EndsWith("_Fire"),"Embedded firing animation: "+names[slot]);capture=names[slot]+" Fire";}
  if(tick==190){Check((int)Get("Ammo")!<ammo,"Ammo consumed: "+names[slot]);Check((int)Get("ShotsFired")!>shots,"Shot fired: "+names[slot]);Check(scene.FindGameObject("Practice target 2")!.GetComponent<ByteEngine.Core.Gameplay.HealthComponent>()!.CurrentHealth<health,"Hitscan damages target: "+names[slot]);Check(view.GameObject.Variables.TryGet("ShotHit",out var didHit)&&didHit!.Boolean,"Event sheet saved the actual weapon hit: "+names[slot]);Check(view.GameObject.Variables.TryGet("ShotObjectId",out var hitId)&&hitId!.String==scene.FindGameObject("Practice target 2")!.Id.ToString(),"Last Ray Hit identifies the shot target: "+names[slot]);if(slot==2)Check((int)Get("ShotsFired")!-shots>=3,"AK automatic fire repeats");}
  if(tick==250){Check((bool)Get("Reloading")!&&view.CurrentAnimation.EndsWith("_Reload"),"Reload plays embedded source animation: "+names[slot]);capture=names[slot]+" Reload";}
  if(tick==530){Check(!(bool)Get("Reloading")!&&(int)Get("Ammo")! ==ammo&&(int)Get("ReserveAmmo")!<reserve,"Reload transfers finite reserve: "+names[slot]);Check(view.CurrentAnimation.EndsWith("_Idle"),"Reload returns to idle: "+names[slot]);}
  if(frame==1785)Check(Vector3.Distance(start,player.Transform.WorldPosition)>.5f,"Native FPS movement");
  if(frame==1798)Check(player.Transform.WorldPosition.Y>.4f,"Native FPS jump");
 }
 protected override void OnEngineRender()
 {
  if(capture==null)return;Directory.CreateDirectory(Root+"/Preview");
  var bytes=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,OpenTK.Graphics.OpenGL4.PixelFormat.Bgra,PixelType.UnsignedByte,bytes);
  using var bmp=new Bitmap(WindowWidth,WindowHeight,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
  var data=bmp.LockBits(new Rectangle(0,0,bmp.Width,bmp.Height),ImageLockMode.WriteOnly,bmp.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(bytes,0,data.Scan0,bytes.Length);bmp.UnlockBits(data);bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);bmp.Save(Root+"/Preview/"+capture+".png");capture=null;
 }
 protected override void OnUnload(){Scenes.UnloadScene();runtime?.Dispose();base.OnUnload();}
}
