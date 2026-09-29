using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

using var loop = new PortableGameLoop();
var scene = new Scene("Portable test");
var camera = new GameObject("Camera");
camera.Transform.LocalPosition = new(0, 0, 5);
camera.AddComponent(new Camera3D());
scene.AddGameObject(camera);
var cube = new GameObject("Cube");
cube.AddComponent(new MeshRenderer { UsePrimitive = true });
var movement = cube.AddComponent(new Movement());
scene.AddGameObject(cube);
var spawner = cube.AddComponent(new StartupSpawner());
var canvas = new GameObject("Canvas");
canvas.AddComponent(new UiCanvas());
scene.AddGameObject(canvas);
var text = new GameObject("Text");
text.SetParent(canvas, false);
text.AddComponent(new UiText { Text = "Shared UI" });
scene.AddGameObject(text);
loop.Scenes.LoadScene(scene);
Check(spawner.Created?.Starts == 1, "component creation during startup");
var input = new RawInputSnapshot();
input.KeysDown.Add(Key.W);
var sink = new Sink();
loop.Tick(.016, input, 800, 600, new(.5f), true, false, sink);
Check(movement.Updates == 1 && movement.Starts == 1, "shared component lifecycle");
Check(Input.IsKeyPressed(Key.W) && Input.IsKeyDown(Key.W), "press transition");
Check(cube.Transform.LocalPosition.Z < 0, "input drives shared scene transform");
Check(sink.Draws == 1 && sink.Mesh != null && !sink.Mesh.IsUploaded, "3D pass without desktop GPU");
Check(sink.Mesh!.VertexData.Length == sink.Mesh.VertexCount * 8 && sink.Mesh.IndexData.Length > 0, "CPU geometry");
Check(sink.Text == "Shared UI", "shared UI commands");
loop.Tick(.016, input, 800, 600, new(.5f), true, false, sink);
Check(!Input.IsKeyPressed(Key.W), "held key not repeated press");
input.Clear();
loop.Tick(20, input, 800, 600, new(.5f), true, false, sink);
Check(Input.IsKeyReleased(Key.W), "release transition");
Check(Time.DeltaTime == .1, "background-tab delta clamp");
Check(movement.Starts == 1 && movement.Updates == 3, "no repeated start");
bool rejected = false;
try { loop.Tick(double.NaN, input, 800, 600, new(.5f), true, false, sink); }
catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "invalid time rejected");
loop.Dispose();
Check(movement.Stops == 1 && !scene.IsLoaded, "shared unload lifecycle");
Console.WriteLine("Portable runtime: 13 checks passed (no GLFW/OpenGL initialization).");

if (args.Length >= 2 && args[0] == "--export") ExportFixture.Run(Path.GetFullPath(args[1]));

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    Console.WriteLine("PASS: " + name);
}

sealed class Movement : Component
{
    public int Starts, Updates, Stops;
    protected override void OnStart() => Starts++;
    protected override void OnStop() => Stops++;
    protected override void OnUpdate()
    {
        Updates++;
        if (Input.IsKeyDown(Key.W)) Transform.LocalPosition -= Vector3.UnitZ * (float)Time.DeltaTime;
    }
}
sealed class StartupSpawner : Component
{
    public Movement? Created;
    protected override void OnStart() => Created = GameObject.AddComponent(new Movement());
}
sealed class Sink : IRenderFrameSink
{
    public int Draws;
    public Mesh? Mesh;
    public string? Text;
    public void Draw3D(RenderView3D? view, RenderLighting3D lighting, RenderEnvironment3D environment,
        IReadOnlyList<RenderSubmission> submissions)
    {
        Draws = submissions.Count;
        Mesh = submissions.FirstOrDefault().Mesh;
        if (view == null) throw new Exception("Camera not captured.");
    }
    public void DrawText(string value, string? fontPath, int size, Vector2 position,
        Vector4 color, float wrapWidth, UiAnchor anchor) => Text = value;
    public void DrawQuad(Vector2 position, Vector2 size, Vector4 color) { }
    public void DrawImage(Texture2D texture, Vector2 position, Vector2 size, Vector4 color) { }
}
