using ByteEngine.Core;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

/// <summary>Hidden GPU smoke test; never drives or interacts with the editor UI.</summary>
internal sealed class GraphicsEdgeSmokeTest : ByteEngineApplication
{
    public GraphicsEdgeSmokeTest() : base(64, 64, "ByteEngine Graphics Smoke Test") { IsVisible = false; }

    private void TestFoliageRendering()
    {
        string root = Path.Combine(Path.GetTempPath(), "ByteEngine-foliage-gpu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        try
        {
            // Synthetic static plant: no external content or user project needed.
            File.WriteAllText(Path.Combine(root, "Assets", "plant.obj"),
                "o Plant\nv -0.3 0 0\nv 0.3 0 0\nv 0 1 0\nf 1 2 3\n");
            using var database = new ByteEngine.Core.Assets.AssetDatabase(root, new[] { "Assets" });
            database.Scan();
            using var assets = new ByteEngine.Core.Assets.AssetManager(database);
            Guid registration = ByteEngine.Core.Animation.AnimationRuntimeAssets.Configure(assets);
            try
            {
                var record = database.Assets.Single(asset => asset.Type == ByteEngine.Core.Assets.AssetType.Model3D);
                var scene = new ByteEngine.Core.Scene.Scene("Foliage GPU");
                var camera = new ByteEngine.Editor.EditorCamera3D();
                camera.Frame(new BoundingBox3D(new(-1, 0, -1), new(1, 1, 1)));
                using var framebuffer = new ByteEngine.Editor.SceneFramebuffer();
                byte[] Render()
                {
                    framebuffer.Render(Renderer, Renderer3D, scene, ByteEngine.Editor.EditorMode.Edit,
                        new ByteEngine.Editor.EditorCamera(), camera, true, 128, 128, 128, 128, drawGrid3D: false);
                    GL.Finish();
                    byte[] pixels = new byte[128 * 128 * 4];
                    GL.BindTexture(TextureTarget.Texture2D, (int)framebuffer.TextureId);
                    GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                    GL.BindTexture(TextureTarget.Texture2D, 0);
                    return pixels;
                }
                byte[] empty = Render();
                var patch = new FoliagePatch
                {
                    Model = new(record.Guid, record.ProjectPath), Area = new(2, 2), Amount = 9,
                    SnapToGround = false, WindEnabled = true, CastShadows = false
                };
                scene.CreateGameObject("Plants").AddComponent(patch);
                byte[] planted = Render();
                int changed = 0;
                for (int i = 0; i < empty.Length; i += 4)
                    if (Math.Abs(empty[i] - planted[i]) > 2) changed++;
                if (!patch.Status.StartsWith("9 plants") || changed == 0)
                    throw new Exception($"Foliage rendering failed: {patch.Status}; pixels={changed}");
                patch.Rebuild();
                Render();
                if (GL.GetError() != ErrorCode.NoError) throw new Exception("OpenGL foliage error.");
                Console.WriteLine($"GPU foliage scatter and rebuild smoke test passed ({changed} visible pixels).");
            }
            finally { ByteEngine.Core.Animation.AnimationRuntimeAssets.Clear(registration); }
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    protected override bool ShouldUpdateScene => false;
    protected override void OnEngineStart()
    {
        int texture = GL.GenTexture();
        using var post = new PostProcess3D();
        try
        {
            GL.BindTexture(TextureTarget.Texture2D, texture);
            byte[] source = new byte[64 * 64 * 4];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                int index = (y * 64 + x) * 4;
                byte color = x > y * .6f + 10 ? (byte)255 : (byte)0;
                source[index] = color; source[index + 1] = color; source[index + 2] = color; source[index + 3] = 255;
            }
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 64, 64, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, source);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            byte[] Render(bool smooth)
            {
                post.Render(texture, 0, 64, 64, true, 1, smooth);
                GL.Finish();
                byte[] pixels = new byte[64 * 64 * 4];
                GL.ReadPixels(0, 0, 64, 64, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                return pixels;
            }
            byte[] off = Render(false), on = Render(true);
            int changed = 0;
            for (int i = 0; i < off.Length; i += 4)
                if (Math.Abs(off[i] - on[i]) > 2) changed++;
            if (changed == 0 || changed > 1000) throw new Exception($"Unexpected AA effect: {changed} pixels.");
            if (GL.GetError() != ErrorCode.NoError) throw new Exception("OpenGL error in post processing.");
            Console.WriteLine($"GPU shader compilation and AA smoke test passed ({changed} edge pixels smoothed).");
        }
        finally { GL.DeleteTexture(texture); }
        TestFoliageRendering();
        Close();
    }
}
