using ByteEngine.Editor.Panels;

namespace ByteEngine.Editor.Selection;

internal sealed class AssetDeleteInteractionState
{
    private readonly PopupInteractionState _interaction = new();
    private readonly List<string> _targets = new();

    public IReadOnlyList<string> Targets => _targets;
    public bool IsDirectory { get; private set; }
    public bool IsOpen => _interaction.IsOpen;

    public void Begin(IEnumerable<string> targets, bool isDirectory)
    {
        string[] snapshot = targets.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (snapshot.Length == 0 || !_interaction.Request()) return;
        _targets.Clear();
        _targets.AddRange(snapshot);
        IsDirectory = isDirectory;
    }

    public bool ConsumeOpenRequest() => _interaction.ConsumeOpenRequest();
    public void MarkVisible() => _interaction.MarkVisible();

    public void RecoverWhenNotVisible()
    {
        _interaction.RecoverWhenNotVisible();
        if (!_interaction.IsOpen) ResetPayload();
    }

    public void Reset()
    {
        _interaction.Reset();
        ResetPayload();
    }

    private void ResetPayload()
    {
        _targets.Clear();
        IsDirectory = false;
    }
}
