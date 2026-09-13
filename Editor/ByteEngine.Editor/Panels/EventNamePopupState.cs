using System.Numerics;

namespace ByteEngine.Editor.Panels;

internal sealed class EventNamePopupState
{
    public bool IsOpen { get; private set; }
    public bool OpenRequested { get; private set; }
    public bool FocusRequested { get; private set; }
    public Guid? TargetEventId { get; private set; }
    public Vector2 CreatePosition { get; private set; }
    public string Buffer { get; set; } = "New Event";
    public bool IsRename => TargetEventId.HasValue;

    public void BeginCreate(Vector2 position)
    {
        if (IsOpen) return;
        TargetEventId = null;
        CreatePosition = position;
        Buffer = "New Event";
        Begin();
    }

    public void BeginRename(Guid eventId, string displayName)
    {
        if (IsOpen) return;
        TargetEventId = eventId;
        Buffer = displayName;
        Begin();
    }

    public bool ConsumeOpenRequest()
    {
        bool value = OpenRequested;
        OpenRequested = false;
        return value;
    }

    public bool ConsumeFocusRequest()
    {
        bool value = FocusRequested;
        FocusRequested = false;
        return value;
    }

    public void Close()
    {
        IsOpen = false;
        OpenRequested = false;
        FocusRequested = false;
        TargetEventId = null;
    }

    private void Begin()
    {
        if (IsOpen) return;
        IsOpen = true;
        OpenRequested = true;
        FocusRequested = true;
    }
}
