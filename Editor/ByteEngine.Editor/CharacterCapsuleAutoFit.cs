using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

internal readonly record struct CharacterCapsuleFitResult(
    Vector3 VisualBounds,
    float CapsuleRadius,
    float CapsuleHeight,
    Vector3 CapsuleCenter,
    string AutoFitSource);

internal static class CharacterCapsuleAutoFit
{
    private const float MinimumRadius = .05f;
    private const float MaximumRadius = 100f;
    private const float MaximumHeight = 200f;

    public static bool TryFit(
        GameObject root,
        AssetManager assets,
        out CharacterCapsuleFitResult result)
    {
        result = default;
        if (!Matrix4x4.Invert(root.Transform.WorldMatrix, out Matrix4x4 worldToRoot)) return false;

        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        int meshCount = 0;
        int vertexCount = 0;

        foreach (GameObject gameObject in SelfAndDescendants(root))
        {
            foreach (MeshRenderer renderer in gameObject.Components.OfType<MeshRenderer>())
            {
                if (!renderer.Visible || renderer.MeshReference is not { } reference) continue;
                try
                {
                    ModelAsset model = assets.LoadModel(reference.Model);
                    ImportedMesh? mesh = model.Meshes.FirstOrDefault(item => item.Key == reference.SubAssetKey);
                    if (mesh == null) continue;
                    Matrix4x4 toRoot = gameObject.Transform.WorldMatrix * worldToRoot;
                    bool usedMesh = false;
                    for (int index = 0; index + 2 < mesh.Vertices.Length; index += 8)
                    {
                        Vector3 point = Vector3.Transform(
                            new Vector3(mesh.Vertices[index], mesh.Vertices[index + 1], mesh.Vertices[index + 2]),
                            toRoot);
                        if (!IsFinite(point)) continue;
                        minimum = Vector3.Min(minimum, point);
                        maximum = Vector3.Max(maximum, point);
                        vertexCount++;
                        usedMesh = true;
                    }
                    if (usedMesh) meshCount++;
                }
                catch
                {
                    // A broken asset reference is reported elsewhere and must not break authoring.
                }
            }
        }

        if (vertexCount == 0) return false;
        result = FitBounds(root, minimum, maximum, $"Rendered model geometry ({meshCount} mesh(es), {vertexCount} vertices)");
        return true;
    }

    public static CharacterCapsuleFitResult FitBounds(
        GameObject root,
        Vector3 minimum,
        Vector3 maximum,
        string source = "Provided visual bounds")
    {
        CapsuleCollider3D capsule = root.GetComponent<CapsuleCollider3D>()
            ?? root.AddComponent(new CapsuleCollider3D());
        Vector3 size = Vector3.Max(maximum - minimum, Vector3.Zero);
        float radius = Math.Clamp(Math.Max(size.X, size.Z) * .525f, MinimumRadius, MaximumRadius);
        float height = Math.Clamp(Math.Max(size.Y * 1.02f, radius * 2f), radius * 2f, MaximumHeight);
        Vector3 center = (minimum + maximum) * .5f;
        if (!IsFinite(center)) center = Vector3.Zero;

        capsule.Radius = radius;
        capsule.Height = height;
        capsule.Center = center;
        capsule.VisualBounds = size;
        capsule.AutoFitSource = source;
        return new CharacterCapsuleFitResult(size, radius, height, center, source);
    }

    private static IEnumerable<GameObject> SelfAndDescendants(GameObject root)
    {
        yield return root;
        foreach (GameObject child in root.Children)
            foreach (GameObject item in SelfAndDescendants(child))
                yield return item;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
