namespace ByteEngine.Editor.Selection;

internal static class AssetDeleteCommand
{
    public static bool ShouldBegin(bool browserFocused, bool deletePressed, int selectionCount) =>
        browserFocused && deletePressed && selectionCount > 0;
}
