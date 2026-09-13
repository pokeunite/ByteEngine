using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class V083CharacterAuthoringTests
{
    private static readonly Type[] GameplayTypes =
    {
        typeof(CapsuleCollider3D), typeof(CharacterController3D), typeof(PlayerController3D),
        typeof(AnimationController), typeof(CameraBoom3D)
    };

    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        TestNormalizedOwnershipAndIdempotency();
        TestScaledModelAutoFit(root, database, assets);
        TestFitClamping();
        TestLookDirectionAndInversion();
        TestSerialization(root, database, assets);
    }

    private static void TestNormalizedOwnershipAndIdempotency()
    {
        var scene = new Scene("Character Authoring");
        GameObject player = scene.CreateGameObject("Gorilla");
        player.Transform.LocalScale = Vector3.One * .01f;
        player.AddComponent(new ModelHierarchyInstance { AppliedImportScale = .01f });
        GameObject importedNode = scene.CreateGameObject("Armature");
        importedNode.SetParent(player, false);
        importedNode.AddComponent(new CharacterController3D());
        importedNode.AddComponent(new CameraBoom3D());

        BlueprintAuthoringService.SetupThirdPersonCharacter(player);
        BlueprintAuthoringService.SetupThirdPersonCharacter(player);

        GameObject visual = player.Children.Single(item => item.Name == "Visual");
        GameObject model = visual.Children.Single(item => item.GetComponent<ModelHierarchyInstance>() != null);
        GameObject camera = player.Children.Single(item => item.GetComponent<Camera3D>() != null);
        Assert(player.Transform.LocalScale == Vector3.One && Near(visual.Transform.LocalScale.X, .01f),
            "Character normalization moves import scale correction from gameplay root to Visual");
        Assert(model.Name == "GorillaModel" && importedNode.IsDescendantOf(model) && camera.Name == "Camera",
            "Character normalization creates Root/Visual/ImportedModel and a direct Camera child");

        foreach (Type type in GameplayTypes)
        {
            Assert(player.Components.Count(component => component.GetType() == type) == 1,
                $"Character preset keeps one {type.Name} on PlayerRoot");
            Assert(Descendants(player).All(item => item.Components.All(component => component.GetType() != type)),
                $"Character preset removes {type.Name} from Visual/model descendants");
        }
        Assert(model.Components.All(component => component is ModelHierarchyInstance),
            "ImportedModel root contains only its model hierarchy marker in normalized test");
        Assert(player.Children.Count(item => item.Name == "Visual") == 1 &&
            player.Children.Count(item => item.GetComponent<Camera3D>() != null) == 1,
            "Third Person Character setup is idempotent for Visual and Camera children");
    }

    private static void TestScaledModelAutoFit(string root, AssetDatabase database, AssetManager assets)
    {
        string path = Path.Combine(root, "Assets", "AutoFitCharacter.obj");
        File.WriteAllText(path,
            "v -50 0 -20\n" +
            "v 50 0 -20\n" +
            "v 0 200 20\n" +
            "f 1 2 3\n");
        database.Scan();
        Assert(database.TryGetAsset("Assets/AutoFitCharacter.obj", out AssetRecord? asset) && asset != null,
            "Auto-fit model asset registered");
        ModelAsset modelAsset = assets.LoadModel(new AssetReference(asset!.Guid, asset.ProjectPath));
        string meshKey = modelAsset.Meshes.Single().Key;

        var scene = new Scene("Scaled Auto Fit");
        GameObject player = scene.CreateGameObject("PlayerRoot");
        GameObject visual = scene.CreateGameObject("Visual");
        visual.SetParent(player, false);
        visual.Transform.LocalScale = Vector3.One * .01f;
        GameObject model = scene.CreateGameObject("ImportedModel");
        model.SetParent(visual, false);
        model.AddComponent(new ModelHierarchyInstance
        {
            Model = new AssetReference(asset.Guid, asset.ProjectPath),
            AppliedImportScale = .01f
        });
        GameObject mesh = scene.CreateGameObject("Mesh");
        mesh.SetParent(model, false);
        mesh.AddComponent(new MeshRenderer
        {
            MeshReference = new ModelMeshReference(new AssetReference(asset.Guid, asset.ProjectPath), meshKey)
        });
        CapsuleCollider3D capsule = player.AddComponent(new CapsuleCollider3D());

        Assert(CharacterCapsuleAutoFit.TryFit(player, assets, out CharacterCapsuleFitResult fit),
            "Capsule auto-fit resolves transformed imported geometry");
        Assert(Near(fit.VisualBounds.X, 1f) && Near(fit.VisualBounds.Y, 2f) && Near(fit.VisualBounds.Z, .4f) &&
            Near(capsule.Radius, .525f) && Near(capsule.Height, 2.04f) && Near(capsule.Center.Y, 1f),
            "Capsule uses displayed bounds after Visual import-scale correction");
        capsule.Radius = .8f;
        capsule.Height = 2.5f;
        BlueprintAuthoringService.SetupThirdPersonCharacter(player, assets);
        Assert(Near(capsule.Radius, .8f) && Near(capsule.Height, 2.5f),
            "Repeated character setup preserves manually authored capsule values");
    }

    private static void TestFitClamping()
    {
        var scene = new Scene("Fit Clamping");
        GameObject tiny = scene.CreateGameObject("Tiny");
        CharacterCapsuleFitResult tinyFit = CharacterCapsuleAutoFit.FitBounds(
            tiny, Vector3.Zero, new Vector3(.0001f), "Tiny import");
        GameObject huge = scene.CreateGameObject("Huge");
        CharacterCapsuleFitResult hugeFit = CharacterCapsuleAutoFit.FitBounds(
            huge, new Vector3(-10000f), new Vector3(10000f), "Huge import");
        Assert(float.IsFinite(tinyFit.CapsuleRadius) && tinyFit.CapsuleRadius >= .05f &&
            tinyFit.CapsuleHeight >= tinyFit.CapsuleRadius * 2f,
            "Very small imports produce finite positive capsule values");
        Assert(float.IsFinite(hugeFit.CapsuleRadius) && hugeFit.CapsuleRadius <= 100f &&
            hugeFit.CapsuleHeight <= 200f && hugeFit.CapsuleHeight >= hugeFit.CapsuleRadius * 2f,
            "Very large imports are clamped to sensible capsule values");
    }

    private static void TestLookDirectionAndInversion()
    {
        var boom = new CameraBoom3D { MouseSensitivityX = .5f, MouseSensitivityY = .25f };
        Vector2 normal = PlayerController3D.CalculateLookDelta(new Vector2(4f, 4f), boom);
        Assert(normal.X > 0f && normal.Y < 0f && PlayerController3D.ForwardFromYaw(90f).X > .99f &&
            CameraBoom3D.CalculateOrbitVector(90f, 0f, 5f).X < -4.99f,
            "Positive mouse X produces positive/rightward control yaw and rightward view rotation");
        boom.InvertHorizontalLook = true;
        boom.InvertVerticalLook = true;
        Vector2 inverted = PlayerController3D.CalculateLookDelta(new Vector2(4f, 4f), boom);
        Assert(inverted.X < 0f && inverted.Y > 0f, "Explicit horizontal and vertical inversion reverse look independently");
    }

    private static void TestSerialization(string root, AssetDatabase database, AssetManager assets)
    {
        var scene = new Scene("b.3 Serialization");
        GameObject player = scene.CreateGameObject("Player");
        player.AddComponent(new CapsuleCollider3D
        {
            Radius = .6f,
            Height = 2.1f,
            Center = new Vector3(0f, 1f, 0f),
            VisualBounds = new Vector3(1.1f, 2f, .8f),
            AutoFitSource = "Regression geometry"
        });
        player.AddComponent(new CameraBoom3D
        {
            InvertHorizontalLook = true,
            InvertVerticalLook = true
        });
        Scene clone = new SceneSerializer(new ComponentSerializer(root, database, assets)).CloneForRuntime(scene);
        CapsuleCollider3D capsule = clone.FindGameObject("Player")!.GetComponent<CapsuleCollider3D>()!;
        CameraBoom3D boom = clone.FindGameObject("Player")!.GetComponent<CameraBoom3D>()!;
        Assert(capsule.VisualBounds == new Vector3(1.1f, 2f, .8f) && capsule.AutoFitSource == "Regression geometry",
            "Capsule auto-fit diagnostics survive serialization and Play Mode cloning");
        Assert(boom.InvertHorizontalLook && boom.InvertVerticalLook,
            "Camera look inversion settings survive serialization and Play Mode cloning");
    }

    private static IEnumerable<GameObject> Descendants(GameObject root)
    {
        foreach (GameObject child in root.Children)
        {
            yield return child;
            foreach (GameObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static bool Near(float value, float expected) => MathF.Abs(value - expected) < .001f;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
