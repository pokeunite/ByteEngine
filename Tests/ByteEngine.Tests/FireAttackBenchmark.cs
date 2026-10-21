using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Vfx;
using ByteEngine.Core.VisualLogic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

/// <summary>Authors native editable assets and records the saved scene; no custom gameplay component.</summary>
internal static class FireAttackBenchmark
{
    internal static void Run(string root)
    {
        root=Path.GetFullPath(root);
        foreach(string dir in new[]{"Scenes","Assets/Vfx","Assets/Events","Assets/Blueprints","Preview"})Directory.CreateDirectory(Path.Combine(root,dir));
        foreach(string file in new[]{"Charge","Projectile","Impact"})if(!File.Exists(Path.Combine(root,$"Assets/Textures/{file}.png")))throw new FileNotFoundException("Import the publisher artwork first: "+file);
        using var window=new NativeWindow(new NativeWindowSettings{ClientSize=new(960,540),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core});
        window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var db=new AssetDatabase(root,["Assets","Scenes"]);
        using var assets=new AssetManager(db,Console.WriteLine);
        var components=new ComponentSerializer(root,db,assets,Console.WriteLine);
        var scenes=new SceneSerializer(components);
        var registration=AnimationRuntimeAssets.Configure(assets);
        var spawn=RuntimeSpawnService.ConfigureBlueprintSpawner((sc,reference,position)=>BlueprintRuntimeFactory.Spawn(sc,reference,position,db,scenes,Console.WriteLine));
        try
        {
            VfxLayer Art(string name,string texture,int cols,int rows,float duration,float size)=>new(){Name=name,Texture=new AssetReference($"Assets/Textures/{texture}.png"),FlipbookColumns=cols,FlipbookRows=rows,SpriteResolution=256,Additive=false,LocalSpace=true,Rate=0,Burst=1,MaxParticles=8,Lifetime=duration,LifetimeVariation=0,SizeVariation=0,Speed=0,SpeedVariation=0,Gravity=Vector3.Zero,StartSize=size,EndSize=size,StartColor=Vector4.One,EndColor=Vector4.One,RandomRotation=false};
            VfxLayer Sparks(string name,int count,float speed,float life)=>new(){Name=name,Sprite=VfxSprite.Spark,RenderMode=VfxRenderMode.Stretched,Burst=count,Rate=0,MaxParticles=96,Lifetime=life,LifetimeVariation=.2f,Speed=speed,Spread=180,Gravity=new(0,-1,0),StartSize=.055f,EndSize=0,StartColor=new(1,.55f,.1f,1),EndColor=new(1,.1f,0,0),Stretch=.07f,RandomRotation=true};
            var charge=new VfxEffect{Name="Fire – charge",Duration=.7f,ParticleBudget=128};
            var swirl=Art("Authored fire swirl","Charge",3,3,.7f,2.2f);swirl.StartSize=.6f;charge.Layers.Add(swirl);
            var ring=new VfxLayer{Name="Gathering ground ring",Sprite=VfxSprite.Ring,RenderMode=VfxRenderMode.Horizontal,Offset=new(0,-.9f,0),Burst=1,Rate=0,Speed=0,Gravity=Vector3.Zero,Lifetime=.7f,LifetimeVariation=0,StartSize=2.6f,EndSize=.3f,StartColor=new(1,.22f,.02f,.65f),EndColor=new(1,.65f,.15f,0),MaxParticles=4};charge.Layers.Add(ring);
            var projectile=new VfxEffect{Name="Fire – projectile",Loop=true,Duration=.24f,ParticleBudget=192};
            var body=Art("Animated fireball","Projectile",4,2,.24f,1.65f);body.Rate=1/.24f;body.Burst=1;projectile.Layers.Add(body);
            var wake=Sparks("World-space ember wake",0,.2f,.45f);wake.Rate=45;wake.Direction=-Vector3.UnitX;wake.Spread=50;wake.Gravity=new(0,.25f,0);projectile.Layers.Add(wake);
            var impact=new VfxEffect{Name="Fire – impact",Duration=.9f,ParticleBudget=256};
            var burst=Art("Authored fire eruption","Impact",4,5,.85f,4.2f);burst.Offset=new(-.4f,.75f,.4f);impact.Layers.Add(burst);
            var embers=Sparks("Radial embers",48,3,.8f);embers.Offset=new(-.4f,0,.4f);impact.Layers.Add(embers);
            var wave=new VfxLayer{Name="Expanding ground shock",Sprite=VfxSprite.Ring,RenderMode=VfxRenderMode.Horizontal,Burst=1,Rate=0,MaxParticles=4,Lifetime=.45f,LifetimeVariation=0,Speed=0,Gravity=Vector3.Zero,StartSize=.3f,EndSize=3.8f,Offset=new(-.4f,-.9f,0),StartColor=new(1,.45f,.03f,.7f),EndColor=new(1,.12f,0,0)};impact.Layers.Add(wave);
            foreach(var effect in new[]{charge,projectile,impact})VfxEffectSerializer.Save(Path.Combine(root,$"Assets/Vfx/{(effect==charge?"Charge":effect==projectile?"Projectile":"Impact")}.bvfx"),effect);
            db.Scan();
            var source=new Scene("Projectile authoring");var fire=source.CreateGameObject("Animated fireball");
            fire.AddComponent(new VfxPlayer{Effect=new("Assets/Vfx/Projectile.bvfx"),PlayOnStart=true,DistanceQuality=false});
            fire.AddComponent(new Projectile3D{Radius=.12f,Damage=25});fire.AddComponent(new LifetimeComponent{LifetimeSeconds=3});
            new BlueprintSerializer().Save(new BlueprintDefinition{Name="Fireball",Root=scenes.SerializeObject(fire)},Path.Combine(root,"Assets/Blueprints/Fireball.byteblueprint"));db.Scan();
            var scene=new Scene("Fire attack benchmark");
            scene.CreateGameObject("Dark preview environment").AddComponent(new SkyEnvironment{ZenithColor=new(.018f,.025f,.038f),HorizonColor=new(.018f,.025f,.038f),GroundColor=new(.018f,.025f,.038f),FogEnabled=false,EnvironmentLightingEnabled=false,Exposure=1});
            var camera=scene.CreateGameObject("Benchmark camera");camera.Transform.WorldPosition=new(0,4,11);
            Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,new(0,.65f,0),Vector3.UnitY),out var cameraWorld);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(cameraWorld);
            camera.AddComponent(new Camera3D{Orthographic=true,OrthographicWidth=10.8f,ActiveGameCamera=true});
            var caster=scene.CreateGameObject("Fire caster – open event sheet");caster.Transform.WorldPosition=new(-3,1,0);caster.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-MathF.PI/2);
            caster.AddComponent(new VfxPlayer{Effect=new("Assets/Vfx/Charge.bvfx"),PlayOnStart=false,DistanceQuality=false});
            caster.AddComponent(new ProjectileLauncher3D{ProjectileBlueprint=new("Assets/Blueprints/Fireball.byteblueprint"),ProjectileSpeed=6,Damage=25,MuzzleOffset=Vector3.Zero});
            var target=scene.CreateGameObject("Impact target – open event sheet");target.Transform.WorldPosition=new(3,1,0);
            target.AddComponent(new BoxCollider3D{Size=new(.6f,1.8f,.6f)});target.AddComponent(new HealthComponent());
            target.AddComponent(new VfxPlayer{Effect=new("Assets/Vfx/Impact.bvfx"),PlayOnStart=false,DistanceQuality=false});
            var targetVisual=scene.CreateGameObject("Target pillar");targetVisual.SetParent(target,false);targetVisual.Transform.LocalScale=new(.6f,1.8f,.6f);targetVisual.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,Material=new Material{Shading=MaterialShadingMode.Unlit,BaseColor=new(.28f,.36f,.43f,1)}});
            for(int x=-5;x<=5;x++)for(int z=-2;z<=2;z++)
            {var tile=scene.CreateGameObject($"Stage {x} {z}");tile.Transform.WorldPosition=new(x,-.1f,z);tile.Transform.LocalScale=new(.98f,.1f,.98f);tile.AddComponent(new MeshRenderer{UsePrimitive=true,Material=new Material{Shading=MaterialShadingMode.Unlit,BaseColor=(x+z)%2==0?new(.11f,.14f,.18f,1):new(.12f,.15f,.19f,1)}});}
            VisualInstruction I(string id,params (string key,EventValue value)[] args)=>new(){Id=id,Arguments=args.ToDictionary(a=>a.key,a=>a.value)};
            void Sheet(GameObject owner,string name,params EventRuleDefinition[] rules)
            {var module=new EventModuleDefinition{Name=name,Rules=rules.ToList()};string path=$"Assets/Events/{name}.byteevents";new EventModuleSerializer().Save(module,Path.Combine(root,path));owner.AddComponent(new EventModuleComponent()).AddResolvedModule(new(path),module);}
            Sheet(caster,"Fire attack sequence",
                new(){DisplayName="Begin – gather fire once",Conditions=[I("system.always"),I("system.triggerOnce")],Actions=[I("vfx.play"),I("time.startTimer",("name",EventValue.String("Charge")),("duration",EventValue.Number(.7)))]},
                new(){DisplayName="Charge ready – launch real projectile",Conditions=[I("time.timerFinished",("name",EventValue.String("Charge")))],Actions=[I("vfx.stop"),I("projectile.fire")]},
                new(){DisplayName="R – replay benchmark",Conditions=[I("input.keyPressed",("key",EventValue.String("R")))],Actions=[I("scene.restart")]});
            Sheet(target,"Fire impact reaction",new EventRuleDefinition(){DisplayName="Projectile damage – play impact once",Conditions=[I("health.percentAtMost",("value",EventValue.Number(.99))),I("system.triggerOnce")],Actions=[I("vfx.play")]});
            string scenePath=Path.Combine(root,"Scenes/Main.bytescene");scenes.Save(scene,scenePath);
            new ProjectSerializer().Save(new ProjectData{Name="VFX Fire Attack Benchmark"},Path.Combine(root,"VFX Fire Attack Benchmark.byteproject"));db.Scan();
            // Reload exactly what the editor opens, including resolved event-sheet assets.
            var saved=scenes.Load(scenePath);saved.RuntimeGlobals=new VariableStore();saved.LoadInternal();
            using var renderer=new Renderer3D();using var sprites=new Renderer2D();var cam=saved.FindGameObject("Benchmark camera")!.GetComponent<Camera3D>()!;
            var context=new RenderContext(sprites,renderer,saved,960,540,viewMatrix3D:cam.GetViewMatrix(),projectionMatrix3D:cam.GetProjectionMatrix(960f/540),prepareEnvironmentLighting3D:false,renderShadows3D:false);
            bool launched=false,hit=false,playedImpact=false;int peakParticles=0;
            for(int frame=0;frame<90;frame++)
            {
                Time.Update(1.0/30);saved.UpdateInternal();
                launched|=saved.GameObjects.Any(o=>o.GetComponent<Projectile3D>()?.Velocity.LengthSquared()>0);
                hit|=saved.FindGameObject(target.Name)!.GetComponent<HealthComponent>()!.CurrentHealth<100;
                playedImpact|=saved.FindGameObject(target.Name)!.GetComponent<VfxPlayer>()!.HasPlayed;
                peakParticles=Math.Max(peakParticles,saved.GameObjects.Sum(o=>o.GetComponent<VfxPlayer>()?.ActiveParticles??0));
                GL.Viewport(0,0,960,540);GL.DepthMask(true);GL.ClearColor(.018f,.025f,.038f,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
                saved.RenderInternal(context);GL.Finish();
                if(GL.GetError()!=ErrorCode.NoError)throw new Exception("Fire benchmark rendering error");
                byte[] pixels=new byte[960*540*4];GL.ReadPixels(0,0,960,540,PixelFormat.Bgra,PixelType.UnsignedByte,pixels);
                using var image=new System.Drawing.Bitmap(960,540,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bits=image.LockBits(new(0,0,960,540),System.Drawing.Imaging.ImageLockMode.WriteOnly,image.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(pixels,0,bits.Scan0,pixels.Length);image.UnlockBits(bits);image.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);
                image.Save(Path.Combine(root,$"Preview/frame-{frame:D3}.png"));
            }
            if(!launched||!hit||!playedImpact)throw new Exception($"Saved event-sheet sequence failed: launch={launched}, hit={hit}, impact={playedImpact}");
            File.WriteAllText(Path.Combine(root,"README.md"),$"# Fire attack benchmark\n\nOpen VFX Fire Attack Benchmark.byteproject in ByteEngine and press Play. R replays.\n\nCharge, Projectile and Impact in Assets/Vfx are editable layered VFX assets. Select the caster or target to open their named event sheets. The existing projectile component performs collision and damage; target health triggers impact. No custom runtime gameplay code.\n\nActual saved scene verified at 30 simulation frames/sec: projectile launched, target hit, impact played. Peak live particles: {peakParticles}. This is a correctness capture, not a laptop FPS benchmark.\n\nArtwork: Vefects free bundle, adapted to PNG; see ARTWORK-SOURCE.md. No claim of original texture authorship or complete Unity shader parity.\n");
            Console.WriteLine($"PASS saved scene: charge → projectile → collision → impact; peak particles {peakParticles}; {root}");
        }
        finally{RuntimeSpawnService.ClearBlueprintSpawner(spawn);AnimationRuntimeAssets.Clear(registration);}
    }
}





