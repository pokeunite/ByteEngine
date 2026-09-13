namespace ByteEngine.Editor.Panels;

internal readonly record struct ByteGraphToolbarLayout(
    bool CollapseSecondary,
    bool TraceOnSecondRow)
{
    public static ByteGraphToolbarLayout ForWidth(float width) => new(
        CollapseSecondary: width < 980f,
        TraceOnSecondRow: width < 1400f);
}
