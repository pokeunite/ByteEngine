using ByteEngine.Core.Animation;
using ByteEngine.Core.Audio;
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
        [typeof(CameraBoom3D)] = new("Player Camera", "Camera", "Positions a child camera on a collision-aware third-person boom.", "spring arm orbit tps follow"),
        [typeof(Camera3D)] = new("Camera", "Camera", "Renders a perspective 3D game view.", "perspective fov"),
        [typeof(Camera2D)] = new("2D Camera", "Camera", "Renders a two-dimensional game view.", "orthographic zoom"),
        [typeof(ThirdPersonCamera3D)] = new("Legacy Third Person Camera", "Camera", "Legacy standalone follow camera kept for older projects.", "tps orbit follow", false, true),
        [typeof(ModelHierarchyInstance)] = new("Model", "Rendering", "References an imported model hierarchy.", "fbx mesh asset"),
        [typeof(VisualModelOverride)] = new("Visual Model Override", "Character", "Adjusts the Character Model child rotation and scale without changing the gameplay root or imported bones.", "model facing rotation scale visual override"),
        [typeof(FoliagePatch)] = new("Foliage Patch", "World", "Scatter static plants with simple wind sway around their grounded base.", "grass tree bush vegetation scatter wind"),
        [typeof(MeshRenderer)] = new("Mesh Renderer", "Rendering", "Draws a static 3D mesh.", "material primitive"),
        [typeof(SkeletalMeshRenderer)] = new("Skeletal Mesh Renderer", "Rendering", "Draws an animated skinned mesh.", "character bones model"),
        [typeof(SpriteRenderer)] = new("Sprite Renderer", "Rendering", "Draws a textured 2D sprite.", "image texture"),
        [typeof(UiCanvas)] = new("Canvas", "UI", "Screen-space root for HUDs and menus.", "ui overlay screen"),
        [typeof(UiWidget)] = new("UI Widget", "UI", "Panel, image, progress bar, or clickable button.", "ui image panel health bar button"),
        [typeof(UiText)] = new("Text", "UI", "Draws screen-space text using a font asset or the system default.", "label font bitmap ttf"),
        [typeof(UiAnimator)] = new("UI Animator", "UI", "Fades, slides or pulses a Text or Widget without changing its authored layout.", "ui transition fade slide pop pulse"),
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
        [typeof(HealthPickup3D)] = new("Health Pickup", "Gameplay", "Heals a player on trigger contact and is consumed once.", "pickup heal medkit"),
        [typeof(SkeletalRagdoll3D)] = new("Skeletal Ragdoll", "Animation", "Adds hit flinch and a short physics-assisted skeletal collapse on death. Add beside Health and a skinned model; a Rigidbody provides whole-body collision.", "ragdoll death hit reaction corpse"),
        [typeof(LifetimeComponent)] = new("Lifetime", "Gameplay", "Destroys its object after a configured duration.", "timer destroy despawn"),
        [typeof(Projectile3D)] = new("Projectile", "Gameplay", "Moves a swept projectile and damages health.", "bullet damage"),
        [typeof(ProjectileLauncher3D)] = new("Projectile Launcher", "Gameplay", "Creates reusable projectiles with a fire cooldown.", "weapon shoot fire"),
        [typeof(PlayerShooter3D)] = new("Player Shooter", "Gameplay", "Maps player fire input to a projectile launcher.", "weapon input"),
        [typeof(ArenaGameManager)] = new("Arena Game Manager", "Gameplay", "Tracks arena match state.", "game rules manager", false, true),
        [typeof(ByteEngine.Core.Construction.VehicleBuilder3D)] = new("Vehicle Builder 3D", "Gameplay", "Socket garage, saved builds, and arcade test driving using imported scrap parts.", "vehicle garage build car"),
        [typeof(WaveSpawner3D)] = new("Wave Spawner 3D", "Gameplay", "Spawns Blueprint enemies in timed waves and tracks their Health.", "wave horde enemy spawn"),
        [typeof(SimpleEnemyAI3D)] = new("Simple Enemy AI", "AI", "Chases and attacks a nearby player without navigation.", "enemy chase attack"),

        [typeof(AudioSource3D)] = new(
            "Audio Source 3D",
            "Audio",
            "Plays a PCM WAV clip as positional 3D audio or listener-relative non-spatial audio.",
            "sound sfx music wav speaker emitter spatial loop volume pitch"),

        [typeof(AudioListener3D)] = new(
            "Audio Listener 3D",
            "Audio",
            "Defines the listener position and orientation. Usually placed on the active game Camera.",
            "listener ears camera master volume"),

        [typeof(EventModuleComponent)] = new("Event Module", "Utility", "Runs a reusable ByteGraph event module.", "visual logic bytegraph"),
        [typeof(BlueprintInstance)] = new("Blueprint Instance", "Advanced", "Maintains the source and override state of a placed Blueprint.", "prefab source override", false, true)
    };

    private static readonly Dictionary<(Type, string), PropertyMetadata> Properties = new()
    {
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.EnemyBlueprint))] = new("Enemy Blueprint", "Enemy", "Blueprint spawned for every wave enemy."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.SpawnPointTagId))] = new("Spawn Point Tag", "Spawning", "Only active objects with this tag can be spawn points."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.SpawnMode))] = new("Spawn Mode", "Spawning", "Round Robin cycles points; Random selects any active point."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.AutoStart))] = new("Auto Start", "Spawning", "Begin Wave 1 automatically in Play."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.SpawnInterval))] = new("Spawn Interval", "Spawning", "Seconds between enemy spawns, including before the first.", "s"),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.MaxWaves))] = new("Max Waves", "Waves", "Number of waves to complete."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.FirstWaveCount))] = new("First Wave Count", "Waves", "Enemy count in Wave 1."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.EnemiesPerWave))] = new("Enemies Per Wave", "Waves", "Additional enemies each successive wave."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.WaveDelay))] = new("Wave Delay", "Waves", "Seconds between a cleared wave and the next.", "s"),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.FailureTargetId))] = new("Failure Target", "Failure", "Optional scene object whose Health death fails the run."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.FailureTargetName))] = new("Fallback Target Name", "Failure", "Legacy name fallback if the selected object cannot be resolved.", Advanced: true),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.StopOnTargetDeath))] = new("Stop On Target Death", "Failure", "Fail and stop spawning when the target dies."),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.State))] = new("State", "Runtime", "Current wave state.", ReadOnly: true),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.CurrentWave))] = new("Current Wave", "Runtime", "One-based current wave.", ReadOnly: true),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.WaveEnemyCount))] = new("Wave Enemy Count", "Runtime", "Scheduled enemies in the current wave.", ReadOnly: true),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.EnemiesSpawned))] = new("Enemies Spawned", "Runtime", "Enemies spawned in the current wave.", ReadOnly: true),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.EnemiesAlive))] = new("Enemies Alive", "Runtime", "Spawned enemies still alive.", ReadOnly: true),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.EnemiesRemaining))] = new("Enemies Remaining", "Runtime", "Still scheduled plus currently alive.", ReadOnly: true),
        [(typeof(WaveSpawner3D), nameof(WaveSpawner3D.TotalKilled))] = new("Total Killed", "Runtime", "Enemies killed across all waves.", ReadOnly: true),
        [(typeof(SkyEnvironment), nameof(SkyEnvironment.SmoothEdges))] = new("Smooth Edges", "Image Quality", "Lightweight spatial anti-aliasing for 3D. Slightly softens edges; no temporal ghosting."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.BrushRadius))] = new("Paint Brush Radius", "Painting", "Size of the cursor brush, not the size of individual plants.", "m"),
        [(typeof(FoliagePatch), nameof(FoliagePatch.PaintDensity))] = new("Paint Density", "Painting", "Target planting density for new strokes. Increase to place plants closer together; existing plants are unchanged.", "plants/m²"),
        [(typeof(FoliagePatch), nameof(FoliagePatch.UsePaintedLayout))] = new("Show Painted Plants (instead of scatter)", "Placement", "Chooses which saved layout is displayed. To paint, turn on the Scene View Paint Foliage brush. Switching layouts preserves both."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.Status))] = new("Status", "Foliage", "Cached patch status.", ReadOnly: true),
        [(typeof(FoliagePatch), nameof(FoliagePatch.Model))] = new("Plant Model", "Foliage", "Choose an imported static plant model."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.MaterialAsset))] = new("Material (optional)", "Foliage", "Choose a .bmat, or None to retain imported materials."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.Area))] = new("Area Width / Depth", "Placement", "Size in local X/Z metres.", "m"),
        [(typeof(FoliagePatch), nameof(FoliagePatch.Amount))] = new("Plant Amount", "Placement", "Start small. Maximum 2000; each model mesh part adds rendering work."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.Seed))] = new("Layout Seed", "Placement", "Change to reshuffle. Same seed gives the same layout."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.PlantHeight))] = new("Plant Size (Height)", "Placement", "Sets every plant to this height in metres, preserving its proportions. Updates existing and newly painted plants. Set Size Variation to 0 for an exact size.", "m"),
        [(typeof(FoliagePatch), nameof(FoliagePatch.SizeVariation))] = new("Size Variation", "Placement", "0.2 means plus/minus 20 percent."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.SnapToGround))] = new("Snap To Ground", "Placement", "Snap to non-trigger colliders within 20m vertically. Rebuild after editing ground."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.WindEnabled))] = new("Wind", "Wind", "Whole-plant sway around the base, not individual branch/leaf simulation."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.WindStrength))] = new("Wind Strength", "Wind", "Maximum sway angle; use small values for trees.", "degrees"),
        [(typeof(FoliagePatch), nameof(FoliagePatch.WindSpeed))] = new("Wind Speed", "Wind", "Speed of the sway; 0 freezes movement."),
        [(typeof(FoliagePatch), nameof(FoliagePatch.ViewDistance))] = new("View Distance", "Performance", "Skip plants beyond this distance.", "m"),
        [(typeof(FoliagePatch), nameof(FoliagePatch.CastShadows))] = new("Cast Shadows", "Performance", "Disable for dense grass to reduce shadow cost."),
        [(typeof(UiCanvas), nameof(UiCanvas.ScaleMode))] = new("Scale Mode", "Layout", "Scale with the screen or keep constant pixel sizes."),
        [(typeof(UiCanvas), nameof(UiCanvas.ReferenceResolution))] = new("Reference Resolution", "Layout", "Screen size used to author the UI."),
        [(typeof(UiCanvas), nameof(UiCanvas.UserScale))] = new("UI Scale", "Layout", "Extra scale multiplier."),
        [(typeof(UiCanvas), nameof(UiCanvas.SafeAreaInsets))] = new("Safe Area (L,T,R,B)", "Layout", "Inset UI from screen edges in reference pixels."),
        [(typeof(UiCanvas), nameof(UiCanvas.Language))] = new("Language", "Localization", "Current language code, such as en or fr. Can be changed with Set UI Language."),
        [(typeof(UiCanvas), nameof(UiCanvas.FallbackLanguage))] = new("Fallback Language", "Localization", "Language used when a key is missing in the current language."),
        [(typeof(UiCanvas), nameof(UiCanvas.TranslationsJson))] = new("Translations", "Localization", "JSON map of language codes to key/value strings, for example: { \"en\": { \"menu.play\": \"Play\" }, \"fr\": { \"menu.play\": \"Jouer\" } }. Invalid JSON leaves authored text visible."),
        [(typeof(UiWidget), nameof(UiWidget.StretchHorizontal))] = new("Stretch Horizontal", "Layout", "Fill parent width using Offset X as side margin."),
        [(typeof(UiWidget), nameof(UiWidget.StretchVertical))] = new("Stretch Vertical", "Layout", "Fill parent height using Offset Y as top/bottom margin."),
        [(typeof(UiWidget), nameof(UiWidget.ImageReference))] = new("Image", "Appearance", "Texture used by an Image widget."),
        [(typeof(UiWidget), nameof(UiWidget.FontReference))] = new("Font", "Appearance", "Font used by a Button label."),
        [(typeof(UiWidget), nameof(UiWidget.Color))] = new("Background", "Appearance", "Background color and opacity."),
        [(typeof(UiWidget), nameof(UiWidget.HoverColor))] = new("Hover Color", "Appearance", "Button color when hovered or focused."),
        [(typeof(UiWidget), nameof(UiWidget.PressedColor))] = new("Pressed Color", "Appearance", "Button color while pressed."),
        [(typeof(UiWidget), nameof(UiWidget.DisabledColor))] = new("Disabled Color", "Appearance", "Button color while disabled."),
        [(typeof(UiWidget), nameof(UiWidget.FillColor))] = new("Fill", "Appearance", "Progress bar fill color."),
        [(typeof(UiWidget), nameof(UiWidget.LabelKey))] = new("Label Key", "Localization", "Key in the parent Canvas translation table; Label is the fallback."),
        [(typeof(UiText), nameof(UiText.Text))] = new("Text", "Content", "Text displayed in the Canvas. Use Enter for a new line."),
        [(typeof(UiText), nameof(UiText.LocalizationKey))] = new("Localization Key", "Localization", "Key in the parent Canvas translation table; Text is the fallback."),
        [(typeof(UiText), nameof(UiText.FontReference))] = new("Font", "Appearance", "Choose a TTF/OTF font or a text-format BMFont .fnt asset. Leave empty for the system font."),
        [(typeof(UiText), nameof(UiText.FontSize))] = new("Font Size", "Appearance", "Text size in screen pixels."),
        [(typeof(UiText), nameof(UiText.Color))] = new("Color", "Appearance", "Text color and opacity."),
        [(typeof(UiText), nameof(UiText.ShadowColor))] = new("Shadow Color", "Appearance", "Optional text shadow color and opacity."),
        [(typeof(UiText), nameof(UiText.ShadowOffset))] = new("Shadow Offset", "Appearance", "Shadow distance in reference pixels."),
        [(typeof(UiText), nameof(UiText.OutlineColor))] = new("Outline Color", "Appearance", "Optional text outline color and opacity."),
        [(typeof(UiText), nameof(UiText.OutlineWidth))] = new("Outline Width", "Appearance", "Outline width from 0 to 4 reference pixels."),
        [(typeof(UiText), nameof(UiText.Anchor))] = new("Anchor", "Layout", "Position and align the text block relative to the screen."),
        [(typeof(UiText), nameof(UiText.Offset))] = new("Offset", "Layout", "Signed pixel offset from the selected anchor."),
        [(typeof(UiText), nameof(UiText.WrapWidth))] = new("Wrap Width", "Layout", "Maximum line width in pixels. Zero disables wrapping."),
        [(typeof(UiAnimator), nameof(UiAnimator.Preset))] = new("Preset", "Animation", "Fade, slide, pop or pulse."),
        [(typeof(UiAnimator), nameof(UiAnimator.Duration))] = new("Duration", "Animation", "Transition length in seconds."),
        [(typeof(UiAnimator), nameof(UiAnimator.Delay))] = new("Delay", "Animation", "Seconds to wait before moving."),
        [(typeof(UiAnimator), nameof(UiAnimator.Distance))] = new("Slide Distance", "Animation", "How far a slide travels in Canvas reference pixels."),
        [(typeof(UiAnimator), nameof(UiAnimator.AutoPlay))] = new("Play On Start", "Animation", "Play when the scene starts."),
        [(typeof(UiAnimator), nameof(UiAnimator.Loop))] = new("Loop", "Animation", "Repeat this preset."),
        [(typeof(UiAnimator), nameof(UiAnimator.HideOnComplete))] = new("Hide After Fade Out", "Animation", "Hide the Text or Widget when Fade Out finishes."),
        [(typeof(CapsuleCollider3D), nameof(CapsuleCollider3D.VisualBounds))] = new("Visual Bounds", "Diagnostics", "Measured bounds from capsule auto-fit.", Advanced: true, ReadOnly: true),
        [(typeof(CapsuleCollider3D), nameof(CapsuleCollider3D.AutoFitSource))] = new("Auto-Fit Source", "Diagnostics", "Source used by capsule auto-fit.", Advanced: true, ReadOnly: true),
        [(typeof(ModelHierarchyInstance), nameof(ModelHierarchyInstance.MaterialOverride))] = new("Material Override (All Mesh Parts)", "Rendering", "Assign a .bmat to override this model's static and animated mesh parts. None keeps imported/per-part materials. No additional Mesh Renderer is needed."),
        [(typeof(ModelHierarchyInstance), nameof(ModelHierarchyInstance.AppliedImportScale))] = new("Applied Import Scale", "Diagnostics", "Legacy editor scale metadata; new imports apply units inside the model.", Advanced: true, ReadOnly: true),
        [(typeof(ModelHierarchyInstance), nameof(ModelHierarchyInstance.AutoGrounded))] = new("Auto Grounded", "Diagnostics", "Model-only feet-origin offset has been applied once.", Advanced: true, ReadOnly: true),
        [(typeof(VisualModelOverride), nameof(VisualModelOverride.ImportScale))] = new("Import Unit Scale", "Model", "Legacy import-scale metadata; new models store unit conversion in the imported hierarchy.", ReadOnly: true),
        [(typeof(VisualModelOverride), nameof(VisualModelOverride.RotationDegrees))] = new("Visual Rotation", "Model", "Model-only rotation in degrees."),
        [(typeof(VisualModelOverride), nameof(VisualModelOverride.ScaleMultiplier))] = new("Visual Scale", "Model", "Model-only authored scale; 1 means the imported physical size."),


        [(typeof(AudioSource3D), nameof(AudioSource3D.ClipReference))] = new("Audio Clip", "Audio", "Drag a PCM WAV asset here. Mono WAV is recommended for positional 3D audio."),
        [(typeof(AudioSource3D), nameof(AudioSource3D.PlayOnStart))] = new("Play On Start", "Playback", "Start playing automatically when the scene starts."),
        [(typeof(AudioSource3D), nameof(AudioSource3D.Loop))] = new("Loop", "Playback", "Repeat playback continuously."),
        [(typeof(AudioSource3D), nameof(AudioSource3D.Spatial))] = new("Spatial 3D", "Spatial", "Position the sound in world space. Disable for music and UI."),
        [(typeof(AudioSource3D), nameof(AudioSource3D.Volume))] = new("Volume", "Playback", "Source gain. 1 is unchanged; values above 1 amplify."),
        [(typeof(AudioSource3D), nameof(AudioSource3D.Pitch))] = new("Pitch", "Playback", "Playback pitch multiplier."),
        [(typeof(AudioSource3D), nameof(AudioSource3D.MinDistance))] = new("Reference Distance", "Spatial", "Distance where attenuation begins.", "m"),
        [(typeof(AudioSource3D), nameof(AudioSource3D.MaxDistance))] = new("Maximum Distance", "Spatial", "Distance used by inverse-clamped attenuation.", "m"),
        [(typeof(AudioSource3D), nameof(AudioSource3D.RolloffFactor))] = new("Rolloff", "Spatial", "How strongly volume falls with distance."),
        [(typeof(AudioSource3D), nameof(AudioSource3D.ClipLoaded))] = new("Clip Loaded", "Diagnostics", "True when the WAV was decoded and uploaded to an OpenAL buffer.", ReadOnly: true),
        [(typeof(AudioSource3D), nameof(AudioSource3D.BackendAvailable))] = new("Audio Backend Ready", "Diagnostics", "True when an OpenAL output device and context are available.", ReadOnly: true),
        [(typeof(AudioSource3D), nameof(AudioSource3D.BackendStatus))] = new("Audio Backend Status", "Diagnostics", "OpenAL device state or the exact backend error.", ReadOnly: true),
        [(typeof(AudioSource3D), nameof(AudioSource3D.IsPlaying))] = new("Is Playing", "Diagnostics", "Whether OpenAL reports this source as currently playing.", Advanced: true, ReadOnly: true),
        [(typeof(AudioSource3D), nameof(AudioSource3D.DurationSeconds))] = new("Duration", "Diagnostics", "Loaded clip duration in seconds.", "s", Advanced: true, ReadOnly: true),
        [(typeof(AudioListener3D), nameof(AudioListener3D.Volume))] = new("Master Volume", "Listener", "Listener gain applied to all audio."),
        [(typeof(AudioListener3D), nameof(AudioListener3D.IsActiveListener))] = new("Active Listener", "Diagnostics", "True when this is the first enabled listener in scene order.", Advanced: true, ReadOnly: true),

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
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.HideFirstPersonBody))] = new("Hide Body In First Person", "First Person", "Leave OFF for arms-only models. ON hides player meshes except those parented under Camera."),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.FirstPersonCameraOffset))] = new("Additional FPS Offset", "First Person", "Optional extra offset: X right, Y up, Z backward. Normally position Camera directly in the Blueprint.", "m", Advanced: true),
        [(typeof(CameraBoom3D), nameof(CameraBoom3D.FirstPerson))] = new("First Person Camera", "Camera", "Uses Camera position authored in Blueprint. Look rotates Camera and its child arms/weapons. Ignores TPS distance, shoulder offset, collision and lag."),
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

