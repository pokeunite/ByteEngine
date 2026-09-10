namespace ByteEngine.Core.Serialization.SerializationModels;

public sealed class ProjectData
{
    public string Name { get; set; } =
        "Untitled";

    public Guid ProjectId { get; set; } =
        Guid.NewGuid();

    public string EngineVersion { get; set; } =
        ByteEngineInfo.Version;

    public string StartupScene { get; set; } =
        "Scenes/Main.bytescene";

    public ProjectWindowData Window { get; set; } =
        new();

    public string AssetDirectory { get; set; } =
        "Assets";

    public string SceneDirectory { get; set; } =
        "Scenes";
}

public sealed class ProjectWindowData
{
    public int Width { get; set; } =
        1280;

    public int Height { get; set; } =
        720;
}
