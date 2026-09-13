using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

internal static class BlueprintAuthoringService
{
    public static T AddComponent<T>(GameObject target, T component) where T : Component
    {
        AddDependencies(target, component.GetType());
        return target.AddComponent(component);
    }

    public static Component AddComponent(GameObject target, Component component)
    {
        AddDependencies(target, component.GetType());
        return target.AddComponent(component);
    }

    private static void AddDependencies(GameObject target, Type componentType)
    {
        foreach (Type requirement in ComponentMetadataRegistry.Get(componentType).RequiresComponents ?? Array.Empty<Type>())
        {
            if (target.Components.Any(existing => existing.GetType() == requirement)) continue;
            if (requirement == typeof(CharacterController3D)) target.AddComponent(new CharacterController3D());
        }
    }

    public static CameraBoom3D SetupThirdPersonCharacter(GameObject player, AssetManager? assets = null)
    {
        bool createInitialCapsule = player.GetComponent<CapsuleCollider3D>() == null;
        NormalizeCharacterStructure(player);
        EnsureSingleRootComponent(player, () => new CapsuleCollider3D { Radius = .5f, Height = 2f });
        EnsureSingleRootComponent(player, () => new CharacterController3D());
        EnsureSingleRootComponent(player, () => new AnimationController());
        EnsureSingleRootComponent(player, () => new PlayerController3D
        {
            UseLocalOrientation = false,
            CharacterRotation = CharacterRotationMode.FaceCamera,
            TurnSpeed = 540f,
            ControlPitch = 12f
        });
        CameraBoom3D boom = EnsureSingleRootComponent(player, () => new CameraBoom3D());

        GameObject? cameraObject = Descendants(player)
            .FirstOrDefault(child => child.GetComponent<Camera3D>() != null);
        Camera3D? previousActive = player.Scene?.ActiveCamera;
        if (cameraObject == null)
        {
            Scene scene = player.Scene ?? throw new InvalidOperationException("The character must belong to a scene.");
            cameraObject = scene.CreateGameObject("Camera");
            cameraObject.SetParent(player, false);
            cameraObject.AddComponent(new Camera3D());
        }
        else if (!ReferenceEquals(cameraObject.Parent, player))
        {
            cameraObject.SetParent(player, false);
        }
        cameraObject.Name = "Camera";
        Camera3D camera = cameraObject.GetComponent<Camera3D>()!;
        boom.CameraObjectId = cameraObject.Id;
        if (previousActive == null || ReferenceEquals(previousActive, camera))
            player.Scene?.SetActiveCamera(camera);

        if (assets != null && createInitialCapsule)
            CharacterCapsuleAutoFit.TryFit(player, assets, out _);

        return boom;
    }

    public static GameObject NormalizeCharacterStructure(GameObject player)
    {
        GameObject[] originalChildren = player.Children.ToArray();
        ModelHierarchyInstance? rootModel = player.GetComponent<ModelHierarchyInstance>();
        GameObject visual = EnsureVisualRoot(player);

        if (rootModel != null)
        {
            Vector3 rootScale = player.Transform.LocalScale;
            if (NearlyUniform(rootScale, rootModel.AppliedImportScale))
            {
                visual.Transform.LocalScale *= rootScale;
                player.Transform.LocalScale = Vector3.One;
            }

            GameObject importedModel = visual.Children.FirstOrDefault(child => child.GetComponent<ModelHierarchyInstance>() != null)
                ?? player.Scene!.CreateGameObject(player.Name.EndsWith("Model", StringComparison.OrdinalIgnoreCase)
                    ? player.Name
                    : player.Name + "Model");
            importedModel.SetParent(visual, false);
            if (!importedModel.HasComponent<ModelHierarchyInstance>())
                importedModel.AddComponent(new ModelHierarchyInstance
                {
                    Model = rootModel.Model,
                    AppliedImportScale = rootModel.AppliedImportScale
                });
            player.RemoveComponent(rootModel);

            foreach (GameObject child in originalChildren)
            {
                if (ReferenceEquals(child, visual) || child.GetComponent<Camera3D>() != null) continue;
                child.SetParent(importedModel, false);
            }
        }

        RemoveGameplayComponentsFromDescendants(player);
        return visual;
    }

    public static GameObject EnsureVisualRoot(GameObject player)
    {
        GameObject? visual = player.Children.FirstOrDefault(child => child.Name.Equals("Visual", StringComparison.OrdinalIgnoreCase));
        if (visual != null) return visual;
        Scene scene = player.Scene ?? throw new InvalidOperationException("The Blueprint root must belong to a preview scene.");
        visual = scene.CreateGameObject("Visual");
        visual.SetParent(player, false);
        return visual;
    }

    private static IEnumerable<GameObject> Descendants(GameObject root)
    {
        foreach (GameObject child in root.Children)
        {
            yield return child;
            foreach (GameObject descendant in Descendants(child)) yield return descendant;
        }
    }

    private static T EnsureSingleRootComponent<T>(GameObject root, Func<T> create) where T : Component
    {
        T? component = root.Components.OfType<T>().FirstOrDefault();
        foreach (T duplicate in root.Components.OfType<T>().Skip(1).ToArray()) root.RemoveComponent(duplicate);
        return component ?? root.AddComponent(create());
    }

    private static void RemoveGameplayComponentsFromDescendants(GameObject root)
    {
        Type[] gameplayTypes =
        {
            typeof(CapsuleCollider3D), typeof(CharacterController3D), typeof(PlayerController3D),
            typeof(AnimationController), typeof(CameraBoom3D)
        };
        foreach (GameObject child in Descendants(root))
            foreach (Component component in child.Components.Where(item => gameplayTypes.Contains(item.GetType())).ToArray())
                child.RemoveComponent(component);
    }

    private static bool NearlyUniform(Vector3 scale, float expected) =>
        MathF.Abs(scale.X - expected) < .0001f &&
        MathF.Abs(scale.Y - expected) < .0001f &&
        MathF.Abs(scale.Z - expected) < .0001f;
}
