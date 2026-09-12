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

    public static CameraBoom3D SetupThirdPersonCharacter(GameObject player)
    {
        if (!player.HasComponent<CapsuleCollider3D>()) player.AddComponent(new CapsuleCollider3D { Radius = .5f, Height = 2f });
        if (!player.HasComponent<CharacterController3D>()) player.AddComponent(new CharacterController3D());
        if (!player.HasComponent<PlayerController3D>())
            player.AddComponent(new PlayerController3D
            {
                UseLocalOrientation = false,
                CharacterRotation = CharacterRotationMode.FaceCamera,
                TurnSpeed = 540f,
                ControlPitch = 12f
            });
        CameraBoom3D boom = player.GetComponent<CameraBoom3D>() ?? player.AddComponent(new CameraBoom3D());
        GameObject? cameraObject = Descendants(player).FirstOrDefault(child => child.GetComponent<Camera3D>() != null);
        if (cameraObject == null)
        {
            Scene scene = player.Scene ?? throw new InvalidOperationException("The character must belong to a scene.");
            cameraObject = scene.CreateGameObject("Camera");
            cameraObject.SetParent(player, false);
            cameraObject.AddComponent(new Camera3D { ActiveGameCamera = true });
        }
        Camera3D camera = cameraObject.GetComponent<Camera3D>()!;
        boom.CameraObjectId = cameraObject.Id;
        player.Scene?.SetActiveCamera(camera);
        return boom;
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
}
