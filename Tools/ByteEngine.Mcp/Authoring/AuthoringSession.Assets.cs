using System.Text.Json;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession
{
    private object ImportAssetTool(JsonElement? args, bool folder)
    {
        string source = Request.Required(args, "source");
        if (!Path.IsPathFullyQualified(source))
            throw new McpFault("INVALID_REQUEST", "Use an absolute source path.");
        source = Path.GetFullPath(source);
        if (folder ? !Directory.Exists(source) : !File.Exists(source))
            throw new McpFault("NOT_FOUND", folder ? "Source folder does not exist." : "Source file does not exist.");

        string destinationFolder = Request.String(args, "dest") ?? Project.AssetDirectory;
        _ = Inside(destinationFolder);
        bool overwrite = Request.Bool(args, "overwrite");
        return folder
            ? ImportFolder(source, destinationFolder, overwrite, Request.Bool(args, "recursive"), Request.Int(args, "limit", 500))
            : ImportFile(source, destinationFolder, overwrite, Request.String(args, "name"));
    }

    private object ImportFile(string source, string destinationFolder, bool overwrite, string? requestedName)
    {
        AssetType type = AssetDatabase.DetectAssetType(source);
        if (type == AssetType.Unknown)
            throw new McpFault("INVALID_ASSET_TYPE",
                "Supported extensions are model (.fbx/.obj/.glb/.gltf), texture (.png/.jpg/.jpeg/.tga/.bmp/.hdr), audio (.wav/.ogg), font (.ttf/.otf/.fnt) and ByteEngine assets (.bytescene/.byteblueprint/.byteevents/.bmat/.bvfx/.uitheme/.byteanim/.byteanimevents).");

        string name = requestedName ?? Path.GetFileName(source);
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new McpFault("INVALID_REQUEST", "The destination name contains invalid characters.");
        if (!Path.HasExtension(name)) name += Path.GetExtension(source);
        string target = Inside(destinationFolder.TrimEnd('/', '\\') + "/" + name);

        if (!string.Equals(Path.GetFullPath(source), target, StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(target) && !overwrite)
                throw new McpFault("ALREADY_EXISTS", $"'{Relative(target)}' already exists. Pass overwrite:true.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite);
        }

        Database.RefreshPaths([target]);
        AssetRecord asset = Database.TryGetAsset(Relative(target), out var record) && record != null
            ? record
            : throw new McpFault("IO_ERROR", "The asset database did not index the imported file.");
        Database.Scan();
        return new { ok = true, count = 1, imported = new[] { DescribeAsset(asset) }, type = type.ToString() };
    }

    private object ImportFolder(string source, string destinationFolder, bool overwrite, bool recursive, int limit)
    {
        limit = Math.Clamp(limit, 1, 5000);
        var files = Directory.EnumerateFiles(source, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(file => AssetDatabase.DetectAssetType(file) != AssetType.Unknown)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
        var imported = new List<object>();
        int skipped = 0;
        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            string target = Inside(destinationFolder.TrimEnd('/', '\\') + "/" + relative);
            if (File.Exists(target) && !overwrite)
            {
                skipped++;
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
            Database.RefreshPaths([target]);
            if (Database.TryGetAsset(Relative(target), out var record) && record != null)
                imported.Add(DescribeAsset(record));
        }
        Database.Scan();
        return new { ok = true, count = imported.Count, skipped, files = files.Length, imported };
    }

    private object SetImportSettingsTool(JsonElement? args)
    {
        AssetRecord asset = Asset(Request.Required(args, "asset"));
        if (asset.Type == AssetType.Model3D)
        {
            var settings = asset.Metadata.ModelImporter.Clone();
            JsonElement model = Request.Get(args, "model");
            if (model.ValueKind == JsonValueKind.Object)
            {
                if (Request.Get(model, "import_scale") is { ValueKind: JsonValueKind.Number } scale && scale.TryGetSingle(out float parsedScale))
                    settings.ImportScale = parsedScale;
                settings.GenerateLods = Request.Bool(model, "generate_lods", settings.GenerateLods);
                settings.GenerateNormals = Request.Bool(model, "generate_normals", settings.GenerateNormals);
                settings.PreferEmbeddedMaterials = Request.Bool(model, "prefer_embedded_materials", settings.PreferEmbeddedMaterials);
                settings.ImportAnimations = Request.Bool(model, "import_animations", settings.ImportAnimations);
                settings.AutoDetectHumanoidRig = Request.Bool(model, "auto_detect_humanoid_rig", settings.AutoDetectHumanoidRig);
                string? rig = Request.String(model, "rig_type");
                if (rig != null)
                {
                    if (!Enum.TryParse(rig, true, out AnimationRigType parsedRig))
                        throw new McpFault("INVALID_REQUEST", "rig_type must be Generic or Humanoid.");
                    settings.RigType = parsedRig;
                }
            }
            Database.SetModelImporterSettings(asset.Guid, settings);
            Database.RefreshPaths([asset.FullPath]);
            ModelAsset? refreshed = null;
            string? error = null;
            if (Request.Bool(args, "reimport", true))
            {
                try { refreshed = Assets.ReimportModel(asset.Guid); }
                catch (Exception exception) { error = exception.Message; }
            }
            return new
            {
                ok = error == null,
                code = error == null ? null : "IMPORT_FAILED",
                hint = error,
                asset = DescribeAsset(asset),
                settings = new
                {
                    import_scale = settings.ImportScale,
                    generate_lods = settings.GenerateLods,
                    generate_normals = settings.GenerateNormals,
                    prefer_embedded_materials = settings.PreferEmbeddedMaterials,
                    import_animations = settings.ImportAnimations,
                    auto_detect_humanoid_rig = settings.AutoDetectHumanoidRig,
                    rig_type = settings.RigType.ToString()
                },
                model = refreshed == null ? null : SummarizeModel(refreshed)
            };
        }
        if (asset.Type == AssetType.Texture2D)
        {
            string filter = Request.Required(args, "filter");
            if (!Enum.TryParse(filter, true, out TextureFilter parsed))
                throw new McpFault("INVALID_REQUEST", "Unknown texture filter.");
            Database.SetTextureFilter(asset.Guid, parsed);
            return new { ok = true, asset = DescribeAsset(asset), filter = parsed.ToString() };
        }
        throw new McpFault("INVALID_ASSET_TYPE", "Import settings exist for models and textures.");
    }

    private object ReimportAssetTool(JsonElement? args)
    {
        AssetRecord asset = Asset(Request.Required(args, "asset"), AssetType.Model3D);
        ModelAsset model = Assets.ReimportModel(asset.Guid);
        return new { ok = true, asset = DescribeAsset(asset), model = SummarizeModel(model) };
    }

    private static object SummarizeModel(ModelAsset model) => new
    {
        name = model.Name,
        guid = model.Guid,
        rig = model.RigType.ToString(),
        humanoid_auto_detected = model.HumanoidRigWasAutoDetected,
        nodes = model.Nodes.Take(200).Select(node => new
        {
            key = node.Key,
            name = node.Name,
            parent = node.ParentKey,
            meshes = node.MeshKeys
        }).ToArray(),
        meshes = model.Meshes.Select(mesh => new
        {
            key = mesh.Key,
            name = mesh.Name,
            vertices = mesh.Vertices.Length / 3,
            triangles = mesh.Indices.Length / 3,
            material = mesh.MaterialKey
        }).ToArray(),
        materials = model.Materials.Select(material => new
        {
            key = material.Key,
            name = material.Name,
            base_color = new[] { material.BaseColor.X, material.BaseColor.Y, material.BaseColor.Z, material.BaseColor.W },
            metallic = material.Metallic,
            roughness = material.Roughness,
            double_sided = material.DoubleSided
        }).ToArray(),
        animations = model.Animations.Select(animation => new
        {
            key = animation.Key,
            name = animation.Name,
            seconds = animation.Duration,
            channels = animation.Channels.Count,
            events = animation.Events.Count
        }).ToArray(),
        skeleton = model.Skeleton is null ? null : new
        {
            name = model.Skeleton.Name,
            bones = model.Skeleton.Bones.Count,
            bone_names = model.Skeleton.Bones.Take(128).Select(bone => bone.Name).ToArray()
        },
        sockets = model.Sockets.Select(socket => new { socket.Id, socket.Name, socket.BoneName }).ToArray()
    };

    private object? AssetDetail(AssetRecord asset, JsonElement? args)
    {
        if (asset.Type == AssetType.Model3D)
        {
            if (Request.Bool(args, "summary_only")) return null;
            try { return new { model = SummarizeModel(Assets.LoadModel(new AssetReference(asset.Guid, asset.ProjectPath))) }; }
            catch (Exception exception) { return new { model_error = exception.Message }; }
        }
        if (asset.Type == AssetType.Material)
        {
            try { return new { material = MaterialAssetSerializer.Load(asset.FullPath) }; }
            catch (Exception exception) { return new { material_error = exception.Message }; }
        }
        if (asset.Type == AssetType.Texture2D)
            return new { importer = new { filter = asset.Metadata.Importer.Filter.ToString() }, bytes = new FileInfo(asset.FullPath).Length };
        return null;
    }

    private object DescribeAsset(AssetRecord asset) => new
    {
        handle = AssetHandle(asset),
        path = asset.ProjectPath,
        type = asset.Type.ToString(),
        guid = asset.Guid,
        rev = Rev(asset.FullPath)
    };
}
