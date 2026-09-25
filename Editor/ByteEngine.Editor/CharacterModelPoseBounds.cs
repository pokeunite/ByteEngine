using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

/// <summary>
/// Measures the same skeletal pose drawn by the editor viewport without adding
/// renderers to the Blueprint or changing serialized component state.
/// </summary>
internal static class CharacterModelPoseBounds
{
    public static bool TryGetWorldBounds(GameObject modelRoot, out BoundingBox3D bounds)
    {
        bounds = BoundingBox3D.Empty;
        Scene? scene = modelRoot.Scene;
        if (scene == null) return false;
        try
        {
            using var preview = new EditorSkeletalPreviewCache();
            preview.Prepare(scene);
            return preview.TryGetWorldBounds(modelRoot, out bounds);
        }
        catch
        {
            // An unresolved or unskinned model continues through the static fallback.
            return false;
        }
    }
}
