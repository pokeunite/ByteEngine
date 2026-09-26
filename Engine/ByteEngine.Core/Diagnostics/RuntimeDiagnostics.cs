using System.Numerics;
using System.Text;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.InputSystem;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Diagnostics;

public interface IRuntimeDiagnosticSource
{
    void WriteDiagnostics(RuntimeDiagnosticWriter writer);
}

public sealed class RuntimeDiagnosticWriter
{
    private readonly StringBuilder _text = new();

    public void Section(string name) => _text.Append('[').Append(name).AppendLine("]");
    public void Add(string name, object? value) => _text.Append(name).Append(": ").AppendLine(Format(value));
    public override string ToString() => _text.ToString();

    private static string Format(object? value) => value switch
    {
        null => "<null>",
        Vector2 vector => $"({vector.X:0.###}, {vector.Y:0.###})",
        Vector3 vector => $"({vector.X:0.###}, {vector.Y:0.###}, {vector.Z:0.###})",
        Quaternion quaternion => $"({quaternion.X:0.###}, {quaternion.Y:0.###}, {quaternion.Z:0.###}, {quaternion.W:0.###})",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
    };
}

public static class RuntimeDiagnostics
{
    public static Action<string>? OutputSink { get; set; }

    private static readonly object FootIkTraceLock = new();
    private static readonly Queue<string> FootIkTrace = new();
    private const int MaxFootIkTraceLines = 600;
    public static bool DebugFootIk { get; set; }

    private static readonly object BlueprintVisibilityTraceLock = new();
    private static readonly Queue<string> BlueprintVisibilityTrace = new();
    private const int MaxBlueprintVisibilityTraceLines = 400;
    public static bool DebugBlueprintVisibility { get; set; }

    private static readonly object TpsJitterTraceLock = new();
    private static readonly Queue<string> TpsJitterTrace = new();
    private static readonly Dictionary<Guid, TpsJitterPreviousSample> TpsJitterPrevious = new();
    private const int MaxTpsJitterTraceLines = 1200;
    public static bool DebugTpsJitter { get; set; }

    private static readonly object WeaponRaycastTraceLock = new();
    private static readonly Queue<string> WeaponRaycastTrace = new();
    private const int MaxWeaponRaycastTraceLines = 1000;
    public static bool DebugWeaponRaycast { get; set; }

    public static void ClearBlueprintVisibilityTrace()
    {
        lock (BlueprintVisibilityTraceLock) BlueprintVisibilityTrace.Clear();
    }

    public static string GetBlueprintVisibilityTrace()
    {
        lock (BlueprintVisibilityTraceLock)
            return string.Join(Environment.NewLine, BlueprintVisibilityTrace);
    }

    public static void RecordBlueprintVisibility(string message)
    {
        if (!DebugBlueprintVisibility) return;
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        lock (BlueprintVisibilityTraceLock)
        {
            BlueprintVisibilityTrace.Enqueue(line);
            while (BlueprintVisibilityTrace.Count > MaxBlueprintVisibilityTraceLines)
                BlueprintVisibilityTrace.Dequeue();
        }
    }

    public static void ClearFootIkTrace()
    {
        lock (FootIkTraceLock) FootIkTrace.Clear();
    }

    public static string GetFootIkTrace()
    {
        lock (FootIkTraceLock) return string.Join(Environment.NewLine, FootIkTrace);
    }

    public static void RecordFootIk(string message)
    {
        if (!DebugFootIk) return;
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        lock (FootIkTraceLock)
        {
            FootIkTrace.Enqueue(line);
            while (FootIkTrace.Count > MaxFootIkTraceLines) FootIkTrace.Dequeue();
        }
    }

    public static void ClearTpsJitterTrace()
    {
        lock (TpsJitterTraceLock)
        {
            TpsJitterTrace.Clear();
            TpsJitterPrevious.Clear();
        }
    }

    public static string GetTpsJitterTrace()
    {
        lock (TpsJitterTraceLock)
            return string.Join(Environment.NewLine, TpsJitterTrace);
    }

    /// <summary>
    /// Captures one end-of-frame TPS sample. SceneManager calls this only after
    /// the complete scene update, physics and LateUpdate camera pass have run.
    /// The trace intentionally compares gameplay-root, camera, skeletal-model
    /// and skeleton-root movement so we can tell which layer is actually
    /// oscillating when visible jitter occurs.
    /// </summary>
    public static void SampleTpsJitter(
        RuntimeScene scene)
    {
        if (!DebugTpsJitter)
        {
            lock (TpsJitterTraceLock)
                TpsJitterPrevious.Clear();
            return;
        }

        foreach (GameObject root in scene.GameObjects)
        {
            if (!root.ActiveInHierarchy)
                continue;

            PlayerController3D? player =
                root.GetComponent<PlayerController3D>();

            CharacterController3D? motor =
                root.GetComponent<CharacterController3D>();

            CameraBoom3D? boom =
                root.GetComponent<CameraBoom3D>();

            if (player?.Enabled != true ||
                motor?.Enabled != true ||
                boom?.Enabled != true)
            {
                continue;
            }

            Camera3D? camera =
                scene.ActiveCamera;

            SkeletalMeshRenderer? skeletal =
                scene.GameObjects
                    .Where(item =>
                        item.ActiveInHierarchy &&
                        (ReferenceEquals(item, root) ||
                         item.IsDescendantOf(root)))
                    .SelectMany(item =>
                        item.Components.OfType<SkeletalMeshRenderer>())
                    .FirstOrDefault(renderer =>
                        renderer.Enabled &&
                        renderer.Visible);

            Vector3 rootPosition =
                root.Transform.WorldPosition;

            float rootYaw =
                root.Transform.EulerAngles.Y;

            Vector3 cameraPosition =
                camera?.GameObject.Transform.WorldPosition ??
                Vector3.Zero;

            Vector3 modelPosition =
                skeletal?.Transform.WorldPosition ??
                Vector3.Zero;

            Vector3 rootBonePosition =
                Vector3.Zero;

            Matrix4x4 rootBoneWorld =
                Matrix4x4.Identity;

            bool hasRootBone =
                skeletal != null &&
                !string.IsNullOrWhiteSpace(
                    skeletal.RootMotionNodeName) &&
                skeletal.TryGetBoneWorldMatrix(
                    skeletal.RootMotionNodeName,
                    out rootBoneWorld);

            if (hasRootBone)
            {
                rootBonePosition =
                    rootBoneWorld.Translation;
            }

            Vector2 move =
                InputActions.ReadAxis2D(
                    player.MoveAction);

            Vector2 look =
                InputActions.ReadAxis2D(
                    player.LookAction);

            Vector2 mouse =
                Input.MouseDelta;

            TpsJitterPrevious.TryGetValue(
                root.Id,
                out TpsJitterPreviousSample previous);

            Vector3 rootDelta =
                previous.Valid
                    ? rootPosition - previous.RootPosition
                    : Vector3.Zero;

            Vector3 cameraDelta =
                previous.Valid
                    ? cameraPosition - previous.CameraPosition
                    : Vector3.Zero;

            Vector3 modelDelta =
                previous.Valid
                    ? modelPosition - previous.ModelPosition
                    : Vector3.Zero;

            Vector3 rootBoneDelta =
                previous.Valid &&
                previous.HasRootBone &&
                hasRootBone
                    ? rootBonePosition - previous.RootBonePosition
                    : Vector3.Zero;

            float yawDelta =
                previous.Valid
                    ? PlayerController3D.DeltaAngle(
                        previous.RootYaw,
                        rootYaw)
                    : 0f;

            float controlYawDelta =
                previous.Valid
                    ? PlayerController3D.DeltaAngle(
                        previous.ControlYaw,
                        player.ControlYaw)
                    : 0f;

            float dtMilliseconds =
                (float)Time.DeltaTime *
                1000f;

            bool frameSpike =
                dtMilliseconds > 28f;

            bool rootSpike =
                rootDelta.Length() > .20f;

            bool modelSpike =
                modelDelta.Length() > .20f;

            bool boneSpike =
                rootBoneDelta.Length() > .20f;

            bool yawSpike =
                MathF.Abs(yawDelta) > 12f;

            string flags =
                string.Join(
                    ",",
                    new[]
                    {
                        frameSpike ? "DT" : null,
                        rootSpike ? "ROOT" : null,
                        modelSpike ? "MODEL" : null,
                        boneSpike ? "BONE" : null,
                        yawSpike ? "YAW" : null
                    }
                    .Where(value => value != null));

            if (flags.Length == 0)
                flags = "-";

            string line =
                $"t={Time.TotalTime:0.000} " +
                $"dt={dtMilliseconds:0.00}ms " +
                $"flags={flags} " +
                $"player={root.Name} " +
                $"move={V2(move)} look={V2(look)} mouse={V2(mouse)} " +
                $"root={V3(rootPosition)} dRoot={V3(rootDelta)} " +
                $"yaw={rootYaw:0.00} dYaw={yawDelta:0.00} " +
                $"ctrl=({player.ControlYaw:0.00},{player.ControlPitch:0.00}) dCtrlYaw={controlYawDelta:0.00} " +
                $"vel={V3(motor.Velocity)} speed={motor.Speed:0.000} grounded={motor.IsGrounded} ground={motor.GroundObject?.Name ?? "<none>"} " +
                $"cam={V3(cameraPosition)} dCam={V3(cameraDelta)} " +
                $"boomYaw={boom.DesiredBoomYaw:0.00}/{boom.SmoothedBoomYaw:0.00} " +
                $"boomPitch={boom.DesiredBoomPitch:0.00}/{boom.SmoothedBoomPitch:0.00} " +
                $"boomSocket={V3(boom.ActualSocketPosition)} arm={boom.ActualArmLength:0.000} " +
                $"model={V3(modelPosition)} dModel={V3(modelDelta)} " +
                $"anim={skeletal?.CurrentAnimation ?? "<none>"} animTime={skeletal?.PlaybackTime ?? 0f:0.000} " +
                $"blend={skeletal?.ActiveBlendSpaceName ?? "<none>"} " +
                $"rootTravel={V3(skeletal?.ModelSpaceRootTravel ?? Vector3.Zero)} " +
                $"rootBone={(hasRootBone ? V3(rootBonePosition) : "<none>")} " +
                $"dRootBone={(hasRootBone ? V3(rootBoneDelta) : "<none>")}";

            lock (TpsJitterTraceLock)
            {
                TpsJitterTrace.Enqueue(
                    $"[{DateTime.Now:HH:mm:ss.fff}] {line}");

                while (TpsJitterTrace.Count >
                       MaxTpsJitterTraceLines)
                {
                    TpsJitterTrace.Dequeue();
                }

                TpsJitterPrevious[root.Id] =
                    new TpsJitterPreviousSample(
                        true,
                        rootPosition,
                        cameraPosition,
                        modelPosition,
                        rootBonePosition,
                        hasRootBone,
                        rootYaw,
                        player.ControlYaw);
            }
        }
    }

    public static void ClearWeaponRaycastTrace()
    {
        lock (WeaponRaycastTraceLock)
            WeaponRaycastTrace.Clear();
    }

    public static string GetWeaponRaycastTrace()
    {
        lock (WeaponRaycastTraceLock)
            return string.Join(Environment.NewLine, WeaponRaycastTrace);
    }

    /// <summary>
    /// Records concise weapon/raycast diagnostics only while the dedicated
    /// debug option is enabled. Callers provide already-resolved world-space
    /// values so the trace exposes exactly which transform/direction the
    /// gameplay system used instead of trying to reconstruct it later.
    /// </summary>
    public static void RecordWeaponRaycast(
        string message)
    {
        if (!DebugWeaponRaycast)
            return;

        string line =
            $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

        lock (WeaponRaycastTraceLock)
        {
            WeaponRaycastTrace.Enqueue(line);

            while (WeaponRaycastTrace.Count >
                   MaxWeaponRaycastTraceLines)
            {
                WeaponRaycastTrace.Dequeue();
            }
        }
    }

    public static string CreateDump(RuntimeScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var writer = new RuntimeDiagnosticWriter();
        writer.Section("ByteEngine Runtime Diagnostics");
        writer.Add("Scene", $"{scene.Name} ({scene.Id})");
        writer.Add("Loaded", scene.IsLoaded);
        writer.Section("Input Actions");
        writer.Add("Gameplay Enabled", InputActions.GameplayEnabled);
        writer.Add("Map", $"{InputActions.Map.Name} ({InputActions.Map.Id})");
        foreach (InputActionDefinition action in InputActions.Map.Actions)
        {
            InputActionState state = InputActions.Get(action.Id);
            writer.Add($"{action.Group}/{action.DisplayName}",
                $"type={action.Type}, id={action.Id}, bindings={action.Bindings.Count}, down={state.Down}, pressed={state.Pressed}, released={state.Released}, axis1D={state.Axis1D:0.###}, axis2D=({state.Axis2D.X:0.###}, {state.Axis2D.Y:0.###}), source={state.ActiveSource}");
        }
        writer.Section("Tags & Layers");
        writer.Add("Tags", string.Join(", ", scene.Classification.Tags.Select(item => item.Name)));
        writer.Add("Layers", string.Join(", ", scene.Classification.Layers.OrderBy(item => item.Index)
            .Select(item => $"{item.Index} {item.Name}")));
        foreach (GameObject gameObject in scene.GameObjects)
        {
            string tags = string.Join(", ", gameObject.Tags.Select(id => scene.Classification.FindTag(id)?.Name ?? $"Missing Tag {id}"));
            string layer = scene.Classification.FindLayer(gameObject.Layer)?.Name ?? "Default";
            writer.Add(gameObject.Name, $"Tags: {(tags.Length == 0 ? "None" : tags)}; Layer: {layer}");
        }
        foreach (var layer in scene.Classification.Layers.OrderBy(item => item.Index))
        {
            string interactions = string.Join(", ", scene.Classification.Layers
                .Where(other => scene.Classification.CollisionMatrix.ShouldInteract(layer.Index, other.Index))
                .Select(other => other.Name));
            writer.Add($"{layer.Name} interacts with", interactions);
        }
        foreach (GameObject attached in scene.GameObjects.Where(item => item.IsAttached))
            SkeletalAttachmentService.WriteDiagnostics(attached, writer);
        int sources = 0;
        foreach (GameObject gameObject in scene.GameObjects)
            foreach (IRuntimeDiagnosticSource source in gameObject.Components.OfType<IRuntimeDiagnosticSource>())
            {
                source.WriteDiagnostics(writer);
                sources++;
            }
        if (sources == 0) writer.Add("DiagnosticSources", "None");
        writer.Section("End Runtime Diagnostics");
        return writer.ToString();
    }

    public static void Dump(RuntimeScene scene)
    {
        string message = "[Runtime Diagnostics]" + Environment.NewLine + CreateDump(scene);
        if (OutputSink != null) OutputSink(message);
        else Console.WriteLine(message);
    }

    private static string V2(
        Vector2 value) =>
        $"({value.X:0.000},{value.Y:0.000})";

    private static string V3(
        Vector3 value) =>
        $"({value.X:0.000},{value.Y:0.000},{value.Z:0.000})";

    private readonly record struct TpsJitterPreviousSample(
        bool Valid,
        Vector3 RootPosition,
        Vector3 CameraPosition,
        Vector3 ModelPosition,
        Vector3 RootBonePosition,
        bool HasRootBone,
        float RootYaw,
        float ControlYaw);
}
