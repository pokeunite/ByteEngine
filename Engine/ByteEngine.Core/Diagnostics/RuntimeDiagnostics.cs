using System.Numerics;
using System.Text;
using ByteEngine.Core.Scene;
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

    public static string CreateDump(RuntimeScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var writer = new RuntimeDiagnosticWriter();
        writer.Section("ByteEngine Runtime Diagnostics");
        writer.Add("Scene", $"{scene.Name} ({scene.Id})");
        writer.Add("Loaded", scene.IsLoaded);
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
    }
}
