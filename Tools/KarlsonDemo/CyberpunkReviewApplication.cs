using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using KarlsonDemo.Plugin;
using OpenTK.Graphics.OpenGL4;
namespace KarlsonDemoTool;
internal sealed class CyberpunkReviewApplication : ByteEngineApplication
{
    DemoRuntime? _runtime;int _frame;string? _capture;readonly List<string> _checks=[];Vector3 _slide,_pump,_origin;float _health;
    readonly bool _posesOnly;
    public CyberpunkReviewApplication(bool posesOnly=false):base(1280,720,"Cyberpunk weapons - native verification"){_posesOnly=posesOnly;}
    protected override bool CloseOnEscape=>false;
    protected override void OnLoad(){base.OnLoad();IsVisible=false;_runtime=new();InputActions.Configure(new ProjectSerializer().Load(Program.Root+"/karlson demo.byteproject").InputMap);Scenes.LoadScene(_runtime.Serializer.Load(Program.Root+"/Scenes/Main.bytescene"));}
    void Check(bool ok,string message){if(!ok)throw new Exception("CYBER FAIL "+message);_checks.Add(message);Console.WriteLine("PASS "+message);}
    SkeletalMeshRenderer View(int slot)=>Scenes.ActiveScene!.GameObjects.Single(o=>o.Name==(slot==3?"PROJECTILE PISTOL":Program.Names[slot].ToUpperInvariant())+" view model").GetComponent<SkeletalMeshRenderer>()!;
    protected override void OnEngineUpdate()
    {
        _frame++;var scene=Scenes.ActiveScene!;var equipment=scene.FindComponent<FpsGunLab3D>()!;var player=equipment.GameObject;var raw=new RawInputSnapshot();
        if(_posesOnly){PoseReview(scene,equipment,raw);return;}
        if(_frame is 60 or 550 or 1470||_frame>=1010&&_frame<=1029)raw.MouseButtonsDown.Add(MouseButton.Left);
        if(_frame is 90 or 260 or 610 or 760 or 1050 or 1220)raw.KeysDown.Add(Key.R);
        if(_frame==500)raw.KeysDown.Add(Key.D2);if(_frame==960)raw.KeysDown.Add(Key.D3);if(_frame==1430)raw.KeysDown.Add(Key.D4);
        if(_frame==1540)raw.KeysDown.Add(Key.Space);if(_frame>=1570&&_frame<1590)raw.KeysDown.Add(Key.W);if(_frame==1620)raw.MouseDelta=new(20,-10);
        if(_frame==250)equipment.SelectedWeapon!.SetAmmo(0,5);if(_frame==460)equipment.SelectedWeapon!.SetAmmo(12,59);
        if(_frame==755)equipment.SelectedWeapon!.SetAmmo(0,1);if(_frame==1215)equipment.SelectedWeapon!.SetAmmo(0,3);
        typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);Input.SetGameViewPointer(new(.5f),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);
        if(_frame==45){Check(player.GetComponent<CharacterController3D>()!.IsGrounded,"Floor contact remains solid");_origin=player.Transform.WorldPosition;Check(View(0).ModelLoaded,"Cyberpunk pistol resolves");CheckForward(0);View(0).TryGetBoneModelMatrix("Slide",out var m);_slide=m.Translation;_capture="Cyberpunk_Pistol_Idle.png";}
        if(_frame==64){View(0).TryGetBoneModelMatrix("Slide",out var m);Check(Vector3.Distance(_slide,m.Translation)>.005f&&equipment.Ammo==11,"Pistol slide cycles and consumes ammo");_capture="Cyberpunk_Pistol_Fire.png";}
        if(_frame==125){Check(equipment.Reloading&&View(0).CurrentAnimation=="Reload","Tactical reload selects correct clip");_capture="Cyberpunk_Pistol_Withdraw.png";}
        if(_frame==185)_capture="Cyberpunk_Pistol_Insert.png";
        if(_frame==245)Check(equipment.Ammo==12&&equipment.ReserveAmmo==59&&!equipment.Reloading,"Tactical reload commits at authored duration");
        if(_frame==275)Check(equipment.Reloading&&View(0).CurrentAnimation=="ReloadEmpty","Empty pistol selects distinct reload animation");
        if(_frame==410)_capture="Cyberpunk_Pistol_Rack.png";
        if(_frame==450)Check(equipment.Ammo==5&&equipment.ReserveAmmo==0&&!equipment.Reloading,"Empty reload transfers only available reserve");
        if(_frame==540){Check(equipment.SelectedSlot==1,"Shotgun equip event works");CheckForward(1);View(1).TryGetBoneModelMatrix("Pump",out var m);_pump=m.Translation;_capture="Cyberpunk_Shotgun_Idle.png";}
        if(_frame==568){View(1).TryGetBoneModelMatrix("Pump",out var m);Check(Vector3.Distance(_pump,m.Translation)>.07f&&equipment.Ammo==5,"Shotgun pump stroke and pellet ammo cost");_capture="Cyberpunk_Shotgun_Pump.png";}
        if(_frame==665){Check(equipment.Reloading,"Shotgun shell reload is active");_capture="Cyberpunk_Shotgun_Shell.png";}
        if(_frame==745)Check(equipment.Ammo==6&&equipment.ReserveAmmo==29&&!equipment.Reloading,"Single shell transfers at end of loading cycle");
        if(_frame==775)Check(View(1).CurrentAnimation=="ReloadEmpty","Empty shotgun uses chambering cycle");
        if(_frame==890)_capture="Cyberpunk_Shotgun_EmptyCycle.png";
        if(_frame==925)Check(equipment.Ammo==1&&equipment.ReserveAmmo==0&&!equipment.Reloading,"Empty shotgun partial reserve completes safely");
        if(_frame==1000){Check(equipment.SelectedSlot==2,"Rifle equip event works");CheckForward(2);_capture="Cyberpunk_Rifle_Idle.png";}
        if(_frame==1015)_capture="Cyberpunk_Rifle_Fire.png";
        if(_frame==1035)Check(equipment.Ammo<=21,"Rifle automatic fire uses configured interval");
        if(_frame==1086){Check(equipment.Reloading,"Rifle magazine reload plays");_capture="Cyberpunk_Rifle_Withdraw.png";}
        if(_frame==1155)_capture="Cyberpunk_Rifle_Insert.png";
        if(_frame==1210)Check(equipment.Ammo==24&&!equipment.Reloading&&equipment.ReserveAmmo<60,"Rifle reload transfers finite reserve");
        if(_frame==1370){Check(View(2).CurrentAnimation=="ReloadEmpty","Empty rifle charging animation plays");_capture="Cyberpunk_Rifle_Charge.png";}
        if(_frame==1410){Check(equipment.Ammo==3&&equipment.ReserveAmmo==0&&!equipment.Reloading,"Empty rifle reload completes");_health=scene.GameObjects.Single(o=>o.Name=="Practice target 2").GetComponent<HealthComponent>()!.CurrentHealth;}
        if(_frame==1476){Check(equipment.SelectedSlot==3&&equipment.Ammo==7,"Projectile pistol variant keeps independent ammunition");Check(scene.GameObjects.Any(o=>o.GetComponent<Projectile3D>()!=null),"Projectile launches from authored socket");_capture="Cyberpunk_Projectile_Pistol.png";}
        if(_frame==1520)Check(scene.GameObjects.Single(o=>o.Name=="Practice target 2").GetComponent<HealthComponent>()!.CurrentHealth<_health,"Projectile hits and damages target");
        if(_frame==1548)Check(player.Transform.WorldPosition.Y>.3f,"Jump remains functional");
        if(_frame==1610)Check(Vector3.Distance(_origin,player.Transform.WorldPosition)>.8f&&player.GetComponent<CharacterController3D>()!.IsGrounded,"Movement returns to solid floor");
        if(_frame==1624)Check(Math.Abs(player.GetComponent<PlayerController3D>()!.ControlYaw)>1,"Mouse look remains functional");
        if(_frame==1630)
        {
            foreach(var obj in scene.GameObjects.Where(o=>o.GetComponent<WeaponInstance3D>()!=null))
            {
                var renderer=obj.GetComponent<SkeletalMeshRenderer>()!;
                Check(renderer.TryGetCurrentModelBounds(out var b)&&b.IsValid,"Finite bounds "+obj.Name);
                Check(new[]{"Idle","Fire","Reload","ReloadEmpty","Equip"}.All(renderer.HasAnimation),"Five clips imported "+obj.Name);
                Check(renderer.TryGetBoneModelMatrix("RightHand",out var hand)&&renderer.TryGetBoneModelMatrix("RightWristControl",out var target)&&Vector3.Distance(hand.Translation,target.Translation)<.003f,"Baked right-wrist contact "+obj.Name);
                Check(renderer.TryGetBoneModelMatrix("LeftHand",out hand)&&renderer.TryGetBoneModelMatrix("LeftWristControl",out target)&&Vector3.Distance(hand.Translation,target.Translation)<.003f,"Baked left-wrist contact "+obj.Name);
                Check(obj.GetComponent<WeaponInstance3D>()!.TryGetMuzzleWorldPosition(out _),"Authored muzzle socket resolves "+obj.Name);
            }
            File.WriteAllText(Program.Root+"/Preview/cyberpunk-gameplay-validation.json",JsonSerializer.Serialize(new{checks=_checks.Count,passed=_checks,frames=_frame,gpu=GL.GetString(StringName.Renderer)},new JsonSerializerOptions{WriteIndented=true}));Close();
        }
    }
    void PoseReview(Scene scene,FpsGunLab3D equipment,RawInputSnapshot raw)
    {
        typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);
        if(_frame<50)return;
        foreach(var controller in scene.GameObjects.Select(o=>o.GetComponent<AnimationController>()).OfType<AnimationController>())controller.Enabled=false;
        equipment.UseBuiltInFireInput=false;
        int index=(_frame-50)/4;if(index>=18){File.WriteAllText(Program.Root+"/Preview/cyberpunk-pose-validation.json",JsonSerializer.Serialize(_checks,new JsonSerializerOptions{WriteIndented=true}));Close();return;}
        if((_frame-50)%4!=0)return;
        int slot=index/6;int pose=index%6;equipment.Select(slot);var renderer=View(slot);
        (string Clip,int Frame)[] poses=[("Idle",1),("Fire",slot==1?18:3),("Reload",39),("Reload",92),("Reload",119),("ReloadEmpty",slot==1?65:150)];
        var sample=poses[pose];Check(renderer.Play(sample.Clip,false,0)&&renderer.Seek((sample.Frame-1)/60f),"Pose samples "+Program.Names[slot]+" "+sample);renderer.Pause();
        foreach(string side in new[]{"Right","Left"})
        {
            renderer.TryGetBoneModelMatrix(side+"Hand",out var hand);renderer.TryGetBoneModelMatrix(side+"WristControl",out var target);
            Check(Vector3.Distance(hand.Translation,target.Translation)<.001f,"Baked "+side+" wrist contact "+Program.Names[slot]+" "+sample);
        }
        Check(renderer.TryGetCurrentModelBounds(out var bounds)&&bounds.IsValid,"Finite sampled skin "+Program.Names[slot]+" "+sample);
        if(pose==0)CheckForward(slot);
        _capture="Pose_"+Program.Names[slot]+"_"+sample.Clip+"_"+sample.Frame+".png";
    }
    void CheckForward(int slot)
    {
        var renderer=View(slot);renderer.TryGetBoneWorldMatrix("Frame",out var frame);renderer.TryGetBoneWorldMatrix("MuzzleSocket",out var muzzle);
        Console.WriteLine($"ORIENTATION {Program.Names[slot]} frame={frame.Translation} muzzle={muzzle.Translation} camera={Scenes.ActiveScene!.ActiveCamera!.Transform.Forward} rotation={renderer.Transform.WorldRotation}");
        Check(Vector3.Dot(Vector3.Normalize(muzzle.Translation-frame.Translation),Scenes.ActiveScene!.ActiveCamera!.Transform.Forward)>.9f,"Barrel points downrange: "+Program.Names[slot]);
        renderer.TryGetBoneWorldMatrix("RightHand",out var right);renderer.TryGetBoneWorldMatrix("LeftHand",out var left);
        Check(Vector3.Dot(right.Translation-left.Translation,Scenes.ActiveScene.ActiveCamera.Transform.Right)>0,"Right grip hand is on player right: "+Program.Names[slot]);
    }
    protected override void OnEngineRender(){if(_capture==null)return;var rgba=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Rgba,PixelType.UnsignedByte,rgba);DemoApplication.Png(Program.Root+"/Preview/"+_capture,rgba,WindowWidth,WindowHeight);_capture=null;}
    protected override void OnUnload(){Scenes.UnloadScene();_runtime?.Dispose();base.OnUnload();}
}
