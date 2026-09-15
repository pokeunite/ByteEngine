using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
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
    bool Advanced = false,
    bool ReadOnly = false,
    bool RuntimeEditable = true);

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
        [typeof(PointLight)] = new("Point Light", "Rendering", "Lights nearby 3D surfaces outward from a position.", "lamp bulb local omni light"),
        [typeof(SkyEnvironment)] = new("Sky Environment", "World", "Draws a procedural 3D world sky and can control scene ambient light.", "sky environment horizon background ambient world"),

        [typeof(BoxCollider3D)] = new("Box Collider", "Physics", "A box-shaped collision volume.", "collision cube"),
        [typeof(CapsuleCollider3D)] = new("Capsule Collider", "Physics", "A character-friendly capsule collision volume.", "collision character"),
        [typeof(Rigidbody3D)] = new(
            "Rigidbody 3D",
            "Physics",
            "Adds dynamic, static or kinematic linear 3D rigid-body simulation. Pair it with a Box or Capsule Collider.",
            "rigidbody physics dynamic static kinematic mass gravity force impulse bounce restitution friction",
            true,
            false),
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
        [(typeof(CapsuleCollider3D), nameof(CapsuleCollider3D.VisualBounds))] = new("Visual Bounds", "Diagnostics", "Measured bounds from capsule auto-fit.", Advanced: true, ReadOnly: true),
        [(typeof(CapsuleCollider3D), nameof(CapsuleCollider3D.AutoFitSource))] = new("Auto-Fit Source", "Diagnostics", "Source used by capsule auto-fit.", Advanced: true, ReadOnly: true),
        [(typeof(ModelHierarchyInstance), nameof(ModelHierarchyInstance.AppliedImportScale))] = new("Applied Import Scale", "Diagnostics", "Scale recorded when importing the hierarchy.", Advanced: true, ReadOnly: true),

        [(typeof(Rigidbody3D), nameof(Rigidbody3D.BodyType))] = new("Body Type", "Body", "Dynamic bodies are simulated; Static bodies do not move; Kinematic bodies are moved by gameplay."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.Mass))] = new("Mass", "Body", "Mass used by forces and collision impulses.", "kg"),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.UseGravity))] = new("Use Gravity", "Forces", "Apply the scene PhysicsWorld gravity to this body."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.GravityScale))] = new("Gravity Scale", "Forces", "Multiplier applied to scene gravity."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.LinearDamping))] = new("Linear Damping", "Body", "Reduces linear velocity over time."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.Restitution))] = new("Restitution", "Material", "Bounciness. 0 = no bounce; 1 = fully elastic."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.Friction))] = new("Friction", "Material", "Surface friction used by collision response."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.Velocity))] = new("Velocity", "Body", "Current world-space linear velocity.", "m/s", true),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.FreezePositionX))] = new("Freeze Position X", "Constraints", "Prevent physics from moving this body along world X."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.FreezePositionY))] = new("Freeze Position Y", "Constraints", "Prevent physics from moving this body along world Y."),
        [(typeof(Rigidbody3D), nameof(Rigidbody3D.FreezePositionZ))] = new("Freeze Position Z", "Constraints", "Prevent physics from moving this body along world Z."),

        [(typeof(CameraBoom3D), nameof(CameraBoom3D.ArmLength))] = new("Camera Distance", "Camera", "Distance from the character pivot.", "m"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.PivotHeight))] = new("Camera Height", "Camera", "Height of the camera pivot.", "m"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MouseSensitivityX))] = new("Horizontal Sensitivity", "Rotation", "Horizontal mouse sensitivity."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MouseSensitivityY))] = new("Vertical Sensitivity", "Rotation", "Vertical mouse sensitivity."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.InvertHorizontalLook))] = new("Invert Horizontal Look", "Rotation", "Reverse horizontal mouse look."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.InvertVerticalLook))] = new("Invert Vertical Look", "Rotation", "Reverse vertical mouse look."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MinPitch))] = new("Minimum Vertical Angle", "Rotation", "Lowest camera pitch.", "degrees"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.MaxPitch))] = new("Maximum Vertical Angle", "Rotation", "Highest camera pitch.", "degrees"),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.PositionSmoothness))] = new("Camera Smoothness", "Smoothing", "How quickly camera position catches up."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.RotationSmoothness))] = new("Rotation Smoothness", "Smoothing", "How quickly boom rotation catches up."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.ShoulderOffset))] = new("Shoulder Offset", "Advanced", "Moves the camera sideways.", "m", true),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.CollisionRadius))] = new("Collision Radius", "Collision", "Radius used to keep the camera out of walls.", "m", true),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.CameraCollisionMask))] = new("Camera Collision Mask", "Collision", "Layers inspected by camera collision queries.", Advanced: true),
        [(typeof(CharacterController3D), nameof(CharacterController3D.GroundCollisionMask))] = new("Ground Collision Mask", "Collision", "Layers inspected by grounding checks.", Advanced: true),
        [(typeof(BoxCollider3D), nameof(Collider3D.UseProjectMatrix))] = new("Use Project Matrix", "Collision", "Use the project collision matrix for this collider.", Advanced: true),
        [(typeof(BoxCollider3D), nameof(Collider3D.CollisionMask))] = new("Collision Mask", "Collision", "Layers accepted when overriding the project matrix.", Advanced: true),
        [(typeof(CapsuleCollider3D), nameof(Collider3D.UseProjectMatrix))] = new("Use Project Matrix", "Collision", "Use the project collision matrix for this collider.", Advanced: true),
        [(typeof(CapsuleCollider3D), nameof(Collider3D.CollisionMask))] = new("Collision Mask", "Collision", "Layers accepted when overriding the project matrix.", Advanced: true),
        [(typeof(Projectile3D), nameof(Projectile3D.CollisionMask))] = new("Collision Mask", "Collision", "Layers inspected by this projectile.", Advanced: true),
        [(typeof(SimpleEnemyAI3D), nameof(SimpleEnemyAI3D.TargetTagId))] = new("Target Tag", "Targeting", "Stable project Tag used after an explicit target and before the legacy target name."),
        [(typeof(Camera3D), nameof(Camera3D.FieldOfView))] = new("Field of View", "Camera", "Vertical camera field of view.", "degrees"),
        [(typeof(CharacterController3D), nameof(CharacterController3D.MoveSpeed))] = new("Move Speed", "Movement", "Maximum movement speed.", "m/s"),
        [(typeof(PlayerController3D), nameof(PlayerController3D.CharacterRotation))] = new("Character Rotation", "Rotation", "How the character body chooses its facing direction."),
        [(typeof(PlayerController3D), nameof(PlayerController3D.TurnSpeed))] = new("Turn Speed", "Rotation", "Maximum body rotation speed.", "degrees / second"),
        [(typeof(PlayerController3D), nameof(PlayerController3D.UseLocalOrientation))] = new("Use Character Direction", "Advanced", "Move relative to character orientation instead of control rotation.", null, true),
        [(typeof(PlayerController3D), nameof(PlayerController3D.MoveAction))] = new("Movement Action", "Input Actions", "The 2D Input Action used for character movement."),
        [(typeof(PlayerController3D), nameof(PlayerController3D.LookAction))] = new("Look Action", "Input Actions", "The 2D Input Action used for camera and control rotation."),
        [(typeof(PlayerController3D), nameof(PlayerController3D.JumpAction))] = new("Jump Action", "Input Actions", "The Button Input Action that requests a jump."),
        [(typeof(PlayerController3D), nameof(PlayerController3D.SprintAction))] = new("Sprint Action", "Input Actions", "The Button Input Action reserved for sprint behavior."),
        [(typeof(ThirdPersonCamera3D), nameof(ThirdPersonCamera3D.LookAction))] = new("Look Action", "Input Actions", "The 2D Input Action used by the legacy orbit camera."),
        [(typeof(PointLight), nameof(PointLight.Intensity))] = new("Intensity", "Lighting", "Brightness of this local light."),
        [(typeof(PointLight), nameof(PointLight.Range))] = new("Range", "Lighting", "Maximum distance affected by this light.", "m"),
        [(typeof(PointLight), nameof(PointLight.Color))] = new("Color", "Lighting", "RGB color of this light."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.DrawSky))] = new("Draw Sky", "Sky", "Render the selected world sky behind 3D geometry."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.SkyMode))] = new("Sky Mode", "Sky", "Choose ByteEngine's procedural sky or an equirectangular environment texture."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.EnvironmentMapReference))] = new("Environment Map", "Environment Map", "Drag a 2:1 equirectangular Texture2D asset here."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.EnvironmentIntensity))] = new("Environment Intensity", "Environment Map", "Brightness multiplier for the environment texture."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.EnvironmentRotationDegrees))] = new("Environment Rotation", "Environment Map", "Horizontal rotation of the 360-degree environment texture.", "degrees"),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.EnvironmentLightingEnabled))] = new("Environment Lighting", "Image Based Lighting", "Use the environment map as diffuse and specular lighting for standard 3D materials."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.Exposure))] = new("Exposure", "Post Processing", "Scene-wide exposure applied before ACES tone mapping."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.ZenithColor))] = new("Zenith Color", "Sky", "Color directly overhead."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.HorizonColor))] = new("Horizon Color", "Sky", "Color around the world horizon."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.GroundColor))] = new("Ground Color", "Sky", "Color used below the horizon."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.SkyIntensity))] = new("Sky Intensity", "Sky", "Brightness multiplier for the procedural sky."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.HorizonSharpness))] = new("Horizon Sharpness", "Sky", "Controls the gradient transition away from the horizon."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.OverrideAmbient))] = new("Override Ambient", "Ambient", "Use this environment's ambient intensity instead of the value derived from directional lights."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.AmbientIntensity))] = new("Ambient Intensity", "Ambient", "Global ambient-light intensity applied to 3D materials."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.FogEnabled))] = new("Enable Fog", "Fog", "Enable atmospheric distance fog for normal 3D scene geometry."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.FogMode))] = new("Fog Mode", "Fog", "Linear uses start/end distances; Exponential uses density."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.FogColor))] = new("Fog Color", "Fog", "Atmospheric color blended into distant 3D geometry."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.FogStartDistance))] = new("Start Distance", "Fog", "Camera distance where linear fog begins.", "m"),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.FogEndDistance))] = new("End Distance", "Fog", "Camera distance where linear fog reaches maximum opacity.", "m"),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.FogDensity))] = new("Density", "Fog", "Density used by Exponential fog."),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.FogMaxOpacity))] = new("Maximum Opacity", "Fog", "Maximum amount of scene color that fog may replace.")
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
