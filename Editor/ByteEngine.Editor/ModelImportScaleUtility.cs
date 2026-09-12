using System.Numerics;

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
    private const float MinimumUsefulSize =
        0.10f;

    private const float MaximumTypicalObjectSize =
        20.0f;

    public static ModelScaleAnalysis Analyze(
        AssetRecord asset,
        ModelAsset model)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        ArgumentNullException.ThrowIfNull(
            model);

        float requestedScale =
            Math.Max(
                asset.Metadata
                    .ModelImporter
                    .ImportScale,
                0.0001f);

        float rawLargestDimension =
            GetLargestRawMeshDimension(
                model);

        float hierarchyLargestDimension =
            GetLargestHierarchyDimension(
                model);

        string extension =
            Path.GetExtension(
                    asset.FullPath)
                .ToLowerInvariant();

        if (extension !=
            ".fbx")
        {
            return new ModelScaleAnalysis(
                requestedScale,
                requestedScale,
                rawLargestDimension,
                hierarchyLargestDimension,
                false);
        }

        bool suspiciousTinyRequestedScale =
            requestedScale >=
                0.001f &&
            requestedScale <=
                0.05f;

        bool hierarchyAlreadySane =
            hierarchyLargestDimension >=
                MinimumUsefulSize &&
            hierarchyLargestDimension <=
                MaximumTypicalObjectSize;

        if (hierarchyAlreadySane &&
            suspiciousTinyRequestedScale)
        {
            return new ModelScaleAnalysis(
                requestedScale,
                1.0f,
                rawLargestDimension,
                hierarchyLargestDimension,
                true);
        }

        bool rawGeometryLooksSane =
            rawLargestDimension >=
                MinimumUsefulSize &&
            rawLargestDimension <=
                MaximumTypicalObjectSize;

        bool hierarchyWasShrunk =
            hierarchyLargestDimension >
                0.000001f &&
            hierarchyLargestDimension <
                MinimumUsefulSize &&
            rawGeometryLooksSane;

        if (hierarchyWasShrunk)
        {
            float correction =
                rawLargestDimension /
                hierarchyLargestDimension;

            correction =
                Math.Clamp(
                    correction,
                    0.0001f,
                    10000.0f);

            return new ModelScaleAnalysis(
                requestedScale,
                correction,
                rawLargestDimension,
                hierarchyLargestDimension,
                true);
        }

        return new ModelScaleAnalysis(
            requestedScale,
            requestedScale,
            rawLargestDimension,
            hierarchyLargestDimension,
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
