using System.Numerics;
using System.Text;
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
    private static RuntimeScene? _socketTraceScene;
    private static DateTime _socketTraceStartUtc;
    private static DateTime _lastSocketTraceSampleUtc;
    private static int _socketTraceSamples;
    private static readonly StringBuilder SocketTrace = new();


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

    internal static void Update(RuntimeScene scene)
    {
        if (Input.IsKeyPressed(Key.F8)) Dump(scene);

        DateTime now = DateTime.UtcNow;
        if (Input.IsKeyPressed(Key.F9))
        {
            if (_socketTraceScene != null) FinishSocketTrace("stopped by F9");
            else StartSocketTrace(scene, now);
        }

        if (_socketTraceScene == null) return;
        if (!ReferenceEquals(_socketTraceScene, scene))
        {
            FinishSocketTrace("scene changed");
            return;
        }

        if (_socketTraceSamples == 0 ||
            (now - _lastSocketTraceSampleUtc).TotalMilliseconds >= 66)
        {
            _lastSocketTraceSampleUtc = now;
            _socketTraceSamples++;
            string elapsed = (now - _socketTraceStartUtc).TotalSeconds
                .ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
            GameObject[] attached = scene.GameObjects.Where(item => item.IsAttached).ToArray();
            if (attached.Length == 0)
                SocketTrace.AppendLine($"t={elapsed} attachments=none");
            foreach (GameObject child in attached)
            {
                try
                {
                    SocketTrace.Append("t=").Append(elapsed).Append(' ')
                        .AppendLine(SkeletalAttachmentService.BuildTraceSample(child));
                }
                catch (Exception error)
                {
                    SocketTrace.Append("t=").Append(elapsed).Append(" object=")
                        .Append(child.Name).Append(" traceError=").AppendLine(error.Message);
                }
            }
        }

        if ((now - _socketTraceStartUtc).TotalSeconds >= 6 || _socketTraceSamples >= 90)
            FinishSocketTrace("six-second capture complete");
    }

    private static void StartSocketTrace(RuntimeScene scene, DateTime now)
    {
        _socketTraceScene = scene;
        _socketTraceStartUtc = now;
        _lastSocketTraceSampleUtc = now;
        _socketTraceSamples = 0;
        SocketTrace.Clear();
        SocketTrace.AppendLine("[Socket Trace] scene=" + scene.Name);
        SocketTrace.AppendLine("Sampled after scene animation, physics and attachment updates; " +
            "position/rotation errors compare the live gun to the same-frame socket.");
        Emit("Socket trace recording for six seconds. Move and jump now; press F9 again to stop early.");
    }

    private static void FinishSocketTrace(string reason)
    {
        if (_socketTraceScene == null) return;
        SocketTrace.Append("[Socket Trace End] ").Append(reason)
            .Append("; samples=").AppendLine(_socketTraceSamples.ToString());
        _socketTraceScene = null;
        Emit(SocketTrace.ToString());
    }

    private static void Emit(string message)
    {
        if (OutputSink != null) OutputSink(message);
        else Console.WriteLine(message);
    }
}
