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
    private const float MinimumUsefulSize =
        0.10f;

    private const float MaximumTypicalObjectSize =
        20.0f;

    /*
     * Character assets are commonly authored/exported in centimetres while
     * ByteEngine gameplay content is authored in metre-like world units.
     *
     * Do not rewrite source vertices, bind poses or animation tracks here.
     * The editor already applies ModelScaleAnalysis.AppliedScale at the model
     * hierarchy root in both Scene authoring and Blueprint authoring, which
     * keeps skeleton/animation data internally consistent.
     */
    private const float CharacterTargetSize =
        1.80f;

    private const float CharacterMinimumSaneSize =
        0.75f;

    private const float CharacterMaximumSaneSize =
        3.50f;

    private const float CharacterLargeUnitMismatchThreshold =
        10.0f;

    private const float CharacterSmallUnitMismatchThreshold =
        0.05f;

    private static readonly float[] CharacterUnitScaleCandidates =
    {
        10000.0f,
        1000.0f,
        100.0f,
        10.0f,
        0.1f,
        0.01f,
        0.001f,
        0.0001f
    };

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

        /*
         * New character-friendly normalization.
         *
         * Only infer a unit correction while Import Scale is still at the
         * untouched default (1). An explicit user import scale always wins.
         *
         * The model must also look like a humanoid/skeletal character and be
         * wildly outside a normal character-size range. This deliberately
         * leaves ordinary props, environments and intentionally unusual
         * explicit scales alone.
         */
        if (IsDefaultRequestedScale(
                requestedScale) &&
            LooksLikeHumanoidCharacter(
                model) &&
            TryChooseCharacterUnitScale(
                hierarchyLargestDimension,
                out float characterUnitScale))
        {
            return new ModelScaleAnalysis(
                requestedScale,
                characterUnitScale,
                rawLargestDimension,
                hierarchyLargestDimension,
                true);
        }

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

        /*
         * Preserve the existing FBX compatibility rules. These cover older
         * assets where a tiny metadata scale was authored even though the FBX
         * hierarchy was already in ByteEngine-sized units, and FBX hierarchies
         * whose node transforms unexpectedly shrank otherwise sane geometry.
         */
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

    private static bool IsDefaultRequestedScale(
        float scale)
    {
        return
            MathF.Abs(
                scale -
                1.0f) <=
            0.0001f;
    }

    private static bool TryChooseCharacterUnitScale(
        float hierarchyLargestDimension,
        out float scale)
    {
        scale =
            1.0f;

        if (!float.IsFinite(
                hierarchyLargestDimension) ||
            hierarchyLargestDimension <=
                0.000001f)
        {
            return false;
        }

        bool clearlyWrongUnits =
            hierarchyLargestDimension >=
                CharacterLargeUnitMismatchThreshold ||
            hierarchyLargestDimension <=
                CharacterSmallUnitMismatchThreshold;

        if (!clearlyWrongUnits)
        {
            return false;
        }

        float bestScore =
            float.PositiveInfinity;

        bool found =
            false;

        foreach (float candidate
                 in CharacterUnitScaleCandidates)
        {
            float finalSize =
                hierarchyLargestDimension *
                candidate;

            if (!float.IsFinite(
                    finalSize) ||
                finalSize <
                    CharacterMinimumSaneSize ||
                finalSize >
                    CharacterMaximumSaneSize)
            {
                continue;
            }

            /*
             * Compare proportionally rather than linearly so 0.9 m and 3.6 m
             * are treated as equally distant from the 1.8 m authoring target.
             */
            float score =
                MathF.Abs(
                    MathF.Log(
                        finalSize /
                        CharacterTargetSize));

            if (score >=
                bestScore)
            {
                continue;
            }

            bestScore =
                score;

            scale =
                candidate;

            found =
                true;
        }

        return found;
    }

    private static bool LooksLikeHumanoidCharacter(
        ModelAsset model)
    {
        if (model.RigType ==
            AnimationRigType.Humanoid)
        {
            return true;
        }

        SkeletonAsset? skeleton =
            model.Skeleton;

        if (skeleton ==
                null ||
            skeleton.Bones.Count <
                8)
        {
            return false;
        }

        string[] names =
            skeleton.Bones
                .Select(
                    bone =>
                        NormalizeBoneName(
                            bone.Name))
                .Where(
                    name =>
                        name.Length >
                        0)
                .ToArray();

        bool hasHips =
            names.Any(
                name =>
                    ContainsAny(
                        name,
                        "hip",
                        "pelvis"));

        bool hasSpine =
            names.Any(
                name =>
                    ContainsAny(
                        name,
                        "spine",
                        "chest",
                        "torso"));

        bool hasHead =
            names.Any(
                name =>
                    name.Contains(
                        "head",
                        StringComparison.Ordinal));

        bool hasLeftLimb =
            names.Any(
                name =>
                    HasSideToken(
                        name,
                        true) &&
                    ContainsAny(
                        name,
                        "arm",
                        "hand",
                        "shoulder",
                        "leg",
                        "thigh",
                        "calf",
                        "foot"));

        bool hasRightLimb =
            names.Any(
                name =>
                    HasSideToken(
                        name,
                        false) &&
                    ContainsAny(
                        name,
                        "arm",
                        "hand",
                        "shoulder",
                        "leg",
                        "thigh",
                        "calf",
                        "foot"));

        return
            hasHips &&
            hasSpine &&
            hasHead &&
            hasLeftLimb &&
            hasRightLimb;
    }

    private static string NormalizeBoneName(
        string? value)
    {
        return
            string.IsNullOrWhiteSpace(
                value)
                ? string.Empty
                : value
                    .Trim()
                    .ToLowerInvariant();
    }

    private static bool HasSideToken(
        string name,
        bool left)
    {
        string word =
            left
                ? "left"
                : "right";

        string suffix =
            left
                ? "_l"
                : "_r";

        string dot =
            left
                ? ".l"
                : ".r";

        string dash =
            left
                ? "-l"
                : "-r";

        return
            name.Contains(
                word,
                StringComparison.Ordinal) ||
            name.EndsWith(
                suffix,
                StringComparison.Ordinal) ||
            name.Contains(
                dot,
                StringComparison.Ordinal) ||
            name.Contains(
                dash,
                StringComparison.Ordinal);
    }

    private static bool ContainsAny(
        string value,
        params string[] terms)
    {
        return
            terms.Any(
                term =>
                    value.Contains(
                        term,
                        StringComparison.Ordinal));
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
