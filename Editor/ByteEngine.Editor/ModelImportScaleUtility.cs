using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Editor;

internal static class ModelImportScaleUtility
{
    public static float Resolve(
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

        string extension =
            Path.GetExtension(
                    asset.FullPath)
                .ToLowerInvariant();

        if (extension !=
            ".fbx")
        {
            return requestedScale;
        }

        float largestUnscaledDimension =
            GetLargestMeshDimension(
                model);

        bool looksAlreadyNormalized =
            largestUnscaledDimension >=
                0.25f &&
            largestUnscaledDimension <=
                20.0f;

        bool suspiciousTinyScale =
            requestedScale >=
                0.005f &&
            requestedScale <=
                0.05f;

        if (looksAlreadyNormalized &&
            suspiciousTinyScale)
        {
            return 1.0f;
        }

        return requestedScale;
    }

    private static float GetLargestMeshDimension(
        ModelAsset model)
    {
        float largest =
            0.0f;

        foreach (ImportedMesh mesh
                 in model.Meshes)
        {
            if (mesh.Vertices.Length <
                8)
            {
                continue;
            }

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

                if (!float.IsFinite(
                        position.X) ||
                    !float.IsFinite(
                        position.Y) ||
                    !float.IsFinite(
                        position.Z))
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

            if (!found)
            {
                continue;
            }

            Vector3 size =
                maximum -
                minimum;

            largest =
                Math.Max(
                    largest,
                    Math.Max(
                        Math.Abs(
                            size.X),
                        Math.Max(
                            Math.Abs(
                                size.Y),
                            Math.Abs(
                                size.Z))));
        }

        return largest;
    }
}
