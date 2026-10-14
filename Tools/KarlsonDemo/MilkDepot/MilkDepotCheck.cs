using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using KarlsonDemo.Plugin;
using OpenTK.Graphics.OpenGL4;
namespace KarlsonDemoTool;
internal sealed class MilkDepotCheck:ByteEngineApplication
{
    DemoRuntime? _runtime;int _frame;string? _capture;readonly List<string> _checks=[];
    public MilkDepotCheck():base(1280,720,"Milk Depot native validation"){}
    protected override bool CloseOnEscape=>false;
    protected override void OnLoad(){base.OnLoad();IsVisible=false;_runtime=new();InputActions.Configure(new ProjectSerializer().Load(Program.Root+"/karlson demo.byteproject").InputMap);Scenes.LoadScene(_runtime.Serializer.Load(Program.Root+"/Scenes/Milk Depot.bytescene"));}
    void Check(bool pass,string message){if(!pass)throw new Exception(message);_checks.Add(message);Console.WriteLine("PASS "+message);}
    static void Tick(Scene scene,RawInputSnapshot? raw=null)
    {
        raw??=new();typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);
        Input.SetGameViewPointer(new(.5f),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
    }
    protected override void OnEngineUpdate()
    {
        _frame++;if(_frame!=4){if(_frame==7){File.WriteAllText(Program.Root+"/Preview/milk-depot-validation.json",JsonSerializer.Serialize(_checks,new JsonSerializerOptions{WriteIndented=true}));Close();}return;}
        var scene=Scenes.ActiveScene!;var equipment=scene.FindComponent<FpsGunLab3D>()!;var player=equipment.GameObject;var motor=player.GetComponent<CharacterController3D>()!;var course=scene.FindComponent<MilkDepotCourse3D>()!;
        Check(!scene.GameObjects.SelectMany(o=>o.Components).Any(c=>c.GetType().Name=="MissingComponent"),"No missing serialized components");
        for(int i=0;i<300;i++)Tick(scene);
        Check(motor.IsGrounded&&player.Transform.WorldPosition.Y>-.05f,"Player stays grounded at spawn for five seconds");
        var start=player.Transform.WorldPosition;
        for(int i=0;i<70;i++)Tick(scene,new(){KeysDown={Key.W}});
        Check(Vector3.Distance(start,player.Transform.WorldPosition)>3&&motor.IsGrounded,"Player moves through spawn doorway on solid floor");
        var pickup=scene.GameObjects.First(o=>o.GetComponent<WeaponAmmoPickup3D>()!=null);
        equipment.SelectedWeapon!.SetAmmo(8,0);player.Transform.WorldPosition=pickup.Transform.WorldPosition;motor.SetVelocity(Vector3.Zero);Tick(scene);
        Check(equipment.ReserveAmmo>0&&!pickup.Active,"Ammo pickup adds reserve and deactivates once");
        int ammo=equipment.ReserveAmmo;Tick(scene);Check(equipment.ReserveAmmo==ammo,"Collected pickup cannot duplicate ammo");
        var checkpoint=scene.GameObjects.First(o=>o.Name=="Checkpoint_Beacon");player.Transform.WorldPosition=checkpoint.Transform.WorldPosition;motor.SetVelocity(Vector3.Zero);Tick(scene);
        var safe=course.CheckpointPosition;player.Transform.WorldPosition=new(0,-10,0);Tick(scene);
        Check(course.Respawns==1&&Vector3.Distance(player.Transform.WorldPosition,safe)<.5f,"Fall recovery returns to activated checkpoint");
        var target=scene.GameObjects.First(o=>o.Name=="Training_Dummy");var health=target.GetComponent<HealthComponent>()!;
        player.Transform.WorldPosition=target.Transform.WorldPosition+new Vector3(3,0,0);motor.SetVelocity(Vector3.Zero);player.GetComponent<PlayerController3D>()!.ControlYaw=-90;player.GetComponent<PlayerController3D>()!.ControlPitch=0;
        for(int i=0;i<35;i++)Tick(scene);float before=health.CurrentHealth;
        equipment.Fire();Tick(scene);Check(health.CurrentHealth<before,"Weapon hits imported practice target collider");
        var milk=scene.FindGameObject(course.MilkObjectId)!;player.Transform.WorldPosition=milk.Transform.WorldPosition-Vector3.UnitY;motor.SetVelocity(Vector3.Zero);Tick(scene);
        Check(!course.Completed,"Milk objective stays locked while targets remain");
        foreach(var h in scene.GameObjects.Where(o=>o.Name=="Training_Dummy").Select(o=>o.GetComponent<HealthComponent>()).OfType<HealthComponent>().ToArray())h.Kill();
        Tick(scene);Check(course.Completed&&!milk.Active&&course.RemainingTargets==0,"Clearing targets and reaching milk completes course");
        // Leave a normal courtyard pose for the native screenshot.
        player.Transform.WorldPosition=new(-21,.08f,10);player.GetComponent<PlayerController3D>()!.ControlYaw=0;motor.SetVelocity(Vector3.Zero);for(int i=0;i<30;i++)Tick(scene);
        _capture="MilkDepot_InEngine.png";
    }
    protected override void OnEngineRender(){if(_capture==null)return;var pixels=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);DemoApplication.Png(Program.Root+"/Preview/"+_capture,pixels,WindowWidth,WindowHeight);_capture=null;}
    protected override void OnUnload(){Scenes.UnloadScene();_runtime?.Dispose();base.OnUnload();}
}
