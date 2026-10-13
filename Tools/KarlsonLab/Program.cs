using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;
using Karlson.Plugin;

namespace KarlsonLab;
internal static class Program
{
    internal const string Root="C:/Users/codex/Documents/KarlsonByte";
    internal const string ProjectFile=Root+"/KarlsonByte.byteproject";
    internal static void Main(string[] args)
    {
        try
        {
        if(args.Contains("--new-demo")){CreateCleanDemo();return;}
        if(args.Contains("--create")){Authoring.Create();return;}
        if(args.Contains("--refresh-content")){Authoring.Create(true);return;}
        if(args.Contains("--lighting")){Authoring.UpdateLighting();return;}
        if(args.Contains("--validate")){Validation.Run();return;}
        if(args.Contains("--traversal")){Validation.RunTraversal();return;}
        if(args.Contains("--spawn-check")){Validation.CheckSpawns();return;}
        if(args.Contains("--package-check")){using var project=new GameProjectRuntime(ProjectFile,Console.WriteLine);foreach(var path in Directory.GetFiles(Root+"/Scenes","*.bytescene")){var scene=project.LoadScene("Scenes/"+Path.GetFileName(path));Console.WriteLine("PACKAGE PASS "+scene.Name+" objects="+scene.GameObjectCount);}return;}
        if(args.Contains("--editor-check")){EditorLaunchCheck.Run();return;}
        using var app=new LabApplication(args.Contains("--smoke"));app.Run();
        }
        finally { AudioEngine.Shutdown(); }
    }
    private static void CreateCleanDemo()
    {
        const string root="C:/Users/codex/Documents/karlson demo";
        string file=root+"/karlson demo.byteproject";
        if(File.Exists(file))throw new InvalidOperationException("The new project already exists; preserving it.");
        foreach(string folder in new[]{"Assets/Concepts","Scenes","Plugins"})Directory.CreateDirectory(root+"/"+folder);
        new ProjectSerializer().Save(new ProjectData{Name="karlson demo",StartupScene="Scenes/Main.bytescene",Window=new(){Width=1280,Height=720}},file);
        using var database=new AssetDatabase(root,["Assets","Scenes"],Console.WriteLine,Console.WriteLine);
        using var assets=new AssetManager(database,Console.WriteLine);
        var serializer=new SceneSerializer(new ComponentSerializer(root,database,assets,Console.WriteLine));
        var scene=new Scene("Main");
        serializer.Save(scene,root+"/Scenes/Main.bytescene");
        database.Scan();
        if(new ProjectSerializer().Load(file).Name!="karlson demo"||serializer.Load(root+"/Scenes/Main.bytescene").GameObjectCount!=0)throw new Exception("Clean project verification failed.");
        File.WriteAllText(root+"/Open in ByteEngine.cmd","@echo off\r\nstart \"\" \"C:\\Users\\codex\\ByteEngine\\Dist\\ByteEngine\\ByteEngine.Editor.exe\" \"%~dp0karlson demo.byteproject\"\r\n");
        Console.WriteLine("Created and verified clean project: "+file);
    }
    internal static void Register()
    {
        var begin=typeof(ByteEnginePluginRegistry).GetMethod("BeginPlugin",BindingFlags.NonPublic|BindingFlags.Static)!;
        var contains=typeof(ByteEnginePluginRegistry).GetMethod("ContainsPlugin",BindingFlags.NonPublic|BindingFlags.Static)!;
        if((bool)contains.Invoke(null,["bytebard.karlson"])!)return;
        begin.Invoke(null,["bytebard.karlson"]);
        var context=(ByteEnginePluginContext)Activator.CreateInstance(typeof(ByteEnginePluginContext),BindingFlags.Instance|BindingFlags.NonPublic,null,["bytebard.karlson"],null)!;
        new KarlsonPlugin().Register(context);
    }
}

internal sealed class Authoring:IDisposable
{
    readonly AssetDatabase _db;readonly AssetManager _assets;readonly ComponentSerializer _components;readonly SceneSerializer _scenes;
    readonly Dictionary<string,AssetReference> _blueprints=new();
    static readonly Vector4 White=new(.84f,.9f,.93f,1),Blue=new(.15f,.65f,.86f,1),Dark=new(.065f,.12f,.18f,1),Red=new(.84f,.13f,.13f,1),Orange=new(1,.62f,.18f,1);
    Authoring(){Program.Register();_db=new(Program.Root,["Assets","Scenes"],Console.WriteLine,Console.WriteLine);_assets=new(_db,Console.WriteLine);_components=new(Program.Root,_db,_assets,Console.WriteLine);ByteEnginePluginRegistry.ApplyComponentCodecs(_components,Console.WriteLine);AudioSerializationRegistrar.Register(_components);PhysicsSerializationRegistrar.Register(_components);_scenes=new(_components);}
    public static void Create(bool refresh=false)
    {
        if(!refresh&&File.Exists(Program.Root+"/Scenes/MainMenu.bytescene"))throw new InvalidOperationException("Existing complete project is preserved; use the authoring tool only for a new project.");
        foreach(var folder in new[]{"Assets/Blueprints","Assets/Events","Assets/Audio","Scenes","Plugins","PluginLibrary","PluginSource/Karlson","Preview","Design"})Directory.CreateDirectory(Path.Combine(Program.Root,folder));
        string package=Path.GetFullPath("artifacts/plugins/Karlson.byteplugin");File.Copy(package,Program.Root+"/Plugins/bytebard.karlson.byteplugin",true);File.Copy(package,Program.Root+"/PluginLibrary/Karlson.byteplugin",true);File.Copy(package,"PluginLibrary/Karlson.byteplugin",true);
        foreach(var file in Directory.GetFiles("Plugins/Karlson","*",SearchOption.TopDirectoryOnly))File.Copy(file,Program.Root+"/PluginSource/Karlson/"+Path.GetFileName(file),true);
        var project=File.Exists(Program.ProjectFile)?new ProjectSerializer().Load(Program.ProjectFile):new ProjectData{Name="Karlson / Byte Lab",StartupScene="Scenes/MainMenu.bytescene",Window=new(){Width=1280,Height=720}};
        project.Classification.Layers=[new(){Index=0,Name="Default"},new(){Index=1,Name="Player"},new(){Index=2,Name="Enemy"},new(){Index=3,Name="Physical props"},new(){Index=4,Name="Objectives"},new(){Index=5,Name="Effects"},new(){Index=6,Name="Triggers"}];
        new ProjectSerializer().Save(project,Program.ProjectFile);
        AudioAssets.Write(Program.Root+"/Assets/Audio");
        using var a=new Authoring();a.Events();a.Blueprints();
        for(int course=1;course<=4;course++){var scene=a.Course(course,false);a._scenes.Save(scene,Program.Root+$"/Scenes/Course{course:00}.bytescene");}
        a._scenes.Save(a.Course(1,true),Program.Root+"/Scenes/MainMenu.bytescene");a._db.Scan();
        File.WriteAllText(Program.Root+"/PLAY ME.txt","KARLSON / BYTE LAB\nOpen KarlsonByte.byteproject in the newly built ByteEngine editor, press Play, then click Game View. Select a course.\nWASD move | Mouse look | Space jump/wall jump | Shift sprint | Ctrl/C slide | RMB grapple | E pick up/grab/release | LMB shoot | Q throw gun | F melee/throw held prop | Tab slow motion | R retry | Escape pause/release cursor | M music\nFour original editable courses recreate the supplied free prototype's central mechanics.\nDesign and validation notes are in Design and Preview. No game export was produced.\n");
        File.WriteAllText(Program.Root+"/Open in ByteEngine.cmd","@echo off\r\nstart \"\" \"C:\\Users\\codex\\ByteEngine\\Dist\\ByteEngine\\ByteEngine.Editor.exe\" \"%~dp0KarlsonByte.byteproject\"\r\n");
        Console.WriteLine("CREATED "+Program.ProjectFile);
    }
    void Events()
    {
        foreach(var spec in new[]{("MilkCompletion","bytebard.karlson.goalReached","bytebard.karlson.completeCourse"),("CombatGate","bytebard.karlson.enemiesCleared","bytebard.karlson.openGate")})
        {
            var module=new EventModuleDefinition{Name=spec.Item1};module.Rules.Add(new(){DisplayName=spec.Item1,Conditions=[new(){Id=spec.Item2},new(){Id="system.triggerOnce"}],Actions=[new(){Id=spec.Item3}]});
            new EventModuleSerializer().Save(module,Program.Root+"/Assets/Events/"+spec.Item1+".byteevents");
        }
        _db.Scan();
    }
    void Blueprints()
    {
        SaveBlueprint("Player",s=>{
            var player=s.CreateGameObject("Player");player.Layer=1;player.AddComponent(new CapsuleCollider3D{Radius=.35f,Height=1.8f,Center=new(0,.9f,0)});player.AddComponent(new CharacterController3D{MoveSpeed=14,JumpForce=8,Gravity=23,CoyoteTime=.12f,JumpBuffer=.14f,StepHeight=.3f});player.AddComponent(new HealthComponent{MaxHealth=100});player.AddComponent(new ResourceMeter3D());
            var eye=s.CreateGameObject("Eye");eye.SetParent(player,false);eye.Transform.LocalPosition=new(0,1.65f,0);eye.AddComponent(new Camera3D{FieldOfView=80,NearClip=.06f,FarClip=200,ActiveGameCamera=true});eye.AddComponent(new AudioListener3D());
            player.AddComponent(new ParkourPlayer3D{CameraObjectId=eye.Id});player.AddComponent(new CableRenderer3D{Enabled=false,Color=new(.2f,.8f,1,1),Radius=.012f});return player;
        },BlueprintType.Character);
        SaveBlueprint("Pistol",s=>Weapon(s,"PULSE PISTOL",12,.22f,40,Blue));
        SaveBlueprint("Scattergun",s=>Weapon(s,"IMPACT RIFLE",6,.65f,100,Orange));
        SaveBlueprint("Enemy",s=>{
            var root=s.CreateGameObject("Red sentry");root.Layer=2;root.AddComponent(new CapsuleCollider3D{Radius=.4f,Height=1.8f,Center=new(0,.9f,0)});root.AddComponent(new CharacterController3D{MoveSpeed=2.5f,Acceleration=12,Deceleration=20,Gravity=23});root.AddComponent(new Rigidbody3D{Enabled=false,Mass=65,SimulateRotation=true});root.AddComponent(new HealthComponent{MaxHealth=80});root.AddComponent(new KarlsonEnemy3D());
            Part(s,root,"Torso",new(0,1.03f,0),new(.62f,.7f,.35f),Red);Part(s,root,"Head",new(0,1.62f,0),new(.38f),White);Part(s,root,"Visor",new(0,1.64f,-.205f),new(.3f,.09f,.02f),Dark);
            foreach(int sign in new[]{-1,1}){Part(s,root,"Leg "+sign,new(sign*.17f,.4f,0),new(.2f,.65f,.22f),Dark);Part(s,root,"Arm "+sign,new(sign*.4f,1,.03f),new(.18f,.65f,.18f),Red);}
            var gun=Weapon(s,"PULSE PISTOL",12,.22f,40,White);gun.SetParent(root,false);gun.Transform.LocalPosition=new(.3f,1.12f,-.38f);gun.GetComponent<Rigidbody3D>()!.Enabled=false;gun.GetComponent<BoxCollider3D>()!.Enabled=false;return root;
        },BlueprintType.Character);
        SaveBlueprint("Milk",s=>{
            var root=Cube(s,"Milk objective",Vector3.Zero,new(.55f,.9f,.45f),White,false);root.Layer=4;root.AddComponent(new MilkGoal3D());Part(s,root,"Blue label",new(0,.03f,-.51f),new(.7f,.42f,.025f),Blue);Part(s,root,"Cap",new(0,.57f,0),new(.6f,.18f,.7f),Blue);
            var events=root.AddComponent(new EventModuleComponent());events.AddModuleReference(_db.ResolveReference("Assets/Events/MilkCompletion.byteevents"));return root;
        });
        SaveBlueprint("Barrel",s=>{var root=Cube(s,"Explosive barrel",Vector3.Zero,new(.8f,1.1f,.8f),Orange,true);root.Layer=3;root.AddComponent(new Rigidbody3D{Mass=12,LinearDamping=.4f,SimulateRotation=true});root.AddComponent(new KarlsonProp3D{Breakable=true,Explosive=true,Threshold=20});Part(s,root,"Hazard stripe",Vector3.Zero,new(1.01f,.2f,1.01f),Dark);return root;});
        SaveBlueprint("Crate",s=>{var root=Cube(s,"Physics crate",Vector3.Zero,Vector3.One,White,true);root.Layer=3;root.AddComponent(new Rigidbody3D{Mass=5,LinearDamping=.5f,SimulateRotation=true});root.AddComponent(new KarlsonProp3D());return root;});
        SaveBlueprint("Glass",s=>{var root=Cube(s,"Breakable glass",Vector3.Zero,new(6,3,.12f),new(.2f,.7f,.85f,.32f),true);root.Layer=3;root.GetComponent<MeshRenderer>()!.Material.BlendMode=BlendMode3D.AlphaBlend;root.AddComponent(new KarlsonProp3D{Breakable=true,Threshold=15});return root;});
        SaveBlueprint("LaunchPad",s=>{var root=Cube(s,"Launch pad",Vector3.Zero,new(2,.15f,2),Blue,false);root.AddComponent(new BoxCollider3D{IsTrigger=true,Center=new(0,1,0),Size=new(1,3,1)});root.AddComponent(new LaunchVolume3D{LaunchVelocity=new(0,12,-10),PreserveHorizontal=false});return root;});
        _db.Scan();foreach(var file in Directory.GetFiles(Program.Root+"/Assets/Blueprints","*.byteblueprint"))_blueprints[Path.GetFileNameWithoutExtension(file)]=_db.ResolveReference("Assets/Blueprints/"+Path.GetFileName(file));
    }
    void SaveBlueprint(string name,Func<Scene,GameObject> make,BlueprintType type=BlueprintType.GenericObject)
    {var scene=new Scene(name);var root=make(scene);var data=_scenes.Serialize(scene);var definition=new BlueprintDefinition{Name=name,Type=type,Root=data.GameObjects.Single(o=>o.Id==root.Id),Children=data.GameObjects.Where(o=>o.Id!=root.Id).ToList()};new BlueprintSerializer().Save(definition,Program.Root+"/Assets/Blueprints/"+name+".byteblueprint");}
    GameObject Spawn(Scene scene,string name,Vector3 pos)=>BlueprintRuntimeFactory.Spawn(scene,_blueprints[name],pos,_db,_scenes,Console.WriteLine)??throw new Exception("Blueprint spawn failed: "+name);
    GameObject Weapon(Scene s,string name,int ammo,float interval,float damage,Vector4 color)
    {var root=Cube(s,name,Vector3.Zero,new(.18f,.2f,.55f),color,false);root.Layer=3;root.AddComponent(new BoxCollider3D());root.AddComponent(new Rigidbody3D{Mass=.8f,SimulateRotation=true,LinearDamping=.2f});root.AddComponent(new HitscanEmitter3D{Damage=damage,FireInterval=interval,Range=90,Impulse=4});root.AddComponent(new KarlsonWeapon3D{DisplayName=name,StartingAmmo=ammo,Recoil=interval>.4?3:1.3f});Part(s,root,"Grip",new(0,-.55f,.22f),new(.65f,.8f,.3f),Dark);Part(s,root,"Muzzle",new(0,.1f,-.52f),new(.6f,.4f,.1f),Blue);return root;}
    Scene Course(int n,bool menu)
    {
        string[] names=["ORIENTATION","WALL RUN","THE GAP","MILK RUN"];
        string[] hints=["E picks up a gun. Shoot the sentries, break the glass, reach the milk.","Carry speed into the gap. Run along the blue wall; Space kicks away.","Hold RMB on the blue anchor. Release to swing onto the far platform.","Chain slide, wall run and grapple. The milk is waiting at the top."];
        var s=new Scene(menu?"Karlson / Main Menu":"Karlson / "+names[n-1]){FixedSimulation=true};s.SimulationClock.MaximumCatchUpSteps=4;
        var director=s.CreateGameObject("Course Director");director.AddComponent(new KarlsonDirector3D{CourseNumber=n,CourseTitle=$"{n:00} / {names[n-1]}",CourseHint=hints[n-1],GoldTime=n==1?28:n==2?22:n==3?20:40,StartInMenu=menu});
        s.CreateGameObject("Daylight").AddComponent(new DirectionalLight{Intensity=.85f,AmbientIntensity=.12f,CastShadows=false,ShadowResolution=1024,ShadowDistance=70});
        var light=s.GameObjects.Last();light.Transform.LocalRotation=Quaternion.CreateFromYawPitchRoll(.5f,-.9f,0);
        var player=Spawn(s,"Player",new(0,.8f,5));
        // Each course is authored once as ordinary editable scene objects, not generated in gameplay.
        if(n==1)
        {
            Deck(s,"Orientation deck",new(0,-.3f,-15),new(14,.6f,48));
            Wall(s,new(-7,2,-15),new(.5f,4,48));Wall(s,new(7,2,-15),new(.5f,4,48));Wall(s,new(0,2,9),new(14,4,.5f));
            for(int i=0;i<5;i++)Stripe(s,new(0,.012f,1-i*8),new(1,.025f,1.8f));
            Spawn(s,"Pistol",new(-1.2f,1,-1));Spawn(s,"Glass",new(0,1.5f,-9));Enemy(s,new(-3,.1f,-15),0);Enemy(s,new(3,.1f,-22),1);Enemy(s,new(0,.1f,-29),2);
            Spawn(s,"Barrel",new(2,.7f,-14));Spawn(s,"Crate",new(-3,.6f,-5));
            Gate(s,new(0,1.6f,-31),new(13,3.2f,.35f));Spawn(s,"Milk",new(0,1,-36));
        }
        else if(n==2)
        {
            Deck(s,"Approach",new(0,-.3f,-3),new(12,.6f,24));Deck(s,"Landing",new(0,-.3f,-31),new(12,.6f,18));
            Wall(s,new(-4.6f,2.7f,-17),new(.6f,5.4f,22),Blue);Wall(s,new(4.6f,2.7f,-17),new(.6f,5.4f,22),White);
            Spawn(s,"Pistol",new(-1,1,-2));Spawn(s,"LaunchPad",new(2,.05f,-12));Enemy(s,new(2,.1f,-28),0);Spawn(s,"Milk",new(0,1,-36));
            for(int i=0;i<4;i++)Stripe(s,new(-3.8f,.02f,-6-i*2),new(.6f,.025f,1));
        }
        else if(n==3)
        {
            Deck(s,"Grapple approach",new(0,-.3f,-2),new(12,.6f,22));Deck(s,"Elevated landing",new(0,2.7f,-37),new(14,.6f,18));
            Wall(s,new(0,11,-23),new(3,2,3),Blue);Wall(s,new(-7,7,-23),new(.5f,14,32),White);Wall(s,new(7,7,-23),new(.5f,14,32),White);
            Spawn(s,"Scattergun",new(-1,1,-3));Enemy(s,new(-3,3.1f,-35),0);Enemy(s,new(3,3.1f,-40),1);Spawn(s,"Milk",new(0,4,-42));
            Wall(s,new(0,12.5f,-23),new(16,.5f,.5f),Dark);
        }
        else
        {
            Deck(s,"Runway",new(0,-.3f,-3),new(14,.6f,24));Deck(s,"Middle deck",new(0,-.3f,-33),new(14,.6f,18));Deck(s,"Milk deck",new(0,3.7f,-65),new(16,.6f,18));
            Wall(s,new(-5,2.7f,-18),new(.5f,5.4f,20),Blue);Wall(s,new(5,2.7f,-18),new(.5f,5.4f,20),White);Wall(s,new(0,12,-51),new(3,2,3),Blue);
            Spawn(s,"Pistol",new(-1,1,-1));Spawn(s,"Glass",new(0,1.5f,-7));Spawn(s,"LaunchPad",new(2,.05f,-12));Spawn(s,"Barrel",new(2,.7f,-30));Spawn(s,"Crate",new(-2,.7f,-31));Spawn(s,"Scattergun",new(-3,1,-35));
            Enemy(s,new(-3,.1f,-31),0);Enemy(s,new(3,.1f,-36),1);Enemy(s,new(-3,4.1f,-64),2);Enemy(s,new(3,4.1f,-69),3);Spawn(s,"Milk",new(0,5,-70));
        }
        // A large distant basin gives visual depth; it is a fail fall, not a playable collision floor.
        Cube(s,"Deep blue basin",new(0,-14,-32),new(120,1,150),new(.055f,.16f,.25f,1),false);
        Hud(s);Audio(s);TuneLighting(s);return s;
    }
    internal static void TuneLighting(Scene scene)
    {
        foreach(var obj in scene.GameObjects)
        {
            if(obj.GetComponent<DirectionalLight>() is {} light){light.Intensity=.85f;light.AmbientIntensity=.12f;light.CastShadows=false;}
            if(obj.GetComponent<MeshRenderer>() is not {} mesh)continue;
            bool deck=obj.GetComponent<BoxCollider3D>()!=null&&obj.Transform.LocalScale.X>8&&obj.Transform.LocalScale.Y<1&&obj.Transform.LocalScale.Z>8;
            bool reinforced=obj.Name.StartsWith("Orientation deck")||obj.Name is "Approach" or "Landing" or "Grapple approach" or "Elevated landing" or "Runway" or "Middle deck" or "Milk deck";
            if(deck||reinforced)
            {
                mesh.Material.BaseColor=new(.34f,.40f,.44f,1);
                float top=obj.Transform.LocalPosition.Y+obj.Transform.LocalScale.Y*.5f;
                obj.Transform.LocalPosition=new(obj.Transform.LocalPosition.X,top-1,obj.Transform.LocalPosition.Z);
                obj.Transform.LocalScale=new(obj.Transform.LocalScale.X,2,obj.Transform.LocalScale.Z);
                var collider=obj.GetComponent<BoxCollider3D>()!;collider.Enabled=true;collider.IsTrigger=false;
                var body=obj.GetComponent<Rigidbody3D>()??obj.AddComponent(new Rigidbody3D());body.BodyType=RigidbodyBodyType3D.Static;body.UseGravity=false;
            }
            else if(Vector4.Distance(mesh.Material.BaseColor,White)<.01f)mesh.Material.BaseColor=new(.55f,.61f,.65f,1);
        }
        var sky=scene.FindComponent<SkyEnvironment>()??scene.CreateGameObject("Facility lighting").AddComponent(new SkyEnvironment());
        sky.Exposure=1f;sky.AmbientIntensity=.28f;sky.EnvironmentIntensity=.25f;sky.SkyIntensity=.4f;sky.ZenithColor=new(.06f,.10f,.16f);sky.HorizonColor=new(.25f,.34f,.42f);sky.FogEnabled=false;
        var player=scene.FindComponent<ParkourPlayer3D>();if(player!=null){player.Transform.WorldPosition=new(0,.8f,5);player.GameObject.GetComponent<CharacterController3D>()!.GroundDistance=.25f;}
    }
    internal static void UpdateLighting()
    {
        using var authoring=new Authoring();var backup=Program.Root+"/Backups/BeforeLightingFix";Directory.CreateDirectory(backup);
        foreach(var path in Directory.GetFiles(Program.Root+"/Scenes","*.bytescene")){var copy=backup+"/"+Path.GetFileName(path);if(!File.Exists(copy))File.Copy(path,copy);var scene=authoring._scenes.Load(path);TuneLighting(scene);authoring._scenes.Save(scene,path);}
        Console.WriteLine("Updated facility lighting and floor/wall contrast in existing scenes.");
    }
    void Enemy(Scene s,Vector3 pos,int i){var obj=Spawn(s,"Enemy",pos);obj.GetComponent<KarlsonEnemy3D>()!.EnemyIndex=i;}
    void Gate(Scene s,Vector3 pos,Vector3 size){var gate=Cube(s,"Combat Gate",pos,size,Dark,true);gate.AddComponent(new CourseGate3D());var events=gate.AddComponent(new EventModuleComponent());events.AddModuleReference(_db.ResolveReference("Assets/Events/CombatGate.byteevents"));}
    static GameObject Cube(Scene s,string name,Vector3 pos,Vector3 size,Vector4 color,bool collision)
    {var o=s.CreateGameObject(name);o.Transform.LocalPosition=pos;o.Transform.LocalScale=size;o.AddComponent(new MeshRenderer{UsePrimitive=true,Material=new(){BaseColor=color,Roughness=.85f}});if(collision)o.AddComponent(new BoxCollider3D());return o;}
    static void Part(Scene s,GameObject parent,string name,Vector3 local,Vector3 size,Vector4 color){var o=Cube(s,name,local,size,color,false);o.SetParent(parent,false);}
    static void Deck(Scene s,string name,Vector3 pos,Vector3 size){Cube(s,name,pos,size,White,true);for(int z=0;z<size.Z;z+=4)Cube(s,"Deck joint",new(pos.X,pos.Y+size.Y*.5f+.006f,pos.Z-size.Z*.5f+z),new(size.X,.012f,.025f),new(.51f,.66f,.72f,1),false);}
    static void Wall(Scene s,Vector3 pos,Vector3 size,Vector4? color=null)=>Cube(s,"Course wall",pos,size,color??White,true);
    static void Stripe(Scene s,Vector3 pos,Vector3 size)=>Cube(s,"Route marker",pos,size,Blue,false);
    void Audio(Scene s){foreach(var file in Directory.GetFiles(Program.Root+"/Assets/Audio","*.wav")){string name=Path.GetFileNameWithoutExtension(file);s.CreateGameObject("Sound-"+name).AddComponent(new AudioSource3D{ClipReference=_db.ResolveReference("Assets/Audio/"+Path.GetFileName(file)),Spatial=false,Loop=name=="music",PlayOnStart=false,Volume=name=="music"?.15f:.45f,Bus=name=="music"?AudioBus.Music:AudioBus.Sfx});}}
    static void Hud(Scene s)
    {
        var canvas=s.CreateGameObject("Interface");canvas.AddComponent(new UiCanvas());
        GameObject Group(string name){var o=s.CreateGameObject(name);o.SetParent(canvas,false);return o;}
        void Text(GameObject p,string name,string text,float x,float y,int size=20,Vector4? color=null,float wrap=1100){var o=s.CreateGameObject(name);o.SetParent(p,false);o.AddComponent(new UiText{Text=text,Offset=new(x,y),FontSize=size,Color=color??White,WrapWidth=wrap,OrderInLayer=20,ShadowColor=new(0,0,0,.4f)});}
        void Panel(GameObject p,string name,float x,float y,float w,float h,Vector4 color){var o=s.CreateGameObject(name);o.SetParent(p,false);o.AddComponent(new UiWidget{Offset=new(x,y),Size=new(w,h),Color=color,OrderInLayer=10});}
        void Button(GameObject p,string name,string label,float x,float y,float w=500,float h=54){var o=s.CreateGameObject(name);o.SetParent(p,false);o.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Label=label,Offset=new(x,y),Size=new(w,h),FontSize=20,Color=new(.11f,.2f,.27f,1),HoverColor=new(.12f,.44f,.58f,1),PressedColor=Blue,OrderInLayer=20});}
        var menu=Group("MenuGroup");Panel(menu,"MenuPanel",0,0,620,720,new(.025f,.06f,.09f,.96f));Text(menu,"MenuKicker","BYTEENGINE / PARKOUR LAB",55,48,15,Blue);Text(menu,"MenuTitle","KARLSON",50,88,64);Text(menu,"MenuSubtitle","Fast feet. Physical weapons. Get the milk.",55,167,21);
        string[] cards=["01   ORIENTATION         Guns / glass / milk","02   WALL RUN                Slide / wall jump","03   THE GAP                    Grapple / swing","04   MILK RUN                  Chain everything"];
        for(int i=0;i<4;i++)Button(menu,"Course"+i,cards[i],55,232+i*72,510,58);
        Button(menu,"OptionsButton","CONTROLS & SETTINGS",55,546,510,48);Text(menu,"MenuFooter","An original ByteEngine recreation of the free prototype.\nChoose a course. Click Game View to capture the mouse.",55,635,15,new(.55f,.69f,.77f,1),510);
        var hud=Group("HUDGroup");Panel(hud,"TopPlate",20,20,1240,58,new(.025f,.06f,.09f,.86f));Text(hud,"CourseTitle","COURSE",40,37,20);Text(hud,"Timer","00.00 s",1045,34,27,Orange,190);Text(hud,"EnemyCount","HOSTILES",700,39,16,Blue,270);
        Text(hud,"Crosshair","+",627,339,26);Text(hud,"HitMarker","x",628,342,22,Orange);Text(hud,"MoveState","",565,387,18,Blue,200);
        Panel(hud,"BottomPlate",20,624,1240,76,new(.025f,.06f,.09f,.88f));Text(hud,"Weapon","EMPTY HANDS",40,638,21);Text(hud,"Speed","0.0 m/s",1080,638,24,Blue,180);Text(hud,"Hint","Reach the milk",40,676,14,new(.66f,.79f,.84f,1));
        foreach(var spec in new[]{("HealthBar",40f,new Vector4(.9f,.24f,.2f,1)),("FocusBar",260f,Blue)}){var o=s.CreateGameObject(spec.Item1);o.SetParent(hud,false);o.AddComponent(new UiWidget{Kind=UiWidgetKind.ProgressBar,Offset=new(spec.Item2,603),Size=new(180,5),Color=new(.12f,.18f,.2f,1),FillColor=spec.Item3});}
        var damage=s.CreateGameObject("DamageOverlay");damage.SetParent(hud,false);damage.AddComponent(new UiWidget{Size=new(1280,720),Color=Vector4.Zero,OrderInLayer=5});
        var pause=Group("PauseGroup");Panel(pause,"PausePanel",400,150,480,430,new(.025f,.06f,.09f,.96f));Text(pause,"PauseTitle","TAKE A BREATH",440,185,30);Button(pause,"ResumeButton","RESUME",440,270,400);Button(pause,"RetryButton","RESTART COURSE",440,340,400);Button(pause,"MenuButton","COURSE SELECT",440,410,400);
        var results=Group("ResultsGroup");Panel(results,"ResultsPanel",340,130,600,470,new(.025f,.06f,.09f,.96f));Text(results,"ResultTitle","MILK SECURED",390,170,40,Orange);Text(results,"ResultDetails","Results",390,245,21,White,490);Button(results,"NextButton","NEXT COURSE",390,375,500);Button(results,"ResultRetry","RETRY",390,445,240);Button(results,"ResultMenu","COURSES",650,445,240);
        var help=Group("HelpGroup");Panel(help,"HelpPanel",655,45,580,600,new(.025f,.06f,.09f,.97f));Text(help,"HelpTitle","CONTROLS",690,75,30,Blue);Text(help,"HelpControls","WASD / mouse   Move / look\nSpace   Jump / wall jump\nShift   Sprint     Ctrl or C   Slide\nRMB   Grapple (hold, release to detach)\nE   Pickup / grab / release\nLMB   Shoot     Q   Throw weapon\nF   Melee / throw grabbed object\nTab   Slow motion     R   Retry\nEscape   Pause / release mouse",690,135,20,White,510);Text(help,"SettingsReadout","Settings",690,435,16,Blue,510);Button(help,"MusicButton","MUSIC",690,480,160,48);Button(help,"ShakeButton","SHAKE",875,480,160,48);Button(help,"SensitivityDown","LOOK -",690,550,160,48);Button(help,"SensitivityUp","LOOK +",875,550,160,48);
    }
    public void Dispose(){_assets.Dispose();_db.Dispose();}
}

internal static class AudioAssets
{
    public static void Write(string directory)
    {
        foreach(var spec in new[]{("shot",.13,150.0),("hit",.08,700.0),("glass",.18,1900.0),("grapple",.15,440.0),("throw",.12,280.0),("pickup",.12,900.0),("explosion",.45,65.0),("goal",.65,660.0),("death",.5,120.0),("hurt",.15,95.0),("music",8.0,220.0)})
        {
            const int rate=22050;int count=(int)(rate*spec.Item2);using var stream=File.Create(Path.Combine(directory,spec.Item1+".wav"));using var w=new BinaryWriter(stream);w.Write("RIFF"u8);w.Write(36+count*2);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(count*2);
            var random=new Random(13);for(int i=0;i<count;i++){double t=(double)i/rate,envelope=Math.Pow(1-t/spec.Item2,2),f=spec.Item3;if(spec.Item1=="music"){int note=(int)(t*2)%8;int[] notes=[0,7,12,7,3,10,15,10];f=110*Math.Pow(2,notes[note]/12.0);envelope=.35*Math.Exp(-(t*2%1)*3);}
                double v=Math.Sin(t*f*2*Math.PI)*envelope;if(spec.Item1 is "shot" or "glass" or "explosion")v=.55*v+.45*(random.NextDouble()*2-1)*envelope;w.Write((short)(Math.Clamp(v,-1,1)*12000));}
        }
    }
}
