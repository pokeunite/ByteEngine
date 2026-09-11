using ByteEngine.Core.Assets;

namespace ByteEngine.Editor.Panels;

internal sealed class EventWorkspaceCollection
{
    private readonly Dictionary<Guid, EventWorkspacePanel> _documents =
        new();

    public void Open(
        AssetRecord asset,
        EditorLog log)
    {
        if (asset.Type !=
            AssetType.EventModule)
        {
            return;
        }

        if (!_documents.TryGetValue(
                asset.Guid,
                out EventWorkspacePanel? workspace))
        {
            workspace =
                new EventWorkspacePanel();

            _documents.Add(
                asset.Guid,
                workspace);
        }

        workspace.Open(
            asset,
            log);
    }

    public void Draw(
        EditorLog log)
    {
        foreach (EventWorkspacePanel workspace
                 in _documents.Values)
        {
            workspace.Draw(
                log);
        }
    }
}
