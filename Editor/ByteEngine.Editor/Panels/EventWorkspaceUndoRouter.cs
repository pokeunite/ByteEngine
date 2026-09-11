namespace ByteEngine.Editor.Panels;

internal static class EventWorkspaceUndoRouter
{
    private static EventWorkspacePanel? _focused;

    public static bool HasFocusedWorkspace =>
        _focused !=
        null;

    public static bool CanUndo =>
        _focused?.CanUndo ==
        true;

    public static bool CanRedo =>
        _focused?.CanRedo ==
        true;

    public static string? UndoName =>
        _focused?.UndoName;

    public static string? RedoName =>
        _focused?.RedoName;

    public static void SetFocused(
        EventWorkspacePanel workspace)
    {
        _focused =
            workspace;
    }

    public static void ClearFocused(
        EventWorkspacePanel workspace)
    {
        if (ReferenceEquals(
                _focused,
                workspace))
        {
            _focused =
                null;
        }
    }

    public static bool TryUndo()
    {
        return _focused?.Undo() ==
               true;
    }

    public static bool TryRedo()
    {
        return _focused?.Redo() ==
               true;
    }
}
