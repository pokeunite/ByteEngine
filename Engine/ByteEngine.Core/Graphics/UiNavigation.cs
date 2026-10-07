using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public static class UiNavigation
{
    private static WeakReference<UiWidget>? _focused;
    private static double _lastUpdate = double.NaN;
    private static bool _previousSouth;
    private static bool _previousUp;
    private static bool _previousDown;
    private static bool _southPressed;

    public static UiWidget? Focused =>
        _focused?.TryGetTarget(out UiWidget? widget) == true ? widget : null;

    public static bool SouthPressed => _southPressed;

    public static void Focus(UiWidget? widget)
    {
        _focused = widget == null ? null : new WeakReference<UiWidget>(widget);
    }

    public static void Update(ByteEngine.Core.Scene.Scene scene)
    {
        if (_lastUpdate == Time.TotalTime) return;
        _lastUpdate = Time.TotalTime;
        bool south = Input.Snapshot.Gamepad.ButtonsDown.Contains(GamepadControl.South);
        _southPressed = south && !_previousSouth;
        _previousSouth = south;
        bool up = Input.Snapshot.Gamepad.ButtonsDown.Contains(GamepadControl.DPadUp);
        bool down = Input.Snapshot.Gamepad.ButtonsDown.Contains(GamepadControl.DPadDown);
        bool upPressed = up && !_previousUp;
        bool downPressed = down && !_previousDown;
        _previousUp = up;
        _previousDown = down;

        UiWidget[] buttons = scene.GameObjects
            .Select(obj => obj.GetComponent<UiWidget>())
            .Where(widget => widget is { Enabled: true, Visible: true,
                Interactable: true, Kind: UiWidgetKind.Button })
            .Cast<UiWidget>()
            .Where(widget => widget.GameObject.ActiveInHierarchy &&
                UiLayout.IsVisible(widget.GameObject))
            .ToArray();
        Input.SetUiPointerBlocked(buttons.Any(widget => widget.IsHovered));
        if (buttons.Length == 0)
        {
            Focus(null);
            return;
        }
        UiWidget? focused = Focused;
        if (focused != null && !buttons.Contains(focused))
            Focus(null);
        if (Input.IsMouseButtonPressedForUi(MouseButton.Left))
        {
            UiWidget? hovered = buttons.LastOrDefault(widget => widget.IsHovered);
            if (hovered != null) Focus(hovered);
        }
        int step = Input.IsKeyPressed(Key.Tab) ||
            Input.IsKeyPressed(Key.Down) ||
            downPressed
            ? 1 : Input.IsKeyPressed(Key.Up) ||
            upPressed
            ? -1 : 0;
        if (step != 0)
        {
            int index = Array.IndexOf(buttons, Focused);
            Focus(buttons[(index + step + buttons.Length) % buttons.Length]);
        }
    }
}
