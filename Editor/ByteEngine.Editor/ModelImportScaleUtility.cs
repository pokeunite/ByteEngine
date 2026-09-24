using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Editor;

internal readonly record struct ModelScaleAnalysis(
    float RequestedScale,
    float AppliedScale,
    float RawLargestDimension,
    float HierarchyLargestDimension,
    bool Normalized)
{
    public float FinalLargestDimension =>
        HierarchyLargestDimension *
        AppliedScale;

    public string Summary =>
        Normalized
            ? $"normalized {RequestedScale:0.####} -> {AppliedScale:0.####} | hierarchy {HierarchyLargestDimension:0.###} -> final {FinalLargestDimension:0.###}"
            : $"scale {AppliedScale:0.####} | hierarchy {HierarchyLargestDimension:0.###} -> final {FinalLargestDimension:0.###}";
}

internal static class ModelImportScaleUtility
{
    // Importers own unit and axis conversion. The editor never guesses an
    // "expected humanoid height" or multiplies imported models a second time.

    public static ModelScaleAnalysis Analyze(
        AssetRecord asset,
        ModelAsset model)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(model);

        // Importers now put source-unit and axis conversion into one unanimated
        // internal node. An authored character's height is never normalized.
        float requestedScale = asset.Metadata.ModelImporter.ImportScale;
        return new ModelScaleAnalysis(
            requestedScale,
            1.0f,
            GetLargestRawMeshDimension(model),
            GetLargestHierarchyDimension(model),
            false);
    }
    public static float GetLargestHierarchyDimension(
        ModelAsset model)
    {
        if (!TryCalculateHierarchyBounds(
                model,
                out Vector3 minimum,
                out Vector3 maximum))
        {
            return 0.0f;
        }

        return LargestDimension(
            maximum -
            minimum);
    }

    public static bool TryCalculateHierarchyBounds(
        ModelAsset model,
        out Vector3 minimum,
        out Vector3 maximum)
    {
        minimum =
            new Vector3(
                float.PositiveInfinity);

        maximum =
            new Vector3(
                float.NegativeInfinity);

        Dictionary<string, ImportedNode> nodes =
            model.Nodes.ToDictionary(
                node =>
                    node.Key,
                StringComparer.Ordinal);

        Dictionary<string, ImportedMesh> meshes =
            model.Meshes.ToDictionary(
                mesh =>
                    mesh.Key,
                StringComparer.Ordinal);

        var worldTransforms =
            new Dictionary<string, Matrix4x4>(
                StringComparer.Ordinal);

        var resolving =
            new HashSet<string>(
                StringComparer.Ordinal);

        Matrix4x4 ResolveWorld(
            ImportedNode node)
        {
            if (worldTransforms.TryGetValue(
                    node.Key,
                    out Matrix4x4 cached))
            {
                return cached;
            }

            if (!resolving.Add(
                    node.Key))
            {
                return node.LocalTransform;
            }

            Matrix4x4 world =
                node.LocalTransform;

            if (node.ParentKey !=
                    null &&
                nodes.TryGetValue(
                    node.ParentKey,
                    out ImportedNode? parent))
            {
                world =
                    node.LocalTransform *
                    ResolveWorld(
                        parent);
            }

            resolving.Remove(
                node.Key);

            worldTransforms[node.Key] =
                world;

            return world;
        }

        bool found =
            false;

        foreach (ImportedNode node
                 in model.Nodes)
        {
            if (node.MeshKeys.Count ==
                0)
            {
                continue;
            }

            Matrix4x4 world =
                ResolveWorld(
                    node);

            foreach (string meshKey
                     in node.MeshKeys)
            {
                if (!meshes.TryGetValue(
                        meshKey,
                        out ImportedMesh? mesh))
                {
                    continue;
                }

                for (int index =
                         0;
                     index +
                     2 <
                     mesh.Vertices.Length;
                     index +=
                     8)
                {
                    Vector3 local =
                        new(
                            mesh.Vertices[index],
                            mesh.Vertices[index + 1],
                            mesh.Vertices[index + 2]);

                    if (!IsFinite(
                            local))
                    {
                        continue;
                    }

                    Vector3 transformed =
                        Vector3.Transform(
                            local,
                            world);

                    if (!IsFinite(
                            transformed))
                    {
                        continue;
                    }

                    minimum =
                        Vector3.Min(
                            minimum,
                            transformed);

                    maximum =
                        Vector3.Max(
                            maximum,
                            transformed);

                    found =
                        true;
                }
            }
        }

        if (!found)
        {
            minimum =
                Vector3.Zero;

            maximum =
                Vector3.Zero;
        }

        return found;
    }

    // No character-height heuristic: source file units are authoritative.

    private static float GetLargestRawMeshDimension(
        ModelAsset model)
    {
        float largest =
            0.0f;

        foreach (ImportedMesh mesh
                 in model.Meshes)
        {
            Vector3 minimum =
                new(
                    float.PositiveInfinity,
                    float.PositiveInfinity,
                    float.PositiveInfinity);

            Vector3 maximum =
                new(
                    float.NegativeInfinity,
                    float.NegativeInfinity,
                    float.NegativeInfinity);

            bool found =
                false;

            for (int index =
                     0;
                 index +
                 2 <
                 mesh.Vertices.Length;
                 index +=
                 8)
            {
                Vector3 position =
                    new(
                        mesh.Vertices[index],
                        mesh.Vertices[index + 1],
                        mesh.Vertices[index + 2]);

                if (!IsFinite(
                        position))
                {
                    continue;
                }

                minimum =
                    Vector3.Min(
                        minimum,
                        position);

                maximum =
                    Vector3.Max(
                        maximum,
                        position);

                found =
                    true;
            }

            if (found)
            {
                largest =
                    Math.Max(
                        largest,
                        LargestDimension(
                            maximum -
                            minimum));
            }
        }

        return largest;
    }

    private static float LargestDimension(
        Vector3 size)
    {
        return Math.Max(
            Math.Abs(
                size.X),
            Math.Max(
                Math.Abs(
                    size.Y),
                Math.Abs(
                    size.Z)));
    }

    private static bool IsFinite(
        Vector3 value)
    {
        return
            float.IsFinite(
                value.X) &&
            float.IsFinite(
                value.Y) &&
            float.IsFinite(
                value.Z);
    }
}
