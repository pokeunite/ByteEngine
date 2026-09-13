using System.Numerics;

namespace ByteEngine.Editor.Panels;

internal sealed class EventNamePopupState
{
    private readonly PopupInteractionState _interaction = new();

    public bool IsOpen => _interaction.IsOpen;
    public bool IsVisible => _interaction.IsVisible;
    public Guid? TargetEventId { get; private set; }
    public Vector2 CreatePosition { get; private set; }
    public string Buffer { get; set; } = "New Event";
    public bool IsRename => TargetEventId.HasValue;

    public void BeginCreate(Vector2 position)
    {
        if (!_interaction.Request(true)) return;
        TargetEventId = null;
        CreatePosition = position;
        Buffer = "New Event";
    }

    public void BeginRename(Guid eventId, string displayName)
    {
        if (!_interaction.Request(true)) return;
        TargetEventId = eventId;
        Buffer = displayName;
    }

    public bool ConsumeOpenRequest()
    {
        return _interaction.ConsumeOpenRequest();
    }

    public bool ConsumeFocusRequest()
    {
        return _interaction.ConsumeFocusRequest();
    }

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

    public void Close() => Reset();

    private void ResetPayload()
    {
        TargetEventId = null;
        Buffer = "New Event";
        CreatePosition = Vector2.Zero;
    }
}
