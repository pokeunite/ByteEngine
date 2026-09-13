namespace ByteEngine.Editor.Panels;

/// <summary>
/// Small reusable lifecycle for ImGui popups whose model state must remain
/// synchronized with ImGui's actual requested/visible state.
/// </summary>
internal sealed class PopupInteractionState
{
    public bool OpenRequested { get; private set; }
    public bool IsVisible { get; private set; }
    public bool FocusRequested { get; private set; }
    public bool IsOpen => OpenRequested || IsVisible;

    public bool Request(bool focus = false)
    {
        if (IsVisible) return false;
        Reset();
        OpenRequested = true;
        FocusRequested = focus;
        return true;
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

    public void MarkVisible() => IsVisible = true;

    public void RecoverWhenNotVisible()
    {
        if (!OpenRequested) Reset();
    }

    public void Reset()
    {
        OpenRequested = false;
        IsVisible = false;
        FocusRequested = false;
    }
}
