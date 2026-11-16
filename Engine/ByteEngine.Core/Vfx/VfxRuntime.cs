using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Diagnostics;

namespace ByteEngine.Core.Vfx;

/// <summary>Reusable pooled one-shots for gameplay components. Presentation failures never interrupt damage or movement.</summary>
public static class VfxRuntime
{
    public static VfxPlayer? Spawn(Scene.Scene scene, AssetReference effect, Vector3 position, Quaternion rotation, float size = 1)
    {
        if (effect.IsEmpty) return null;
        try
        {
            if (!AnimationRuntimeAssets.TryGet(out var assets) || assets == null) return null;
            return VfxSpawnPool.For(scene).Spawn(scene, effect, assets.VfxRevision, assets.LoadVfxEffect(effect), position, rotation, size);
        }
        catch (Exception e)
        {
            RuntimeDiagnostics.OutputSink?.Invoke("VFX could not spawn: " + e.Message);
            return null;
        }
    }
}
