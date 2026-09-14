using ByteEngine.Core;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor;

using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

internal sealed class GpuBlueprintRenderDiagnostic
    : ByteEngineApplication
{
    private readonly string _projectPath;
    private readonly string _blueprintPath;

    public GpuBlueprintRenderDiagnostic(
        string projectPath,
        string blueprintPath)
        : base(800, 600, "ByteEngine GPU Blueprint Diagnostic")
    {
        _projectPath = projectPath;
        _blueprintPath = blueprintPath;
        IsVisible = false;
    }

    protected override void OnEngineStart()
    {
        using EditorProjectContext project =
            EditorProjectContext.Open(_projectPath, message => Console.WriteLine($"WARNING: {message}"));

        BlueprintDefinition blueprint =
            new BlueprintSerializer().Load(_blueprintPath);

        var objects = new List<GameObjectData> { blueprint.Root };
        objects.AddRange(blueprint.Children);

        Scene preview = project.Scenes.Deserialize(
            new SceneData
            {
                Name = blueprint.Name + " GPU Diagnostic",
                SceneId = Guid.NewGuid(),
                GameObjects = objects
            });

        var camera = new EditorCamera3D
        {
            Position = new System.Numerics.Vector3(2.800691f, 2.185876f, 2.696458f),
            Yaw = -135f,
            Pitch = -18f
        };

        using var framebuffer = new SceneFramebuffer();
        framebuffer.Render(
            Renderer,
            Renderer3D,
            preview,
            EditorMode.Edit,
            new EditorCamera(),
            camera,
            true,
            800,
            600,
            800,
            600,
            drawGrid3D: false);
        GL.Finish();

        byte[] pixels = new byte[800 * 600 * 4];
        GL.BindTexture(TextureTarget.Texture2D, (int)framebuffer.TextureId);
        GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        byte backgroundR = (byte)MathF.Round(0.055f * 255f);
        byte backgroundG = (byte)MathF.Round(0.065f * 255f);
        byte backgroundB = (byte)MathF.Round(0.085f * 255f);
        int changedPixels = 0;

        for (int index = 0; index < pixels.Length; index += 4)
        {
            if (Math.Abs(pixels[index] - backgroundR) > 2 ||
                Math.Abs(pixels[index + 1] - backgroundG) > 2 ||
                Math.Abs(pixels[index + 2] - backgroundB) > 2)
            {
                changedPixels++;
            }
        }

        Console.WriteLine($"GPU_DIAGNOSTIC changedPixels={changedPixels} glError={GL.GetError()}");

        if (changedPixels == 0)
        {
            throw new InvalidOperationException("The Blueprint model produced no visible GPU pixels.");
        }

        Close();
    }

    protected override bool ShouldUpdateScene => false;

    protected override bool ShouldRenderSceneToWindow => false;
}
