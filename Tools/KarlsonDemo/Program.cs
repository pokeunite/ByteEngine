using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
using KarlsonDemo.Plugin;
namespace KarlsonDemoTool;
internal static class Program
{
    internal const string Root="C:/Users/codex/Documents/karlson demo";
    internal static readonly string[] Names=["Pistol","Shotgun","Rifle"];
    [STAThread] static void Main(string[] args)
    {
        try{if(args.Contains("--milk-occlusion-check")){using var visibilityCheck=new MilkDepotOcclusionCheck();visibilityCheck.Run();return;}if(args.Contains("--milk-editor-perf")){MilkDepotEditorPerformance.Run();return;}if(args.Contains("--milk-perf")){using var perfApplication=new MilkDepotPerformance();perfApplication.Run();return;}if(args.Contains("--milk-editor-check")){MilkDepotEditorCheck.Run();return;}if(args.Contains("--milk-check")){using var review=new MilkDepotCheck();review.Run();return;}if(args.Contains("--milk-author")){MilkDepotAuthor.Run();return;}if(args.Contains("--environment-check")){EnvironmentImportCheck.Run();return;}if(args.Contains("--weapon-effects")){WeaponEffectsAuthor.Run();return;}if(args.Contains("--cyber-poses")){using var review=new CyberpunkReviewApplication(true);review.Run();return;}if(args.Contains("--cyber-check")){using var review=new CyberpunkReviewApplication();review.Run();return;}if(args.Contains("--cyber-import")){CyberpunkImport.Run();return;}if(args.Contains("--design-check")){DesignCheck.Run();return;}if(args.Contains("--author")){Author();return;}if(args.Contains("--editor-check")){EditorCheck.Run();return;}if(args.Contains("--package-check")){using var runtime=new ByteEngine.Core.Runtime.GameProjectRuntime(Root+"/karlson demo.byteproject",Console.WriteLine);var scene=runtime.LoadScene("Scenes/Main.bytescene");if(scene.GameObjects.SelectMany(o=>o.Components).Any(c=>c.GetType().Name=="MissingComponent"))throw new Exception("Missing packaged component");Console.WriteLine("PACKAGE PASS "+scene.GameObjectCount);return;}using var app=new DemoApplication(args.Contains("--trace-preview"),args.Contains("--socket-check"));app.Run();}
        finally{AudioEngine.Shutdown();}
    }
    internal static void Register()
    {
        typeof(ByteEnginePluginRegistry).GetMethod("BeginPlugin",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,["bytebard.karlsondemo"]);
        var context=(ByteEnginePluginContext)Activator.CreateInstance(typeof(ByteEnginePluginContext),BindingFlags.Instance|BindingFlags.NonPublic,null,["bytebard.karlsondemo"],null)!;new KarlsonDemoPlugin().Register(context);
    }
    static void Author()
    {
        foreach(var folder in new[]{"Assets/Models","Assets/Animation","Assets/Events","Assets/Blueprints","Preview","Plugins","PluginLibrary","PluginSource/KarlsonDemo"})Directory.CreateDirectory(Root+"/"+folder);
        foreach(var name in Names)File.Copy("C:/Users/codex/Downloads/byte engine files/Karlson V2/Karlson_"+name+".glb",Root+"/Assets/Models/Karlson_"+name+".glb",true);
        File.Copy("artifacts/plugins/KarlsonDemo.byteplugin",Root+"/Plugins/bytebard.karlsondemo.byteplugin",true);File.Copy("artifacts/plugins/KarlsonDemo.byteplugin",Root+"/PluginLibrary/KarlsonDemo.byteplugin",true);File.Copy("artifacts/plugins/KarlsonDemo.byteplugin","PluginLibrary/KarlsonDemo.byteplugin",true);
        foreach(var file in Directory.GetFiles("Plugins/KarlsonDemo","*",SearchOption.TopDirectoryOnly))File.Copy(file,Root+"/PluginSource/KarlsonDemo/"+Path.GetFileName(file),true);
        using var runtime=new DemoRuntime();runtime.Database.Scan();
        var report=new List<object>();
        foreach(var name in Names)
        {
            var reference=runtime.Database.ResolveReference("Assets/Models/Karlson_"+name+".glb");var model=runtime.Assets.LoadModel(reference);
            if(model.Skeleton==null||!new[]{"Idle","Fire","Reload","Equip"}.All(clip=>model.Animations.Any(a=>a.Name==clip)))throw new Exception("Missing rig/clips: "+name);
            var profile=new AnimationProfile{Name=name+" Weapon",Rig=new(){Type=AnimationRigType.Generic,ReferenceModel=reference},Locomotion=new(){Idle="Idle",Walk="Idle",Run="Idle",Jump="Idle",Fall="Idle",Land="Idle",TransitionDuration=.04f}};
            foreach(string clip in new[]{"Fire","Reload","Equip"})profile.Actions.Add(new(){Name=clip,Clip=clip,BlendIn=.025f,BlendOut=.04f,Priority=clip=="Reload"?20:10,Interruptible=true});
            AnimationProfileSerializer.Save(Root+"/Assets/Animation/"+name+".byteanim",profile);
            report.Add(new{name,triangles=model.Meshes.Sum(m=>m.Indices.Length/3),bones=model.Skeleton.Bones.Count,clips=model.Animations.Select(a=>new{a.Name,a.Duration}).ToArray()});
        }
        var events=new EventModuleDefinition{Name="FPS weapon controls"};
        for(int i=0;i<4;i++)events.Rules.Add(new(){DisplayName="Equip slot "+i,Conditions=[new(){Id="input.keyPressed",Arguments=new(){["key"]=new(){Constant=VariableValue.FromString("D"+(i+1))}}}],Actions=[new(){Id="bytebard.karlsondemo.equip",Arguments=new(){["target"]=new(){Constant=VariableValue.FromString("Self")},["slot"]=new(){Constant=VariableValue.FromNumber(i)}}}]});
        events.Rules.Add(new(){DisplayName="Reload",Conditions=[new(){Id="input.keyPressed",Arguments=new(){["key"]=new(){Constant=VariableValue.FromString("R")}}}],Actions=[new(){Id="bytebard.karlsondemo.reload"}]});
        new EventModuleSerializer().Save(events,Root+"/Assets/Events/FPS controls.byteevents");runtime.Database.Scan();
        var scene=new Scene("Karlson demo - FPS gun lab"){FixedSimulation=true};scene.SimulationClock.MaximumCatchUpSteps=4;
        scene.CreateGameObject("Soft daylight").AddComponent(new DirectionalLight{Intensity=.9f,AmbientIntensity=.12f,CastShadows=false});
        scene.CreateGameObject("Neutral environment").AddComponent(new SkyEnvironment{AmbientIntensity=.28f,EnvironmentIntensity=.35f,Exposure=1,DrawSky=true,SkyIntensity=.4f,ZenithColor=new(.07f,.11f,.17f),HorizonColor=new(.24f,.32f,.4f)});
        var floor=Box(scene,"Solid practice floor",new(0,-1,-3),new(30,2,30),new(.3f,.36f,.4f,1),true);floor.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Static,UseGravity=false});
        for(int x=-12;x<=12;x+=3)Box(scene,"Floor line",new(x,.004f,-3),new(.025f,.008f,30),new(.42f,.48f,.52f,1),false);
        for(int z=-15;z<=9;z+=3)Box(scene,"Floor line",new(0,.004f,z),new(30,.008f,.025f),new(.42f,.48f,.52f,1),false);
        var player=scene.CreateGameObject("FPS Character");player.Layer=1;player.Transform.LocalPosition=new(0,.8f,6);
        player.AddComponent(new CapsuleCollider3D{Radius=.35f,Height=1.8f,Center=new(0,.9f,0)});
        player.AddComponent(new CharacterController3D{MoveSpeed=6,Acceleration=35,Gravity=22,JumpForce=7,GroundDistance=.25f});
        player.AddComponent(new PlayerController3D{CharacterRotation=CharacterRotationMode.FaceCamera,ControlPitch=0,UseLocalOrientation=false});
        var eye=scene.CreateGameObject("FPS Camera");eye.SetParent(player,false);eye.Transform.LocalPosition=new(0,1.65f,0);eye.AddComponent(new Camera3D{ActiveGameCamera=true,FieldOfView=75,NearClip=.04f,FarClip=120});
        player.AddComponent(new FirstPersonCamera3D{CameraObjectId=eye.Id});
        for(int i=0;i<4;i++)
        {
            string name=i==3?"Pistol":Names[i];var obj=scene.CreateGameObject((i==3?"PROJECTILE PISTOL":name.ToUpperInvariant())+" view model");obj.SetParent(eye,false);obj.Active=i==0;obj.Transform.LocalPosition=i==0?new(.22f,-.18f,-.45f):new(.24f,-.23f,-.68f);obj.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);
            obj.AddComponent(new SkeletalMeshRenderer{Model=runtime.Database.ResolveReference("Assets/Models/Karlson_"+name+".glb"),DefaultAnimation="Idle",PlayOnStart=true,Loop=true,TransitionDuration=.04f});
            obj.AddComponent(new AnimationController{AnimationProfile=runtime.Database.ResolveReference("Assets/Animation/"+name+".byteanim")});
            obj.AddComponent(new WeaponDefinition3D { DisplayName=i==3?"Heavy projectile pistol":name, Slot=i,
                MagazineCapacity=i==1?6:i==2?24:i==3?8:12, ReserveCapacity=240,
                RoundsPerMinute=i==1?80:i==2?550:i==3?160:270, DamagePerPellet=i==1?12:i==2?18:i==3?65:25,
                PelletsPerShot=i==1?8:1, SpreadDegrees=i==1?6:0, ReloadSeconds=i==1?2:1.8f,
                ReloadMode=i==1?WeaponReloadMode.PerRound:WeaponReloadMode.Magazine,
                ShotMode=i==3?WeaponShotMode.Projectile:WeaponShotMode.Hitscan,
                TriggerMode=i==2?WeaponTriggerMode.Automatic:WeaponTriggerMode.SemiAutomatic,
                ProjectileSpeed=22, ShowShotTrace=true, TraceSeconds=.35f });
            obj.AddComponent(new WeaponInstance3D { CurrentAmmo=i==1?6:i==2?24:i==3?8:12, ReserveAmmo=i==1?30:60 });
        }
        var equipment=player.AddComponent(new FpsGunLab3D{CameraObjectId=eye.Id,InventoryRootId=eye.Id});player.AddComponent(new EventModuleComponent()).AddModuleReference(runtime.Database.ResolveReference("Assets/Events/FPS controls.byteevents"));
        for(int i=-1;i<=1;i++){var target=Box(scene,"Practice target "+(i+2),new(i*4,1.35f,-7),new(1.1f,2.7f,.5f),new(.9f,.4f,.13f,1),true);target.Layer=2;target.AddComponent(new HealthComponent{MaxHealth=1000,CurrentHealth=1000});}
        for(int i=0;i<3;i++){var stand=Box(scene,Names[i]+" stand",new(-8+i*3.5f,.5f,0),new(2,1,1),new(.16f,.2f,.24f,1),true);var model=scene.CreateGameObject(Names[i]+" display");model.Transform.LocalPosition=stand.Transform.WorldPosition+new Vector3(0,.95f,0);model.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI/2);model.AddComponent(new SkeletalMeshRenderer{Model=runtime.Database.ResolveReference("Assets/Models/Karlson_"+Names[i]+".glb"),DefaultAnimation="Idle",PlayOnStart=true});}
        var canvas=scene.CreateGameObject("HUD");canvas.AddComponent(new UiCanvas{ReferenceResolution=new(1280,720),ScaleMode=UiScaleMode.ScaleWithScreen});
        Text(scene,canvas,"Controls","WASD MOVE  /  MOUSE LOOK  /  SPACE JUMP  /  1 2 3 4 GUNS  /  LMB FIRE  /  R RELOAD",new(28,25),17);
        Text(scene,canvas,"Weapon status","PISTOL 12/12",new(28,657),23);equipment.StatusTextObjectId=scene.GameObjects.Single(o=>o.Name=="Weapon status").Id;Text(scene,canvas,"Crosshair","+",new(631,343),24);
        runtime.Serializer.Save(scene,Root+"/Scenes/Main.bytescene");
        var serialized=runtime.Serializer.Serialize(scene);var ids=new HashSet<Guid>{player.Id,eye.Id};foreach(var gun in eye.Children)ids.Add(gun.Id);
        var blueprint=new ByteEngine.Core.Blueprints.BlueprintDefinition{Name="FPS Character with animated guns",Type=ByteEngine.Core.Blueprints.BlueprintType.Character,Root=serialized.GameObjects.Single(o=>o.Id==player.Id),Children=serialized.GameObjects.Where(o=>o.Id!=player.Id&&ids.Contains(o.Id)).ToList()};new ByteEngine.Core.Blueprints.BlueprintSerializer().Save(blueprint,Root+"/Assets/Blueprints/FPS Character.byteblueprint");
        foreach(var gun in eye.Children.Where(o=>o.GetComponent<WeaponDefinition3D>()!=null))
        {
            var data=serialized.GameObjects.Single(o=>o.Id==gun.Id);data.ParentId=null;
            var weaponBlueprint=new ByteEngine.Core.Blueprints.BlueprintDefinition{Name=gun.GetComponent<WeaponDefinition3D>()!.DisplayName,Root=data};
            new ByteEngine.Core.Blueprints.BlueprintSerializer().Save(weaponBlueprint,Root+"/Assets/Blueprints/"+weaponBlueprint.Name+".byteblueprint");
        }
        runtime.Database.Scan();File.WriteAllText(Root+"/Preview/import-validation.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Root+"/CONTROLS.txt","Press Play and click Game View. WASD move; mouse look; Space jump; 1 pistol, 2 shotgun, 3 rifle, 4 heavy projectile pistol; left mouse fires; R reloads; Escape releases mouse. Animated stylized gloves are included. Select a weapon child: Weapon Definition holds tuning; Weapon Instance holds Current Ammo and Reserve Ammo. The Weapons event category exposes editable actions and conditions.\nBlender source and three animated GLB files are in Downloads/byte engine files. Native model, animation-profile and event assets are in Assets.\n");
        Console.WriteLine("FPS PROJECT AUTHORED AND IMPORTS VERIFIED");
    }
    static GameObject Box(Scene scene,string name,Vector3 pos,Vector3 size,Vector4 color,bool collider){var obj=scene.CreateGameObject(name);obj.Transform.LocalPosition=pos;obj.Transform.LocalScale=size;obj.AddComponent(new MeshRenderer{UsePrimitive=true,Material=new(){BaseColor=color}});if(collider)obj.AddComponent(new BoxCollider3D());return obj;}
    static void Text(Scene scene,GameObject parent,string name,string value,Vector2 offset,int size){var obj=scene.CreateGameObject(name);obj.SetParent(parent,false);obj.AddComponent(new UiText{Text=value,Offset=offset,FontSize=size,Color=new(.95f,.97f,1,1),ShadowColor=new(0,0,0,.8f),WrapWidth=1220});}
}
internal sealed class DemoRuntime:IDisposable
{
    public AssetDatabase Database{get;}public AssetManager Assets{get;}public SceneSerializer Serializer{get;}
    readonly Guid _animation;
    public DemoRuntime()
    {
        Program.Register();Database=new(Program.Root,["Assets","Scenes"],Console.WriteLine,Console.WriteLine);Assets=new(Database,Console.WriteLine);
        var components=new ComponentSerializer(Program.Root,Database,Assets,Console.WriteLine);PhysicsSerializationRegistrar.Register(components);AnimationSerializationRegistrar.Register(components);ByteEnginePluginRegistry.ApplyComponentCodecs(components,Console.WriteLine);Serializer=new(components);_animation=AnimationRuntimeAssets.Configure(Assets);
    }
    public void Dispose(){AnimationRuntimeAssets.Clear(_animation);Assets.Dispose();Database.Dispose();}
}
