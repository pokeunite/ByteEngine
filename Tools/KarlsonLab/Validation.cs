using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.VisualLogic;
using Karlson.Plugin;
namespace KarlsonLab;

internal sealed class LabRuntime:IDisposable
{
    public AssetDatabase Database{get;}public AssetManager Assets{get;}public SceneSerializer Serializer{get;}
    readonly Guid _audio,_spawner;
    public LabRuntime()
    {
        Program.Register();Database=new(Program.Root,["Assets","Scenes"],Console.WriteLine,Console.WriteLine);Assets=new(Database,Console.WriteLine);
        var components=new ComponentSerializer(Program.Root,Database,Assets,Console.WriteLine);ByteEnginePluginRegistry.ApplyComponentCodecs(components,Console.WriteLine);AudioSerializationRegistrar.Register(components);PhysicsSerializationRegistrar.Register(components);Serializer=new(components);
        _audio=AudioRuntimeAssets.Configure(Database,Console.WriteLine);_spawner=RuntimeSpawnService.ConfigureBlueprintSpawner((s,b,p)=>BlueprintRuntimeFactory.Spawn(s,b,p,Database,Serializer,Console.WriteLine));
    }
    public Scene Load(string path)
    {
        var scene=Serializer.Load(Path.Combine(Program.Root,path));
        // Keep test best times, settings and deliberate failure reports out of the playable project's saves/logs.
        foreach(var director in scene.GameObjects.Select(o=>o.GetComponent<KarlsonDirector3D>()).OfType<KarlsonDirector3D>())typeof(KarlsonDirector3D).GetField("ProjectRoot",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(director,Program.Root+"/Preview/TestState");
        return scene;
    }
    public void Dispose(){AudioRuntimeAssets.Clear(_audio);RuntimeSpawnService.ClearBlueprintSpawner(_spawner);Assets.Dispose();Database.Dispose();}
}
internal static class TestInput
{
    static readonly Action<double> UpdateTime=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate<Action<double>>();
    static readonly Action<RawInputSnapshot,bool> UpdateRaw=typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate<Action<RawInputSnapshot,bool>>();
    static readonly Action<bool> SetCapture=typeof(Input).GetMethod("SetGameInputCaptured",BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate<Action<bool>>();
    internal static void Frame(Scene scene,float dt=1f/60,Key[]? keys=null,MouseButton[]? buttons=null,Vector2? mouse=null)
    {
        var raw=new RawInputSnapshot();if(keys!=null)raw.KeysDown.UnionWith(keys);if(buttons!=null)raw.MouseButtonsDown.UnionWith(buttons);
        if(mouse is {} delta)raw.MouseDelta=delta;
        UpdateRaw(raw,true);SetCapture(true);Input.SetGameViewPointer(new(.5f),new(1280,720),true);UpdateTime(dt);
        typeof(Scene).GetMethod("UpdateInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(scene,null);
    }
    internal static void Clear(){UpdateRaw(new(),true);SetCapture(false);}
}
internal static class Validation
{
    public static void CheckSpawns()
    {
        using var runtime=new LabRuntime();var host=new SceneManager();
        for(int number=1;number<=4;number++)foreach(float dt in new[]{1f/60,.1f,.25f})
        {
            var scene=runtime.Load($"Scenes/Course{number:00}.bytescene");host.LoadScene(scene);scene.FindComponent<KarlsonDirector3D>()!.Begin();var player=scene.FindComponent<ParkourPlayer3D>()!;
            for(int i=0;i<Math.Ceiling(3/dt);i++)TestInput.Frame(scene,dt:dt);
            Check(player.IsGrounded&&player.Transform.WorldPosition.Y>=-.05f&&!player.GameObject.GetComponent<HealthComponent>()!.IsDead,$"Course {number} stationary spawn survives frame time {dt:0.000}s");
            for(int i=0;i<Math.Ceiling(1/dt);i++)TestInput.Frame(scene,keys:[Key.W],dt:dt);
            Check(player.Transform.WorldPosition.Y>=-.05f,$"Course {number} approach floor holds a moving character at frame time {dt:0.000}s");host.UnloadScene();
        }
        TestInput.Clear();
    }
    static readonly List<string> Checks=new();
    internal static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);Checks.Add(message);Console.WriteLine("PASS "+message);}
    public static void RunTraversal(){using var runtime=new LabRuntime();var host=new SceneManager();Traversal(runtime,host);}
    public static void Run()
    {
        using var runtime=new LabRuntime();var host=new SceneManager();
        foreach(var path in Directory.GetFiles(Program.Root+"/Scenes","*.bytescene"))
        {
            var scene=runtime.Load("Scenes/"+Path.GetFileName(path));
            Check(!scene.GameObjects.SelectMany(o=>o.Components).Any(c=>c.GetType().Name=="MissingComponent"),"All components deserialize: "+scene.Name);
            Check(scene.FindComponent<ParkourPlayer3D>()?.GameObject.Children.Any(c=>c.Id==scene.FindComponent<ParkourPlayer3D>()!.CameraObjectId)==true,"Blueprint camera reference remaps: "+scene.Name);
            Check(scene.GameObjectCount<350,"Bounded authored object count: "+scene.Name);
            host.LoadScene(scene);var director=scene.FindComponent<KarlsonDirector3D>()!;director.Begin();
            // Native movement/physics is advanced with portable input; no transform mock replaces the motor.
            for(int i=0;i<20;i++)TestInput.Frame(scene);
            var player=scene.FindComponent<ParkourPlayer3D>()!;Check(player.IsGrounded,"Native motor grounds: "+scene.Name);
            var start=player.Transform.WorldPosition;for(int i=0;i<35;i++)TestInput.Frame(scene,keys:[Key.W]);
            Check(Vector3.Distance(start,player.Transform.WorldPosition)>2,"Native input moves the character: "+scene.Name);
            TestInput.Frame(scene,keys:[Key.W,Key.Space]);for(int i=0;i<8;i++)TestInput.Frame(scene,keys:[Key.W]);Check(player.Transform.WorldPosition.Y>.25f,"Jump rises through native capsule motor: "+scene.Name);
            TestInput.Frame(scene,keys:[Key.W,Key.C]);Check(player.GameObject.GetComponent<CapsuleCollider3D>()!.Height<=1.01f,"Crouch changes collision height: "+scene.Name);
            TestInput.Frame(scene);float beforeFocus=director.Focus;for(int i=0;i<30;i++)TestInput.Frame(scene,keys:[Key.Tab]);Check(director.Focus<beforeFocus&&scene.SimulationTimeScale<.5f,"Slow motion drains focus and scales scene simulation: "+scene.Name);
            TestInput.Frame(scene);director.Pause();var frozen=player.Transform.WorldPosition;for(int i=0;i<10;i++)TestInput.Frame(scene);Check(Vector3.Distance(frozen,player.Transform.WorldPosition)<.02f,"Pause freezes physics: "+scene.Name);director.Resume();
            var gun=scene.GameObjects.Select(o=>o.GetComponent<KarlsonWeapon3D>()).First(w=>w!=null&&w.GameObject.Parent==null)!;player.Equip(gun);Check(gun.Held,"Equip physical weapon: "+scene.Name);
            var ammo=gun.Ammo;TestInput.Frame(scene);gun.Shoot(player);Check(gun.Ammo==ammo-1,"Native hitscan consumes ammo: "+scene.Name);player.ThrowWeapon();Check(!gun.Held&&gun.GameObject.GetComponent<Rigidbody3D>()?.Enabled==true,"Throw restores physical body: "+scene.Name);
            foreach(var enemy in scene.GameObjects.Select(o=>o.GetComponent<KarlsonEnemy3D>()).OfType<KarlsonEnemy3D>().ToArray())enemy.GameObject.GetComponent<HealthComponent>()!.Kill();
            for(int i=0;i<3;i++)TestInput.Frame(scene);Check(director.EnemiesCleared,"Enemy deaths update course state: "+scene.Name);
            var gate=scene.FindComponent<CourseGate3D>();if(gate!=null)Check(gate.IsOpen,"Authored ByteGraph opens gate after clear");
            var goal=scene.FindComponent<MilkGoal3D>()!;player.Transform.WorldPosition=goal.Transform.WorldPosition-Vector3.UnitY*.8f;player.Motor!.SetVelocity(Vector3.Zero);for(int i=0;i<3;i++)TestInput.Frame(scene);
            Check(director.Mode==CourseMode.Completed,"Authored milk event completes course: "+scene.Name);
            var roundtrip=Program.Root+"/Preview/roundtrip.bytescene";runtime.Serializer.Save(scene,roundtrip);var restored=runtime.Serializer.Load(roundtrip);Check(restored.GameObjectCount==scene.GameObjectCount,"Scene serialization roundtrip: "+scene.Name);
            host.UnloadScene();
        }
        var test=new Scene("Mechanism checks");var victim=test.CreateGameObject("Victim");var hp=victim.AddComponent(new HealthComponent{MaxHealth=100});int deaths=0;hp.Died+=_=>deaths++;hp.Kill();hp.Kill();hp.Revive();hp.Kill();Check(deaths==2,"Health death event occurs once per life and revival resets it");
        var obstruction=test.CreateGameObject("Wall");obstruction.Transform.WorldPosition=new(0,1,-2);obstruction.AddComponent(new BoxCollider3D{Size=new(3,3,.3f)});var behind=test.CreateGameObject("Target behind wall");behind.Transform.WorldPosition=new(0,1,-4);behind.AddComponent(new BoxCollider3D());var health=behind.AddComponent(new HealthComponent());var shooter=test.CreateGameObject("Ray shooter");var emitter=shooter.AddComponent(new HitscanEmitter3D());emitter.Fire(new(0,1,0),-Vector3.UnitZ);Check(health.CurrentHealth==100,"Hitscan cannot damage through walls");
        host.LoadScene(test);test.SimulationTimeScale=.25f;TestInput.Frame(test,dt:.1f);Check(Math.Abs(Time.DeltaTime-.1)<1e-5,"Scene time scope restores host delta");host.UnloadScene();TestInput.Clear();
        Traversal(runtime,host);
        Combat(runtime,host);
        File.WriteAllText(Program.Root+"/Preview/validation.json",JsonSerializer.Serialize(new{checks=Checks.Count,passed=Checks,scope="Headless native engine components, portable input, physics, serialized scenes, ByteGraph events; visual checks are separate"},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("VALIDATION COMPLETE "+Checks.Count+" checks");
    }
    static void Combat(LabRuntime runtime,SceneManager host)
    {
        var scene=runtime.Load("Scenes/Course01.bytescene");host.LoadScene(scene);var director=scene.FindComponent<KarlsonDirector3D>()!;director.Begin();var player=scene.FindComponent<ParkourPlayer3D>()!;
        var health=player.GameObject.GetComponent<HealthComponent>()!;health.Invulnerable=true;
        for(int i=0;i<10;i++)TestInput.Frame(scene);
        var glass=scene.GameObjects.First(o=>o.GetComponent<KarlsonProp3D>() is {Breakable:true,Explosive:false});var glassId=glass.Id;
        player.Transform.WorldPosition=new(0,0,-4);player.Transform.ResetInterpolation();player.SetLookRotation(0,0);player.Equip(scene.GameObjects.Select(o=>o.GetComponent<KarlsonWeapon3D>()).First(w=>w!=null&&w.GameObject.Parent==null)!);
        TestInput.Frame(scene);TestInput.Frame(scene,buttons:[MouseButton.Left]);Check(scene.FindGameObject(glassId)==null,"Actual LMB hitscan breaks glass and removes its collision");
        var enemy=scene.FindComponent<KarlsonEnemy3D>()!;var enemyHealth=enemy.GameObject.GetComponent<HealthComponent>()!;
        enemy.Transform.WorldPosition=new(0,0,-18);enemy.Pursue=false;player.Transform.WorldPosition=new(0,0,-12);player.Transform.ResetInterpolation();player.Motor!.SetVelocity(Vector3.Zero);
        health.Invulnerable=false;for(int i=0;i<420;i++)TestInput.Frame(scene);Check(health.IsDead&&director.Mode==CourseMode.Failed,"Enemy line of sight and successive shots kill a stationary player and show retry state");
        enemyHealth.Kill();TestInput.Frame(scene);Check(enemy.GameObject.GetComponent<Rigidbody3D>()?.Enabled==true&&enemy.GameObject.GetComponent<CharacterController3D>()?.Enabled==false,"Enemy death switches native motor to a physical tumbling body");
        Check(enemy.GameObject.Children.All(o=>o.GetComponent<KarlsonWeapon3D>()==null),"Enemy death releases its physical weapon");
        host.UnloadScene();
        scene=runtime.Load("Scenes/Course01.bytescene");host.LoadScene(scene);director=scene.FindComponent<KarlsonDirector3D>()!;director.Begin();player=scene.FindComponent<ParkourPlayer3D>()!;player.GameObject.GetComponent<HealthComponent>()!.Invulnerable=true;
        var barrel=scene.GameObjects.First(o=>o.GetComponent<KarlsonProp3D>() is {Explosive:true});enemy=scene.FindComponent<KarlsonEnemy3D>()!;enemy.Transform.WorldPosition=barrel.Transform.WorldPosition+Vector3.UnitZ*2;
        barrel.GetComponent<KarlsonProp3D>()!.Impact(100);TestInput.Frame(scene);Check(enemy.GameObject.GetComponent<HealthComponent>()!.IsDead&&scene.FindGameObject(barrel.Id)==null,"Barrel blast damages nearby enemies once and destroys the barrel");
        Check(scene.GameObjects.Count(o=>o.Name=="Budgeted breakable fragment")==6,"Barrel fragments obey the six-piece effect budget");
        for(int i=0;i<140;i++)TestInput.Frame(scene);Check(!scene.GameObjects.Any(o=>o.Name=="Budgeted breakable fragment"),"Temporary breakable fragments expire through native lifetime component");
        director.Complete();var best=director.BestTime;host.UnloadScene();scene=runtime.Load("Scenes/Course01.bytescene");host.LoadScene(scene);Check(Math.Abs(scene.FindComponent<KarlsonDirector3D>()!.BestTime-best)<.001f,"Best course time survives scene reload through native save service");host.UnloadScene();TestInput.Clear();
    }
    static void Traversal(LabRuntime runtime,SceneManager host)
    {
        var scene=runtime.Load("Scenes/Course02.bytescene");host.LoadScene(scene);var director=scene.FindComponent<KarlsonDirector3D>()!;director.Begin();var player=scene.FindComponent<ParkourPlayer3D>()!;player.GameObject.GetComponent<HealthComponent>()!.Invulnerable=true;
        for(int i=0;i<25;i++)TestInput.Frame(scene);for(int i=0;i<25;i++)TestInput.Frame(scene,keys:[Key.W,Key.LeftShift]);
        TestInput.Frame(scene,keys:[Key.W,Key.C]);Check(player.Sliding&&player.Speed>4,"Momentum slide enters at running speed");
        player.Transform.WorldPosition=new(-4,1,-17);player.Transform.ResetInterpolation();player.Motor!.SetVelocity(new(0,0,-10));TestInput.Frame(scene);TestInput.Frame(scene,keys:[Key.W]);Check(player.WallRunning,"Native side-wall query activates wall running over the gap");
        TestInput.Frame(scene,keys:[Key.W,Key.Space]);Check(player.Motor.Velocity.Y>5&&player.Motor.Velocity.X>1,"Wall jump gives outward and upward velocity");
        host.UnloadScene();scene=runtime.Load("Scenes/Course03.bytescene");host.LoadScene(scene);director=scene.FindComponent<KarlsonDirector3D>()!;director.Begin();player=scene.FindComponent<ParkourPlayer3D>()!;player.GameObject.GetComponent<HealthComponent>()!.Invulnerable=true;
        for(int i=0;i<20;i++)TestInput.Frame(scene);player.SetLookRotation(0,18.5f);TestInput.Frame(scene);TestInput.Frame(scene,buttons:[MouseButton.Right]);Check(player.Grappling,"Native aimed ray attaches grapple to the elevated course anchor");
        var start=player.Transform.WorldPosition;for(int i=0;i<65;i++){TestInput.Frame(scene,keys:[Key.W,Key.LeftShift],buttons:[MouseButton.Right]);if(i%20==0)Console.WriteLine($"GRAPPLE {i} active={player.Grappling} target={player.GrapplePoint} mode={director.Mode} position={player.Transform.WorldPosition} velocity={player.Motor!.Velocity}");}Check(player.Transform.WorldPosition.Z<start.Z-8&&player.Transform.WorldPosition.Y>1,"Grapple pulls native character forward and upward");
        TestInput.Frame(scene);Check(!player.Grappling,"Releasing grapple detaches cable and retains movement");
        for(int i=0;i<35;i++)TestInput.Frame(scene,keys:[Key.W]);Check(player.Transform.WorldPosition.Z<-28&&player.Transform.WorldPosition.Y>=2.9f,"Grapple course reaches elevated landing using native movement without teleporting the crossing");
        host.UnloadScene();scene=runtime.Load("Scenes/Course01.bytescene");host.LoadScene(scene);director=scene.FindComponent<KarlsonDirector3D>()!;director.Begin();player=scene.FindComponent<ParkourPlayer3D>()!;player.GameObject.GetComponent<HealthComponent>()!.Invulnerable=true;
        for(int i=0;i<10;i++)TestInput.Frame(scene);
        var gun=scene.GameObjects.Select(o=>o.GetComponent<KarlsonWeapon3D>()).First(w=>w!=null&&w.GameObject.Parent==null)!;
        player.Transform.WorldPosition=gun.Transform.WorldPosition+new Vector3(0,-.1f,2);player.Transform.ResetInterpolation();player.SetLookRotation(0,-35);TestInput.Frame(scene);TestInput.Frame(scene,keys:[Key.E]);Check(player.Equipped==gun,"E input picks up the physical weapon through native interaction query");
        player.SetLookRotation(0,0);TestInput.Frame(scene);var camera=scene.ActiveCamera!;Check(Vector3.Distance(gun.Transform.WorldPosition,camera.Transform.WorldPosition)<1,"Equipped weapon attaches to remapped eye anchor");
        player.ThrowWeapon(false);var crate=scene.GameObjects.First(o=>o.GetComponent<KarlsonProp3D>() is {Breakable:false}&&o.GetComponent<Rigidbody3D>()!=null);var body=crate.GetComponent<Rigidbody3D>()!;var damping=body.LinearDamping;
        player.Transform.WorldPosition=crate.Transform.WorldPosition+new Vector3(0,-.1f,2);player.SetLookRotation(0,-35);TestInput.Frame(scene);TestInput.Frame(scene,keys:[Key.E]);Check(body.LinearDamping>5,"E input grabs native dynamic body");
        TestInput.Frame(scene,keys:[Key.F]);Check(Math.Abs(body.LinearDamping-damping)<.001f&&body.Velocity.Length()>5,"Throwing a grabbed prop restores body settings and applies impulse");
        host.UnloadScene();
        var resources=new Scene("Resource validation");var meter=resources.CreateGameObject("Energy").AddComponent(new ResourceMeter3D{Capacity=40,InitialAmount=35});host.LoadScene(resources);Check(!meter.TrySpend(36)&&meter.Current==35,"Resource rejects overspending without mutation");Check(meter.TrySpend(15)&&meter.Current==20,"Resource spends exact amount");meter.Restore(100);Check(meter.Current==40,"Resource restoration clamps to capacity");host.UnloadScene();
        var mechanism=new Scene("Launch and persistence"){FixedSimulation=true};var floor=mechanism.CreateGameObject("Floor");floor.Transform.WorldPosition=new(0,-.3f,0);floor.AddComponent(new BoxCollider3D{Size=new(20,.6f,20)});
        var pad=mechanism.CreateGameObject("Pad");pad.Transform.WorldPosition=new(0,.2f,0);pad.AddComponent(new BoxCollider3D{Size=new(3,.6f,3),IsTrigger=true});pad.AddComponent(new LaunchVolume3D{LaunchVelocity=new(0,12,0)});
        var launched=mechanism.CreateGameObject("Launch target");launched.Transform.WorldPosition=new(0,.1f,0);launched.AddComponent(new CapsuleCollider3D());launched.AddComponent(new CharacterController3D());host.LoadScene(mechanism);for(int i=0;i<12;i++)TestInput.Frame(mechanism);Check(launched.Transform.WorldPosition.Y>1,"Native launch trigger applies velocity to character motor");host.UnloadScene();
        var mask=ByteEngine.Core.Classification.LayerMask.FromLayers(2,3);var ray=mechanism.CreateGameObject("Masked ray").AddComponent(new HitscanEmitter3D{HitMask=mask});var path=Program.Root+"/Preview/mechanisms.bytescene";runtime.Serializer.Save(mechanism,path);var loaded=runtime.Serializer.Load(path);Check(loaded.FindComponent<HitscanEmitter3D>()?.HitMask==mask,"Hitscan layer mask survives serialized scene roundtrip");
        TestInput.Clear();
    }
}
