using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("browser")]
await Task.CompletedTask;

public static partial class BrowserGame
{
    private static readonly PortableGameLoop Loop = new();
    private static readonly BrowserFrameSink Sink = new();
    private static readonly BrowserAudio Audio = new();
    private static readonly RawInputSnapshot Snapshot = new();
    private static GameProjectRuntime? _project;
    private static bool _started;
    private static double _lastLoopMilliseconds, _lastEncodeMilliseconds;
    private static readonly List<BrowserRuntimeMarker> RuntimeMarkers=[];

    [JSExport]
    public static void MountFile(string path, string base64)
    {
        if (_started) throw new InvalidOperationException("Content cannot change after startup.");
        string full = GamePackageExporter.ResolveInside("/game", path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, Convert.FromBase64String(base64));
    }

    [JSExport]
    public static void MountBytes(string path, [JSMarshalAs<JSType.Array<JSType.Number>>] byte[] data)
    {
        if (_started) throw new InvalidOperationException("Content cannot change after startup.");
        string full=GamePackageExporter.ResolveInside("/game",path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using var file=File.Create(full);file.Write(data);
    }

    [JSExport]
    [return: JSMarshalAs<JSType.Array<JSType.Number>>]
    public static byte[] ReadBuffer(int index) => Sink.ReadBuffer(index);
    [JSExport]
    public static void ConfigureSkinning(int maximumBones)=>Sink.MaximumSkinningBones=Math.Clamp(maximumBones,0,128);

    [JSExport]
    public static void PhysicsProfile(int iterations,int substeps){Loop.Scenes.ActiveScene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneCompany.DuneWorkshop3D>().FirstOrDefault()?.SetPhysicsProfile(iterations,substeps);}
    [JSExport]
    public static string Diagnostics(){var scene=Loop.Scenes.ActiveScene;var vehicle=scene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneCompany.DuneWorkshop3D>().FirstOrDefault();return JsonSerializer.Serialize(new {scene=scene?.Name,sceneMetrics=scene?.LastFrameMetrics,building=vehicle?.Building,velocity=vehicle?.PhysicalVelocity.Length(),position=vehicle?.Transform.WorldPosition.ToString(),winch=vehicle?.WinchMission,blocks=vehicle?.Blocks.Count,motorsport=vehicle?.MotorsportDiagnostics,physicsMs=vehicle?.LastDriveCpuMilliseconds,physicsSteps=vehicle?.PhysicsSteps,physicsPhases=vehicle?.PhysicsPhases,suspension=vehicle?.SuspensionDiagnostics,droppedPhysicsSeconds=vehicle?.DroppedPhysicsSeconds, loopMs=_lastLoopMilliseconds, encodeMs=_lastEncodeMilliseconds,managedMemoryMB=GC.GetTotalMemory(false)/1048576.0,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2)});}
    [JSExport]
    public static string SaveKey(){var data=new ByteEngine.Core.Serialization.ProjectSerializer().Load("/game/Content/Game.byteproject");return "byteengine-saves-"+data.ProjectId.ToString("N")+(data.RuntimeSaveId is {} id ? "-"+id.ToString("N") : "");}

    [JSExport]
    public static string AnimationDiagnostics()
    {
        var skins=Loop.Scenes.ActiveScene?.GameObjects.SelectMany(o=>o.Components.OfType<SkeletalMeshRenderer>()).ToArray()??[];
        return JsonSerializer.Serialize(skins.Select(s=>{
            var m=s.ResolvedModel;bool valid=s.TryGetCurrentModelBounds(out var b);
            return new {name=s.GameObject.Name,s.Enabled,s.Visible,active=s.GameObject.ActiveInHierarchy,
                model=m?.Name,s.CurrentAnimation,s.PlaybackTime,s.SkinnedMeshCount,
                position=new[]{s.Transform.WorldPosition.X,s.Transform.WorldPosition.Y,s.Transform.WorldPosition.Z},
                bounds=valid?new[]{b.Minimum.X,b.Minimum.Y,b.Minimum.Z,b.Maximum.X,b.Maximum.Y,b.Maximum.Z}:null,
                firstNode=m?.Nodes.FirstOrDefault()?.LocalTransform.ToString(),
                firstRotation=m?.Animations.FirstOrDefault()?.Channels.FirstOrDefault(c=>c.Rotation?.Keys.Count>0)?.Rotation?.Keys.FirstOrDefault().Value.ToString()};
        }));
    }
    [JSExport]
    public static void RestoreSaves(string json){if(_started)throw new InvalidOperationException("Restore before startup");if(json.Length>10*1024*1024)throw new InvalidDataException("Browser save is too large");var data=new ByteEngine.Core.Serialization.ProjectSerializer().Load("/game/Content/Game.byteproject");var directory=GameSaveStorage.Register("/game/Content",data.ProjectId,data.RuntimeSaveId);foreach(var entry in JsonSerializer.Deserialize<Dictionary<string,string>>(json)??[]){if(!entry.Key.EndsWith(".json",StringComparison.OrdinalIgnoreCase)&&!entry.Key.EndsWith(".json.name",StringComparison.OrdinalIgnoreCase)&&entry.Key!="active-blueprint.txt")continue;var path=GamePackageExporter.ResolveInside(directory,entry.Key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,entry.Value);}}
    [JSExport]
    public static string SaveData(){if(_project==null)return "{}";var root=_project.SaveDirectory;return JsonSerializer.Serialize(Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(p=>(p.EndsWith(".json")||p.EndsWith(".json.name")||Path.GetFileName(p)=="active-blueprint.txt")&&!p.Contains("/Diagnostics/")&&!p.Contains("/Backups/")).ToDictionary(p=>Path.GetRelativePath(root,p).Replace('\\','/'),File.ReadAllText));}

    [JSExport]
    public static void ConfigurePresentation(string json)
    {
        using var doc=JsonDocument.Parse(json);var e=doc.RootElement;
        if(e.TryGetProperty("quality",out var q)&&Enum.TryParse<GraphicsQuality>(q.GetString(),out var quality))BrowserPresentationSettings.Quality=quality;
        if(e.TryGetProperty("worldResolution",out var res))BrowserPresentationSettings.WorldResolution=res.GetString()??"720p";
        if(e.TryGetProperty("textureLimit",out var size))BrowserPresentationSettings.TextureLimit=Math.Clamp(size.GetInt32(),256,8192);
    }

    [JSExport]
    public static void Start(bool demo)
    {
        RuntimePerformanceMarkers.Observer=(kind,duration)=>{if(RuntimeMarkers.Count<128)RuntimeMarkers.Add(new(kind,duration));};
        // Editor node highlighting has no consumer in a packaged game.
        ByteEngine.Core.VisualLogic.VisualLogicDebugTrace.Enabled = false;
        ByteEngine.Core.VisualLogic.VisualLogicDebugTrace.Clear();
        ByteEngine.Core.VisualLogic.CreatorHost.OpenUrl=Audio.OpenUrl;
        ByteEngine.Core.Media.VideoHost.Create=path=>new ByteEngine.Browser.BrowserVideoPlayback(path,Audio.Enqueue);
        if (_started) return;
        ByteEngine.Core.Plugins.ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.desertterrain", new DesertTerrain.DesertTerrainPlugin());
        ByteEngine.Core.Plugins.ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.dunecompany", new DuneCompany.DuneCompanyPlugin());
        ByteEngine.Core.Plugins.ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.karlsondemo", new KarlsonDemo.Plugin.KarlsonDemoPlugin());
        PortableAudio.Backend = Audio;
        ByteEngine.Core.Net.NetworkRuntime.CreateLink=()=>new BrowserNetworkLink();
        ByteEngine.Core.Net.NetworkDiagnostics.Download=(name,text)=>BrowserNetworkLink.Outbound.Enqueue((new BrowserNetworkCommand(0,"download",signal:new ByteEngine.Core.Net.NetworkSignal("log",text,name)),null));
        if (!demo)
        {
            _project = new GameProjectRuntime("/game/Content/Game.byteproject", Console.Error.WriteLine);
            string currentPath=_project.Project.StartupScene;
            Loop.Scenes.SceneFactory=path=>{var fresh=_project.LoadScene(path);Sink.PrewarmTextures(_project.PreparedSpawnTextures);currentPath=path;return fresh;};
            Loop.Scenes.ProgressiveSceneFactory=path=>{currentPath=path;return PreparedSteps(path);};
            Loop.Scenes.ShowLoadingScreen=true;
            Loop.Scenes.QuitHandler=()=>{_exited=true;Loop.Scenes.UnloadScene();};
            var scene = _project.LoadStartupScene();
            Sink.PrewarmTextures(_project.PreparedSpawnTextures);
            Loop.Scenes.RestartSceneFactory = () =>
            {
                Loop.Scenes.GlobalVariables.Clear();
                foreach (var variable in _project.Project.GlobalVariables)
                    Loop.Scenes.GlobalVariables.Set(variable.Name, variable.Value.Clone());
                var fresh=_project.LoadScene(currentPath);Sink.PrewarmTextures(_project.PreparedSpawnTextures);return fresh;
            };
            Loop.Scenes.LoadScene(scene);
        }
        else
        {
            var scene = new Scene("Browser backend verification");
            var camera = new GameObject("Camera");
            camera.Transform.LocalPosition = new(0, 2, 6);
            camera.Transform.EulerAngles = new(-12, 0, 0);
            camera.AddComponent(new Camera3D { ActiveGameCamera = true });
            scene.AddGameObject(camera);
            var cube = new GameObject("Shared Core object");
            cube.AddComponent(new MeshRenderer { UsePrimitive = true,
                Material = new Material { BaseColor = new(.15f, .65f, 1, 1) } });
            cube.AddComponent(new VerificationMovement());
            scene.AddGameObject(cube);
            InputActions.Configure(InputMap.CreateDefault());
            Loop.Scenes.LoadScene(scene);
        }
        _started = true;
    }

    [JSExport]
    public static void AudioEnded(string id)
    {
        if (Guid.TryParse(id, out var voiceId)) Audio.Ended(voiceId);
    }

    [JSExport]
    public static void Touch(int id,double x,double y,string phase)
    {if(Enum.TryParse<TouchPhase>(phase,out var parsed))TouchInput.Feed(id,(float)x,(float)y,parsed);}
    [JSExport]
    public static void CancelTouches()=>TouchInput.CancelAll();
    [JSExport]
    public static void AudioPosition(string id,double seconds){if(Guid.TryParse(id,out var voice))Audio.SetPosition(voice,seconds);}
    [JSExport]
    public static void VideoState(string id,double seconds,bool paused,bool ended,string error)=>ByteEngine.Browser.BrowserVideoPlayback.Report(id,seconds,paused,ended,error);

    private static string _lastFrameError = "";
    private static IEnumerable<SceneLoadStep> PreparedSteps(string path)
    {foreach(var step in _project!.LoadSceneSteps(path)){if(step.Scene!=null)Sink.PrewarmTextures(_project.PreparedSpawnTextures);yield return step;}}
    [JSExport]
    public static string LastFrameError() => _lastFrameError;

    [JSExport]
    public static void ProfileComponents(bool enabled)
    {
        _captureComponents=enabled;
        _captureAccumulate=false;
        if (Loop.Scenes.ActiveScene is { } scene){scene.ComponentProfiler.Enabled = enabled;scene.ComponentProfiler.AccumulateFrames=false;}
    }

    [JSExport]
    public static string ComponentTimings() => JsonSerializer.Serialize(
        Loop.Scenes.ActiveScene?.ComponentProfiler.Snapshot() ?? []);

    [JSExport]
    public static string VfxDiagnostics() => JsonSerializer.Serialize(
        Loop.Scenes.ActiveScene?.GameObjects.SelectMany(o=>o.Components.OfType<ByteEngine.Core.Vfx.VfxPlayer>())
        .Select(v=>new { name=v.GameObject.Name,v.Status,v.IsPlaying,v.ActiveParticles,v.RenderedParticles,
            layers=VfxLayers(v) }).ToArray() ?? []);

    private static object[] VfxLayers(ByteEngine.Core.Vfx.VfxPlayer player)
    {
        try
        {
            var effect=player.Effect.IsEmpty?ByteEngine.Core.Vfx.VfxPresets.Create(player.Preset):
                ByteEngine.Core.Animation.AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null?assets.LoadVfxEffect(player.Effect):null;
            return effect?.Layers.Select(l=>(object)new {l.Name,l.Rate,l.Burst,l.StartSize,
                color=new[]{l.StartColor.X,l.StartColor.Y,l.StartColor.Z,l.StartColor.W},
                offset=new[]{l.Offset.X,l.Offset.Y,l.Offset.Z}}).ToArray() ?? [];
        }
        catch(Exception e) { return [new {error=e.Message}]; }
    }

    [JSExport]
    public static bool HasExited() => _exited;
    private static bool _exited;
    [JSExport]
    public static bool WantsPointerLock() => !Input.KeepGameViewPointerFree &&
        Loop.Scenes.ActiveScene?.GameObjects.Any(o=>o.ActiveInHierarchy&&(o.Components.OfType<PlayerController3D>().Any(c=>c.Enabled&&c.AcceptLookInput)||o.Components.Any(c=>c.GetType().Name=="PvpMatch3D")&&ByteEngine.Core.Net.NetworkRuntime.Session is {IsActive:true}))==true;

    [JSExport]
    public static string Frame(double seconds, int width, int height, string keys,
        string buttons, double mouseX, double mouseY, double deltaX, double deltaY,
        double wheel, bool focused, bool captured, string gamepad)
    {
        if (!_started) throw new InvalidOperationException("Call Start first.");
        string phase="input";
        _lastFrameError = "";
        try
        {
        Snapshot.Clear();
        foreach (string name in keys.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (Enum.TryParse(name, out Key key)) Snapshot.KeysDown.Add(key);
        foreach (string name in buttons.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (Enum.TryParse(name, out MouseButton button)) Snapshot.MouseButtonsDown.Add(button);
        Snapshot.MouseDelta = new((float)deltaX, (float)deltaY);
        Snapshot.MouseWheel = (float)wheel;
        if (!string.IsNullOrWhiteSpace(gamepad))
        {
            using var json = JsonDocument.Parse(gamepad);
            var axes = json.RootElement.GetProperty("axes").EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
            float Axis(int i) => i < axes.Length ? axes[i] : 0;
            Snapshot.Gamepad.LeftStick = new(Axis(0), -Axis(1));
            Snapshot.Gamepad.RightStick = new(Axis(2), -Axis(3));
            var values = json.RootElement.GetProperty("buttons").EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
            Snapshot.Gamepad.LeftTrigger = values.Length > 6 ? values[6] : 0;
            Snapshot.Gamepad.RightTrigger = values.Length > 7 ? values[7] : 0;
            string[] names = ["South","East","West","North","LeftShoulder","RightShoulder","LeftTrigger","RightTrigger",
                "Back","Start","LeftStickButton","RightStickButton","DPadUp","DPadDown","DPadLeft","DPadRight"];
            for (int i = 0; i < Math.Min(values.Length, names.Length); i++)
                if (values[i] > .5 && Enum.TryParse<GamepadControl>(names[i], out var control))
                    Snapshot.Gamepad.ButtonsDown.Add(control);
        }
        Sink.ProfileStages=true;Sink.Begin();
        phase="scene update and rendering";
        if(Loop.Scenes.ActiveScene is {} profiledScene){profiledScene.ComponentProfiler.Enabled=_captureComponents;profiledScene.ComponentProfiler.AccumulateFrames=_captureAccumulate;}
        long start=System.Diagnostics.Stopwatch.GetTimestamp();
        Loop.Tick(seconds, Snapshot, width, height, new((float)mouseX, (float)mouseY), focused, captured, Sink);
        _lastLoopMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        start=System.Diagnostics.Stopwatch.GetTimestamp();
        phase="deferred uploads and frame encoding";
        string result=Sink.Finish(Audio.Drain(),_lastLoopMilliseconds,Loop,RuntimeMarkers);
        RuntimeMarkers.Clear();
        _lastEncodeMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return result;
        }
        catch(Exception exception)
        {
            _lastFrameError = $"Browser frame failed during {phase} in '{Loop.Scenes.ActiveScene?.Name}' ({width}x{height}): {exception}";
            throw new InvalidOperationException(_lastFrameError,exception);
        }
    }

    private sealed class VerificationMovement : Component
    {
        protected override void OnUpdate()
        {
            float delta = (float)Time.DeltaTime;
            float x = (Input.IsKeyDown(Key.D) ? 1 : 0) - (Input.IsKeyDown(Key.A) ? 1 : 0);
            float z = (Input.IsKeyDown(Key.S) ? 1 : 0) - (Input.IsKeyDown(Key.W) ? 1 : 0);
            Transform.LocalPosition += new Vector3(x, 0, z) * delta * 2;
            Transform.EulerAngles += new Vector3(0, delta * 25, 0);
        }
    }
}

internal sealed class BrowserFrameSink : IRenderFrameSink
{
    private readonly Dictionary<string,double> _stageTimes=new();
    private bool _profileStages;private int _profileStageFrames;
    internal bool ProfileStages{get=>_profileStages;set{if(value&&!_profileStages){_stageTimes.Clear();_profileStageFrames=0;}_profileStages=value;}}
    private long ProfileStart()=>ProfileStages?System.Diagnostics.Stopwatch.GetTimestamp():0;
    private void ProfileEnd(string name,long start){if(start!=0)_stageTimes[name]=_stageTimes.GetValueOrDefault(name)+System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
    public int MaximumSkinningBones {get;set;}
    internal long HiddenMeshBudgetBytes {get;set;}=96L*1024*1024;
    private readonly HashSet<Texture2D> _preparedTextures=new();
    public void PrewarmTextures(IReadOnlyList<Texture2D> textures)
    {
        _preparedTextures.Clear();long retained=0;
        foreach(var texture in textures){long bytes=(long)texture.PixelData.Length+(long)texture.FloatPixelData.Length*4;
            if(bytes==0||retained+bytes>64L*1024*1024)continue;retained+=bytes;_preparedTextures.Add(texture);Texture(texture);}
    }
    private int _submittedCount,_visibleCount;
    public object CaptureStats()=>new {submitted=_submittedCount,visible=_visibleCount,culled=_submittedCount-_visibleCount,batches=_draws.Count,profiledFrames=_profileStageFrames,stagesMs=_stageTimes.ToDictionary(p=>p.Key,p=>p.Value/Math.Max(1,_profileStageFrames)),
        staticCacheHits=_staticCacheHits,staticCacheMisses=_staticCacheMisses,staticDraws=_cachedStaticDraws.Count,dynamicSubmissions=_dynamicRender.Count,
        buffers=_buffers.Count,bufferBytes=_buffers.ActiveBytes,transferPoolBytes=_buffers.RetainedBytes,transferCreatedBytes=_buffers.CreatedBytes,transferReuseHits=_buffers.ReuseHits,pendingTextureChunks=_pendingTextureUploads.Count,
        residentMeshes=_meshes.Count,residentTextures=_textures.Count,materials=_materials.Count,fontAtlases=_fonts.Count,
        cpuMeshBytes=_meshes.Keys.Sum(m=>(long)m.VertexData.Length*4+(long)m.IndexData.Length*4),
        cpuTextureBytes=_textures.Keys.Sum(t=>(long)t.PixelData.Length+(long)t.FloatPixelData.Length*4)};
    private readonly Dictionary<Mesh, (int Id, int Version)> _meshes = new();
    private readonly Dictionary<Texture2D, (int Id, int Version)> _textures = new();
    private readonly Dictionary<Texture2D, int> _textureLastFrame = new();
    private int _frameNumber;
    private readonly Dictionary<(string Path, int Size), FontAtlas> _fonts = new();
    private readonly HashSet<FontAtlas> _usedFonts = new();
    private readonly HashSet<Mesh> _usedMeshes = new();
    private readonly Dictionary<Mesh,ReadOnlyMemory<uint>> _meshIndices = new();
    private readonly Dictionary<Mesh,int> _meshLastFrame = new();
    private readonly List<Mesh> _hiddenMeshes = new();
    private readonly List<Texture2D> _unusedTextures = new();
    private readonly List<MaterialKey> _unusedMaterials = new();
    private readonly HashSet<Texture2D> _usedTextures = new();
    private readonly List<BrowserMeshUpload> _uploads = new();
    private readonly List<BrowserTextureUpload> _textureUploads = new();
    private readonly List<BrowserDrawCommand> _draws = new();
    private readonly List<BrowserDrawCommand> _drawPool = new();
    private readonly RenderBatcher.Builder _batchBuilder = new(){MaterialAffinity=true};
    private readonly List<RenderSubmission> _visible = new();
    private readonly List<RenderSubmission> _shadowVisible = new();
    private readonly List<RenderSubmission> _staticRender = new(), _dynamicRender = new(), _cachedStaticRender = new();
    private readonly List<BrowserDrawCommand> _cachedStaticDraws = new();
    private readonly List<Mesh> _cachedStaticMeshes = new();
    private readonly List<int> _cachedStaticVersions = new();
    private readonly List<Texture2D> _cachedStaticTextures = new();
    private readonly List<MaterialKey> _cachedStaticMaterialKeys = new();
    private readonly Dictionary<Material,Material> _cachedStaticMaterials = new(ReferenceEqualityComparer.Instance);
    private readonly RenderBatcher.Builder _staticBatchBuilder = new(){MaterialAffinity=true};
    private bool _staticCacheValid;
    private int _staticCacheHits, _staticCacheMisses, _staticMissStreak, _staticCacheCooldown;
    private readonly RenderBatcher.Builder _shadowBatchBuilder = new();
    private readonly SoftwareOcclusionCuller _occlusion = new();
    private Matrix4x4? _shadowViewProjection;
    private readonly List<byte[]> _retainedPackets=new(),_retainedSkins=new();
    private byte[] _packetScratch=Array.Empty<byte>();
    private int RetainPacket(List<byte[]> cache,int slot,ReadOnlySpan<byte> bytes){
        while(cache.Count<=slot)cache.Add(Array.Empty<byte>());
        if(cache[slot].AsSpan().SequenceEqual(bytes))return -1;
        if(cache[slot].Length!=bytes.Length)cache[slot]=new byte[bytes.Length];
        bytes.CopyTo(cache[slot]);return Buffer(bytes);
    }
    private readonly List<RenderSubmission> _otherVisible = new();
    private readonly Dictionary<MaterialKey, int> _materials = new();
    private readonly Dictionary<Material,int> _frameMaterialIds = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<MaterialKey> _usedMaterials = new();
    private readonly List<BrowserDraw> _materialUploads = new();
    private int _nextMaterial;
    private readonly record struct MaterialKey(int Height, int Texture, int Normal, int MetallicMap, int RoughnessMap,
        int AoMap, int PackedMap, int EmissionMap, Vector4 Color, Vector4 HeightUv, Vector2 HeightSize,
        Vector3 Emission, Vector2 Tiling, Vector2 Offset, float HeightRange, float HeightBias, float NormalStrength,
        float Metallic, float Roughness, float Ao, float Cutoff, int AoChannel, int RoughnessChannel,
        int MetallicChannel, bool DirectX, bool Packed, bool Unlit, bool Srgb, bool Depth, bool Write,
        BlendMode3D Blend, CullMode3D Cull, FrontFaceWinding3D Front, int SandImprint, Vector2 SandOrigin,Vector4[]? Palette);
    private readonly List<BrowserUi> _ui = new();
    private readonly List<BrowserUi> _uiPool = new();
    private int _uiUsed;
    private BrowserUi ReuseUi()
    {
        var command=_uiUsed<_uiPool.Count?_uiPool[_uiUsed]:new BrowserUi();
        if(_uiUsed==_uiPool.Count)_uiPool.Add(command);
        _uiUsed++;_ui.Add(command);return command;
    }
    private readonly FrameTransferBuffers _buffers = new(32L*1024*1024,pinned:true);
    public byte[] ReadBuffer(int index) => _buffers[index];
    private int Buffer(ReadOnlySpan<byte> data) => _buffers.Copy(data);
    readonly Queue<(Texture2D Texture,int Id,int X,int Y,int W,int H,bool Allocate)> _pendingTextureUploads=new();
    private float[] _inverseVp=[];
    private float[] _vp = new float[16], _eye = [0,0,0], _background = [.07f,.1f,.15f];
    private BrowserLight[] _lights = [], _points = [];
    private float _ambient = .25f, _exposure = 1;
    private float[] _pixelArt=[0,4,8,.35f];
    private float[] _look=[1,1,0],_fog=[0,0,0,0,1,0,0,0];
    private BrowserSky? _sky;
    private BrowserShadow? _shadow;
    private int _nextId, _nextTexture;
    public void Begin()
    {
        if(ProfileStages)_profileStageFrames++;
        _frameNumber++;
        _submittedCount=_visibleCount=0;
        _uploads.Clear(); _textureUploads.Clear(); _draws.Clear(); _ui.Clear(); _uiUsed=0; _buffers.BeginFrame();
        _revalidatedTextures.Clear();
        _usedMeshes.Clear(); _usedTextures.Clear(); _usedFonts.Clear(); _sky=null;_shadow=null;_shadowViewProjection=null;
        _usedMaterials.Clear(); _frameMaterialIds.Clear(); _materialUploads.Clear();
    }

    private readonly HashSet<Texture2D> _uiTextures=new();
    private int UiTexture(Texture2D texture){if(_uiTextures.Add(texture)&&_textures.TryGetValue(texture,out var cached))_textures[texture]=(cached.Id,-1);return Texture(texture);}
    private int Texture(Texture2D? texture)
    {
        if (texture == null) return 0;
        _usedTextures.Add(texture);_textureLastFrame[texture]=_frameNumber;
        bool resident=_textures.TryGetValue(texture,out var cached);
        if (resident && cached.Version == texture.ContentVersion) return cached.Id;
        if (texture.PixelData.IsEmpty && texture.FloatPixelData.IsEmpty)
        {
            // Disposal released the CPU pixels. A resident copy is already correct in
            // the worker; an unuploaded texture degrades to untextured instead of
            // failing the whole frame when teardown or atlas eviction disposes it.
            if (resident) { _textures[texture]=(cached.Id,texture.ContentVersion); return cached.Id; }
            _usedTextures.Remove(texture);_textureLastFrame.Remove(texture);_uiTextures.Remove(texture);
            ReportMissingPixels(texture);return 0;
        }
        if (!resident) cached = (++_nextTexture, -1);
        bool allocate=cached.Version<0;
        foreach(var r in texture.ChangedRegionsSince(cached.Version))for(int y=r.Y;y<r.Y+r.Height;y+=128)for(int x=r.X;x<r.X+r.Width;x+=128){_pendingTextureUploads.Enqueue((texture,cached.Id,x,y,Math.Min(128,r.X+r.Width-x),Math.Min(128,r.Y+r.Height-y),allocate));allocate=false;}
        _textures[texture] = (cached.Id, texture.ContentVersion);
        return cached.Id;
    }
    private readonly HashSet<Texture2D> _reportedMissingTextures=new();
    private void ReportMissingPixels(Texture2D texture)
    {
        // Bounded diagnostics: a texture disposed before upload must never fail a
        // frame, but the first occurrences still name the owner of the bug.
        if(_reportedMissingTextures.Count>=32||!_reportedMissingTextures.Add(texture))return;
        string source=texture.DiagnosticSource??(string.IsNullOrEmpty(texture.FilePath)?"generated texture":Path.GetFileName(texture.FilePath));
        Console.WriteLine($"Browser texture '{source}' ({texture.Width}x{texture.Height}) was disposed before it was uploaded; drawing it untextured.");
    }
    private readonly HashSet<Texture2D> _revalidatedTextures=new();
    private void InvalidateTexture(Texture2D texture,int id)
    {
        // One invalidation per texture per frame is enough: the next request stages
        // the whole image again from ChangedRegionsSince(-1).
        if(!_revalidatedTextures.Add(texture))return;
        if(_textures.TryGetValue(texture,out var resident)&&resident.Id==id)_textures[texture]=(id,-1);
    }

    public void Draw3D(RenderView3D? view, RenderLighting3D lighting,
        RenderEnvironment3D environment, IReadOnlyList<RenderSubmission> submissions)
    {
        if (view is not { } camera) return;
        long stage=ProfileStart();
        _submittedCount=submissions.Count;
        _vp = Matrix(camera.ViewProjectionMatrix);Matrix4x4.Invert(camera.ViewProjectionMatrix,out var inverseWater);_inverseVp=Matrix(inverseWater);
        _eye = V3(camera.CameraPosition);
        _background = environment.DrawSky ? V3(environment.HorizonColor * environment.SkyIntensity) : [.07f,.1f,.15f];
        _ambient = lighting.AmbientIntensity;
        _exposure=environment.Exposure;
        _look=[environment.Look.Saturation,environment.Look.Contrast,environment.Look.Warmth];
        _pixelArt=[environment.Look.ShaderStyle==BuiltInSceneShader.PixelArt?1:0,environment.Look.PixelSize,environment.Look.PixelColorLevels,environment.Look.PixelDither];
        _fog=[environment.FogColor.X,environment.FogColor.Y,environment.FogColor.Z,environment.FogStartDistance,environment.FogEndDistance,environment.FogDensity,environment.FogEnabled?environment.FogMaxOpacity:0,(float)environment.FogMode];
        if(BrowserPresentationSettings.Quality!=GraphicsQuality.Fast){
            for(int i=0;i<lighting.DirectionalLights.Count;i++){
                var sun=lighting.DirectionalLights[i];if(!sun.CastShadows||sun.ShadowStrength<=0)continue;
                sun=sun with {ShadowResolution=BrowserPresentationSettings.Quality==GraphicsQuality.High?2048:1024,ShadowDistance=Math.Min(40,sun.ShadowDistance)};
                _shadowViewProjection=Renderer3D.BuildDirectionalShadowMatrix(camera,sun);
                _shadow=new BrowserShadow{matrix=Matrix(_shadowViewProjection.Value),resolution=sun.ShadowResolution,light=i,bias=sun.ShadowBias,strength=sun.ShadowStrength};break;
            }
        }
        if(environment.DrawSky){Matrix4x4.Invert(camera.ViewProjectionMatrix,out var inverse);var map=environment.SkyMode==SkyMode3D.EnvironmentMap?environment.EnvironmentMapTexture:null;
         _sky=new BrowserSky{texture=Texture(map),hdr=map?.IsHdr==true,inverseVp=Matrix(inverse),rotation=environment.EnvironmentRotationDegrees*MathF.PI/180,intensity=environment.SkyIntensity,zenith=V3(environment.ZenithColor),horizon=V3(environment.HorizonColor),ground=V3(environment.GroundColor),sharpness=environment.HorizonSharpness};}

        _lights = lighting.DirectionalLights.Select(l => new BrowserLight { direction = V3(l.Direction),
            color = V3(l.Color * l.Intensity) }).ToArray();
        _points = lighting.PointLights.Select(l => new BrowserLight { position = V3(l.Position),
            color = V3(l.Color * l.Intensity), range = l.Range }).ToArray();
        ProfileEnd("view and lights",stage);stage=ProfileStart();
        _staticRender.Clear();_dynamicRender.Clear();
        if(BrowserPresentationSettings.Quality==GraphicsQuality.Fast)
        {
            for(int i=0;i<submissions.Count;i++){var item=submissions[i];if(CacheableStatic(item))_staticRender.Add(item);else _dynamicRender.Add(item);}
            if(_staticCacheCooldown>0){_staticCacheCooldown--;_dynamicRender.AddRange(_staticRender);_staticRender.Clear();}
            else if(StaticCacheMatches()){_staticMissStreak=0;TouchStaticCache();}
            else
            {
                _staticMissStreak++;
                if(_staticMissStreak>3){_staticCacheCooldown=120;_staticCacheValid=false;_dynamicRender.AddRange(_staticRender);_staticRender.Clear();}
                else BuildStaticCache();
            }
        }
        else {_staticCacheValid=false;_staticMissStreak=0;_dynamicRender.AddRange(submissions);}
        RenderVisibility.Collect(_dynamicRender,camera,_visible);_otherVisible.Clear();_shadowVisible.Clear();
        Frustum3D? shadowFrustum=_shadowViewProjection is {} shadowVp?new Frustum3D(shadowVp):null;
        for (int i = 0; i < _dynamicRender.Count; i++)
        {
            var item = _dynamicRender[i];
            if(shadowFrustum is {} sf&&item.CastShadows&&item.Queue==RenderQueue3D.Opaque){
                var caster=item.AsShadowCaster();
                if(sf.Intersects(caster.WorldBounds))_shadowVisible.Add(caster);
            }

        }
        if(BrowserPresentationSettings.Quality!=GraphicsQuality.Fast)_occlusion.Cull(_visible,camera);
        _visible.Sort(PortableRenderOrdering.Compare);
        _visibleCount=_visible.Count+_staticRender.Count;
        ProfileEnd("cull and order",stage);stage=ProfileStart();
        var mainBatches=_batchBuilder.Build(_visible);
        var shadowBatches=_shadowBatchBuilder.Build(_shadowVisible,shadows:true);ProfileEnd("build batches",stage);
        EmitBatches(mainBatches,0);EmitBatches(shadowBatches,1);
        if(_retainedPackets.Count>_draws.Count)_retainedPackets.RemoveRange(_draws.Count,_retainedPackets.Count-_draws.Count);
        if(_retainedSkins.Count>_draws.Count)_retainedSkins.RemoveRange(_draws.Count,_retainedSkins.Count-_draws.Count);
    }
    private BrowserDrawCommand ReuseDraw(int slot,int id,int material,int dataBuffer,bool shadowOnly,bool skinned,int skinBuffer,bool receiveShadows,BrowserWater? water)
    {
        var command=slot<_drawPool.Count?_drawPool[slot]:new BrowserDrawCommand();
        if(slot==_drawPool.Count)_drawPool.Add(command);
        command.packetSlot=slot;command.id=id;command.material=material;command.dataBuffer=dataBuffer;command.shadowOnly=shadowOnly;
        command.skinned=skinned;command.skinBuffer=skinBuffer;command.receiveShadows=receiveShadows;command.water=water;
        return command;
    }

    private static bool CacheableStatic(RenderSubmission item) =>
        item.Queue==RenderQueue3D.Opaque && !item.Mesh.IsDynamic && item.Mesh.GpuSkinning==null &&
        !item.Material.WaterSurface && !item.Material.WaterVolume && item.Material.BlendMode==BlendMode3D.Opaque;
    private bool StaticCacheMatches()
    {
        if(!_staticCacheValid||_staticRender.Count!=_cachedStaticRender.Count)return false;
        for(int i=0;i<_staticRender.Count;i++)
        {
            var a=_staticRender[i];var b=_cachedStaticRender[i];
            if(!ReferenceEquals(a.Mesh,b.Mesh)||a.Mesh.GeometryVersion!=_cachedStaticVersions[i]||
               a.ModelMatrix!=b.ModelMatrix||a.Queue!=b.Queue||a.ReceiveShadows!=b.ReceiveShadows||
               a.CastShadows!=b.CastShadows||!ReferenceEquals(a.ShadowMesh,b.ShadowMesh)||a.ShadowModelMatrix!=b.ShadowModelMatrix||
               !_cachedStaticMaterials.TryGetValue(a.Material,out var material)||
               !MaterialRenderStateComparer.Instance.Equals(material,a.Material))return false;
        }
        return true;
    }
    private void TouchStaticCache()
    {
        foreach(var mesh in _cachedStaticMeshes){_usedMeshes.Add(mesh);_meshLastFrame[mesh]=_frameNumber;}
        foreach(var texture in _cachedStaticTextures)Texture(texture);
        foreach(var key in _cachedStaticMaterialKeys)_usedMaterials.Add(key);
        foreach(var command in _cachedStaticDraws){command.dataBuffer=-1;command.skinBuffer=-1;}
        _staticCacheHits++;_draws.AddRange(_cachedStaticDraws);
    }
    private void BuildStaticCache()
    {
        _staticCacheMisses++;_staticCacheValid=false;
        _cachedStaticRender.Clear();_cachedStaticDraws.Clear();_cachedStaticMeshes.Clear();_cachedStaticVersions.Clear();_cachedStaticTextures.Clear();_cachedStaticMaterials.Clear();
        foreach(var item in _staticRender){_cachedStaticRender.Add(item);_cachedStaticVersions.Add(item.Mesh.GeometryVersion);}
        foreach(var item in _staticRender)
        {
            if(!_cachedStaticMeshes.Contains(item.Mesh))_cachedStaticMeshes.Add(item.Mesh);
            if(!_cachedStaticMaterials.ContainsKey(item.Material))_cachedStaticMaterials.Add(item.Material,item.Material.Clone());
            void Keep(Texture2D? texture){if(texture!=null&&!_cachedStaticTextures.Contains(texture))_cachedStaticTextures.Add(texture);}
            var m=item.Material;Keep(m.HeightFieldTexture);Keep(m.MainTexture);Keep(m.NormalTexture);Keep(m.MetallicTexture);
            Keep(m.RoughnessTexture);Keep(m.AmbientOcclusionTexture);Keep(m.PackedPbrTexture);Keep(m.EmissionTexture);Keep(m.SandImprintTexture);
        }
        int before=_draws.Count;EmitBatches(_staticBatchBuilder.Build(_staticRender),0);
        for(int i=before;i<_draws.Count;i++)_cachedStaticDraws.Add(_draws[i]);
        _cachedStaticMaterialKeys.Clear();_cachedStaticMaterialKeys.AddRange(_usedMaterials);
        _staticCacheValid=true;
    }

    private void EmitBatches(IReadOnlyList<RenderBatcher.Batch> batches,int pass)
    {
        long stage;
        foreach(var batch in batches)
        {
            stage=ProfileStart();
            var draw=batch.First;
            _usedMeshes.Add(draw.Mesh);
            _meshLastFrame[draw.Mesh]=_frameNumber;
            if (!_meshes.TryGetValue(draw.Mesh, out var cached)) cached = (++_nextId, -1);
            if (cached.Version != draw.Mesh.GeometryVersion)
            {
                _uploads.Add(new BrowserMeshUpload { id = cached.Id,
                    vertices = Buffer(System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Mesh.VertexData.Span)),
                    indices = cached.Version>=0&&_meshIndices.TryGetValue(draw.Mesh,out var previousIndices)&&previousIndices.Equals(draw.Mesh.IndexData)?-1:Buffer(System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Mesh.IndexData.Span)),
                    skin=draw.Mesh.GpuSkinning is {} skin?Buffer(System.Runtime.InteropServices.MemoryMarshal.AsBytes(skin.Influences.AsSpan())):-1 });
                _meshIndices[draw.Mesh]=draw.Mesh.IndexData;
                _meshes[draw.Mesh] = (cached.Id, draw.Mesh.GeometryVersion);
            }
            ProfileEnd("mesh staging",stage);stage=ProfileStart();
            var m = draw.Material;
            if(!_frameMaterialIds.TryGetValue(m,out int materialId))
            {
            var key = new MaterialKey(Texture(m.HeightFieldTexture), Texture(m.MainTexture), Texture(m.NormalTexture),
                Texture(m.MetallicTexture), Texture(m.RoughnessTexture), Texture(m.AmbientOcclusionTexture), Texture(m.PackedPbrTexture), Texture(m.EmissionTexture),
                m.BaseColor, m.HeightFieldUvTransform, m.HeightFieldWorldSize, m.EmissionEnabled ? m.EmissionColor * m.EmissionIntensity : Vector3.Zero,
                m.UvTiling, m.UvOffset, m.HeightFieldRange, m.HeightFieldBias, m.NormalStrength, m.Metallic, m.Roughness, m.AmbientOcclusionStrength,
                m.BlendMode == BlendMode3D.Cutout ? m.AlphaCutoff : -1, (int)m.PackedAoChannel-1, (int)m.PackedRoughnessChannel-1,
                (int)m.PackedMetallicChannel-1, m.DirectXNormalMap, m.PbrMapMode == ByteEngine.Core.Assets.MaterialPbrMapMode.Packed,
                m.Shading == ByteEngine.Core.Assets.MaterialShadingMode.Unlit, m.DecodeColorTexturesSrgb, m.DepthTest, m.ResolveDepthWrite(), m.BlendMode, m.CullMode, m.FrontFace,Texture(m.SandImprintTexture),m.SandImprintOrigin,m.SolidColorPalette);
            _usedMaterials.Add(key);
            if (!_materials.TryGetValue(key, out materialId))
            {
            materialId = ++_nextMaterial; _materials.Add(key, materialId);
            _materialUploads.Add(new BrowserDraw { material = materialId, color = V4(m.BaseColor),palette=m.SolidColorPalette?.SelectMany(c=>new[]{c.X,c.Y,c.Z,c.W}).ToArray()??[],sandImprint=key.SandImprint,sandOrigin=[key.SandOrigin.X,key.SandOrigin.Y],
                heightField = key.Height, heightUv = V4(m.HeightFieldUvTransform), heightRange = m.HeightFieldRange, heightBias = m.HeightFieldBias, heightSize = new[]{m.HeightFieldWorldSize.X,m.HeightFieldWorldSize.Y}, texture = key.Texture, normal = key.Normal,
                normalStrength = m.NormalStrength, directX = m.DirectXNormalMap,
                metallicTexture = key.MetallicMap, roughnessTexture = key.RoughnessMap,
                aoTexture = key.AoMap, packedTexture = key.PackedMap,
                metallic = m.Metallic, roughness = m.Roughness, ao = m.AmbientOcclusionStrength,
                packed = m.PbrMapMode == ByteEngine.Core.Assets.MaterialPbrMapMode.Packed,
                channels = new[] {(int)m.PackedAoChannel-1,(int)m.PackedRoughnessChannel-1,(int)m.PackedMetallicChannel-1},
                emissionTexture = key.EmissionMap, emission = V3(key.Emission),
                tiling = new[] {m.UvTiling.X,m.UvTiling.Y}, offset = new[] {m.UvOffset.X,m.UvOffset.Y},
                unlit = m.Shading == ByteEngine.Core.Assets.MaterialShadingMode.Unlit,
                srgb = m.DecodeColorTexturesSrgb, cutoff = m.BlendMode == BlendMode3D.Cutout ? m.AlphaCutoff : -1,
                blend = m.BlendMode.ToString(), depth = m.DepthTest, write = m.ResolveDepthWrite(),
                cull = m.CullMode.ToString(), front = m.FrontFace.ToString() });
            }
            _frameMaterialIds.Add(m,materialId);
            }
            ProfileEnd("material staging",stage);stage=ProfileStart();
            int slot=_draws.Count,length=BrowserMatrixPacking.PacketLength(batch.Instances);
            if(_packetScratch.Length<length)_packetScratch=new byte[length];
            var drawBytes=_packetScratch.AsSpan(0,length);BrowserMatrixPacking.WritePacket(drawBytes,batch.Instances);
            int drawBuffer=RetainPacket(_retainedPackets,slot,drawBytes);
            ReadOnlySpan<byte> skinBytes=draw.Mesh.GpuSkinning is {} palette?System.Runtime.InteropServices.MemoryMarshal.AsBytes(palette.Bones.AsSpan()):ReadOnlySpan<byte>.Empty;
            int skinBuffer=RetainPacket(_retainedSkins,slot,skinBytes);
            _draws.Add(ReuseDraw(slot, cached.Id, materialId, drawBuffer, pass==1, !skinBytes.IsEmpty, skinBuffer, draw.ReceiveShadows,
                m.WaterSurface||m.WaterVolume?new BrowserWater{surface=m.WaterSurface,reflections=m.WaterReflections,depthTexture=Texture(m.WaterDepthTexture),size=[m.WaterSize.X,m.WaterSize.Y],column=V4(m.WaterColumn),motion=V4(m.WaterMotion),flow=V4(m.WaterFlow),impact=V4(m.WaterImpact),wake=V4(m.WaterWake),absorption=V4(m.WaterAbsorption),scattering=V4(m.WaterScattering),style=V4(m.WaterStyle),waves=V4(m.WaterOceanWaves),depthRect=V4(m.WaterDepthRect),depthSettings=V4(m.WaterDepthSettings)}:null));
            ProfileEnd("matrix packets",stage);
        }
    }

    public void SetUiClip(Vector4? rectangle)
    {
        var r=rectangle??Vector4.Zero;
        var clip=ReuseUi();clip.kind=rectangle.HasValue?"clip":"unclip";clip.x=r.X;clip.y=r.Y;clip.w=r.Z;clip.h=r.W;clip.color=[1,1,1,1];
    }
    public Vector2 MeasureText(string text,string? fontPath,int size)
    {
        string path=string.IsNullOrWhiteSpace(fontPath)?"/game/Resources/Fonts/TypeLightSans.ttf":fontPath;
        size=Math.Clamp(size,8,96);bool bitmap=path.EndsWith(".fnt",StringComparison.OrdinalIgnoreCase);
        int raster=bitmap?size:Math.Clamp((int)MathF.Ceiling(size*2f/16)*16,16,64);var key=(path,raster);
        if(!_fonts.TryGetValue(key,out var atlas)){atlas=FontAtlas.Load(path,raster);_fonts.Add(key,atlas);}
        _usedFonts.Add(atlas);
        return atlas.Measure(text,bitmap?size/Math.Max(1,atlas.LineHeight):size/(float)raster);
    }
    public void DrawText(string value, string? fontPath, int size, Vector2 position,
        Vector4 color, float wrapWidth, UiAnchor anchor)
    {
        if (string.IsNullOrEmpty(value)) return;
        string path = string.IsNullOrWhiteSpace(fontPath) ? "/game/Resources/Fonts/TypeLightSans.ttf" : fontPath;
        size = Math.Clamp(size, 8, 96);
        bool bitmap=path.EndsWith(".fnt",StringComparison.OrdinalIgnoreCase);
        int raster=bitmap?size:Math.Clamp((int)MathF.Ceiling(size*2f/16)*16,16,64);var key = (path, raster);
        if (!_fonts.TryGetValue(key, out var atlas)) { atlas = FontAtlas.Load(path, raster); _fonts.Add(key, atlas); }
        _usedFonts.Add(atlas);
        float scale = bitmap ? size / Math.Max(1, atlas.LineHeight) : size/(float)raster;
        float Measure(string line)
        {
            float width = 0; int previous = -1;
            foreach (char c in line)
            {
                if (!atlas.TryGetGlyph(c, out var g) && !atlas.TryGetGlyph('?', out g)) continue;
                width += (g.Advance + (previous < 0 ? 0 : atlas.GetKerning(previous, c))) * scale;
                previous = c;
            }
            return width;
        }
        var lines = UiTextLayout.WrapLines(value, wrapWidth, Measure);
        float horizontal = anchor is UiAnchor.TopCenter or UiAnchor.Center or UiAnchor.BottomCenter ? .5f :
            anchor is UiAnchor.TopRight or UiAnchor.BottomRight ? 1 : 0;
        float vertical = anchor == UiAnchor.Center ? .5f :
            anchor is UiAnchor.BottomLeft or UiAnchor.BottomCenter or UiAnchor.BottomRight ? 1 : 0;
        float top = position.Y - lines.Count * atlas.LineHeight * scale * vertical;
        int texture = UiTexture(atlas.Texture);
        for (int i = 0; i < lines.Count; i++)
        {
            float x = position.X - Measure(lines[i]) * horizontal;
            float baseline = top + i * atlas.LineHeight * scale + atlas.BaseLine * scale;
            int previous = -1;
            foreach (char c in lines[i])
            {
                if (!atlas.TryGetGlyph(c, out var g) && !atlas.TryGetGlyph('?', out g)) continue;
                x += (previous < 0 ? 0 : atlas.GetKerning(previous, c)) * scale;
                if (g.Width > 0 && g.Height > 0)
                    {var glyph=ReuseUi();glyph.kind="image";glyph.texture=texture;glyph.x=x+g.OffsetX*scale;glyph.y=baseline+g.OffsetY*scale;glyph.w=g.Width*scale;glyph.h=g.Height*scale;glyph.sx=g.X;glyph.sy=g.Y;glyph.sw=g.Width;glyph.sh=g.Height;glyph.color=V4(color);}
                x += g.Advance * scale; previous = c;
            }
        }
    }

    public void DrawQuad(Vector2 position, Vector2 size, Vector4 color)
    {var quad=ReuseUi();quad.kind="quad";quad.x=position.X;quad.y=position.Y;quad.w=size.X;quad.h=size.Y;quad.color=V4(color);}
    public void DrawImage(Texture2D texture, Vector2 position, Vector2 size, Vector4 color)
    {var image=ReuseUi();image.kind="image";image.texture=UiTexture(texture);image.x=position.X;image.y=position.Y;image.w=size.X;image.h=size.Y;image.sx=0;image.sy=0;image.sw=texture.Width;image.sh=texture.Height;image.color=V4(color);}

    public void DrawLine(Vector2 from, Vector2 to, float thickness, Vector4 color)
        {var line=ReuseUi();line.kind="line";line.x=from.X;line.y=from.Y;line.w=to.X;line.h=to.Y;line.sx=thickness;line.color=V4(color);}

    public string Finish(object[] audio,double loopMs,PortableGameLoop loop,List<BrowserRuntimeMarker> markers)
    {
        long stage=ProfileStart();
        int budget=4*1024*1024;
        while(budget>0&&_pendingTextureUploads.TryDequeue(out var r))
        {
            if(!_textures.TryGetValue(r.Texture,out var resident)||resident.Id!=r.Id)continue;
            // A texture disposed between staging and this drain has no pixels left to
            // copy, and a resized reload invalidates the queued rectangles. Never leave
            // the resident copy recorded as current: re-stage instead of blanking it.
            if(!BrowserTextureTransfer.TryGetLength(r.Texture,r.X,r.Y,r.W,r.H,out int length,out bool hdr)){InvalidateTexture(r.Texture,r.Id);continue;}
            int pixelBuffer=_buffers.Allocate(length,out var pixels);
            if(!BrowserTextureTransfer.TryCopy(r.Texture,r.X,r.Y,r.W,r.H,pixels,out hdr)){InvalidateTexture(r.Texture,r.Id);continue;}
            budget-=length;
            _textureUploads.Add(new BrowserTextureUpload {source=r.Texture.DiagnosticSource??(string.IsNullOrEmpty(r.Texture.FilePath)?"generated texture":Path.GetFileName(r.Texture.FilePath)),id=r.Id,width=r.Texture.Width,height=r.Texture.Height,x=r.X,y=r.Y,w=r.W,h=r.H,allocate=r.Allocate,partial=true,pixels=pixelBuffer,hdr=hdr,ui=_uiTextures.Contains(r.Texture),nearest=r.Texture.Filter==TextureFilter.Nearest});
        }
        // Retain a small cache; size animations must not accumulate large atlas textures forever.
        foreach (var key in (_fonts.Count>32?_fonts.Keys.Where(k => !_usedFonts.Contains(_fonts[k])).ToArray():[]))
        {
            // Layout measurements and scaled drawing can use different sizes.
            // Keep both resident across frames instead of recreating measurement atlases.
            if (_fonts.Count <= 32) break;
            _fonts[key].Dispose(); _fonts.Remove(key);
        }
        _hiddenMeshes.Clear();long hiddenMeshBytes=0;
        foreach(var key in _meshes.Keys)if(!_usedMeshes.Contains(key)){_hiddenMeshes.Add(key);hiddenMeshBytes+=(long)key.VertexData.Length*4+(long)key.IndexData.Length*4;}
        // Age expiry needs no ordering. Only pay for LRU sorting when the byte
        // budget actually requires selecting which otherwise-live entries go.
        if(hiddenMeshBytes>HiddenMeshBudgetBytes)_hiddenMeshes.Sort((a,b)=>_meshLastFrame[a].CompareTo(_meshLastFrame[b]));
        var expiredMeshes=new List<int>();
        foreach(var key in _hiddenMeshes)if(_frameNumber-_meshLastFrame[key]>120||hiddenMeshBytes>HiddenMeshBudgetBytes){expiredMeshes.Add(_meshes[key].Id);hiddenMeshBytes-=(long)key.VertexData.Length*4+(long)key.IndexData.Length*4;_meshes.Remove(key);_meshIndices.Remove(key);_meshLastFrame.Remove(key);}
        var deadMeshes = expiredMeshes.ToArray();
        // Keep recently hidden UI frames resident, but bound unused residency to 16 MiB / 128 textures.
        _unusedTextures.Clear();long unusedBytes=0;
        foreach(var key in _textures.Keys)if(!_usedTextures.Contains(key)&&!_preparedTextures.Contains(key)){_unusedTextures.Add(key);unusedBytes+=(long)key.PixelData.Length+(long)key.FloatPixelData.Length*4;}
        int unusedCount=_unusedTextures.Count;
        if(unusedBytes>16L*1024*1024||unusedCount>128)_unusedTextures.Sort((a,b)=>_textureLastFrame[a].CompareTo(_textureLastFrame[b]));
        var expired=new List<int>();
        foreach(var key in _unusedTextures)if((key.PixelData.IsEmpty&&key.FloatPixelData.IsEmpty)||_frameNumber-_textureLastFrame[key]>600||unusedBytes>16L*1024*1024||unusedCount>128){expired.Add(_textures[key].Id);unusedBytes-=(long)key.PixelData.Length+(long)key.FloatPixelData.Length*4;unusedCount--;_textures.Remove(key);_textureLastFrame.Remove(key);_uiTextures.Remove(key);}
        var deadTextures=expired.ToArray();
        _unusedMaterials.Clear();foreach(var key in _materials.Keys)if(!_usedMaterials.Contains(key))_unusedMaterials.Add(key);
        var deadMaterials = new int[_unusedMaterials.Count];
        for(int i=0;i<_unusedMaterials.Count;i++){var key=_unusedMaterials[i];deadMaterials[i]=_materials[key];_materials.Remove(key);}
        ProfileEnd("resource retention",stage);stage=ProfileStart();
        int uiBuffer = _buffers.Allocate(_ui.Count * 56,out var uiBytes);
        Span<float> values = stackalloc float[12];
        for (int i = 0; i < _ui.Count; i++)
        {
            var command = _ui[i]; var bytes = uiBytes.AsSpan(i * 56, 56);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes, command.kind switch { "quad" => 0, "image" => 1, "clip" => 2, "unclip" => 3, "line" => 4, _ => throw new InvalidOperationException("Unknown UI command") });
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes[4..], command.texture);
            values[0]=command.x; values[1]=command.y; values[2]=command.w; values[3]=command.h;
            values[4]=command.sx; values[5]=command.sy; values[6]=command.sw; values[7]=command.sh;
            for (int j=0;j<4;j++) values[8+j]=command.color[j];
            for (int j = 0; j < values.Length; j++) System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(bytes[(8+j*4)..], values[j]);
        }
        var networkCommands=new List<BrowserNetworkCommand>();
        while(BrowserNetworkLink.Outbound.TryDequeue(out var outgoing)){
            var command=outgoing.Command;
            if(outgoing.Bytes is {} networkBytes){int buffer=_buffers.Allocate(networkBytes.Length,out var target);networkBytes.AsSpan().CopyTo(target);command=command with{buffer=buffer};}
            networkCommands.Add(command);
        }
        foreach(var link in BrowserNetworkLink.Links.Values)link.Flushed();
        var bufferViews=new uint[_buffers.Count*2];
        for(int i=0;i<_buffers.Count;i++){bufferViews[i*2]=checked((uint)(nuint)_buffers.GetPinnedAddress(i));bufferViews[i*2+1]=checked((uint)_buffers[i].Length);}
        ProfileEnd("UI and buffer table",stage);stage=ProfileStart();
        string encoded=JsonSerializer.Serialize(new BrowserFrame { net=networkCommands,bufferViews=bufferViews,quality=BrowserPresentationSettings.Quality.ToString(),worldResolution=BrowserPresentationSettings.WorldResolution,shadow=_shadow,look=_look,pixelArt=_pixelArt,fog=_fog,sky=_sky, exposure=_exposure, loopMs=loopMs, pendingTextures=_pendingTextureUploads.Count, vp = _vp,inverseVp=_inverseVp, eye = _eye, background = _background, ambient = _ambient,
            sceneMetrics=loop.Scenes.ActiveScene?.LastFrameMetrics??default,inputMs=loop.LastInputMilliseconds,updateStepMs=loop.LastUpdateMilliseconds,
            renderPreparationMs=loop.LastRenderMilliseconds,renderAllocatedBytes=loop.LastRenderAllocatedBytes,
            lights = _lights, points = _points, uploads = _uploads, textures = _textureUploads,
            draws = _draws, materials = _materialUploads, deadMaterials = deadMaterials, uiBuffer = uiBuffer, uiCount = _ui.Count,
            deadMeshes=deadMeshes, deadTextures=deadTextures, runtimeMarkers=markers, audio=JsonSerializer.SerializeToElement(audio) },BrowserJsonContext.Default.BrowserFrame);
        ProfileEnd("JSON encoding",stage);return encoded;
    }
    private static float[] V3(Vector3 v) => [v.X,v.Y,v.Z];
    private static float[] V4(Vector4 v) => [v.X,v.Y,v.Z,v.W];
    private static float[] Matrix(Matrix4x4 m) =>
        [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
         m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
}

internal sealed class BrowserAudio : IPortableAudio
{
    public void Enqueue(object command)=>_commands.Add(command);
    public void OpenUrl(string url)=>_commands.Add(new{kind="openUrl",url});
    private readonly HashSet<Guid> _paused=new();
    private readonly Dictionary<Guid,double> _positions=new();
    public bool IsPaused(Guid id)=>_paused.Contains(id);
    public double Position(Guid id)=>_positions.GetValueOrDefault(id);
    public void SetPosition(Guid id,double seconds)=>_positions[id]=seconds;
    public void Seek(Guid id,double seconds){_positions[id]=seconds;_commands.Add(new{kind="seek",id,seconds});}
    private readonly List<object> _commands = new();
    private readonly HashSet<Guid> _playing = new();
    public bool IsPlaying(Guid id) => _playing.Contains(id);
    public void Ended(Guid id) {_playing.Remove(id);_paused.Remove(id);}
    public object[] Drain() { var commands = _commands.ToArray(); _commands.Clear(); return commands; }
    public void PlayStream(Guid id,string path,AudioSource3D source)
    {
        _paused.Remove(id);_playing.Add(id);_commands.Add(new{kind="play",id,path=path.Replace("/game/",""),loop=source.Loop,stream=true});Update(id,source);
    }
    public void Play(Guid id, AudioClip clip, AudioSource3D source)
    {
        _playing.Add(id);
        _paused.Remove(id);
        _commands.Add(new { kind = "play", id, path = clip.SourcePath.Replace("/game/", ""), loop = source.Loop });
        Update(id, source);
    }
    public void Pause(Guid id) { _paused.Add(id);_playing.Remove(id); _commands.Add(new { kind = "pause", id }); }
    public void Stop(Guid id) { _paused.Remove(id);_positions.Remove(id);_playing.Remove(id); _commands.Add(new { kind = "stop", id }); }
    public void Update(Guid id, AudioSource3D source)
    {
        if (!_playing.Contains(id)) return;
        var p = source.Transform.WorldPosition;
        _commands.Add(new { kind = "settings", id, volume = source.EffectiveVolume, pitch = source.Pitch, loop = source.Loop,
            spatial = source.Spatial, position = new[] {p.X,p.Y,p.Z},
            min = source.MinDistance, max = source.MaxDistance, rolloff = source.RolloffFactor });
    }
    public void Listener(Vector3 position, Vector3 forward, Vector3 up, float volume) =>
        _commands.Add(new { kind = "listener", position = new[] {position.X,position.Y,position.Z},
            forward = new[] {forward.X,forward.Y,forward.Z}, up = new[] {up.X,up.Y,up.Z}, volume });
}

