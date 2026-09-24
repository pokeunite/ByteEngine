using System.Numerics;
using SharpGLTF.Schema2;

namespace ByteEngine.Core.Assets.Importers;

public sealed class GltfModelImporter : ModelImporter
{
    public override IReadOnlyCollection<string> Extensions { get; } =
        new[] { ".glb", ".gltf" };

    public override ImportedModel Import(
        AssetRecord source,
        ModelImporterSettings settings)
    {
        ModelRoot model = ModelRoot.Load(source.FullPath);

        List<ImportedMaterial> materials =
            model.LogicalMaterials.Select(ReadMaterial).ToList();

        var meshes = new List<ImportedMesh>();
        var meshKeys = new Dictionary<(int Mesh, int Primitive), string>();

        foreach (SharpGLTF.Schema2.Mesh mesh in model.LogicalMeshes)
        {
            for (int primitiveIndex = 0;
                 primitiveIndex < mesh.Primitives.Count;
                 primitiveIndex++)
            {
                MeshPrimitive primitive = mesh.Primitives[primitiveIndex];

                string key = SubAssetKey(
                    source.Guid,
                    "mesh",
                    mesh.Name,
                    mesh.LogicalIndex,
                    primitiveIndex);

                meshKeys[(mesh.LogicalIndex, primitiveIndex)] = key;

                meshes.Add(
                    ReadMesh(
                        mesh,
                        primitive,
                        primitiveIndex,
                        key,
                        settings));
            }
        }

        var nodes = new List<ImportedNode>();

        foreach (Node node in model.LogicalNodes)
        {
            var keys = new List<string>();

            if (node.Mesh != null)
            {
                for (int index = 0;
                     index < node.Mesh.Primitives.Count;
                     index++)
                {
                    keys.Add(meshKeys[(node.Mesh.LogicalIndex, index)]);
                }
            }

            nodes.Add(
                new ImportedNode
                {
                    Key = NodeKey(node),
                    Name = NameOr(node.Name, "Node", node.LogicalIndex),
                    ParentKey =
                        node.VisualParent == null
                            ? null
                            : NodeKey(node.VisualParent),
                    LocalTransform = node.LocalMatrix,
                    MeshKeys = keys
                });
        }

        SkeletonAsset? skeleton =
            model.LogicalSkins.Count > 0
                ? ReadSkeleton(source.Guid, model.LogicalSkins[0])
                : null;

        List<ImportedAnimation> animations =
            model.LogicalAnimations
                .Select(animation => ReadAnimation(source.Guid, animation))
                .ToList();

        return ImportedModelSpace.Apply(new ImportedModel
            {
                Guid = source.Guid,
                SourceAssetGuid = source.Guid,
                Name = Path.GetFileNameWithoutExtension(source.ProjectPath),
                Nodes = nodes,
                Meshes = meshes,
                Materials = materials,
                Skeleton = skeleton,
                Animations = animations
            }, ImportedModelSpace.GltfCorrection(settings.ImportScale));
    }

    private static ImportedMesh ReadMesh(
        SharpGLTF.Schema2.Mesh mesh,
        MeshPrimitive primitive,
        int primitiveIndex,
        string key,
        ModelImporterSettings settings)
    {
        IReadOnlyList<Vector3> positions =
            primitive.GetVertexAccessor("POSITION")?.AsVector3Array()
            ?? throw new InvalidDataException(
                $"Mesh '{mesh.Name}' has no POSITION data.");

        IReadOnlyList<Vector3>? normals =
            primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();

        IReadOnlyList<Vector2>? textureCoordinates =
            primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();

        uint[] indices =
            primitive.GetIndices()?.ToArray()
            ?? Enumerable
                .Range(0, positions.Count)
                .Select(index => (uint)index)
                .ToArray();

        Vector3[] generatedNormals =
            normals == null && settings.GenerateNormals
                ? GenerateNormals(positions, indices)
                : Array.Empty<Vector3>();

        var vertices = new float[positions.Count * 8];

        for (int index = 0; index < positions.Count; index++)
        {
            Vector3 position = positions[index];

            Vector3 normal =
                normals != null && index < normals.Count
                    ? normals[index]
                    : generatedNormals.Length > index
                        ? generatedNormals[index]
                        : Vector3.UnitY;

            Vector2 uv =
                textureCoordinates != null && index < textureCoordinates.Count
                    ? textureCoordinates[index]
                    : Vector2.Zero;

            int offset = index * 8;

            vertices[offset] = position.X;
            vertices[offset + 1] = position.Y;
            vertices[offset + 2] = position.Z;
            vertices[offset + 3] = normal.X;
            vertices[offset + 4] = normal.Y;
            vertices[offset + 5] = normal.Z;
            vertices[offset + 6] = uv.X;
            vertices[offset + 7] = uv.Y;
        }

        Accessor? joints = primitive.GetVertexAccessor("JOINTS_0");
        Accessor? weights = primitive.GetVertexAccessor("WEIGHTS_0");

        return
            new ImportedMesh
            {
                Key = key,
                Name =
                    $"{NameOr(mesh.Name, "Mesh", mesh.LogicalIndex)} / {primitiveIndex}",
                Vertices = vertices,
                Indices = indices,
                MaterialKey =
                    primitive.Material == null
                        ? null
                        : MaterialKey(primitive.Material),
                JointIndices =
                    joints?.AsVector4Array().ToArray()
                    ?? Array.Empty<Vector4>(),
                JointWeights =
                    weights?.AsVector4Array().ToArray()
                    ?? Array.Empty<Vector4>()
            };
    }

    private static ImportedMaterial ReadMaterial(
        SharpGLTF.Schema2.Material material)
    {
        MaterialChannel? baseChannel = material.FindChannel("BaseColor");
        MaterialChannel? metalChannel = material.FindChannel("MetallicRoughness");
        MaterialChannel? normalChannel = material.FindChannel("Normal");

        return
            new ImportedMaterial
            {
                Key = MaterialKey(material),
                Name = NameOr(material.Name, "Material", material.LogicalIndex),
                BaseColor = baseChannel?.Color ?? Vector4.One,
                Metallic =
                    metalChannel.HasValue
                        ? metalChannel.Value.GetFactor("MetallicFactor")
                        : 0.0f,
                Roughness =
                    metalChannel.HasValue
                        ? metalChannel.Value.GetFactor("RoughnessFactor")
                        : 1.0f,
                BaseColorTexture =
                    ReadTexture(baseChannel?.Texture, "baseColor"),
                NormalTexture =
                    ReadTexture(normalChannel?.Texture, "normal")
            };
    }

    private static ImportedTexture? ReadTexture(
        Texture? texture,
        string usage)
    {
        if (texture?.PrimaryImage == null ||
            texture.PrimaryImage.Content.IsEmpty)
        {
            return null;
        }

        return
            new ImportedTexture
            {
                Key = $"image:{texture.PrimaryImage.LogicalIndex}:{usage}",
                Name =
                    NameOr(
                        texture.Name ?? texture.PrimaryImage.Name,
                        "Texture",
                        texture.LogicalIndex),
                EncodedData =
                    texture.PrimaryImage.Content.Content.ToArray(),
                SourcePath =
                    texture.PrimaryImage.Content.SourcePath
            };
    }

    private static SkeletonAsset ReadSkeleton(
        Guid guid,
        Skin skin)
    {
        IReadOnlyList<Node> joints = skin.Joints;
        IReadOnlyList<Matrix4x4> inverseBindMatrices =
            skin.InverseBindMatrices;

        Dictionary<Node, int> indices =
            joints
                .Select((node, index) => (node, index))
                .ToDictionary(pair => pair.node, pair => pair.index);

        var bones = new List<Bone>();

        for (int index = 0; index < joints.Count; index++)
        {
            Node joint = joints[index];

            int parent =
                joint.VisualParent != null &&
                indices.TryGetValue(joint.VisualParent, out int parentIndex)
                    ? parentIndex
                    : -1;

            bones.Add(
                new Bone
                {
                    Name = NameOr(joint.Name, "Bone", index),
                    ParentIndex = parent,
                    BindPose =
                        index < inverseBindMatrices.Count
                            ? inverseBindMatrices[index]
                            : Matrix4x4.Identity
                });
        }

        return
            new SkeletonAsset
            {
                Key =
                    SubAssetKey(
                        guid,
                        "skeleton",
                        skin.Name,
                        skin.LogicalIndex),
                Name =
                    NameOr(
                        skin.Name,
                        "Skeleton",
                        skin.LogicalIndex),
                Bones = bones
            };
    }

    private static ImportedAnimation ReadAnimation(
        Guid guid,
        SharpGLTF.Schema2.Animation animation)
    {
        var channels =
            new Dictionary<int, MutableAnimationChannel>();

        foreach (AnimationChannel sourceChannel in animation.Channels)
        {
            Node? node = sourceChannel.TargetNode;

            if (node == null)
            {
                continue;
            }

            if (!channels.TryGetValue(
                    node.LogicalIndex,
                    out MutableAnimationChannel? destination))
            {
                destination =
                    new MutableAnimationChannel(
                        NameOr(
                            node.Name,
                            "Node",
                            node.LogicalIndex));

                channels[node.LogicalIndex] = destination;
            }

            switch (sourceChannel.TargetNodePath)
            {
                case PropertyPath.translation:
                    destination.Translation =
                        ReadVectorTrack(
                            sourceChannel.GetTranslationSampler());
                    break;

                case PropertyPath.rotation:
                    destination.Rotation =
                        ReadQuaternionTrack(
                            sourceChannel.GetRotationSampler());
                    break;

                case PropertyPath.scale:
                    destination.Scale =
                        ReadVectorTrack(
                            sourceChannel.GetScaleSampler());
                    break;
            }
        }

        return
            new ImportedAnimation
            {
                Key =
                    SubAssetKey(
                        guid,
                        "animation",
                        animation.Name,
                        animation.LogicalIndex),
                Name =
                    NameOr(
                        animation.Name,
                        "Animation",
                        animation.LogicalIndex),
                Duration = animation.Duration,
                Channels =
                    channels.Values
                        .Select(channel => channel.ToImported())
                        .Where(channel => channel.HasKeys)
                        .ToList()
            };
    }

    private static ImportedVectorTrack? ReadVectorTrack(
        IAnimationSampler<Vector3>? sampler)
    {
        if (sampler == null)
        {
            return null;
        }

        var track =
            new ImportedVectorTrack
            {
                Interpolation =
                    MapInterpolation(sampler.InterpolationMode)
            };

        if (sampler.InterpolationMode ==
            AnimationInterpolationMode.CUBICSPLINE)
        {
            foreach (var key in sampler.GetCubicKeys())
            {
                track.Keys.Add(
                    new ImportedVectorKey(
                        key.Key,
                        key.Value.Value,
                        key.Value.TangentIn,
                        key.Value.TangentOut));
            }
        }
        else
        {
            foreach (var key in sampler.GetLinearKeys())
            {
                track.Keys.Add(
                    new ImportedVectorKey(
                        key.Key,
                        key.Value,
                        Vector3.Zero,
                        Vector3.Zero));
            }
        }

        return track.Keys.Count > 0 ? track : null;
    }

    private static ImportedQuaternionTrack? ReadQuaternionTrack(
        IAnimationSampler<Quaternion>? sampler)
    {
        if (sampler == null)
        {
            return null;
        }

        var track =
            new ImportedQuaternionTrack
            {
                Interpolation =
                    MapInterpolation(sampler.InterpolationMode)
            };

        if (sampler.InterpolationMode ==
            AnimationInterpolationMode.CUBICSPLINE)
        {
            foreach (var key in sampler.GetCubicKeys())
            {
                track.Keys.Add(
                    new ImportedQuaternionKey(
                        key.Key,
                        NormalizeSafe(key.Value.Value),
                        key.Value.TangentIn,
                        key.Value.TangentOut));
            }
        }
        else
        {
            foreach (var key in sampler.GetLinearKeys())
            {
                track.Keys.Add(
                    new ImportedQuaternionKey(
                        key.Key,
                        NormalizeSafe(key.Value),
                        default,
                        default));
            }
        }

        return track.Keys.Count > 0 ? track : null;
    }

    private static ImportedAnimationInterpolation MapInterpolation(
        AnimationInterpolationMode mode)
    {
        return mode switch
        {
            AnimationInterpolationMode.STEP =>
                ImportedAnimationInterpolation.Step,
            AnimationInterpolationMode.CUBICSPLINE =>
                ImportedAnimationInterpolation.CubicSpline,
            _ =>
                ImportedAnimationInterpolation.Linear
        };
    }

    private static Quaternion NormalizeSafe(Quaternion value) =>
        value.LengthSquared() > 0.000001f
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;

    private static Vector3[] GenerateNormals(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<uint> indices)
    {
        var normals = new Vector3[positions.Count];

        for (int index = 0; index + 2 < indices.Count; index += 3)
        {
            int a = (int)indices[index];
            int b = (int)indices[index + 1];
            int c = (int)indices[index + 2];

            Vector3 normal =
                Vector3.Cross(
                    positions[b] - positions[a],
                    positions[c] - positions[a]);

            if (normal.LengthSquared() <= 0.000001f)
            {
                continue;
            }

            normals[a] += normal;
            normals[b] += normal;
            normals[c] += normal;
        }

        for (int index = 0; index < normals.Length; index++)
        {
            normals[index] =
                normals[index].LengthSquared() > 0.000001f
                    ? Vector3.Normalize(normals[index])
                    : Vector3.UnitY;
        }

        return normals;
    }

    private static string NodeKey(Node node) =>
        $"node:{NameOr(node.Name, "Node", node.LogicalIndex)}:{node.LogicalIndex}";

    private static string MaterialKey(
        SharpGLTF.Schema2.Material material) =>
        $"material:{NameOr(material.Name, "Material", material.LogicalIndex)}:{material.LogicalIndex}";

    private static string SubAssetKey(
        Guid guid,
        string type,
        string? name,
        int index,
        int child = -1) =>
        $"{guid:N}:{type}:{NameOr(name, type, index)}:{index}" +
        (child >= 0 ? $":{child}" : string.Empty);

    private static string NameOr(
        string? name,
        string fallback,
        int index) =>
        string.IsNullOrWhiteSpace(name)
            ? $"{fallback} {index}"
            : name;

    private sealed class MutableAnimationChannel
    {
        public string NodeName { get; }

        public ImportedVectorTrack? Translation { get; set; }
        public ImportedQuaternionTrack? Rotation { get; set; }
        public ImportedVectorTrack? Scale { get; set; }

        public MutableAnimationChannel(string nodeName)
        {
            NodeName = nodeName;
        }

        public ImportedAnimationChannel ToImported() =>
            new()
            {
                NodeName = NodeName,
                Translation = Translation,
                Rotation = Rotation,
                Scale = Scale
            };
    }
}
