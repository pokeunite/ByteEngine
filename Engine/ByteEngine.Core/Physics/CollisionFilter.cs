using ByteEngine.Core.Characters;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Physics;

public static class CollisionFilter
{
    public static bool ShouldInteract(GameObject first, Collider3D? firstCollider, GameObject second, Collider3D? secondCollider, ClassificationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(first); ArgumentNullException.ThrowIfNull(second); ArgumentNullException.ThrowIfNull(settings);
        return Accepts(first, firstCollider, second.Layer, settings) && Accepts(second, secondCollider, first.Layer, settings);
    }
    public static string Explain(GameObject first, Collider3D? firstCollider, GameObject second, Collider3D? secondCollider, ClassificationSettings settings)
    {
        bool result = ShouldInteract(first, firstCollider, second, secondCollider, settings);
        string a = settings.FindLayer(first.Layer)?.Name ?? $"Layer {first.Layer}";
        string b = settings.FindLayer(second.Layer)?.Name ?? $"Layer {second.Layer}";
        return $"A Layer: {a}; B Layer: {b}; Result: {(result ? "interact" : "ignored")}";
    }
    private static bool Accepts(GameObject owner, Collider3D? collider, int otherLayer, ClassificationSettings settings) =>
        collider is { UseProjectMatrix: false } ? collider.CollisionMask.Contains(otherLayer) : settings.CollisionMatrix.ShouldInteract(owner.Layer, otherLayer);
}
