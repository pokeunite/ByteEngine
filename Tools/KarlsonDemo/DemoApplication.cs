using System.Numerics;
using System.Reflection;
using System.IO.Compression;
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
internal sealed class DemoApplication:ByteEngineApplication
{
    DemoRuntime? _runtime;int _frame;string? _capture;readonly List<string> _checks=[];float _projectileHealth;Vector3 _slideRest,_pumpRest,_magRest,_start;
    readonly bool _tracePreview,_socketCheck;
    public DemoApplication(bool tracePreview=false,bool socketCheck=false):base(1280,720,"Karlson demo - native validation"){_tracePreview=tracePreview;_socketCheck=socketCheck;}
    protected override bool CloseOnEscape=>false;
    protected override void OnLoad(){base.OnLoad();IsVisible=false;_runtime=new();InputActions.Configure(new ProjectSerializer().Load(Program.Root+"/karlson demo.byteproject").InputMap);Scenes.LoadScene(_runtime.Serializer.Load(Program.Root+"/Scenes/Main.bytescene"));}
    void Check(bool ok,string description){if(!ok)throw new Exception("FAIL: "+description);_checks.Add(description);Console.WriteLine("PASS: "+description);}
    SkeletalMeshRenderer View(int slot)=>Scenes.ActiveScene!.GameObjects.Single(o=>o.Name==Program.Names[slot].ToUpperInvariant()+" view model").GetComponent<SkeletalMeshRenderer>()!;
    protected override void OnEngineUpdate()
    {
        if(_tracePreview&&_frame==80){File.WriteAllText(Program.Root+"/Preview/trace-validation.json",JsonSerializer.Serialize(_checks));Close();return;}
        _frame++;var scene=Scenes.ActiveScene!;var player=scene.FindComponent<FpsGunLab3D>()!;var motor=player.GameObject.GetComponent<CharacterController3D>()!;
        if(_socketCheck){if(_frame==40){SocketCheck.Run(scene);Close();}return;}
        var raw=new RawInputSnapshot();
        if(_frame is 60 or 270 or 750 || (_frame>=510&&_frame<=529))raw.MouseButtonsDown.Add(MouseButton.Left);
        if(_frame is 90 or 320 or 550)raw.KeysDown.Add(Key.R);
        if(_frame==220)raw.KeysDown.Add(Key.D2);if(_frame==460)raw.KeysDown.Add(Key.D3);if(_frame==710)raw.KeysDown.Add(Key.D4);
        if(_frame==810)raw.KeysDown.Add(Key.D1);
        if(_frame==840)raw.KeysDown.Add(Key.Space);if(_frame>=870&&_frame<890)raw.KeysDown.Add(Key.W);
        if(_frame==920)raw.MouseDelta=new(20,-10);
        NativeInput(raw);
        if(_frame==40){Check(motor.IsGrounded&&player.Transform.WorldPosition.Y>-.05f,"FPS character stays on solid floor");_start=player.Transform.WorldPosition;_capture="FPS_Pistol.png";Check(View(0).ModelLoaded,"Rebuilt pistol imports");View(0).TryGetBoneModelMatrix("Slide",out var bone);_slideRest=bone.Translation;}
        if(_frame==64){Check(player.ShotsFired==1&&player.Ammo==11,"Pistol consumes one round");Check(View(0).CurrentAnimation=="Fire","Pistol fire animation plays");View(0).TryGetBoneModelMatrix("Slide",out var bone);Check(Vector3.Distance(_slideRest,bone.Translation)>.005f,"Slide retracts");Check(scene.GameObjects.Any(o=>o.Name=="Weapon shot trace"),"Visible shot traces are created");_capture="FPS_Pistol_Fire.png";}
        if(_frame==130){Check(player.Reloading&&View(0).CurrentAnimation=="Reload","Reload action drives pistol profile");_capture="FPS_Pistol_Reload.png";}
        if(_frame==205){Check(player.Ammo==12&&player.ReserveAmmo==59&&!player.Reloading,"Magazine reload transfers finite reserve");Check(View(0).CurrentAnimation=="Idle","Profile returns to idle");}
        if(_frame==258){Check(player.SelectedSlot==1,"Parameterized equip event selects shotgun");View(1).TryGetBoneModelMatrix("Pump",out var bone);_pumpRest=bone.Translation;_capture="FPS_Shotgun.png";}
        if(_frame==285){Check(player.Ammo==5&&View(1).CurrentAnimation=="Fire","Shotgun consumes one shell for eight pellets");View(1).TryGetBoneModelMatrix("Pump",out var bone);Check(Vector3.Distance(_pumpRest,bone.Translation)>.02f,"Pump moves during cycling");_capture="FPS_Shotgun_Pump.png";}
        if(_frame==365){Check(player.Reloading,"Per-round loading state is active");_capture="FPS_Shotgun_Reload.png";}
        if(_frame==450)Check(player.Ammo==6&&player.ReserveAmmo==29&&!player.Reloading,"Per-round reload transfers one reserve shell");
        if(_frame==500){Check(player.SelectedSlot==2&&View(2).ModelLoaded,"Rifle uses same equipment controller");View(2).TryGetBoneModelMatrix("Magazine",out var bone);_magRest=bone.Translation;_capture="FPS_Rifle.png";}
        if(_frame==535)Check(player.Ammo<=21&&player.ShotsFired>=5,"Configured automatic trigger repeats shots");
        if(_frame==590){View(2).TryGetBoneModelMatrix("Magazine",out var bone);Check(player.Reloading&&Vector3.Distance(_magRest,bone.Translation)>.1f,"Rifle magazine clears the well");_capture="FPS_Rifle_Reload.png";}
        if(_frame==670)Check(player.Ammo==24&&!player.Reloading&&player.ReserveAmmo<60,"Rifle reload consumes reserve");
        if(_frame==705){_projectileHealth=scene.GameObjects.Single(o=>o.Name=="Practice target 2").GetComponent<HealthComponent>()!.CurrentHealth;Check(scene.GameObjects.Single(o=>o.Name=="Practice target 2").GetComponent<HealthComponent>()!.CurrentHealth<1000,"Hitscan damages target");}
        if(_frame==755){Check(player.SelectedSlot==3&&player.Ammo==7,"Second pistol variant has independent eight-round magazine");Check(player.SelectedWeapon!.Definition!.ShotMode==WeaponShotMode.Projectile,"Variant uses projectile mode");Check(scene.GameObjects.Any(o=>o.GetComponent<Projectile3D>()!=null),"Physical projectile is spawned");_capture="FPS_Projectile_Pistol.png";}
        if(_frame==800)Check(scene.GameObjects.Single(o=>o.Name=="Practice target 2").GetComponent<HealthComponent>()!.CurrentHealth<_projectileHealth,"Swept projectile collision damages target");
        if(_frame==815)Check(player.SelectedSlot==0&&player.Ammo==12&&player.ReserveAmmo==59,"Switching preserves each weapon's ammunition");
        if(_frame==848)Check(player.Transform.WorldPosition.Y>.4f,"FPS jump works");
        if(_frame==910){Check(Vector3.Distance(_start,player.Transform.WorldPosition)>1,"FPS movement works");Check(motor.IsGrounded&&player.Transform.WorldPosition.Y>-.05f,"Floor remains solid after movement");}
        if(_frame==924)Check(Math.Abs(player.GameObject.GetComponent<PlayerController3D>()!.ControlYaw)>1,"FPS mouse look works");
        if(_frame==940){foreach(var renderer in scene.GameObjects.Select(o=>o.GetComponent<SkeletalMeshRenderer>()).OfType<SkeletalMeshRenderer>()){Check(renderer.TryGetCurrentModelBounds(out var bounds)&&bounds.IsValid,"Finite animated bounds: "+renderer.GameObject.Name);Check(new[]{"Idle","Fire","Reload","Equip"}.All(renderer.HasAnimation),"All clips imported: "+renderer.GameObject.Name);}Check(scene.GameObjects.Count(o=>o.Name=="Weapon shot trace")<64,"Debug traces expire and remain bounded");File.WriteAllText(Program.Root+"/Preview/gameplay-validation.json",JsonSerializer.Serialize(new{checks=_checks.Count,passed=_checks,nativeFrames=_frame,gpu=GL.GetString(StringName.Renderer)},new JsonSerializerOptions{WriteIndented=true}));Close();}
    }
    static void NativeInput(RawInputSnapshot raw)
    {
        typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);Input.SetGameViewPointer(new(.5f),new(1280,720),true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);
    }
    protected override void OnEngineRender(){if(_capture==null)return;var pixels=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);if(GL.GetError()!=ErrorCode.NoError)throw new Exception("Native capture GL error");Png(Program.Root+"/Preview/"+_capture,pixels,WindowWidth,WindowHeight);Console.WriteLine("CAPTURE "+_capture);_capture=null;}
    protected override void OnUnload(){Scenes.UnloadScene();_runtime?.Dispose();base.OnUnload();}
    internal static void Png(string file,byte[] rgba,int w,int h)
    {
        using var stream=File.Create(file);stream.Write(new byte[]{137,80,78,71,13,10,26,10});var header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),w);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),h);header[8]=8;header[9]=6;Chunk(stream,"IHDR",header);using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))for(int y=h-1;y>=0;y--){z.WriteByte(0);z.Write(rgba,y*w*4,w*4);}Chunk(stream,"IDAT",compressed.ToArray());Chunk(stream,"IEND",[]);
    }
    static void Chunk(Stream stream,string type,byte[] data){var name=System.Text.Encoding.ASCII.GetBytes(type);var size=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(size,data.Length);stream.Write(size);stream.Write(name);stream.Write(data);uint crc=0xffffffff;foreach(byte b in name.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(size,~crc);stream.Write(size);}
}
