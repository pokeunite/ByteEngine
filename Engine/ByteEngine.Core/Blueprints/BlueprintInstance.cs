using ByteEngine.Core.Assets;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Blueprints;

public sealed class BlueprintInstance : Component, IRuntimeDiagnosticSource
{
    public AssetReference Blueprint { get; set; } = AssetReference.Empty;
    public Guid InstanceId { get; set; } = Guid.NewGuid();
    public string SourceSnapshot { get; set; } = string.Empty;
    public Dictionary<Guid, Guid> ObjectMap { get; } = new();
    public int ModifiedPropertyCount { get; set; }
    public int AddedComponentCount { get; set; }
    public int RemovedComponentCount { get; set; }
    public int AddedChildCount { get; set; }
    public int RemovedChildCount { get; set; }
    public string LastPropagation { get; set; } = "Not synchronized";
    public int OverrideCount => ModifiedPropertyCount + AddedComponentCount + RemovedComponentCount +
        AddedChildCount + RemovedChildCount;

    public void WriteDiagnostics(RuntimeDiagnosticWriter writer)
    {
        writer.Section($"BlueprintInstance: {AttachedGameObject?.Name ?? "<detached>"}");
        writer.Add("Blueprint.Guid", Blueprint.Guid);
        writer.Add("Blueprint.Path", Blueprint.CachedProjectPath ?? "<none>");
        writer.Add("Instance.InstanceId", InstanceId);
        writer.Add("Overrides.Properties", ModifiedPropertyCount);
        writer.Add("Overrides.ComponentsAdded", AddedComponentCount);
        writer.Add("Overrides.ComponentsRemoved", RemovedComponentCount);
        writer.Add("Overrides.ChildrenAdded", AddedChildCount);
        writer.Add("Overrides.ChildrenRemoved", RemovedChildCount);
        writer.Add("LastPropagation", LastPropagation);
    }
}
