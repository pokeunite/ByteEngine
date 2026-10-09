using System.Text.Json;

namespace ByteEngine.Core.Runtime;

/// <summary>Project/build-scoped save files, usable by every game without a plugin dependency.</summary>
public sealed class GameSaveService
{
    private readonly string _root;
    public GameSaveService(string projectRoot) : this(GameSaveStorage.GetDirectory(projectRoot), true) { }
    private GameSaveService(string directory, bool _) => _root = Path.GetFullPath(directory);
    public static GameSaveService FromDirectory(string directory) => new(directory, true);
    private string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.IsPathRooted(key)) throw new ArgumentException("Save keys must be relative paths.", nameof(key));
        string path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Save keys cannot leave the save directory.", nameof(key));
        return path;
    }
    public bool Exists(string key) => File.Exists(Resolve(key));
    public string LoadText(string key) => File.ReadAllText(Resolve(key));
    public T? Load<T>(string key) => JsonSerializer.Deserialize<T>(LoadText(key));
    public void Save<T>(string key, T value) => SaveText(key, JsonSerializer.Serialize(value));
    public void SaveText(string key, string value)
    {
        string path = Resolve(key); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, value); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public void Delete(string key) => File.Delete(Resolve(key));
}
