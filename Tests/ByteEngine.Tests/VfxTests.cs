using System.Diagnostics;
using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Vfx;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class VfxTests
{
    private static int _checks;
    private static void Check(bool condition,string message)
    { if(!condition) throw new Exception("VFX: "+message); _checks++; }
    public static void Run(string root)
    {
        foreach(var preset in Enum.GetValues<VfxPreset>())
        {
            var effect=VfxPresets.Create(preset);
            string path=Path.Combine(root,"Assets",preset+".bvfx");
            effect.Layers[0].Texture=new AssetReference(Guid.NewGuid(),"Assets/sprite.png");
            VfxEffectSerializer.Save(path,effect);
            var loaded=VfxEffectSerializer.Load(path);
            Check(loaded.Name==effect.Name && loaded.Layers.Count==effect.Layers.Count,"preset round trip "+preset);
            Check(loaded.Layers[0].Texture==effect.Layers[0].Texture,"sprite reference round trip");
            loaded.Layers[0].Texture=AssetReference.Empty;
            var sim=new VfxSimulation(loaded,7); sim.Play(Matrix4x4.Identity);
            for(int frame=0;frame<300;frame++) sim.Advance(1f/60,Matrix4x4.Identity);
            Check(sim.ActiveCount<=sim.Capacity && sim.Capacity<=loaded.ParticleBudget,"bounded "+preset);
            foreach(var layer in sim.Layers)
                for(int i=0;i<layer.Count;i++)
                    Check(float.IsFinite(layer.Particles[i].Position.LengthSquared()),"finite particle position "+preset);
        }
        BudgetAndTiming(); CurvesAndMotion(); AtlasAndMesh(); Performance();
        using var db=new AssetDatabase(root,new[]{"Assets"});
        using var assets=new AssetManager(db);
        var record=db.Assets.First(a=>a.Type==AssetType.VfxEffect);
        Check(record!=null,"VFX asset type recognized");
        var reference=new AssetReference(record!.Guid,record.ProjectPath);
        var first=assets.LoadVfxEffect(reference);
        Check(ReferenceEquals(first,assets.LoadVfxEffect(reference)),"cached effect loading");
        assets.ReloadVfxEffects();
        Check(!ReferenceEquals(first,assets.LoadVfxEffect(reference)),"explicit hot reload invalidates cache");
        var codec=new ComponentSerializer(root,db,assets);
        var component=new VfxPlayer { Effect=reference, Preset=VfxPreset.Fire, Size=2, Intensity=.5f, Seed=123, PlayOnStart=false, DestroyWhenFinished=true,DistanceQuality=false,QualityDistance=12 };
        var data=codec.Serialize(component)!;
        var restored=(VfxPlayer)codec.Deserialize(data)!;
        Check(restored.Effect==reference && restored.Size==2 && restored.Seed==123 && !restored.PlayOnStart && restored.DestroyWhenFinished,"component round trip");
        Check(!restored.DistanceQuality && restored.QualityDistance==12,"distance quality round trip");
        Events();
        SpawnAndCleanup(assets,reference);
        PoolBudgets(reference);
        InspectorPresetChanges(reference);
        Console.WriteLine($"VFX: {_checks} checks passed.");
    }
    private static void BudgetAndTiming()
    {
        var effect=new VfxEffect { ParticleBudget=6,Duration=.2f,Layers=new() {
            new VfxLayer { MaxParticles=4,Burst=100,Rate=10000,Lifetime=.1f },
            new VfxLayer { MaxParticles=4,Burst=100,Rate=10000,Lifetime=.1f } }};
        var sim=new VfxSimulation(effect); sim.Play(Matrix4x4.Identity);
        Check(sim.Capacity==6 && sim.ActiveCount==6 && sim.DroppedParticles==194,"shared hard cap drops overflow");
        for(int i=0;i<100;i++) sim.Advance(1f/60,Matrix4x4.Identity);
        Check(!sim.IsPlaying && sim.ActiveCount==0,"one-shot completes and drains");
        sim.Play(Matrix4x4.Identity); sim.Stop(); Check(sim.ActiveCount>0 && !sim.IsEmitting,"stop drains instead of killing");
        sim.Stop(true); Check(sim.ActiveCount==0 && !sim.IsPlaying,"clear kills");
        var a=new VfxSimulation(VfxPresets.Create(VfxPreset.Magic),9);
        var b=new VfxSimulation(VfxPresets.Create(VfxPreset.Magic),9);
        a.Play(Matrix4x4.Identity); b.Play(Matrix4x4.Identity);
        for(int i=0;i<60;i++) a.Advance(1f/60,Matrix4x4.Identity);
        for(int i=0;i<120;i++) b.Advance(1f/120,Matrix4x4.Identity);
        Check(a.ActiveCount==b.ActiveCount,"fixed-step frame-rate independence");
        Check(Vector3.Distance(a.Layers[0].Particles[0].Position,b.Layers[0].Particles[0].Position)<.00001,"seeded reproducibility");
        int count=a.ActiveCount; a.Advance(float.NaN,Matrix4x4.Identity); a.Advance(-1,Matrix4x4.Identity);
        Check(a.ActiveCount==count,"invalid elapsed time ignored");
        var invalid=VfxPresets.Create(VfxPreset.Fire);
        invalid.Layers[0].Speed=float.NaN; invalid.Layers[0].Gravity=new(float.PositiveInfinity,0,0);
        var safe=new VfxSimulation(invalid);
        Check(float.IsFinite(safe.Layers[0].Settings.Speed) && safe.Layers[0].Settings.Gravity==Vector3.Zero,"nonfinite authoring inputs normalized");
        var world=VfxPresets.Create(VfxPreset.Sparks); world.Layers[0].Burst=1; world.Layers[0].Speed=0; world.Layers[0].Gravity=Vector3.Zero;
        var ws=new VfxSimulation(world); ws.Play(Matrix4x4.CreateTranslation(4,0,0));
        Check(Math.Abs(ws.Layers[0].Particles[0].Position.X-4)<.0001,"world emitter transform");
        world.Layers[0].LocalSpace=true; var ls=new VfxSimulation(world); ls.Play(Matrix4x4.CreateTranslation(4,0,0));
        Check(ls.Layers[0].Particles[0].Position==Vector3.Zero,"local-space positions remain local");
        var scene=new Scene("VFX tests"); var player=scene.CreateGameObject().AddComponent(new VfxPlayer { PlayOnStart=false });
        player.SetDefinition(VfxPresets.Create(VfxPreset.Sparks)); player.Play(); player.Paused=true;
        player.Advance(5); Check(player.ActiveParticles==30,"pause freezes playback");
        player.Paused=false; player.Stop(true); Check(!player.IsPlaying,"player clear");
        scene.DestroyGameObject(player.GameObject);
    }
    private static void AtlasAndMesh()
    {
        var layer=new VfxLayer { StartColor=new(1,0,0,1), EndColor=new(0,0,1,0),Sprite=VfxSprite.SoftDisc };
        byte[] pixels=VfxAtlas.GeneratePixels(layer,null,out int width,out int height);
        int start=(16*width+16)*4, end=((height-16)*width+16)*4;
        Check(pixels[start]>240 && pixels[start+3]>200 && pixels[end+2]>240 && pixels[end+3]==0,"baked lifetime color and fade");
        Check(pixels[3]==0,"soft sprite edge transparent");
        layer.OpacityOverLife=VfxCurve.Linear(0,0);
        pixels=VfxAtlas.GeneratePixels(layer,null,out _,out _);
        Check(pixels[start+3]==0,"opacity curve baked into atlas");
        using var mesh=new Mesh(new float[8*8],new uint[]{0,1,2,0,2,3,4,5,6,4,6,7},true);
        mesh.SetDrawIndexCount(6);
        Check(mesh.IndexCount==6 && mesh.IndexData.Length==6,"active triangles honored on desktop and portable path");
        bool rejected=false; try { mesh.SetDrawIndexCount(13); } catch(ArgumentOutOfRangeException) { rejected=true; }
        Check(rejected,"invalid draw count rejected");
    }
    private static void InspectorPresetChanges(AssetReference reference)
    {
        var scene=new Scene("VFX Inspector changes");
        var player=scene.CreateGameObject().AddComponent(new VfxPlayer { PlayOnStart=false });
        try
        {
            var descriptor=ByteEngine.Editor.ComponentPropertyRenderer.Descriptors(typeof(VfxPlayer),ByteEngine.Editor.PropertyEditorContext.Scene)
                .Single(p=>p.Property.Name==nameof(VfxPlayer.Preset));
            Check(ByteEngine.Editor.ComponentPropertyRenderer.SetValue(player,descriptor,VfxPreset.Fire,ByteEngine.Editor.PropertyEditorContext.Scene),
                "Inspector preset edit succeeds");
            Check(player.IsPlaying && player.ActiveParticles>0,"fresh continuous preset previews immediately");
            player.Stop(true);
            ByteEngine.Editor.ComponentPropertyRenderer.SetValue(player,descriptor,VfxPreset.Explosion,ByteEngine.Editor.PropertyEditorContext.Scene);
            Check(player.ActiveParticles>30,"stopped preset switches to explosion instead of staying blank");
            for(int i=0;i<360;i++) player.Advance(1f/60);
            Check(!player.IsPlaying,"one-shot finishes before switching");
            ByteEngine.Editor.ComponentPropertyRenderer.SetValue(player,descriptor,VfxPreset.Rain,ByteEngine.Editor.PropertyEditorContext.Scene);
            Check(player.IsPlaying && player.ActiveParticles>0 && player.ParticleCapacity==1024,"finished one-shot switches to rain immediately");
            player.Effect=reference;
            ByteEngine.Editor.ComponentPropertyRenderer.SetValue(player,descriptor,VfxPreset.Sparks,ByteEngine.Editor.PropertyEditorContext.Scene);
            Check(player.Effect.IsEmpty && player.Preset==VfxPreset.Sparks && player.ActiveParticles>0,"explicit preset clears overriding asset");
            Check(!player.PlayOnStart,"preview edit preserves gameplay startup setting");
        }
        finally { scene.DestroyGameObject(player.GameObject); }
    }
    private static void Events()
    {
        var registry=VisualLogicRegistry.CreateDefault();
        var scene=new Scene("VFX Events");
        var owner=scene.CreateGameObject("Effect");
        var player=owner.AddComponent(new VfxPlayer { PlayOnStart=false });
        var context=new EventExecutionContext { Self=owner,Scene=scene,Globals=new VariableStore() };
        Check(registry.TryGetCondition("vfx.finished",out var finished) && !finished!.Evaluate(new(),context),"finished is false before first playback");
        Check(registry.TryGetAction("vfx.play",out var play),"named play event registered"); play!.Execute(new(),context);
        Check(player.IsPlaying,"play action plays on self");
        Check(registry.TryGetAction("vfx.intensity",out var intensity),"intensity event registered");
        intensity!.Execute(new VisualInstruction { Arguments=new() { ["value"]=EventValue.Number(.25) } },context);
        Check(player.Intensity==.25f,"numeric event input");
        registry.TryGetAction("vfx.clear",out var clear); clear!.Execute(new(),context);
        Check(!player.IsPlaying && finished!.Evaluate(new(),context),"clear stops and finished condition");
        Check(registry.TryGetAction("vfx.spawn",out _),"spawn-at-position action");
        Check(registry.GetArguments("vfx.spawnOnObject").Count==5 && registry.GetArguments("vfx.impact").Count==4,"spawn event metadata exposes authoring inputs");
        registry.TryGetAction("vfx.beamTarget",out var beam);
        beam!.Execute(new VisualInstruction { Arguments=new() { ["position"]=EventValue.Vector3(new(3,4,5)) } },context);
        Check(registry.TryGetAction("vfx.rayImpact",out var rayImpact),"named raycast impact action registered");
        rayImpact!.Execute(new(),context); Check(scene.GameObjects.Count==1,"ray impact does nothing on a miss");
        scene.DestroyGameObject(owner);
    }
    private static void Performance()
    {
        var effect=VfxPresets.Create(VfxPreset.Fire); effect.ParticleBudget=4096;
        var layer=effect.Layers[0]; layer.MaxParticles=4096; layer.Rate=6000; layer.Lifetime=1;
        layer.Turbulence=.4f; layer.Attraction=.1f; layer.SpeedOverLife=VfxCurve.Linear(1,.3f);
        var sim=new VfxSimulation(effect); sim.Play(Matrix4x4.Identity);
        for(int i=0;i<240;i++) sim.Advance(1f/60,Matrix4x4.Identity);
        var samples=new double[600];
        long allocated=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
        for(int i=0;i<samples.Length;i++)
        {
            long t=Stopwatch.GetTimestamp(); sim.Advance(1f/60,Matrix4x4.Identity);
            samples[i]=Stopwatch.GetElapsedTime(t).TotalMilliseconds;
        }
        double elapsed=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        long bytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Array.Sort(samples);
        Check(bytes==0,"steady-state particle simulation allocates zero managed bytes");
        Check(sim.ActiveCount<=4096,"stress cap respected");
        Console.WriteLine($"VFX simulation benchmark: 4096 capacity, 600 frames, avg {elapsed/600:0.000} ms, p95 {samples[570]:0.000} ms, allocation {bytes} bytes. Rendering/GPU cost is not included.");
    }
    private static void SpawnAndCleanup(AssetManager assets,AssetReference reference)
    {
        var registration=AnimationRuntimeAssets.Configure(assets);
        var scene=new Scene("VFX disposable tests");
        try
        {
            var owner=scene.CreateGameObject("Owner");
            var context=new EventExecutionContext { Scene=scene,Self=owner,Globals=new VariableStore() };
            var registry=VisualLogicRegistry.CreateDefault();
            registry.TryGetAction("vfx.spawn",out var spawn);
            var instruction=new VisualInstruction { Arguments=new() {
                ["effect"]=EventValue.String(reference.Guid.ToString()), ["position"]=EventValue.Vector3(new(2,3,4))
            }};
            for(int i=0;i<70;i++) spawn!.Execute(instruction,context);
            Check(scene.GameObjects.Count==65,"spawn has a 64-effect concurrency cap");
            Check(scene.GameObjects[1].Transform.WorldPosition==new Vector3(2,3,4),"spawn uses world position");
            for(int frame=0;frame<600;frame++)
                foreach(var obj in scene.GameObjects.ToArray()) if(obj.Active) obj.GetComponent<VfxPlayer>()?.Advance(1f/60);
            Check(scene.GameObjects.Count<=17 && scene.GameObjects.All(o=>o==owner || !o.Active),"disposable effects drain into bounded idle cache");
            int pooledCount=scene.GameObjects.Count;
            var pooledIds=scene.GameObjects.Select(o=>o.Id).ToHashSet();
            spawn!.Execute(instruction,context);
            Check(scene.GameObjects.Count==pooledCount && scene.GameObjects.Any(o=>o!=owner && o.Active && pooledIds.Contains(o.Id)),"repeat spawn reuses existing object and buffers");
            registry.TryGetAction("vfx.impact",out var impact);
            instruction.Arguments["normal"]=EventValue.Vector3(Vector3.UnitX);
            impact!.Execute(instruction,context);
            Check(scene.GameObjects.Any(o=>o!=owner && o.Active && Vector3.Distance(o.Transform.Up,Vector3.UnitX)<.001f),"impact aligns emission up to hit normal");
            registry.TryGetAction("vfx.spawnOnObject",out var onObject);
            onObject!.Execute(new VisualInstruction { Arguments=new() {
                ["effect"]=EventValue.String(reference.Guid.ToString()),["object"]=EventValue.String("Self"),["follow"]=EventValue.Boolean(true) } },context);
            Check(owner.Children.Count==1,"spawn on object follows parent");
            var box=owner.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D());
            context.LastRaycastHit=new ByteEngine.Core.Physics.RaycastHit3D(owner,box,new(7,8,9),Vector3.UnitZ,5);
            registry.TryGetAction("vfx.rayImpact",out var ray);
            ray!.Execute(instruction,context);
            Check(scene.GameObjects.Any(o=>o.Active && o.Transform.WorldPosition==new Vector3(7,8,9) && Vector3.Distance(o.Transform.Up,Vector3.UnitZ)<.001f),"ray impact consumes actual hit position and normal");
            Check(assets.LoadVfxEffect(reference).Loop==VfxEffectSerializer.Load(assets.ResolveProjectPath(reference.ProjectPath)).Loop,"spawn does not mutate cached authoring definition");
        }
        finally
        {
            foreach(var obj in scene.GameObjects.ToArray()) scene.DestroyGameObject(obj);
            AnimationRuntimeAssets.Clear(registration);
        }
    }
    private static void CurvesAndMotion()
    {
        var curve=new VfxCurve { Keys=new() { new(1,0),new(.5f,2),new(0,1) } }; curve.Validate();
        Check(Math.Abs(curve.Evaluate(.25f)-1.5f)<.0001f && curve.Evaluate(2)==0,"sorted piecewise lifetime curve");
        curve.Keys=new() { new(float.NaN,float.PositiveInfinity),new(0,2),new(1,10) }; curve.Validate();
        Check(curve.Keys.Count==2 && curve.Evaluate(1)==4,"invalid curve values and duplicate times normalized");
        var effect=new VfxEffect { Duration=.1f,Layers=new() { new VfxLayer {
            Burst=1,Rate=0,Speed=1,SpeedVariation=0,Direction=Vector3.UnitX,Spread=0,Gravity=Vector3.Zero,Drag=0,Lifetime=2,LifetimeVariation=0,
            SpeedOverLife=VfxCurve.Linear(0,0),Wind=new(1,0,0),Turbulence=.3f,Attraction=1,AttractionPoint=new(2,0,0) } } };
        var sim=new VfxSimulation(effect); sim.Play(Matrix4x4.Identity);
        for(int n=0;n<20;n++) sim.Advance(1f/60,Matrix4x4.Identity);
        Check(sim.Layers[0].Particles[0].Position==Vector3.Zero,"zero speed curve freezes displacement while forces integrate");
        Check(sim.Layers[0].Particles[0].Velocity.X>1,"wind and attraction accelerate particle");
        string path=Path.Combine(Path.GetTempPath(),"vfx-curves-"+Guid.NewGuid()+".bvfx");
        try { VfxEffectSerializer.Save(path,effect); var restored=VfxEffectSerializer.Load(path);
            Check(restored.Layers[0].SpeedOverLife?.Evaluate(.5f)==0 && restored.Layers[0].Wind==new Vector3(1,0,0),"curves and forces round trip"); }
        finally { File.Delete(path); }
        effect.Layers[0].Delay=1; var delayed=new VfxSimulation(effect); delayed.Play(Matrix4x4.Identity);
        delayed.Advance(.2f,Matrix4x4.Identity); Check(delayed.ActiveCount==0 && !delayed.IsEmitting,"delay beyond one-shot duration does not emit");
        var trail=VfxPresets.Create(VfxPreset.Trail); trail.Layers[0].TrailBreakDistance=1;
        var ribbon=new VfxSimulation(trail); ribbon.Play(Matrix4x4.Identity);
        ribbon.Advance(1f/60,Matrix4x4.Identity); ribbon.Advance(1f/60,Matrix4x4.CreateTranslation(10,0,0));
        var last=ribbon.Layers[0].Particles[ribbon.Layers[0].Count-1];
        Check(last.Position==last.PreviousPosition,"teleports break ribbon instead of spanning scene");
        foreach(var normal in new[]{Vector3.UnitY,-Vector3.UnitY,Vector3.UnitX,Vector3.UnitZ})
            Check(Vector3.Distance(Vector3.Transform(Vector3.UnitY,VfxSpawnPool.AlignUp(normal)),normal)<.001f,"impact normal orientation "+normal);
        var legacy=new VfxLayer(); legacy.Validate(); Check(legacy.SizeOverLife==null && legacy.Wind==Vector3.Zero,"legacy defaults retain original look");
    }
    private static void PoolBudgets(AssetReference reference)
    {
        var scene=new Scene("VFX global budget");
        var large=new VfxEffect { Duration=.01f,ParticleBudget=8192,Layers=new() { new VfxLayer { MaxParticles=8192,Burst=1,Rate=0,Lifetime=.02f,LifetimeVariation=0 } } };
        try
        {
            var pool=VfxSpawnPool.For(scene);
            for(int n=0;n<8;n++) pool.Spawn(scene,reference,0,large,Vector3.Zero,Quaternion.Identity,1);
            bool capped=false;
            try { pool.Spawn(scene,reference,0,large,Vector3.Zero,Quaternion.Identity,1); }
            catch(InvalidOperationException) { capped=true; }
            Check(capped && scene.GameObjects.Count==8,"scene-wide reserved particle budget rejects excess effects before allocating");
            foreach(var obj in scene.GameObjects.ToArray()) if(obj.Active) obj.GetComponent<VfxPlayer>()!.Advance(.1f);
            Check(scene.GameObjects.Count<=2 && scene.GameObjects.Sum(o=>o.GetComponent<VfxPlayer>()!.ParticleCapacity)<=16384,"idle pool obeys reserved-particle memory ceiling");
            var ids=scene.GameObjects.Select(o=>o.Id).ToHashSet();
            var refreshed=pool.Spawn(scene,reference,1,large,Vector3.Zero,Quaternion.Identity,1);
            Check(!ids.Contains(refreshed.GameObject.Id),"asset revision does not reuse stale definitions");
        }
        finally { foreach(var obj in scene.GameObjects.ToArray()) scene.DestroyGameObject(obj); }
    }
}
