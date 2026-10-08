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
    public static string Diagnostics(){var scene=Loop.Scenes.ActiveScene;var vehicle=scene?.GameObjects.SelectMany(o=>o.Components).OfType<DuneCompany.DuneWorkshop3D>().FirstOrDefault();return JsonSerializer.Serialize(new {scene=scene?.Name,building=vehicle?.Building,velocity=vehicle?.PhysicalVelocity.Length(),position=vehicle?.Transform.WorldPosition.ToString(),winch=vehicle?.WinchMission,blocks=vehicle?.Blocks.Count, physicsMs=vehicle?.LastDriveCpuMilliseconds,physicsSteps=vehicle?.PhysicsSteps,droppedPhysicsSeconds=vehicle?.DroppedPhysicsSeconds, loopMs=_lastLoopMilliseconds, encodeMs=_lastEncodeMilliseconds});}
    [JSExport]
    public static string SaveKey(){var data=new ByteEngine.Core.Serialization.ProjectSerializer().Load("/game/Content/Game.byteproject");return "byteengine-saves-"+data.ProjectId.ToString("N")+(data.RuntimeSaveId is {} id ? "-"+id.ToString("N") : "");}
    [JSExport]
    public static void RestoreSaves(string json){if(_started)throw new InvalidOperationException("Restore before startup");if(json.Length>10*1024*1024)throw new InvalidDataException("Browser save is too large");var data=new ByteEngine.Core.Serialization.ProjectSerializer().Load("/game/Content/Game.byteproject");var directory=GameSaveStorage.Register("/game/Content",data.ProjectId,data.RuntimeSaveId);foreach(var entry in JsonSerializer.Deserialize<Dictionary<string,string>>(json)??[]){if(!entry.Key.EndsWith(".json",StringComparison.OrdinalIgnoreCase)&&!entry.Key.EndsWith(".json.name",StringComparison.OrdinalIgnoreCase)&&entry.Key!="active-blueprint.txt")continue;var path=GamePackageExporter.ResolveInside(directory,entry.Key);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,entry.Value);}}
    [JSExport]
    public static string SaveData(){if(_project==null)return "{}";var root=_project.SaveDirectory;return JsonSerializer.Serialize(Directory.GetFiles(root,"*",SearchOption.AllDirectories).Where(p=>(p.EndsWith(".json")||p.EndsWith(".json.name")||Path.GetFileName(p)=="active-blueprint.txt")&&!p.Contains("/Diagnostics/")&&!p.Contains("/Backups/")).ToDictionary(p=>Path.GetRelativePath(root,p).Replace('\\','/'),File.ReadAllText));}

    [JSExport]
    public static void Start(bool demo)
    {
        if (_started) return;
        ByteEngine.Core.Plugins.ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.desertterrain", new DesertTerrain.DesertTerrainPlugin());
        ByteEngine.Core.Plugins.ByteEnginePluginManager.RegisterLinkedPlugin("bytebard.dunecompany", new DuneCompany.DuneCompanyPlugin());
        PortableAudio.Backend = Audio;
        if (!demo)
        {
            _project = new GameProjectRuntime("/game/Content/Game.byteproject", Console.Error.WriteLine);
            string currentPath=_project.Project.StartupScene;
            Loop.Scenes.SceneFactory=path=>{var fresh=_project.LoadScene(path);currentPath=path;return fresh;};
            Loop.Scenes.QuitHandler=()=>Loop.Scenes.UnloadScene();
            var scene = _project.LoadStartupScene();
            Loop.Scenes.RestartSceneFactory = () =>
            {
                Loop.Scenes.GlobalVariables.Clear();
                foreach (var variable in _project.Project.GlobalVariables)
                    Loop.Scenes.GlobalVariables.Set(variable.Name, variable.Value.Clone());
                return _project.LoadScene(currentPath);
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
    public static void AudioEnded(string id) => Audio.Ended(Guid.Parse(id));

    [JSExport]
    public static bool WantsPointerLock() => !Input.KeepGameViewPointerFree &&
        Loop.Scenes.ActiveScene?.GameObjects.Any(o => o.ActiveInHierarchy &&
            o.Components.OfType<PlayerController3D>().Any(c => c.Enabled && c.AcceptLookInput)) == true;

    [JSExport]
    public static string Frame(double seconds, int width, int height, string keys,
        string buttons, double mouseX, double mouseY, double deltaX, double deltaY,
        double wheel, bool focused, bool captured, string gamepad)
    {
        if (!_started) throw new InvalidOperationException("Call Start first.");
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
        Sink.Begin();
        long start=System.Diagnostics.Stopwatch.GetTimestamp();
        Loop.Tick(seconds, Snapshot, width, height, new((float)mouseX, (float)mouseY), focused, captured, Sink);
        _lastLoopMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        start=System.Diagnostics.Stopwatch.GetTimestamp();
        string result=Sink.Finish(Audio.Drain(),_lastLoopMilliseconds);
        _lastEncodeMilliseconds=System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return result;
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
    private readonly Dictionary<Mesh, (int Id, int Version)> _meshes = new();
    private readonly Dictionary<Texture2D, (int Id, int Version)> _textures = new();
    private readonly Dictionary<(string Path, int Size), FontAtlas> _fonts = new();
    private readonly HashSet<FontAtlas> _usedFonts = new();
    private readonly HashSet<Mesh> _usedMeshes = new();
    private readonly HashSet<Texture2D> _usedTextures = new();
    private readonly List<BrowserMeshUpload> _uploads = new();
    private readonly List<BrowserTextureUpload> _textureUploads = new();
    private readonly List<BrowserDraw> _draws = new();
    private readonly List<BrowserUi> _ui = new();
    private readonly List<byte[]> _buffers = new();
    public byte[] ReadBuffer(int index) => _buffers[index];
    private int Buffer(ReadOnlySpan<byte> data) { int index=_buffers.Count; _buffers.Add(data.ToArray()); return index; }
    readonly Queue<(Texture2D Texture,int Id,int X,int Y,int W,int H,bool Allocate)> _pendingTextureUploads=new();
    private float[] _vp = new float[16], _eye = [0,0,0], _background = [.07f,.1f,.15f];
    private BrowserLight[] _lights = [], _points = [];
    private float _ambient = .25f;
    private int _nextId, _nextTexture;
    public void Begin()
    {
        _uploads.Clear(); _textureUploads.Clear(); _draws.Clear(); _ui.Clear(); _buffers.Clear();
        _usedMeshes.Clear(); _usedTextures.Clear(); _usedFonts.Clear();
    }

    private int Texture(Texture2D? texture)
    {
        if (texture == null) return 0;
        _usedTextures.Add(texture);
        if (!_textures.TryGetValue(texture, out var cached)) cached = (++_nextTexture, -1);
        if (cached.Version != texture.ContentVersion)
        {
            if (texture.PixelData.IsEmpty) throw new InvalidOperationException("Browser texture has no CPU pixels.");
            bool allocate=cached.Version<0;
            foreach(var r in texture.ChangedRegionsSince(cached.Version))for(int y=r.Y;y<r.Y+r.Height;y+=128)for(int x=r.X;x<r.X+r.Width;x+=128){_pendingTextureUploads.Enqueue((texture,cached.Id,x,y,Math.Min(128,r.X+r.Width-x),Math.Min(128,r.Y+r.Height-y),allocate));allocate=false;}
            _textures[texture] = (cached.Id, texture.ContentVersion);
        }
        return cached.Id;
    }

    public void Draw3D(RenderView3D? view, RenderLighting3D lighting,
        RenderEnvironment3D environment, IReadOnlyList<RenderSubmission> submissions)
    {
        if (view is not { } camera) return;
        _vp = Matrix(camera.ViewProjectionMatrix);
        _eye = V3(camera.CameraPosition);
        _background = environment.DrawSky ? V3(environment.HorizonColor * environment.SkyIntensity) : [.07f,.1f,.15f];
        _ambient = lighting.AmbientIntensity;
        _lights = lighting.DirectionalLights.Select(l => new BrowserLight { direction = V3(l.Direction),
            color = V3(l.Color * l.Intensity) }).ToArray();
        _points = lighting.PointLights.Select(l => new BrowserLight { position = V3(l.Position),
            color = V3(l.Color * l.Intensity), range = l.Range }).ToArray();
        foreach (var draw in submissions.OrderBy(s => s.Queue).ThenBy(s =>
            s.Queue == RenderQueue3D.Transparent ? -s.DistanceSquaredToCamera : s.SubmissionIndex))
        {
            if (draw.FrustumCullingEnabled && !camera.Frustum.Intersects(draw.WorldBounds)) continue;
            _usedMeshes.Add(draw.Mesh);
            if (!_meshes.TryGetValue(draw.Mesh, out var cached)) cached = (++_nextId, -1);
            if (cached.Version != draw.Mesh.GeometryVersion)
            {
                _uploads.Add(new BrowserMeshUpload { id = cached.Id,
                    vertices = Buffer(System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Mesh.VertexData.Span)),
                    indices = Buffer(System.Runtime.InteropServices.MemoryMarshal.AsBytes(draw.Mesh.IndexData.Span)) });
                _meshes[draw.Mesh] = (cached.Id, draw.Mesh.GeometryVersion);
            }
            var m = draw.Material;
            _draws.Add(new BrowserDraw { id = cached.Id, model = Matrix(draw.ModelMatrix), color = V4(m.BaseColor),
                heightField = Texture(m.HeightFieldTexture), heightUv = V4(m.HeightFieldUvTransform), heightRange = m.HeightFieldRange, heightBias = m.HeightFieldBias, heightSize = new[]{m.HeightFieldWorldSize.X,m.HeightFieldWorldSize.Y}, texture = Texture(m.MainTexture), normal = Texture(m.NormalTexture),
                normalStrength = m.NormalStrength, directX = m.DirectXNormalMap,
                metallicTexture = Texture(m.MetallicTexture), roughnessTexture = Texture(m.RoughnessTexture),
                aoTexture = Texture(m.AmbientOcclusionTexture), packedTexture = Texture(m.PackedPbrTexture),
                metallic = m.Metallic, roughness = m.Roughness, ao = m.AmbientOcclusionStrength,
                packed = m.PbrMapMode == ByteEngine.Core.Assets.MaterialPbrMapMode.Packed,
                channels = new[] {(int)m.PackedAoChannel-1,(int)m.PackedRoughnessChannel-1,(int)m.PackedMetallicChannel-1},
                emissionTexture = Texture(m.EmissionTexture), emission = V3(m.EmissionEnabled ? m.EmissionColor * m.EmissionIntensity : Vector3.Zero),
                tiling = new[] {m.UvTiling.X,m.UvTiling.Y}, offset = new[] {m.UvOffset.X,m.UvOffset.Y},
                unlit = m.Shading == ByteEngine.Core.Assets.MaterialShadingMode.Unlit,
                srgb = m.DecodeColorTexturesSrgb, cutoff = m.BlendMode == BlendMode3D.Cutout ? m.AlphaCutoff : -1,
                blend = m.BlendMode.ToString(), depth = m.DepthTest, write = m.ResolveDepthWrite(),
                cull = m.CullMode.ToString(), front = m.FrontFace.ToString() });
        }
    }

    public void DrawText(string value, string? fontPath, int size, Vector2 position,
        Vector4 color, float wrapWidth, UiAnchor anchor)
    {
        if (string.IsNullOrEmpty(value)) return;
        string path = string.IsNullOrWhiteSpace(fontPath) ? "/game/Resources/Fonts/TypeLightSans.ttf" : fontPath;
        size = Math.Clamp(size, 8, 96);
        var key = (path, size);
        if (!_fonts.TryGetValue(key, out var atlas)) { atlas = FontAtlas.Load(path, size); _fonts.Add(key, atlas); }
        _usedFonts.Add(atlas);
        float scale = path.EndsWith(".fnt", StringComparison.OrdinalIgnoreCase) ? size / Math.Max(1, atlas.LineHeight) : 1;
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
        int texture = Texture(atlas.Texture);
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
                    _ui.Add(new BrowserUi { kind = "image", texture = texture, x = x + g.OffsetX * scale, y = baseline + g.OffsetY * scale,
                        w = g.Width * scale, h = g.Height * scale, sx = g.X, sy = g.Y, sw = g.Width, sh = g.Height, color = V4(color) });
                x += g.Advance * scale; previous = c;
            }
        }
    }

    public void DrawQuad(Vector2 position, Vector2 size, Vector4 color) =>
        _ui.Add(new BrowserUi { kind = "quad", x = position.X, y = position.Y, w = size.X, h = size.Y, color = V4(color) });
    public void DrawImage(Texture2D texture, Vector2 position, Vector2 size, Vector4 color) =>
        _ui.Add(new BrowserUi { kind = "image", texture = Texture(texture), x = position.X, y = position.Y, w = size.X, h = size.Y,
            sx = 0, sy = 0, sw = texture.Width, sh = texture.Height, color = V4(color) });

    public string Finish(object[] audio,double loopMs)
    {
        int budget=4*1024*1024;
        while(budget>0&&_pendingTextureUploads.TryDequeue(out var r)){if(!_usedTextures.Contains(r.Texture)||!_textures.TryGetValue(r.Texture,out var resident)||resident.Id!=r.Id)continue;var pixels=new byte[r.W*r.H*4];for(int y=0;y<r.H;y++)r.Texture.PixelData.Span.Slice(((r.Y+y)*r.Texture.Width+r.X)*4,r.W*4).CopyTo(pixels.AsSpan(y*r.W*4));budget-=pixels.Length;_textureUploads.Add(new BrowserTextureUpload {id=r.Id,width=r.Texture.Width,height=r.Texture.Height,x=r.X,y=r.Y,w=r.W,h=r.H,allocate=r.Allocate,partial=true,pixels=Buffer(pixels),nearest=r.Texture.Filter==TextureFilter.Nearest});}
        // Retain a small cache; size animations must not accumulate large atlas textures forever.
        foreach (var key in _fonts.Keys.Where(k => !_usedFonts.Contains(_fonts[k])).ToArray())
        {
            if (_fonts.Count <= 8) break;
            _fonts[key].Dispose(); _fonts.Remove(key);
        }
        var deadMeshes = _meshes.Where(p => !_usedMeshes.Contains(p.Key)).Select(p => p.Value.Id).ToArray();
        foreach (var key in _meshes.Keys.Where(k => !_usedMeshes.Contains(k)).ToArray()) _meshes.Remove(key);
        var deadTextures = _textures.Where(p => !_usedTextures.Contains(p.Key)).Select(p => p.Value.Id).ToArray();
        foreach (var key in _textures.Keys.Where(k => !_usedTextures.Contains(k)).ToArray()) _textures.Remove(key);
        if(_uploads.Count>0&&GC.GetTotalMemory(false)>512L*1024*1024)GC.Collect();
        if(_uploads.Count>0)Console.WriteLine($"Browser uploads meshes={_uploads.Count}, textures={_textureUploads.Count}, managedMB={GC.GetTotalMemory(false)/1048576}, pixelsMB={_textures.Keys.Sum(t=>(long)t.PixelData.Length)/1048576}, vertices={_meshes.Keys.Sum(m=>(long)m.VertexData.Length)}");
        return JsonSerializer.Serialize(new BrowserFrame { loopMs=loopMs, pendingTextures=_pendingTextureUploads.Count, vp = _vp, eye = _eye, background = _background, ambient = _ambient,
            lights = _lights, points = _points, uploads = _uploads, textures = _textureUploads,
            draws = _draws, ui = _ui, deadMeshes=deadMeshes, deadTextures=deadTextures, audio=JsonSerializer.SerializeToElement(audio) },BrowserJsonContext.Default.BrowserFrame);
    }
    private static float[] V3(Vector3 v) => [v.X,v.Y,v.Z];
    private static float[] V4(Vector4 v) => [v.X,v.Y,v.Z,v.W];
    private static float[] Matrix(Matrix4x4 m) =>
        [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
         m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
}

internal sealed class BrowserAudio : IPortableAudio
{
    private readonly List<object> _commands = new();
    private readonly HashSet<Guid> _playing = new();
    public bool IsPlaying(Guid id) => _playing.Contains(id);
    public void Ended(Guid id) => _playing.Remove(id);
    public object[] Drain() { var commands = _commands.ToArray(); _commands.Clear(); return commands; }
    public void Play(Guid id, AudioClip clip, AudioSource3D source)
    {
        _playing.Add(id);
        _commands.Add(new { kind = "play", id, path = clip.SourcePath.Replace("/game/", ""), loop = source.Loop });
        Update(id, source);
    }
    public void Pause(Guid id) { _playing.Remove(id); _commands.Add(new { kind = "pause", id }); }
    public void Stop(Guid id) { _playing.Remove(id); _commands.Add(new { kind = "stop", id }); }
    public void Update(Guid id, AudioSource3D source)
    {
        if (!_playing.Contains(id)) return;
        var p = source.Transform.WorldPosition;
        _commands.Add(new { kind = "settings", id, volume = source.Volume, pitch = source.Pitch, loop = source.Loop,
            spatial = source.Spatial, position = new[] {p.X,p.Y,p.Z},
            min = source.MinDistance, max = source.MaxDistance, rolloff = source.RolloffFactor });
    }
    public void Listener(Vector3 position, Vector3 forward, Vector3 up, float volume) =>
        _commands.Add(new { kind = "listener", position = new[] {position.X,position.Y,position.Z},
            forward = new[] {forward.X,forward.Y,forward.Z}, up = new[] {up.X,up.Y,up.Z}, volume });
}
