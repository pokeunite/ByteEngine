using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Editor.Commands;

internal interface IEditorCommand
{
    string Name { get; }
    SceneData Before { get; }
    SceneData After { get; }
    Guid[] BeforeSelection { get; }
    Guid[] AfterSelection { get; }
    int BeforeRevision { get; }
    int AfterRevision { get; }
}

internal sealed record SnapshotEditorCommand(
    string Name,
    SceneData Before,
    SceneData After,
    Guid[] BeforeSelection,
    Guid[] AfterSelection,
    int BeforeRevision,
    int AfterRevision) : IEditorCommand;
