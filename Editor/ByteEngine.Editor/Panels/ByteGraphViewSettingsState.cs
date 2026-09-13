namespace ByteEngine.Editor.Panels;

internal sealed class ByteGraphViewSettingsState
{
    public const float MinimumNodeScale = .55f;
    public const float MaximumNodeScale = 1.15f;
    private readonly PopupInteractionState _interaction = new();

    public float NodeScale { get; private set; } = 1f;
    public bool IsOpen => _interaction.IsOpen;

    public void Begin(float nodeScale)
    {
        if (!_interaction.Request()) return;
        NodeScale = Clamp(nodeScale);
    }

    public bool ConsumeOpenRequest() => _interaction.ConsumeOpenRequest();
    public void MarkVisible() => _interaction.MarkVisible();
    public void RecoverWhenNotVisible() => _interaction.RecoverWhenNotVisible();
    public void Reset() => _interaction.Reset();

    public bool SetNodeScale(float value)
    {
        float next = Clamp(value);
        if (MathF.Abs(next - NodeScale) < .0001f) return false;
        NodeScale = next;
        return true;
    }

    private static float Clamp(float value) =>
        Math.Clamp(float.IsFinite(value) ? value : 1f, MinimumNodeScale, MaximumNodeScale);
}
