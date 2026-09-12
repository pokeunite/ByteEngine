using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Editor;

internal sealed record ComponentMetadata(
    string DisplayName,
    string Category,
    string Description,
    string SearchKeywords = "",
    bool BeginnerVisible = true,
    bool Advanced = false,
    Type[]? RequiresComponents = null);

internal sealed record PropertyMetadata(
    string DisplayName,
    string Category,
    string Tooltip,
    string? Unit = null,
    bool Advanced = false);

internal static class ComponentMetadataRegistry
{
    public static readonly string[] CategoryOrder =
    {
        "Character", "Camera", "Rendering", "Physics", "Gameplay",
        "AI", "Audio", "UI", "World", "Utility", "Advanced"
    };

    private static readonly Dictionary<Type, ComponentMetadata> Components = new()
    {
        [typeof(CharacterController3D)] = new("Character Movement", "Character", "Controls grounded movement, jumping, slopes and air control.", "controller motor walking jump"),
        [typeof(PlayerController3D)] = new("Player Input", "Character", "Creates camera-relative movement intent and player control rotation.", "controls wasd mouse control yaw pitch", true, false, new[] { typeof(CharacterController3D) }),
        [typeof(AnimationController)] = new("Animation Controller", "Character", "Selects character animation states from movement.", "character animator"),
        [typeof(CameraBoom3D)] = new("Third Person Camera", "Camera", "Positions a child camera on a collision-aware third-person boom.", "spring arm orbit tps follow"),
        [typeof(Camera3D)] = new("Camera", "Camera", "Renders a perspective 3D game view.", "perspective fov"),
        [typeof(Camera2D)] = new("2D Camera", "Camera", "Renders a two-dimensional game view.", "orthographic zoom"),
        [typeof(ThirdPersonCamera3D)] = new("Legacy Third Person Camera", "Camera", "Legacy standalone follow camera kept for older projects.", "tps orbit follow", false, true),
        [typeof(ModelHierarchyInstance)] = new("Model", "Rendering", "References an imported model hierarchy.", "fbx mesh asset"),
        [typeof(MeshRenderer)] = new("Mesh Renderer", "Rendering", "Draws a static 3D mesh.", "material primitive"),
        [typeof(SkeletalMeshRenderer)] = new("Skeletal Mesh Renderer", "Rendering", "Draws an animated skinned mesh.", "character bones model"),
        [typeof(SpriteRenderer)] = new("Sprite Renderer", "Rendering", "Draws a textured 2D sprite.", "image texture"),
        [typeof(DirectionalLight)] = new("Directional Light", "Rendering", "Lights the scene from one direction.", "sun world light"),
        [typeof(BoxCollider3D)] = new("Box Collider", "Physics", "A box-shaped collision volume.", "collision cube"),
        [typeof(CapsuleCollider3D)] = new("Capsule Collider", "Physics", "A character-friendly capsule collision volume.", "collision character"),
        [typeof(GroundSurface)] = new("Ground Surface", "Physics", "Marks a surface as walkable by character movement.", "floor slope"),
        [typeof(HealthComponent)] = new("Health", "Gameplay", "Tracks damage, healing and death.", "hit points hp damage"),
        [typeof(LifetimeComponent)] = new("Lifetime", "Gameplay", "Destroys its object after a configured duration.", "timer destroy despawn"),
        [typeof(Projectile3D)] = new("Projectile", "Gameplay", "Moves a swept projectile and damages health.", "bullet damage"),
        [typeof(ProjectileLauncher3D)] = new("Projectile Launcher", "Gameplay", "Creates reusable projectiles with a fire cooldown.", "weapon shoot fire"),
        [typeof(PlayerShooter3D)] = new("Player Shooter", "Gameplay", "Maps player fire input to a projectile launcher.", "weapon input"),
        [typeof(ArenaGameManager)] = new("Arena Game Manager", "Gameplay", "Tracks arena match state.", "game rules manager", false, true),
        [typeof(SimpleEnemyAI3D)] = new("Simple Enemy AI", "AI", "Chases and attacks a nearby player without navigation.", "enemy chase attack"),
        [typeof(EventModuleComponent)] = new("Event Module", "Utility", "Runs a reusable ByteGraph event module.", "visual logic bytegraph"),
        [typeof(BlueprintInstance)] = new("Blueprint Instance", "Advanced", "Maintains the source and override state of a placed Blueprint.", "prefab source override", false, true)
    };

    private static readonly Dictionary<(Type, string), PropertyMetadata> Properties = new()
    {
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.ArmLength))] = new("Camera Distance", "Camera", "Distance from the character pivot.", "m"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.PivotHeight))] = new("Camera Height", "Camera", "Height of the camera pivot.", "m"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MouseSensitivityX))] = new("Horizontal Sensitivity", "Rotation", "Horizontal mouse sensitivity."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MouseSensitivityY))] = new("Vertical Sensitivity", "Rotation", "Vertical mouse sensitivity."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MinPitch))] = new("Minimum Vertical Angle", "Rotation", "Lowest camera pitch.", "degrees"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MaxPitch))] = new("Maximum Vertical Angle", "Rotation", "Highest camera pitch.", "degrees"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.PositionSmoothness))] = new("Camera Smoothness", "Smoothing", "How quickly camera position catches up."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.RotationSmoothness))] = new("Rotation Smoothness", "Smoothing", "How quickly boom rotation catches up."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.ShoulderOffset))] = new("Shoulder Offset", "Advanced", "Moves the camera sideways.", "m", true),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.CollisionRadius))] = new("Collision Radius", "Collision", "Radius used to keep the camera out of walls.", "m", true),
        [(typeof(Camera3D), nameof(Camera3D.FieldOfView))] = new("Field of View", "Camera", "Vertical camera field of view.", "degrees"),
        [(typeof(CharacterController3D), nameof(CharacterController3D.MoveSpeed))] = new("Move Speed", "Movement", "Maximum movement speed.", "m/s"),
        [(typeof(PlayerController3D), nameof(PlayerController3D.CharacterRotation))] = new("Character Rotation", "Rotation", "How the character body chooses its facing direction."),
        [(typeof(PlayerController3D), nameof(PlayerController3D.TurnSpeed))] = new("Turn Speed", "Rotation", "Maximum body rotation speed.", "degrees / second"),
        [(typeof(PlayerController3D), nameof(PlayerController3D.UseLocalOrientation))] = new("Use Character Direction", "Advanced", "Move relative to character orientation instead of control rotation.", null, true)
    };

    public static IReadOnlyCollection<Type> RegisteredTypes => Components.Keys;

    public static ComponentMetadata Get(Type type) => Components.TryGetValue(type, out ComponentMetadata? value)
        ? value
        : new ComponentMetadata(FriendlyTypeName(type.Name), "Advanced", $"{type.Name} engine component.", type.Name, false, true);

    public static PropertyMetadata? GetProperty(Type type, string property) =>
        Properties.TryGetValue((type, property), out PropertyMetadata? value) ? value : null;

    public static string DisplayName(Type type) => Get(type).DisplayName;

    public static bool Matches(Type type, string search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        ComponentMetadata metadata = Get(type);
        return metadata.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            metadata.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            metadata.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            metadata.SearchKeywords.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            type.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            Properties.Where(pair => pair.Key.Item1 == type)
                .Any(pair => pair.Value.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private static string FriendlyTypeName(string name)
    {
        name = name.Replace("Component", string.Empty).Replace("3D", " 3D");
        return string.Concat(name.Select((character, index) =>
            index > 0 && char.IsUpper(character) && !char.IsWhiteSpace(name[index - 1])
                ? " " + character
                : character.ToString()));
    }
}
