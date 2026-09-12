using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Editor;

internal sealed record ComponentMetadata(
    string DisplayName,
    string Category,
    string Description,
    bool BeginnerVisible = true,
    Type[]? RequiresComponents = null);

internal sealed record PropertyMetadata(
    string DisplayName,
    string Category,
    string Tooltip,
    string? Unit = null,
    bool Advanced = false);

internal static class ComponentMetadataRegistry
{
    private static readonly Dictionary<Type, ComponentMetadata> Components = new()
    {
        [typeof(CharacterController3D)] = new("Character Movement", "Gameplay", "Moves a character with gravity, jumping and ground handling."),
        [typeof(PlayerController3D)] = new("Player Input", "Gameplay", "Turns player input into movement requests.", true, new[] { typeof(CharacterController3D) }),
        [typeof(CameraBoom3D)] = new("Third Person Camera", "Camera", "Orbits a child camera around this character."),
        [typeof(Camera3D)] = new("Camera", "Camera", "Renders the 3D game view."),
        [typeof(ModelHierarchyInstance)] = new("Model", "Rendering", "An imported model hierarchy."),
        [typeof(HealthComponent)] = new("Health", "Gameplay", "Tracks damage, healing and death."),
        [typeof(BlueprintInstance)] = new("Blueprint Instance", "Blueprint", "Links this object to a Blueprint asset."),
        [typeof(MeshRenderer)] = new("Mesh Renderer", "Rendering", "Draws a 3D mesh."),
        [typeof(DirectionalLight)] = new("Directional Light", "Rendering", "Lights the scene from one direction."),
        [typeof(BoxCollider3D)] = new("Box Collider", "Physics", "A box-shaped collision volume."),
        [typeof(CapsuleCollider3D)] = new("Capsule Collider", "Physics", "A character-friendly collision volume.")
    };

    private static readonly Dictionary<(Type, string), PropertyMetadata> Properties = new()
    {
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.ArmLength))] = new("Camera Distance", "Camera", "Distance from the character pivot.", "m"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.PivotHeight))] = new("Camera Height", "Camera", "Height of the camera pivot.", "m"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MouseSensitivityX))] = new("Horizontal Sensitivity", "Camera", "Horizontal mouse orbit sensitivity."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MouseSensitivityY))] = new("Vertical Sensitivity", "Camera", "Vertical mouse orbit sensitivity."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MinPitch))] = new("Minimum Vertical Angle", "Rotation", "Lowest camera pitch.", "degrees"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MaxPitch))] = new("Maximum Vertical Angle", "Rotation", "Highest camera pitch.", "degrees"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.ShoulderOffset))] = new("Shoulder Offset", "Advanced", "Moves the camera sideways.", "m", true),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.CollisionRadius))] = new("Collision Radius", "Collision", "Radius used to keep the camera out of walls.", "m", true),
        [(typeof(Camera3D), nameof(Camera3D.FieldOfView))] = new("Field of View", "Camera", "Vertical camera field of view.", "degrees"),
        [(typeof(CharacterController3D), nameof(CharacterController3D.MoveSpeed))] = new("Move Speed", "Movement", "Maximum movement speed.", "m/s"),
        [(typeof(PlayerController3D), nameof(PlayerController3D.UseLocalOrientation))] = new("Use Character Direction", "Advanced", "Move relative to character orientation instead of the active camera.", null, true)
    };

    public static ComponentMetadata Get(Type type) => Components.TryGetValue(type, out ComponentMetadata? value)
        ? value : new ComponentMetadata(FriendlyTypeName(type.Name), "Advanced", $"{type.Name} engine component.");
    public static PropertyMetadata? GetProperty(Type type, string property) =>
        Properties.TryGetValue((type, property), out PropertyMetadata? value) ? value : null;
    public static string DisplayName(Type type) => Get(type).DisplayName;
    public static bool Matches(Type type, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        ComponentMetadata metadata = Get(type);
        return metadata.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            type.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            Properties.Where(pair => pair.Key.Item1 == type)
                .Any(pair => pair.Value.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static string FriendlyTypeName(string name)
    {
        name = name.Replace("Component", string.Empty).Replace("3D", " 3D");
        return string.Concat(name.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsWhiteSpace(name[index - 1]) ? " " + character : character.ToString()));
    }
}
