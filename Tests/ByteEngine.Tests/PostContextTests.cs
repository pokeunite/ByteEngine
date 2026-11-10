using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;
internal static class PostContextTests
{
    internal static void Run()
    {
        using var main = Window(); main.Context.MakeCurrent(); GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        float[] pixels = new float[128 * 128 * 4], depth = new float[128 * 128];
        for (int i = 0; i < depth.Length; i++) { pixels[i * 4] = i % 128 < 64 ? .04f : 3f; pixels[i * 4 + 1] = .15f; pixels[i * 4 + 2] = .3f; pixels[i * 4 + 3] = 1; depth[i] = .98f; }
        int color = GL.GenTexture(), z = GL.GenTexture();
        void Upload(int id, float[] data, bool isDepth)
        {
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, isDepth ? PixelInternalFormat.DepthComponent32f : PixelInternalFormat.Rgba16f, 128, 128, 0, isDepth ? PixelFormat.DepthComponent : PixelFormat.Rgba, PixelType.Float, data);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }
        Upload(color, pixels, false); Upload(z, depth, true); GL.Finish();
        using var post = new PostProcess3D { ResolveAntiAliasing = true };
        byte[] Render(PostProcess3D renderer, int width)
        {
            var look = new SkyEnvironment { Quality = GraphicsQuality.High }.CaptureLook();
            renderer.Render(color, 0, width, 64, true, 1, true, z, Matrix4x4.CreatePerspectiveFieldOfView(1, width / 64f, .1f, 100), look);
            byte[] result = new byte[width * 64 * 4]; GL.ReadPixels(0, 0, width, 64, PixelFormat.Rgba, PixelType.UnsignedByte, result);
            if (GL.GetError() != ErrorCode.NoError) throw new Exception("Shared-context post GL error");
            return result;
        }
        try
        {
            for (int pass = 0; pass < 4; pass++)
            {
                int width = pass % 2 == 0 ? 64 : 96;
                byte[] expected = Render(post, width);
                using (var secondary = new NativeWindow(new NativeWindowSettings { ClientSize = new(128, 128), StartVisible = false, API = ContextAPI.OpenGL, APIVersion = new(3, 3), Profile = ContextProfile.Core, SharedContext = main.Context }))
                {
                    try
                    {
                        secondary.Context.MakeCurrent();
                        using var scope = GraphicsContextScope.Enter(GraphicsContextScope.AllocateContextId());
                        using var otherPost = new PostProcess3D { ResolveAntiAliasing = true };
                        byte[] actual = Render(otherPost, width);
                        if (!expected.Zip(actual).All(pair => Math.Abs(pair.First - pair.Second) <= 1)) throw new Exception("Second GL context changed resolved post pixels");
                    }
                    finally { main.Context.MakeCurrent(); }
                }
                if (!expected.SequenceEqual(Render(post, width))) throw new Exception("Returning to main GL context changed post pixels");
            }
        }
        finally { main.Context.MakeCurrent(); GL.DeleteTexture(color); GL.DeleteTexture(z); }
        Console.WriteLine("PASS resolved post: four real shared-context window transitions, resize/reuse, identical returning pixels and no GL errors");
    }
    static NativeWindow Window() => new(new NativeWindowSettings { ClientSize = new(128, 128), StartVisible = false, API = ContextAPI.OpenGL, APIVersion = new(3, 3), Profile = ContextProfile.Core });
}
