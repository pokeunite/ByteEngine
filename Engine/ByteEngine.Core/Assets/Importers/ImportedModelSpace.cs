using System.Numerics;
using Assimp;

namespace ByteEngine.Core.Assets.Importers;

/// <summary>
/// One immutable source-to-ByteEngine correction above every imported node.
/// It is never animated and is shared by mesh, skeleton, clips and sockets.
/// </summary>
internal static class ImportedModelSpace
{
    internal const string CorrectionNodeKey = "__byteengine_import_space__";

    public static ImportedModel Apply(ImportedModel source, Matrix4x4 correction)
    {
        if (correction == Matrix4x4.Identity)
            return source;

        var nodes = new List<ImportedNode>(source.Nodes.Count + 1)
        {
            new() { Key = CorrectionNodeKey, Name = "Import Space", LocalTransform = correction }
        };
        foreach (ImportedNode node in source.Nodes)
        {
            nodes.Add(node.ParentKey != null ? node : new ImportedNode
            {
                Key = node.Key,
                Name = node.Name,
                ParentKey = CorrectionNodeKey,
                LocalTransform = node.LocalTransform,
                MeshKeys = node.MeshKeys
            });
        }
        return new ImportedModel
        {
            Guid = source.Guid,
            SourceAssetGuid = source.SourceAssetGuid,
            Name = source.Name,
            Nodes = nodes,
            Meshes = source.Meshes,
            Materials = source.Materials,
            Skeleton = source.Skeleton,
            Animations = source.Animations
        };
    }

    public static Matrix4x4 GltfCorrection(float userScale) =>
        Matrix4x4.CreateScale(PositiveScale(userScale)) *
        Matrix4x4.CreateRotationY(MathF.PI);

    public static Matrix4x4 FbxCorrection(Assimp.Scene scene, float userScale)
    {
        float sourceUnitMetres = 1.0f;
        if (TryNumber(scene.Metadata, "UnitScaleFactor", out double centimetresPerUnit) &&
            centimetresPerUnit > 0 && double.IsFinite(centimetresPerUnit))
            sourceUnitMetres = (float)(centimetresPerUnit / 100.0);

        Matrix4x4 axes = Matrix4x4.Identity;
        if (TryAxis(scene.Metadata, "UpAxis", "UpAxisSign", out Vector3 up) &&
            TryAxis(scene.Metadata, "FrontAxis", "FrontAxisSign", out Vector3 forward) &&
            MathF.Abs(Vector3.Dot(up, forward)) < 0.001f)
        {
            Vector3 right = Vector3.Normalize(Vector3.Cross(forward, up));
            Vector3 back = -forward;
            Matrix4x4 sourceBasis = new(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                back.X, back.Y, back.Z, 0,
                0, 0, 0, 1);
            axes = Matrix4x4.Transpose(sourceBasis);
        }

        return Matrix4x4.CreateScale(sourceUnitMetres * PositiveScale(userScale)) * axes;
    }

    private static bool TryAxis(Metadata? metadata, string axisKey, string signKey, out Vector3 axis)
    {
        axis = Vector3.Zero;
        if (!TryNumber(metadata, axisKey, out double component) ||
            !TryNumber(metadata, signKey, out double sign) ||
            component < 0 || component > 2 || (sign != 1 && sign != -1))
            return false;

        axis[(int)component] = (float)sign;
        return true;
    }

    private static bool TryNumber(Metadata? metadata, string key, out double value)
    {
        value = 0;
        if (metadata == null || !metadata.TryGetValue(key, out Metadata.Entry entry) || entry.Data == null)
            return false;
        try { value = Convert.ToDouble(entry.Data); }
        catch { return false; }
        return double.IsFinite(value);
    }

    private static float PositiveScale(float value) =>
        float.IsFinite(value) && value > 0 ? value : 1.0f;
}
