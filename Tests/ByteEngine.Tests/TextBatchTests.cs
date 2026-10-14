using System.Numerics;
using ByteEngine.Core.Graphics;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

internal static class TextBatchTests
{
    internal static void Run()
    {
        using var renderer = new Renderer2D();
        renderer.Initialize(128, 128);
        string font = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
        foreach (var anchor in new[] { UiAnchor.TopLeft, UiAnchor.Center, UiAnchor.BottomRight })
        {
            byte[] Draw(bool batched)
            {
                GL.Viewport(0, 0, 128, 128); GL.Disable(EnableCap.ScissorTest);
                GL.ClearColor(0, 0, 0, 1); GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                renderer.EnableTextBatching = batched; renderer.BeginUi();
                renderer.DrawQuad(new(64), new(128), new(.05f, .1f, .15f, 1));
                renderer.SetUiClip(new Vector4(8, 8, 112, 104));
                renderer.DrawText("Av WA 12/12\nReload interrupted!", font, 16, new(64), new(1, .7f, .2f, .8f), 85, anchor);
                renderer.SetUiClip(null);
                renderer.DrawQuad(new(70, 76), new(10, 7), new(.2f, .5f, .7f, .6f));
                GL.Finish(); var pixels = new byte[128 * 128 * 4];
                GL.ReadPixels(0, 0, 128, 128, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
                return pixels;
            }
            var reference = Draw(false); var actual = Draw(true);
            if (!reference.Zip(actual).All(p => Math.Abs(p.First - p.Second) <= 1))
                throw new Exception("Text batch changes glyph pixels: " + anchor);
            if (reference.Where((v, i) => i % 4 == 0 && v > 40).Count() < 20)
                throw new Exception("Text fixture rendered no glyphs: " + anchor);
        }
        if (GL.GetError() != ErrorCode.NoError) throw new Exception("Text batching GL error");
        Console.WriteLine("PASS text batching: pixel-equivalent kerning, wrapping, anchors, clipping, alpha and surrounding quad order");
    }
}
