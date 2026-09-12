using System.Numerics;

using Assimp;

using NumericsMatrix4x4 =
    System.Numerics.Matrix4x4;

using NumericsVector2 =
    System.Numerics.Vector2;

using NumericsVector3 =
    System.Numerics.Vector3;

using NumericsVector4 =
    System.Numerics.Vector4;

namespace ByteEngine.Core.Assets.Importers;

/// <summary>
/// FBX importer backed by AssimpNetter.
///
/// This preserves the FBX node hierarchy, static mesh geometry,
/// material colors/external textures, skeleton metadata, vertex
/// weights and animation clip metadata in ByteEngine's ImportedModel
/// representation.
/// </summary>
public sealed class FbxModelImporter
    : ModelImporter
{
    public override IReadOnlyCollection<string> Extensions { get; } =
        new[]
        {
            ".fbx"
        };

    public override ImportedModel Import(
        AssetRecord source,
        ModelImporterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        ArgumentNullException.ThrowIfNull(
            settings);

        using var context =
            new AssimpContext();

        PostProcessSteps steps =
            PostProcessSteps.Triangulate |
            PostProcessSteps.JoinIdenticalVertices |
            PostProcessSteps.SortByPrimitiveType |
            PostProcessSteps.FlipUVs |
            PostProcessSteps.FlipWindingOrder |
            PostProcessSteps.LimitBoneWeights;

        if (settings.GenerateNormals)
        {
            steps |=
                PostProcessSteps.GenerateSmoothNormals;
        }

        Assimp.Scene scene =
            context.ImportFile(
                source.FullPath,
                steps)
            ?? throw new InvalidDataException(
                $"Assimp returned no scene for '{source.ProjectPath}'.");

        if (scene.RootNode ==
            null)
        {
            throw new InvalidDataException(
                $"FBX '{source.ProjectPath}' contains no root node.");
        }

        if (scene.MeshCount ==
            0)
        {
            throw new InvalidDataException(
                $"FBX '{source.ProjectPath}' contains no mesh geometry.");
        }

        List<ImportedMaterial> materials =
            ReadMaterials(
                source,
                scene);

        List<string> materialKeys =
            materials
                .Select(
                    material =>
                        material.Key)
                .ToList();

        SkeletonAsset? skeleton =
            ReadSkeleton(
                source,
                scene);

        Dictionary<string, int> boneIndices =
            skeleton?.Bones
                .Select(
                    (bone, index) =>
                        new
                        {
                            bone.Name,
                            Index =
                                index
                        })
                .GroupBy(
                    item =>
                        item.Name,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group =>
                        group.Key,
                    group =>
                        group.First().Index,
                    StringComparer.Ordinal)
            ?? new Dictionary<string, int>(
                StringComparer.Ordinal);

        List<ImportedMesh> meshes =
            new();

        var meshKeys =
            new Dictionary<int, string>();

        for (int meshIndex =
                 0;
             meshIndex <
             scene.MeshCount;
             meshIndex++)
        {
            Assimp.Mesh mesh =
                scene.Meshes[meshIndex];

            string meshKey =
                SubAssetKey(
                    source.Guid,
                    "mesh",
                    NameOr(
                        mesh.Name,
                        "Mesh",
                        meshIndex),
                    meshIndex);

            meshKeys[meshIndex] =
                meshKey;

            meshes.Add(
                ReadMesh(
                    mesh,
                    meshIndex,
                    meshKey,
                    materialKeys,
                    boneIndices));
        }

        List<ImportedNode> nodes =
            new();

        int nodeIndex =
            0;

        ReadNodeRecursive(
            scene.RootNode,
            null,
            meshKeys,
            nodes,
            ref nodeIndex);

        List<ImportedAnimation> animations =
            ReadAnimations(
                source,
                scene);

        return new ImportedModel
        {
            Guid =
                source.Guid,

            SourceAssetGuid =
                source.Guid,

            Name =
                Path.GetFileNameWithoutExtension(
                    source.ProjectPath),

            Nodes =
                nodes,

            Meshes =
                meshes,

            Materials =
                materials,

            Skeleton =
                skeleton,

            Animations =
                animations
        };
    }

    private static List<ImportedMaterial> ReadMaterials(
        AssetRecord source,
        Assimp.Scene scene)
    {
        var result =
            new List<ImportedMaterial>();

        for (int index =
                 0;
             index <
             scene.MaterialCount;
             index++)
        {
            Assimp.Material material =
                scene.Materials[index];

            string name =
                NameOr(
                    material.Name,
                    "Material",
                    index);

            var baseColor =
                NumericsVector4.One;

            if (material.HasColorDiffuse)
            {
                NumericsVector4 color =
                    material.ColorDiffuse;

                baseColor =
                    color;

                // Some FBX exporters leave diffuse alpha at zero even when
                // the material is intended to be fully opaque. ByteEngine's
                // preview renderer treats that literally, so normalize it.
                if (!float.IsFinite(baseColor.W) ||
                    baseColor.W <= 0.001f)
                {
                    baseColor.W =
                        1.0f;
                }
            }

            ImportedTexture? baseColorTexture =
                material.HasTextureDiffuse
                    ? ReadExternalTexture(
                        source,
                        material.TextureDiffuse,
                        $"material:{index}:diffuse")
                    : null;

            ImportedTexture? normalTexture =
                material.HasTextureNormal
                    ? ReadExternalTexture(
                        source,
                        material.TextureNormal,
                        $"material:{index}:normal")
                    : null;

            result.Add(
                new ImportedMaterial
                {
                    Key =
                        SubAssetKey(
                            source.Guid,
                            "material",
                            name,
                            index),

                    Name =
                        name,

                    BaseColor =
                        baseColor,

                    Metallic =
                        0.0f,

                    Roughness =
                        1.0f,

                    BaseColorTexture =
                        baseColorTexture,

                    NormalTexture =
                        normalTexture
                });
        }

        if (result.Count ==
            0)
        {
            result.Add(
                new ImportedMaterial
                {
                    Key =
                        SubAssetKey(
                            source.Guid,
                            "material",
                            "Default",
                            0),

                    Name =
                        "Default"
                });
        }

        return result;
    }

    private static ImportedTexture? ReadExternalTexture(
        AssetRecord source,
        TextureSlot slot,
        string keySuffix)
    {
        string rawPath =
            slot.FilePath ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                rawPath) ||
            rawPath.StartsWith(
                "*",
                StringComparison.Ordinal))
        {
            /*
             * Embedded FBX textures are intentionally left for the next
             * material-pipeline pass. Geometry/material color still imports.
             */
            return null;
        }

        string normalized =
            rawPath.Replace(
                '\\',
                Path.DirectorySeparatorChar)
            .Replace(
                '/',
                Path.DirectorySeparatorChar);

        string fullPath =
            Path.IsPathRooted(
                normalized)
                ? normalized
                : Path.Combine(
                    Path.GetDirectoryName(
                        source.FullPath)
                    ?? string.Empty,
                    normalized);

        fullPath =
            Path.GetFullPath(
                fullPath);

        if (!File.Exists(
                fullPath))
        {
            return null;
        }

        return new ImportedTexture
        {
            Key =
                $"{source.Guid:N}:{keySuffix}",

            Name =
                Path.GetFileNameWithoutExtension(
                    fullPath),

            SourcePath =
                fullPath,

            EncodedData =
                File.ReadAllBytes(
                    fullPath)
        };
    }

    private static ImportedMesh ReadMesh(
        Assimp.Mesh mesh,
        int meshIndex,
        string key,
        IReadOnlyList<string> materialKeys,
        IReadOnlyDictionary<string, int> boneIndices)
    {
        if (mesh.VertexCount ==
            0)
        {
            throw new InvalidDataException(
                $"FBX mesh '{mesh.Name}' contains no vertices.");
        }

        bool hasNormals =
            mesh.HasNormals;

        bool hasUv =
            mesh.TextureCoordinateChannelCount >
                0 &&
            mesh.HasTextureCoords(
                0);

        var vertices =
            new float[
                mesh.VertexCount *
                8];

        for (int index =
                 0;
             index <
             mesh.VertexCount;
             index++)
        {
            NumericsVector3 sourcePosition =
                mesh.Vertices[index];

            NumericsVector3 sourceNormal =
                hasNormals
                    ? mesh.Normals[index]
                    : NumericsVector3.UnitY;

            NumericsVector3 sourceUv =
                hasUv
                    ? mesh.TextureCoordinateChannels[0][index]
                    : NumericsVector3.Zero;

            int offset =
                index *
                8;

            vertices[offset] =
                sourcePosition.X;

            vertices[offset + 1] =
                sourcePosition.Y;

            vertices[offset + 2] =
                sourcePosition.Z;

            vertices[offset + 3] =
                sourceNormal.X;

            vertices[offset + 4] =
                sourceNormal.Y;

            vertices[offset + 5] =
                sourceNormal.Z;

            vertices[offset + 6] =
                sourceUv.X;

            vertices[offset + 7] =
                sourceUv.Y;
        }

        var indices =
            new List<uint>();

        foreach (Face face
                 in mesh.Faces)
        {
            if (face.IndexCount !=
                3)
            {
                continue;
            }

            indices.Add(
                (uint)face.Indices[0]);

            indices.Add(
                (uint)face.Indices[1]);

            indices.Add(
                (uint)face.Indices[2]);
        }

        if (indices.Count ==
            0)
        {
            throw new InvalidDataException(
                $"FBX mesh '{mesh.Name}' contains no triangles.");
        }

        (
            NumericsVector4[] joints,
            NumericsVector4[] weights
        ) =
            ReadBoneWeights(
                mesh,
                boneIndices);

        string? materialKey =
            mesh.MaterialIndex >=
                0 &&
            mesh.MaterialIndex <
                materialKeys.Count
                ? materialKeys[
                    mesh.MaterialIndex]
                : null;

        return new ImportedMesh
        {
            Key =
                key,

            Name =
                NameOr(
                    mesh.Name,
                    "Mesh",
                    meshIndex),

            Vertices =
                vertices,

            Indices =
                indices.ToArray(),

            MaterialKey =
                materialKey,

            JointIndices =
                joints,

            JointWeights =
                weights
        };
    }

    private static (
        NumericsVector4[] JointIndices,
        NumericsVector4[] JointWeights
    ) ReadBoneWeights(
        Assimp.Mesh mesh,
        IReadOnlyDictionary<string, int> boneIndices)
    {
        if (!mesh.HasBones ||
            boneIndices.Count ==
                0)
        {
            return (
                Array.Empty<NumericsVector4>(),
                Array.Empty<NumericsVector4>()
            );
        }

        var influences =
            Enumerable
                .Range(
                    0,
                    mesh.VertexCount)
                .Select(
                    _ =>
                        new List<(
                            int Bone,
                            float Weight
                        )>())
                .ToArray();

        foreach (Assimp.Bone bone
                 in mesh.Bones)
        {
            if (!boneIndices.TryGetValue(
                    bone.Name,
                    out int boneIndex))
            {
                continue;
            }

            foreach (VertexWeight vertexWeight
                     in bone.VertexWeights)
            {
                if (vertexWeight.VertexID <
                        0 ||
                    vertexWeight.VertexID >=
                        influences.Length ||
                    vertexWeight.Weight <=
                        0.0f)
                {
                    continue;
                }

                influences[
                    vertexWeight.VertexID]
                    .Add(
                        (
                            boneIndex,
                            vertexWeight.Weight
                        ));
            }
        }

        var joints =
            new NumericsVector4[
                mesh.VertexCount];

        var weights =
            new NumericsVector4[
                mesh.VertexCount];

        for (int vertexIndex =
                 0;
             vertexIndex <
             influences.Length;
             vertexIndex++)
        {
            (
                int Bone,
                float Weight
            )[] strongest =
                influences[vertexIndex]
                    .OrderByDescending(
                        influence =>
                            influence.Weight)
                    .Take(
                        4)
                    .ToArray();

            if (strongest.Length ==
                0)
            {
                continue;
            }

            float total =
                strongest.Sum(
                    influence =>
                        influence.Weight);

            if (total <=
                0.000001f)
            {
                continue;
            }

            float[] jointValues =
                new float[4];

            float[] weightValues =
                new float[4];

            for (int slot =
                     0;
                 slot <
                 strongest.Length;
                 slot++)
            {
                jointValues[slot] =
                    strongest[slot].Bone;

                weightValues[slot] =
                    strongest[slot].Weight /
                    total;
            }

            joints[vertexIndex] =
                new NumericsVector4(
                    jointValues[0],
                    jointValues[1],
                    jointValues[2],
                    jointValues[3]);

            weights[vertexIndex] =
                new NumericsVector4(
                    weightValues[0],
                    weightValues[1],
                    weightValues[2],
                    weightValues[3]);
        }

        return (
            joints,
            weights
        );
    }

    private static void ReadNodeRecursive(
        Node node,
        string? parentKey,
        IReadOnlyDictionary<int, string> meshKeys,
        ICollection<ImportedNode> output,
        ref int nodeIndex)
    {
        int currentIndex =
            nodeIndex++;

        string key =
            $"node:{currentIndex}:{NameOr(node.Name, "Node", currentIndex)}";

        var nodeMeshKeys =
            new List<string>();

        foreach (int meshIndex
                 in node.MeshIndices)
        {
            if (meshKeys.TryGetValue(
                    meshIndex,
                    out string? meshKey))
            {
                nodeMeshKeys.Add(
                    meshKey);
            }
        }

        output.Add(
            new ImportedNode
            {
                Key =
                    key,

                Name =
                    NameOr(
                        node.Name,
                        "Node",
                        currentIndex),

                ParentKey =
                    parentKey,

                LocalTransform =
                    ToNumerics(
                        node.Transform),

                MeshKeys =
                    nodeMeshKeys
            });

        foreach (Node child
                 in node.Children)
        {
            ReadNodeRecursive(
                child,
                key,
                meshKeys,
                output,
                ref nodeIndex);
        }
    }

    private static SkeletonAsset? ReadSkeleton(
        AssetRecord source,
        Assimp.Scene scene)
    {
        var offsets =
            new Dictionary<string, NumericsMatrix4x4>(
                StringComparer.Ordinal);

        foreach (Assimp.Mesh mesh
                 in scene.Meshes)
        {
            foreach (Assimp.Bone bone
                     in mesh.Bones)
            {
                offsets.TryAdd(
                    bone.Name,
                    bone.OffsetMatrix);
            }
        }

        if (offsets.Count ==
            0)
        {
            return null;
        }

        var parentNames =
            new Dictionary<string, string?>(
                StringComparer.Ordinal);

        BuildNodeParentMap(
            scene.RootNode,
            null,
            parentNames);

        string[] names =
            offsets.Keys
                .ToArray();

        var indices =
            names
                .Select(
                    (name, index) =>
                        new
                        {
                            name,
                            index
                        })
                .ToDictionary(
                    item =>
                        item.name,
                    item =>
                        item.index,
                    StringComparer.Ordinal);

        var bones =
            new List<Bone>();

        foreach (string name
                 in names)
        {
            int parentIndex =
                FindBoneParent(
                    name,
                    parentNames,
                    indices);

            bones.Add(
                new Bone
                {
                    Name =
                        name,

                    ParentIndex =
                        parentIndex,

                    BindPose =
                        ToNumerics(
                            offsets[name])
                });
        }

        return new SkeletonAsset
        {
            Key =
                SubAssetKey(
                    source.Guid,
                    "skeleton",
                    Path.GetFileNameWithoutExtension(
                        source.ProjectPath),
                    0),

            Name =
                Path.GetFileNameWithoutExtension(
                    source.ProjectPath),

            Bones =
                bones
        };
    }

    private static void BuildNodeParentMap(
        Node node,
        string? parentName,
        IDictionary<string, string?> map)
    {
        if (!string.IsNullOrWhiteSpace(
                node.Name))
        {
            map[node.Name] =
                parentName;

            parentName =
                node.Name;
        }

        foreach (Node child
                 in node.Children)
        {
            BuildNodeParentMap(
                child,
                parentName,
                map);
        }
    }

    private static int FindBoneParent(
        string boneName,
        IReadOnlyDictionary<string, string?> parentNames,
        IReadOnlyDictionary<string, int> boneIndices)
    {
        string? current =
            parentNames.TryGetValue(
                boneName,
                out string? parentName)
                ? parentName
                : null;

        var visited =
            new HashSet<string>(
                StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(
                   current) &&
               visited.Add(
                   current))
        {
            if (boneIndices.TryGetValue(
                    current,
                    out int index))
            {
                return index;
            }

            current =
                parentNames.TryGetValue(
                    current,
                    out string? next)
                    ? next
                    : null;
        }

        return -1;
    }

    private static List<ImportedAnimation> ReadAnimations(
        AssetRecord source,
        Assimp.Scene scene)
    {
        var result =
            new List<ImportedAnimation>();

        for (int index =
                 0;
             index <
             scene.AnimationCount;
             index++)
        {
            Assimp.Animation animation =
                scene.Animations[index];

            string name =
                NameOr(
                    animation.Name,
                    "Animation",
                    index);

            double ticksPerSecond =
                animation.TicksPerSecond;

            float duration =
                ticksPerSecond >
                    0.000001
                    ? (float)(
                        animation.DurationInTicks /
                        ticksPerSecond)
                    : 0.0f;

            result.Add(
                new ImportedAnimation
                {
                    Key =
                        SubAssetKey(
                            source.Guid,
                            "animation",
                            name,
                            index),

                    Name =
                        name,

                    Duration =
                        duration
                });
        }

        return result;
    }

    private static NumericsMatrix4x4 ToNumerics(
        NumericsMatrix4x4 matrix)
    {
        /*
         * AssimpNetter 6.x exposes transforms as System.Numerics.Matrix4x4,
         * but Assimp treats imported matrices as column-vector matrices while
         * ByteEngine/System.Numerics uses row-vector transforms. Transposing
         * converts the imported basis/translation into ByteEngine's convention.
         */
        return NumericsMatrix4x4.Transpose(
            matrix);
    }

    private static string SubAssetKey(
        Guid guid,
        string type,
        string? name,
        int index)
    {
        return
            $"{guid:N}:{type}:{NameOr(name, type, index)}:{index}";
    }

    private static string NameOr(
        string? name,
        string fallback,
        int index)
    {
        return string.IsNullOrWhiteSpace(
                name)
            ? $"{fallback} {index}"
            : name;
    }
}
