using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Editor.Commands;

internal interface IEditorHistoryEntry
{
    string Name { get; }
    int BeforeRevision { get; }
    int AfterRevision { get; }
}

internal readonly record struct TransformValue(System.Numerics.Vector3 Position,
    System.Numerics.Quaternion Rotation, System.Numerics.Vector3 Scale);
internal sealed record TransformEditorCommand(string Name,
    Dictionary<Guid, TransformValue> Before, Dictionary<Guid, TransformValue> After,
    int BeforeRevision, int AfterRevision) : IEditorHistoryEntry;

internal interface IEditorCommand : IEditorHistoryEntry
{
    SceneData Before { get; }
    SceneData After { get; }
    List<VariableData> BeforeGlobals { get; }
    List<VariableData> AfterGlobals { get; }
    Guid[] BeforeSelection { get; }
    Guid[] AfterSelection { get; }
}

internal sealed record SnapshotEditorCommand(
    string Name,
    SceneData Before,
    SceneData After,
    List<VariableData> BeforeGlobals,
    List<VariableData> AfterGlobals,
    Guid[] BeforeSelection,
    Guid[] AfterSelection,
    int BeforeRevision,
    int AfterRevision) : IEditorCommand;
