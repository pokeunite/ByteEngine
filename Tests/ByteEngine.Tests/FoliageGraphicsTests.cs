using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class FoliageGraphicsTests
{
    public static void Run(string root)
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        Matrix4x4[] first = FoliagePatch.CreateLayout(new(10, 6), 100, 42, 2, .2f);
        Matrix4x4[] same = FoliagePatch.CreateLayout(new(10, 6), 100, 42, 2, .2f);
        Matrix4x4[] other = FoliagePatch.CreateLayout(new(10, 6), 100, 43, 2, .2f);
        Check(first.SequenceEqual(same) && !first.SequenceEqual(other), "Seed determinism.");
        foreach (Matrix4x4 matrix in first)
        {
            Check(Math.Abs(matrix.Translation.X) <= 5 && Math.Abs(matrix.Translation.Z) <= 3,
                "Scatter within patch.");
            Matrix4x4.Decompose(matrix, out Vector3 scale, out _, out _);
            Check(scale.X >= 1.6f - .001f && scale.X <= 2.4f + .001f, "Positive scale variation.");
        }
        Check(Vector3.Transform(Vector3.Zero, FoliagePatch.WindSway(2, 4, 3, 1)) == Vector3.Zero,
            "Wind pins the base.");
        Check(FoliagePatch.WindSway(0, 0, 3, 1) != FoliagePatch.WindSway(1, 0, 3, 1),
            "Wind changes over time.");
        Check(FoliagePatch.WindSway(4, 0, 0, 1) == Matrix4x4.Identity, "Zero strength disables sway.");
        var safe = new FoliagePatch { Amount = int.MaxValue, PlantHeight = float.NaN, WindStrength = -5 };
        Check(safe.Amount == 2000 && safe.PlantHeight == 1 && safe.WindStrength == 0, "Safe limits.");
        using var database = new AssetDatabase(root, new[] { "Assets" });
        using var assets = new AssetManager(database);
        var serializer = new ComponentSerializer(root, database, assets);
        var scene = new Scene("Foliage");
        var patch = new FoliagePatch
        {
            Model = new(Guid.NewGuid(), "Assets/plant.glb"), MaterialAsset = new(Guid.NewGuid(), "Assets/leaves.bmat"),
            Area = new(12, 8), Amount = 35, Seed = 91, PlantHeight = 2, SizeVariation = .3f,
            WindEnabled = false, WindStrength = 5, WindSpeed = 2, ViewDistance = 60, CastShadows = false,
            SnapToGround = false
        };
        scene.CreateGameObject("Plants").AddComponent(patch);
        patch.BrushRadius = 2.5f; patch.PaintDensity = 12;
        patch.UsePaintedLayout = true;
        Check(patch.PaintPlant(new(2,0,3), .4f, .5f), "Paint adds a plant.");
        Check(!patch.PaintPlant(new(2.1f,0,3), 0, .5f), "Brush spacing prevents duplicates.");
        Check(patch.PaintPlant(new(5,0,3), 1, .5f), "Second paint point.");
        Check(patch.ErasePlants(new(5,0,3), .5f) == 1 && patch.PaintedPlants.Count == 1,
            "Erase only removes plants within brush.");
        scene.CreateGameObject("Sky").AddComponent(new SkyEnvironment { SmoothEdges = false });
        var sceneSerializer = new SceneSerializer(serializer);
        SkyEnvironmentExposureSerialization.Register(serializer);
        Scene copy = sceneSerializer.CloneForRuntime(scene);
        FoliagePatch loaded = copy.FindGameObject("Plants")!.GetComponent<FoliagePatch>()!;
        Check(loaded.Model == patch.Model && loaded.MaterialAsset == patch.MaterialAsset &&
            loaded.Area == patch.Area && loaded.Amount == 35 && loaded.Seed == 91 &&
            loaded.PlantHeight == 2 && loaded.SizeVariation == .3f && !loaded.WindEnabled &&
            loaded.WindStrength == 5 && loaded.WindSpeed == 2 && loaded.ViewDistance == 60 &&
            !loaded.CastShadows && !loaded.SnapToGround, "Foliage round-trip.");
        Check(loaded.BrushRadius == 2.5f && loaded.PaintDensity == 12, "Brush settings survive save/load.");
        Check(loaded.UsePaintedLayout && loaded.PaintedPlants.SequenceEqual(patch.PaintedPlants),
            "Painted points and mode survive scene save/load.");
        Check(loaded.ErasePlants(new(2,0,3), .5f) == 1, "Loaded points remain editable.");
        Check(copy.FindGameObject("Sky")!.GetComponent<SkyEnvironment>()!.SmoothEdges == false,
            "Anti-aliasing round-trip.");
        Check(ComponentMetadataRegistry.RegisteredTypes.Contains(typeof(FoliagePatch)),
            "Foliage available in Add Component.");
        Check(ComponentPropertyRenderer.Descriptors(typeof(FoliagePatch), PropertyEditorContext.Scene)
            .Any(property => property.Property.Name == nameof(FoliagePatch.Model)), "Plant picker exposed.");
        Console.WriteLine("Foliage layout, wind, safety and serialization tests passed.");
    }
}
