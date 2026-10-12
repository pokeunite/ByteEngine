using System.Numerics;
using System.Text.Json;

namespace ByteEngine.Editor;

internal sealed record BlueprintViewportState(float X, float Y, float Z, float Yaw, float Pitch, float FieldOfView)
{
    public static BlueprintViewportState Capture(EditorCamera3D camera) => new(camera.Position.X, camera.Position.Y, camera.Position.Z, camera.Yaw, camera.Pitch, camera.FieldOfView);
    public bool Valid => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && float.IsFinite(Yaw) &&
        float.IsFinite(Pitch) && Pitch is >= -89 and <= 89 && FieldOfView is >= 1 and <= 170;
    public void Apply(EditorCamera3D camera)
    {
        if (!Valid) return;
        camera.Position = new Vector3(X, Y, Z); camera.Yaw = Yaw; camera.Pitch = Pitch; camera.FieldOfView = FieldOfView;
    }
}

// Navigation preferences are separate from Blueprint defaults and game camera transforms.
internal sealed class BlueprintViewportStateStore
{
    private readonly string _path;
    private Dictionary<string, BlueprintViewportState> _views = new();
    public BlueprintViewportStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ByteEngine", "Editor", "blueprint-views.json");
        try { if (File.Exists(_path)) _views = JsonSerializer.Deserialize<Dictionary<string, BlueprintViewportState>>(File.ReadAllText(_path)) ?? new(); }
        catch { /* Corrupt convenience preferences must not prevent opening an asset. */ }
    }
    private static string Key(string project, Guid asset) => Path.GetFullPath(project).ToUpperInvariant() + "|" + asset.ToString("N");
    public bool Restore(string project, Guid asset, EditorCamera3D camera)
    {
        if (!_views.TryGetValue(Key(project, asset), out var view) || view is null || !view.Valid) return false;
        view.Apply(camera); return true;
    }
    public void Save(string project, Guid asset, EditorCamera3D camera)
    {
        var view = BlueprintViewportState.Capture(camera); if (!view.Valid) return;
        _views[Key(project, asset)] = view;
        while (_views.Count > 1024) _views.Remove(_views.Keys.First());
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(_views)); File.Move(_path + ".tmp", _path, true);
        }
        catch { /* Editor navigation is best-effort and must never block saving gameplay assets. */ }
    }
}
