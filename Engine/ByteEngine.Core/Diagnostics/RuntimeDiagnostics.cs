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
    private static readonly object FootIkTraceLock = new();
    private static readonly Queue<string> FootIkTrace = new();
    private const int MaxFootIkTraceLines = 600;
    public static bool DebugFootIk { get; set; }

    private static readonly object BlueprintVisibilityTraceLock = new();
    private static readonly Queue<string> BlueprintVisibilityTrace = new();
    private const int MaxBlueprintVisibilityTraceLines = 400;
    public static bool DebugBlueprintVisibility { get; set; }

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

}
