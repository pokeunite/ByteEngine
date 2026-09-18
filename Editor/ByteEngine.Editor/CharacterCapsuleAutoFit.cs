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

    /*
     * A gameplay capsule should fit the character's body, not every extremity.
     * Using the largest horizontal visual dimension makes a T-pose arm span
     * become the collision diameter. The narrower horizontal axis is a much
     * better estimate of torso/body depth and remains stable across common
     * humanoid facing conventions.
     */
    private const float RadiusFromNarrowAxis = .55f;
    private const float MinimumRadiusHeightFraction = .08f;
    private const float MaximumRadiusHeightFraction = .22f;

    /*
     * Keep a little space above the head/hair without moving the capsule bottom.
     * Ground placement depends on the bottom matching the visual foot minimum.
     */
    private const float HeightCoverage = .98f;

    public static bool TryFit(
        GameObject root,
        AssetManager assets,
        out CharacterCapsuleFitResult result)
    {
        result = default;

        if (!Matrix4x4.Invert(
                root.Transform.WorldMatrix,
                out Matrix4x4 worldToRoot))
        {
            return false;
        }

        Vector3 minimum =
            new(float.PositiveInfinity);

        Vector3 maximum =
            new(float.NegativeInfinity);

        int meshCount = 0;
        int vertexCount = 0;

        /*
         * Normal imported/static hierarchy path.
         */
        foreach (GameObject gameObject
                 in SelfAndDescendants(root))
        {
            foreach (MeshRenderer renderer
                     in gameObject.Components
                         .OfType<MeshRenderer>())
            {
                if (!renderer.Visible ||
                    renderer.MeshReference is not
                        { } reference)
                {
                    continue;
                }

                try
                {
                    ModelAsset model =
                        assets.LoadModel(
                            reference.Model);

                    ImportedMesh? mesh =
                        model.Meshes.FirstOrDefault(
                            item =>
                                item.Key ==
                                reference.SubAssetKey);

                    if (mesh ==
                        null)
                    {
                        continue;
                    }

                    Matrix4x4 toRoot =
                        gameObject.Transform.WorldMatrix *
                        worldToRoot;

                    if (AccumulateMeshBounds(
                            mesh,
                            toRoot,
                            ref minimum,
                            ref maximum,
                            ref vertexCount))
                    {
                        meshCount++;
                    }
                }
                catch
                {
                    /*
                     * A broken asset reference is reported elsewhere and must
                     * not make character setup unusable.
                     */
                }
            }
        }

        /*
         * Some animated character setups expose only a SkeletalMeshRenderer
         * rather than one MeshRenderer per imported node. Include the model's
         * bind-pose geometry as a fallback/companion so auto-fit works for both
         * character rendering paths.
         */
        foreach (GameObject gameObject
                 in SelfAndDescendants(root))
        {
            foreach (SkeletalMeshRenderer renderer
                     in gameObject.Components
                         .OfType<SkeletalMeshRenderer>())
            {
                if (!renderer.Visible ||
                    renderer.Model.IsEmpty)
                {
                    continue;
                }

                try
                {
                    ModelAsset model =
                        assets.LoadModel(
                            renderer.Model);

                    Dictionary<string, Matrix4x4>
                        meshTransforms =
                            BuildMeshModelTransforms(
                                model);

                    Matrix4x4 modelToRoot =
                        gameObject.Transform.WorldMatrix *
                        worldToRoot;

                    foreach (ImportedMesh mesh
                             in model.Meshes)
                    {
                        Matrix4x4 meshModel =
                            meshTransforms.TryGetValue(
                                mesh.Key,
                                out Matrix4x4 transform)
                                ? transform
                                : Matrix4x4.Identity;

                        if (AccumulateMeshBounds(
                                mesh,
                                meshModel * modelToRoot,
                                ref minimum,
                                ref maximum,
                                ref vertexCount))
                        {
                            meshCount++;
                        }
                    }
                }
                catch
                {
                    /*
                     * Keep authoring functional when an animation/model asset
                     * is temporarily unresolved.
                     */
                }
            }
        }

        if (vertexCount ==
            0)
        {
            return false;
        }

        result =
            FitBounds(
                root,
                minimum,
                maximum,
                $"Character body geometry ({meshCount} mesh(es), {vertexCount} vertices)");

        return true;
    }

    public static CharacterCapsuleFitResult FitBounds(
        GameObject root,
        Vector3 minimum,
        Vector3 maximum,
        string source = "Provided visual bounds")
    {
        CapsuleCollider3D capsule =
            root.GetComponent<CapsuleCollider3D>() ??
            root.AddComponent(
                new CapsuleCollider3D());

        Vector3 size =
            Vector3.Max(
                maximum - minimum,
                Vector3.Zero);

        float visualHeight =
            Math.Max(
                size.Y,
                MinimumRadius * 2f);

        float narrowHorizontal =
            NarrowHorizontalExtent(
                size);

        float radiusFromBody =
            narrowHorizontal *
            RadiusFromNarrowAxis;

        float minimumBodyRadius =
            visualHeight *
            MinimumRadiusHeightFraction;

        float maximumBodyRadius =
            visualHeight *
            MaximumRadiusHeightFraction;

        float radius =
            Math.Clamp(
                Math.Clamp(
                    radiusFromBody,
                    minimumBodyRadius,
                    Math.Max(
                        minimumBodyRadius,
                        maximumBodyRadius)),
                MinimumRadius,
                MaximumRadius);

        /*
         * IMPORTANT:
         * Do not grow the height symmetrically around the visual midpoint.
         * That pushes the lower sphere below the character's feet and can start
         * the CharacterController overlapping the floor.
         *
         * Anchor the physical capsule bottom directly to the lowest rendered
         * point, then build upward from there.
         */
        float height =
            Math.Clamp(
                Math.Max(
                    visualHeight *
                    HeightCoverage,
                    radius * 2f),
                radius * 2f,
                MaximumHeight);

        Vector3 center =
            new(
                (minimum.X + maximum.X) *
                    .5f,
                minimum.Y +
                    height *
                    .5f,
                (minimum.Z + maximum.Z) *
                    .5f);

        if (!IsFinite(
                center))
        {
            center =
                new Vector3(
                    0f,
                    height * .5f,
                    0f);
        }

        capsule.Radius =
            radius;

        capsule.Height =
            height;

        capsule.Center =
            center;

        capsule.VisualBounds =
            size;

        capsule.AutoFitSource =
            source;

        return
            new CharacterCapsuleFitResult(
                size,
                radius,
                height,
                center,
                source);
    }

    private static float NarrowHorizontalExtent(
        Vector3 size)
    {
        float x =
            Math.Max(
                size.X,
                0f);

        float z =
            Math.Max(
                size.Z,
                0f);

        if (x <=
            .00001f)
        {
            return z;
        }

        if (z <=
            .00001f)
        {
            return x;
        }

        return
            Math.Min(
                x,
                z);
    }

    private static bool AccumulateMeshBounds(
        ImportedMesh mesh,
        Matrix4x4 toRoot,
        ref Vector3 minimum,
        ref Vector3 maximum,
        ref int vertexCount)
    {
        bool usedMesh =
            false;

        for (int index = 0;
             index + 2 <
             mesh.Vertices.Length;
             index += 8)
        {
            Vector3 point =
                Vector3.Transform(
                    new Vector3(
                        mesh.Vertices[index],
                        mesh.Vertices[index + 1],
                        mesh.Vertices[index + 2]),
                    toRoot);

            if (!IsFinite(
                    point))
            {
                continue;
            }

            minimum =
                Vector3.Min(
                    minimum,
                    point);

            maximum =
                Vector3.Max(
                    maximum,
                    point);

            vertexCount++;
            usedMesh =
                true;
        }

        return
            usedMesh;
    }

    private static Dictionary<string, Matrix4x4>
        BuildMeshModelTransforms(
            ModelAsset model)
    {
        var nodesByKey =
            model.Nodes.ToDictionary(
                node => node.Key,
                node => node,
                StringComparer.Ordinal);

        var globalByKey =
            new Dictionary<string, Matrix4x4>(
                StringComparer.Ordinal);

        Matrix4x4 ResolveGlobal(
            ImportedNode node,
            HashSet<string> visiting)
        {
            if (globalByKey.TryGetValue(
                    node.Key,
                    out Matrix4x4 cached))
            {
                return cached;
            }

            if (!visiting.Add(
                    node.Key))
            {
                return
                    node.LocalTransform;
            }

            Matrix4x4 global =
                node.LocalTransform;

            if (node.ParentKey !=
                    null &&
                nodesByKey.TryGetValue(
                    node.ParentKey,
                    out ImportedNode? parent))
            {
                /*
                 * ByteEngine/System.Numerics use row-vector composition:
                 * local * parentGlobal.
                 */
                global *=
                    ResolveGlobal(
                        parent,
                        visiting);
            }

            visiting.Remove(
                node.Key);

            globalByKey[node.Key] =
                global;

            return
                global;
        }

        var meshTransforms =
            new Dictionary<string, Matrix4x4>(
                StringComparer.Ordinal);

        foreach (ImportedNode node
                 in model.Nodes)
        {
            Matrix4x4 global =
                ResolveGlobal(
                    node,
                    new HashSet<string>(
                        StringComparer.Ordinal));

            foreach (string meshKey
                     in node.MeshKeys)
            {
                meshTransforms.TryAdd(
                    meshKey,
                    global);
            }
        }

        return
            meshTransforms;
    }

    private static IEnumerable<GameObject>
        SelfAndDescendants(
            GameObject root)
    {
        yield return
            root;

        foreach (GameObject child
                 in root.Children)
        {
            foreach (GameObject item
                     in SelfAndDescendants(
                         child))
            {
                yield return
                    item;
            }
        }
    }

    private static bool IsFinite(
        Vector3 value) =>
        float.IsFinite(
            value.X) &&
        float.IsFinite(
            value.Y) &&
        float.IsFinite(
            value.Z);
}
