using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

internal static class MaterialSamplingTests
{
    internal static void Run()
    {
        using var sampling = new MaterialTextureSampling();
        using var texture = Texture2D.FromPixels(1, 1, [255, 255, 255, 255]);
        sampling.BeginBatch();
        for (int draw = 0; draw < 150; draw++)
            foreach (int slot in new[] { 0, 1, 8, 9, 10, 11, 12 })
                sampling.Bind(texture, slot);
        if (sampling.BindCalls != 7 || sampling.SkippedBinds != 1043)
            throw new Exception("Repeated material maps were not deduplicated");
        texture.UpdatePixels([128, 255, 255, 255]);
        sampling.Bind(texture, 0);
        if (sampling.BindCalls != 8) throw new Exception("Dynamic texture edit was skipped");
        sampling.EndBatch();
        // An external caller may overwrite a material unit between passes.
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.BindSampler(0, 0);
        sampling.BeginBatch();
        sampling.Bind(texture, 0);
        GL.GetInteger(GetPName.TextureBinding2D, out int bound);
        if (bound == 0 || sampling.BindCalls != 9)
            throw new Exception("Next pass retained stale texture state");
        using (GraphicsContextScope.Enter(GraphicsContextScope.AllocateContextId()))
            sampling.Bind(texture, 0);
        if (sampling.BindCalls != 10) throw new Exception("Context transition retained bindings");
        sampling.EndBatch();
        Console.WriteLine("PASS material sampling: 1050 -> 7 binds (99.3% fewer); edits, pass boundaries and context invalidation");
    }
}
