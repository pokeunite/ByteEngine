using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Tests;

internal static class MaterialFoundationTests
{
    public static void Run(string root)
    {
        string content = Path.Combine(root, "Assets");
        string parentPath = Path.Combine(content, "M_Base.bmat");
        Guid textureGuid = Guid.NewGuid();
        var parent = new MaterialAsset
        {
            Name = "M_Base",
            Standard = new MaterialParameters
            {
                BaseColor = new Vector4(.2f, .3f, .4f, 1f),
                BaseColorTexture = new AssetReference(textureGuid, "Assets/BaseColor.png"),
                Metallic = .7f,
                Roughness = .5f
            }
        };
        MaterialAssetSerializer.Save(parentPath, parent);
        MaterialAsset loaded = MaterialAssetSerializer.Load(parentPath);
        Assert(loaded.Standard.BaseColor == parent.Standard.BaseColor &&
            loaded.Standard.BaseColorTexture.Guid == textureGuid &&
            loaded.Standard.Metallic == .7f,
            "Standard .bmat round-trips colors, texture references and PBR factors");

        using var database = new AssetDatabase(root, new[] { "Assets" });
        AssetRecord baseRecord = database.Assets.Single(asset =>
            asset.ProjectPath == "Assets/M_Base.bmat");
        Assert(baseRecord.Type == AssetType.Material && baseRecord.Guid != Guid.Empty,
            ".bmat participates in normal asset GUID indexing");
        AssetReference parentReference = new(baseRecord.Guid, baseRecord.ProjectPath);
        string instancePath = Path.Combine(content, "MI_Wet.bmat");
        var instance = new MaterialAsset
        {
            Kind = MaterialAssetKind.Instance,
            Name = "MI_Wet",
            ParentMaterial = parentReference
        };
        instance.SetOverride(nameof(MaterialParameters.Roughness), .15f);
        instance.SetOverride(nameof(MaterialParameters.BaseColor),
            new Vector4(.1f, .15f, .2f, 1f));
        instance.SetOverride(nameof(MaterialParameters.NormalTexture),
            new AssetReference(Guid.NewGuid(), "Assets/WetNormal.png"));
        MaterialAssetSerializer.Save(instancePath, instance);
        string instanceJson = File.ReadAllText(instancePath);
        Assert(instanceJson.Contains("\"overrides\"", StringComparison.Ordinal) &&
            !instanceJson.Contains("\"standard\"", StringComparison.Ordinal),
            "Instance file stores sparse overrides rather than a copied Standard material");
        database.Scan();
        AssetRecord instanceRecord = database.Assets.Single(asset =>
            asset.ProjectPath == "Assets/MI_Wet.bmat");
        AssetReference instanceReference = new(instanceRecord.Guid, instanceRecord.ProjectPath);
        using var assets = new AssetManager(database);
        MaterialParameters effective = assets.ResolveMaterialParameters(instanceReference);
        Assert(effective.Roughness == .15f &&
            effective.BaseColor == new Vector4(.1f, .15f, .2f, 1f) &&
            effective.Metallic == .7f &&
            effective.NormalTexture.CachedProjectPath == "Assets/WetNormal.png",
            "Instance overrides only flagged scalar, color and texture fields");

        parent.Standard.Metallic = .4f;
        MaterialAssetSerializer.Save(parentPath, parent);
        database.Scan();
        effective = assets.ResolveMaterialParameters(instanceReference);
        Assert(effective.Metallic == .4f && effective.Roughness == .15f,
            "Parent changes propagate while overridden fields remain unchanged");

        instance.ParentMaterial = instanceReference;
        MaterialAssetSerializer.Save(instancePath, instance);
        assets.ReloadMaterialAsset(instanceReference);
        bool cycleRejected = false;
        try { assets.ResolveMaterialParameters(instanceReference); }
        catch (InvalidDataException) { cycleRejected = true; }
        Assert(cycleRejected, "Material instance inheritance cycles are rejected");

        instance.ParentMaterial = parentReference;
        MaterialAssetSerializer.Save(instancePath, instance);
        assets.ReloadMaterialAsset(instanceReference);
        string moved = Path.Combine(content, "MI_Wet_Renamed.bmat");
        File.Move(instancePath, moved);
        File.Move(instancePath + ".meta", moved + ".meta");
        database.Scan();
        Assert(database.Resolve(instanceReference)?.ProjectPath ==
            "Assets/MI_Wet_Renamed.bmat",
            "A moved material keeps its GUID through the normal .meta sidecar");
        Assert(MaterialTextureAutoDetection.Classify("Cliff_Base_Color.png") ==
            MaterialTextureAutoDetection.Slot.BaseColor &&
            MaterialTextureAutoDetection.Classify("Cliff_NormalDX.png") ==
            MaterialTextureAutoDetection.Slot.NormalDirectX &&
            MaterialTextureAutoDetection.Classify("Cliff_ORM.png") ==
            MaterialTextureAutoDetection.Slot.PackedOrm &&
            MaterialTextureAutoDetection.Classify("Cliff_Maybe.png") ==
            MaterialTextureAutoDetection.Slot.Unknown,
            "Filename inference recognizes common maps without guessing unknown ones");

        AssetRecord[] textureRecords =
        {
            new(Guid.NewGuid(), AssetType.Texture2D, "Assets/Cliff_Albedo.png",
                Path.Combine(content, "Cliff_Albedo.png"), "", new AssetMetadata()),
            new(Guid.NewGuid(), AssetType.Texture2D, "Assets/Cliff_NormalDX.png",
                Path.Combine(content, "Cliff_NormalDX.png"), "", new AssetMetadata()),
            new(Guid.NewGuid(), AssetType.Texture2D, "Assets/Cliff_ORM.png",
                Path.Combine(content, "Cliff_ORM.png"), "", new AssetMetadata())
        };
        MaterialAsset inferred = MaterialTextureAutoDetection.Create("M_Cliff", textureRecords);
        Assert(inferred.Standard.BaseColorTexture.Guid == textureRecords[0].Guid &&
            inferred.Standard.NormalTexture.Guid == textureRecords[1].Guid &&
            inferred.Standard.NormalConvention == MaterialNormalConvention.DirectX &&
            inferred.Standard.PbrMapMode == MaterialPbrMapMode.Packed &&
            inferred.Standard.PackedPbrTexture.Guid == textureRecords[2].Guid,
            "Create Material From Textures keeps GUID-backed map assignments");

        var componentSerializer = new ComponentSerializer(root, database, assets);
        var modelScene = new Scene("Material Override");
        modelScene.CreateGameObject("Character").AddComponent(new ModelHierarchyInstance
            { MaterialOverride = parentReference });
        Scene modelCopy = new SceneSerializer(componentSerializer).CloneForRuntime(modelScene);
        Assert(modelCopy.FindGameObject("Character")!.GetComponent<ModelHierarchyInstance>()!
            .MaterialOverride == parentReference, "Model material override persists across scene/Blueprint cloning");
        var proxy = new GameObject("Preview");
        var sourceModel = new ModelHierarchyInstance { MaterialOverride = parentReference };
        var sync = typeof(Scene).Assembly.GetType("ByteEngine.Core.Scene.EditorSkeletalPreviewCache")!
            .GetMethod("SynchronizeMaterialOverride", System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)!;
        sync.Invoke(null, new object[] { sourceModel, proxy });
        Assert(proxy.GetComponent<ModelHierarchyInstance>()!.MaterialOverride == parentReference,
            "Editor skeletal proxy receives model material override");
        sourceModel.MaterialOverride = instanceReference;
        sync.Invoke(null, new object[] { sourceModel, proxy });
        Assert(proxy.GetComponent<ModelHierarchyInstance>()!.MaterialOverride == instanceReference,
            "Editor skeletal proxy follows changed material");
        sourceModel.MaterialOverride = AssetReference.Empty;
        sync.Invoke(null, new object[] { sourceModel, proxy });
        Assert(proxy.GetComponent<ModelHierarchyInstance>()!.MaterialOverride.IsEmpty,
            "Clearing model override clears editor skeletal proxy override");
        var picker = typeof(ByteEngine.Editor.ComponentPropertyRenderer).GetMethod("GetExpectedAssetType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert((AssetType?)picker.Invoke(null, new object[] { new ModelHierarchyInstance(), "MaterialOverride" })
            == AssetType.Material, "Model material picker accepts only materials");
        Assert((AssetType?)picker.Invoke(null, new object[] { new ModelHierarchyInstance(), "Model" })
            == AssetType.Model3D, "Model picker accepts only models");
        Assert((AssetType?)picker.Invoke(null, new object[] { new ByteEngine.Core.Blueprints.BlueprintInstance(), "Blueprint" })
            == AssetType.Blueprint, "Blueprint picker accepts only Blueprints");

        var shared = new ByteEngine.Core.Graphics.ThreeD.Material
        { BaseColor = Vector4.One, Roughness = .8f };
        var objectA = shared.Clone();
        var objectB = shared.Clone();
        var overrideA = new ByteEngine.Core.Graphics.ThreeD.MaterialOverrideState
        { BaseColor = new Vector4(.9f, .1f, .1f, 1f), Roughness = .2f };
        overrideA.Apply(shared, objectA, null);
        Assert(shared.BaseColor == Vector4.One && shared.Roughness == .8f &&
            objectB.BaseColor == Vector4.One && objectA.Roughness == .2f,
            "Renderer override changes A without modifying a shared material or B");
        overrideA.Reset();
        overrideA.Apply(shared, objectA, null);
        Assert(objectA.BaseColor == Vector4.One && objectA.Roughness == .8f,
            "Reset Material Overrides restores inherited values");

        var scene = new Scene("Material Slots");
        var staticObject = scene.CreateGameObject("Static");
        staticObject.AddComponent(new MeshRenderer
        {
            UsePrimitive = true,
            MaterialAssetReference = parentReference
        });
        var skeletalObject = scene.CreateGameObject("Skeletal");
        skeletalObject.AddComponent(new SkeletalMeshRenderer
        {
            MaterialAssetSlots = new List<AssetReference>
            {
                parentReference,
                instanceReference
            }
        });
        Scene copy = new SceneSerializer(new ComponentSerializer(root, database, assets))
            .CloneForRuntime(scene);
        MeshRenderer? staticCopy = copy.FindGameObject("Static")?.GetComponent<MeshRenderer>();
        SkeletalMeshRenderer? skeletalCopy =
            copy.FindGameObject("Skeletal")?.GetComponent<SkeletalMeshRenderer>();
        Assert(staticCopy?.MaterialAssetReference.Guid == parentReference.Guid &&
            skeletalCopy?.MaterialAssetSlots.Count == 2 &&
            skeletalCopy.MaterialAssetSlots[0].Guid == parentReference.Guid &&
            skeletalCopy.MaterialAssetSlots[1].Guid == instanceReference.Guid,
            "Static and skeletal material assignments survive scene serialization");
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + description);
    }
}
