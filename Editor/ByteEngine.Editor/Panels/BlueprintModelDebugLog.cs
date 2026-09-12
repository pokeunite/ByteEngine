using System.Numerics;
using System.Text;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Writes a plain-text diagnostic snapshot for Blueprint model preview problems.
/// The file is intentionally human-readable so it can be attached directly to
/// a bug report or pasted into a support conversation.
/// </summary>
internal static class BlueprintModelDebugLog
{
    public static string Write(
        EditorProjectContext project,
        Scene preview,
        BlueprintDefinition? blueprint,
        AssetRecord? blueprintAsset,
        EditorCamera3D camera,
        Vector3? previewBoundsMin,
        Vector3? previewBoundsMax,
        string reason,
        ModelAsset? importedModel = null,
        AssetRecord? importedAsset = null)
    {
        ArgumentNullException.ThrowIfNull(
            project);

        ArgumentNullException.ThrowIfNull(
            preview);

        string logDirectory =
            Path.Combine(
                project.ProjectRoot,
                "Logs");

        Directory.CreateDirectory(
            logDirectory);

        string path =
            Path.Combine(
                logDirectory,
                "BlueprintModelDebug.txt");

        var output =
            new StringBuilder();

        Line(
            output,
            "============================================================");

        Line(
            output,
            "BYTEENGINE BLUEPRINT MODEL DEBUG DUMP");

        Line(
            output,
            "============================================================");

        Key(
            output,
            "Generated",
            DateTime.Now.ToString(
                "yyyy-MM-dd HH:mm:ss.fff"));

        Key(
            output,
            "Reason",
            reason);

        Key(
            output,
            "Project Root",
            project.ProjectRoot);

        Key(
            output,
            "Blueprint",
            blueprint?.Name ??
            "(null)");

        Key(
            output,
            "Blueprint Asset",
            blueprintAsset?.ProjectPath ??
            "(null)");

        Key(
            output,
            "Preview Scene",
            preview.Name);

        Line(
            output,
            string.Empty);

        Section(
            output,
            "CAMERA");

        Key(
            output,
            "Position",
            Format(
                camera.Position));

        Key(
            output,
            "Yaw",
            camera.Yaw.ToString(
                "0.###"));

        Key(
            output,
            "Pitch",
            camera.Pitch.ToString(
                "0.###"));

        Key(
            output,
            "Forward",
            Format(
                camera.Forward));

        if (previewBoundsMin.HasValue &&
            previewBoundsMax.HasValue)
        {
            Vector3 minimum =
                previewBoundsMin.Value;

            Vector3 maximum =
                previewBoundsMax.Value;

            Key(
                output,
                "Preview Bounds Min",
                Format(
                    minimum));

            Key(
                output,
                "Preview Bounds Max",
                Format(
                    maximum));

            Key(
                output,
                "Preview Bounds Size",
                Format(
                    maximum -
                    minimum));

            Key(
                output,
                "Preview Bounds Center",
                Format(
                    (
                        minimum +
                        maximum
                    ) *
                    0.5f));
        }
        else
        {
            Key(
                output,
                "Preview Bounds",
                "NONE");

            Warn(
                output,
                "No valid preview bounds were found from mesh vertices.");
        }

        Line(
            output,
            string.Empty);

        Section(
            output,
            "PREVIEW SCENE SUMMARY");

        GameObject[] objects =
            preview.GameObjects
                .ToArray();

        MeshRenderer[] renderers =
            objects
                .SelectMany(
                    gameObject =>
                        gameObject.Components
                            .OfType<MeshRenderer>())
                .ToArray();

        Key(
            output,
            "GameObjects",
            objects.Length.ToString());

        Key(
            output,
            "MeshRenderers",
            renderers.Length.ToString());

        Key(
            output,
            "Enabled + Visible MeshRenderers",
            renderers.Count(
                    renderer =>
                        renderer.Enabled &&
                        renderer.Visible)
                .ToString());

        if (renderers.Length ==
            0)
        {
            Warn(
                output,
                "The preview contains ZERO MeshRenderer components. The hierarchy may have imported, but no renderable mesh was attached.");
        }

        Line(
            output,
            string.Empty);

        Section(
            output,
            "GAMEOBJECT / RENDERER STATE");

        int rendererNumber =
            0;

        foreach (GameObject gameObject
                 in objects)
        {
            MeshRenderer[] gameObjectRenderers =
                gameObject.Components
                    .OfType<MeshRenderer>()
                    .ToArray();

            if (gameObjectRenderers.Length ==
                0)
            {
                continue;
            }

            foreach (MeshRenderer renderer
                     in gameObjectRenderers)
            {
                rendererNumber++;

                Line(
                    output,
                    $"Renderer #{rendererNumber}");

                Key(
                    output,
                    "  GameObject",
                    gameObject.Name);

                Key(
                    output,
                    "  Object Id",
                    gameObject.Id.ToString());

                Key(
                    output,
                    "  Active",
                    gameObject.Active.ToString());

                Key(
                    output,
                    "  ActiveInHierarchy",
                    gameObject.ActiveInHierarchy.ToString());

                Key(
                    output,
                    "  Component Enabled",
                    renderer.Enabled.ToString());

                Key(
                    output,
                    "  Visible",
                    renderer.Visible.ToString());

                Key(
                    output,
                    "  Mesh Object Is Null",
                    (
                        renderer.Mesh ==
                        null
                    ).ToString());

                Key(
                    output,
                    "  Mesh IndexCount",
                    renderer.Mesh?.IndexCount
                        .ToString() ??
                    "(mesh null)");

                Key(
                    output,
                    "  Mesh Reference",
                    renderer.MeshReference ==
                        null
                        ? "(null)"
                        : $"{renderer.MeshReference.Model.Guid} :: {renderer.MeshReference.SubAssetKey}");

                Key(
                    output,
                    "  Mesh Asset Path",
                    renderer.MeshReference?
                        .Model
                        .CachedProjectPath ??
                    "(null)");

                Key(
                    output,
                    "  Material Reference",
                    renderer.MaterialReference ==
                        null
                        ? "(null)"
                        : $"{renderer.MaterialReference.Model.Guid} :: {renderer.MaterialReference.SubAssetKey}");

                Key(
                    output,
                    "  Material BaseColor",
                    Format(
                        renderer.Material.BaseColor));

                Key(
                    output,
                    "  Local Position",
                    Format(
                        gameObject.Transform.LocalPosition));

                Key(
                    output,
                    "  Local Scale",
                    Format(
                        gameObject.Transform.LocalScale));

                Key(
                    output,
                    "  World Position",
                    Format(
                        gameObject.Transform.WorldPosition));

                Key(
                    output,
                    "  World Matrix",
                    Format(
                        gameObject.Transform.WorldMatrix));

                if (!gameObject.ActiveInHierarchy)
                {
                    Warn(
                        output,
                        $"Renderer #{rendererNumber} is under an inactive GameObject.");
                }

                if (!renderer.Enabled)
                {
                    Warn(
                        output,
                        $"Renderer #{rendererNumber} component is disabled.");
                }

                if (!renderer.Visible)
                {
                    Warn(
                        output,
                        $"Renderer #{rendererNumber} has Visible = false.");
                }

                if (renderer.Mesh ==
                    null)
                {
                    Warn(
                        output,
                        $"Renderer #{rendererNumber} has no GPU Mesh assigned.");
                }

                if (renderer.Mesh?.IndexCount <=
                    0)
                {
                    Warn(
                        output,
                        $"Renderer #{rendererNumber} has zero indices.");
                }

                if (renderer.Material.BaseColor.W <=
                    0.001f)
                {
                    Warn(
                        output,
                        $"Renderer #{rendererNumber} material alpha is effectively zero.");
                }

                Line(
                    output,
                    string.Empty);
            }
        }

        if (importedAsset !=
                null ||
            importedModel !=
                null)
        {
            Section(
                output,
                "JUST-IMPORTED MODEL");

            Key(
                output,
                "Asset",
                importedAsset?.ProjectPath ??
                "(not supplied)");

            Key(
                output,
                "Full Path",
                importedAsset?.FullPath ??
                "(not supplied)");

            Key(
                output,
                "Import Scale",
                importedAsset?
                    .Metadata
                    .ModelImporter
                    .ImportScale
                    .ToString(
                        "0.########") ??
                "(not supplied)");

            if (importedModel !=
                null)
            {
                DumpImportedModel(
                    output,
                    importedModel);
            }
        }

        Line(
            output,
            string.Empty);

        Section(
            output,
            "REFERENCE RESOLUTION CHECK");

        foreach (MeshRenderer renderer
                 in renderers)
        {
            ModelMeshReference? reference =
                renderer.MeshReference;

            if (reference ==
                null)
            {
                Warn(
                    output,
                    "A MeshRenderer has no ModelMeshReference.");

                continue;
            }

            try
            {
                ModelAsset resolved =
                    project.Assets.LoadModel(
                        reference.Model);

                ImportedMesh? importedMesh =
                    resolved.Meshes
                        .FirstOrDefault(
                            mesh =>
                                mesh.Key ==
                                reference.SubAssetKey);

                Key(
                    output,
                    "Resolved Model",
                    resolved.Name);

                Key(
                    output,
                    "Resolved SubAsset",
                    importedMesh?.Name ??
                    "NOT FOUND");

                if (importedMesh ==
                    null)
                {
                    Warn(
                        output,
                        $"Mesh subasset key '{reference.SubAssetKey}' did not resolve.");
                }
                else
                {
                    Key(
                        output,
                        "Resolved Vertex Count",
                        (
                            importedMesh.Vertices.Length /
                            8
                        ).ToString());

                    Key(
                        output,
                        "Resolved Index Count",
                        importedMesh.Indices.Length.ToString());

                    DumpMeshBounds(
                        output,
                        importedMesh,
                        "Resolved Local Bounds");
                }
            }
            catch (Exception exception)
            {
                Warn(
                    output,
                    $"Mesh reference resolution threw: {exception}");
            }

            Line(
                output,
                string.Empty);
        }

        Section(
            output,
            "AUTOMATIC DIAGNOSIS");

        if (renderers.Length ==
            0)
        {
            Diagnosis(
                output,
                "FAIL: hierarchy imported but no MeshRenderer components exist. Focus on FBX mesh-to-node assignment.");
        }
        else if (renderers.All(
                     renderer =>
                         renderer.Mesh ==
                         null))
        {
            Diagnosis(
                output,
                "FAIL: MeshRenderers exist but every renderer has Mesh = null. Focus on AssetManager.GetModelMesh / GPU mesh creation.");
        }
        else if (renderers.All(
                     renderer =>
                         renderer.Mesh == null ||
                         renderer.Mesh.IndexCount <=
                         0))
        {
            Diagnosis(
                output,
                "FAIL: renderers have no drawable triangle indices.");
        }
        else if (!previewBoundsMin.HasValue ||
                 !previewBoundsMax.HasValue)
        {
            Diagnosis(
                output,
                "FAIL: drawable renderers exist, but no finite mesh bounds were produced. Focus on imported vertex values and transforms.");
        }
        else if (renderers.All(
                     renderer =>
                         !renderer.Enabled ||
                         !renderer.Visible))
        {
            Diagnosis(
                output,
                "FAIL: all MeshRenderer components are disabled or invisible.");
        }
        else
        {
            Diagnosis(
                output,
                "No obvious CPU-side failure found. If the viewport is still empty, investigate GPU draw state, projection/view matrices, shader inputs, winding/depth state, or FBX vertex-space conversion.");
        }

        Line(
            output,
            string.Empty);

        Line(
            output,
            "END OF DUMP");

        File.WriteAllText(
            path,
            output.ToString(),
            new UTF8Encoding(
                false));

        return path;
    }

    private static void DumpImportedModel(
        StringBuilder output,
        ModelAsset model)
    {
        Key(
            output,
            "Model Name",
            model.Name);

        Key(
            output,
            "Node Count",
            model.Nodes.Count.ToString());

        Key(
            output,
            "Mesh Count",
            model.Meshes.Count.ToString());

        Key(
            output,
            "Material Count",
            model.Materials.Count.ToString());

        Key(
            output,
            "Animation Count",
            model.Animations.Count.ToString());

        Key(
            output,
            "Skeleton",
            model.Skeleton ==
                null
                ? "NONE"
                : $"{model.Skeleton.Name} ({model.Skeleton.Bones.Count} bones)");

        Line(
            output,
            string.Empty);

        int meshNumber =
            0;

        foreach (ImportedMesh mesh
                 in model.Meshes)
        {
            meshNumber++;

            Line(
                output,
                $"Imported Mesh #{meshNumber}");

            Key(
                output,
                "  Name",
                mesh.Name);

            Key(
                output,
                "  Key",
                mesh.Key);

            Key(
                output,
                "  Vertex Float Count",
                mesh.Vertices.Length.ToString());

            Key(
                output,
                "  Vertex Count",
                (
                    mesh.Vertices.Length /
                    8
                ).ToString());

            Key(
                output,
                "  Index Count",
                mesh.Indices.Length.ToString());

            Key(
                output,
                "  Triangle Count",
                (
                    mesh.Indices.Length /
                    3
                ).ToString());

            Key(
                output,
                "  Material Key",
                mesh.MaterialKey ??
                "(null)");

            DumpMeshBounds(
                output,
                mesh,
                "  Local Bounds");

            if (mesh.Vertices.Length <
                8)
            {
                Warn(
                    output,
                    $"Imported mesh '{mesh.Name}' has no complete vertex.");
            }

            if (mesh.Indices.Length ==
                0)
            {
                Warn(
                    output,
                    $"Imported mesh '{mesh.Name}' has zero indices.");
            }

            Line(
                output,
                string.Empty);
        }

        Section(
            output,
            "NODE -> MESH LINKS");

        foreach (ImportedNode node
                 in model.Nodes)
        {
            if (node.MeshKeys.Count ==
                0)
            {
                continue;
            }

            Key(
                output,
                node.Name,
                string.Join(
                    ", ",
                    node.MeshKeys));

            Key(
                output,
                "  Local Transform",
                Format(
                    node.LocalTransform));
        }
    }

    private static void DumpMeshBounds(
        StringBuilder output,
        ImportedMesh mesh,
        string label)
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

        int finiteVertices =
            0;

        int invalidVertices =
            0;

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
                invalidVertices++;

                continue;
            }

            finiteVertices++;

            minimum =
                Vector3.Min(
                    minimum,
                    position);

            maximum =
                Vector3.Max(
                    maximum,
                    position);
        }

        if (finiteVertices ==
            0)
        {
            Key(
                output,
                label,
                "NONE");

            Warn(
                output,
                $"{mesh.Name}: no finite position vertices.");

            return;
        }

        Key(
            output,
            label + " Min",
            Format(
                minimum));

        Key(
            output,
            label + " Max",
            Format(
                maximum));

        Key(
            output,
            label + " Size",
            Format(
                maximum -
                minimum));

        Key(
            output,
            label + " Finite Vertices",
            finiteVertices.ToString());

        Key(
            output,
            label + " Invalid Vertices",
            invalidVertices.ToString());
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

    private static string Format(
        Vector3 value)
    {
        return
            $"({value.X:0.########}, {value.Y:0.########}, {value.Z:0.########})";
    }

    private static string Format(
        Vector4 value)
    {
        return
            $"({value.X:0.########}, {value.Y:0.########}, {value.Z:0.########}, {value.W:0.########})";
    }

    private static string Format(
        Matrix4x4 value)
    {
        return
            $"[{value.M11:0.#####}, {value.M12:0.#####}, {value.M13:0.#####}, {value.M14:0.#####}] " +
            $"[{value.M21:0.#####}, {value.M22:0.#####}, {value.M23:0.#####}, {value.M24:0.#####}] " +
            $"[{value.M31:0.#####}, {value.M32:0.#####}, {value.M33:0.#####}, {value.M34:0.#####}] " +
            $"[{value.M41:0.#####}, {value.M42:0.#####}, {value.M43:0.#####}, {value.M44:0.#####}]";
    }

    private static void Section(
        StringBuilder output,
        string title)
    {
        Line(
            output,
            "------------------------------------------------------------");

        Line(
            output,
            title);

        Line(
            output,
            "------------------------------------------------------------");
    }

    private static void Key(
        StringBuilder output,
        string key,
        string value)
    {
        Line(
            output,
            $"{key}: {value}");
    }

    private static void Warn(
        StringBuilder output,
        string value)
    {
        Line(
            output,
            $"[WARN] {value}");
    }

    private static void Diagnosis(
        StringBuilder output,
        string value)
    {
        Line(
            output,
            $"[DIAGNOSIS] {value}");
    }

    private static void Line(
        StringBuilder output,
        string value)
    {
        output.AppendLine(
            value);
    }
}
