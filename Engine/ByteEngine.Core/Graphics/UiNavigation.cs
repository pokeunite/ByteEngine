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
    private static System.Numerics.Vector2 _lastPointer;
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ByteEngine.Core.Scene.Scene,List<UiWidget>> Buttons = new();
    public static bool UsingKeyboardNavigation { get; private set; }

    public static UiWidget? Focused =>
        _focused?.TryGetTarget(out UiWidget? widget) == true ? widget : null;

    public static bool SouthPressed => _southPressed;

    public static void Focus(UiWidget? widget)
    {
        _focused = widget == null ? null : new WeakReference<UiWidget>(widget);
        if(widget!=null)for(var parent=widget.GameObject.Parent;parent!=null;parent=parent.Parent)
            if(parent.GetComponent<UiScrollContainer>() is {Enabled:true} scroll)scroll.Reveal(widget,Input.GameViewSize);
    }

    public static void Update(ByteEngine.Core.Scene.Scene scene)
    {
        if (_lastUpdate == Time.TotalTime) return;
        _lastUpdate = Time.TotalTime;
        if(System.Numerics.Vector2.DistanceSquared(_lastPointer,Input.GameViewPointerNormalized)>.000001f||Input.IsMouseButtonPressedForUi(MouseButton.Left))UsingKeyboardNavigation=false;
        _lastPointer=Input.GameViewPointerNormalized;
        bool south = Input.Snapshot.Gamepad.ButtonsDown.Contains(GamepadControl.South);
        _southPressed = south && !_previousSouth;
        _previousSouth = south;
        bool up = Input.Snapshot.Gamepad.ButtonsDown.Contains(GamepadControl.DPadUp);
        bool down = Input.Snapshot.Gamepad.ButtonsDown.Contains(GamepadControl.DPadDown);
        bool upPressed = up && !_previousUp;
        bool downPressed = down && !_previousDown;
        _previousUp = up;
        _previousDown = down;

        var buttons=Buttons.GetValue(scene,_=>new());buttons.Clear();bool pointerBlocked=false;
        var widgets=scene.GetComponentsSnapshot<UiWidget>();
        for(int i=0;i<widgets.Count;i++)
        {
            var widget=widgets[i];var owner=widget.GameObject;
            // Navigation historically uses the first widget on each object.
            if(!ReferenceEquals(owner.GetComponent<UiWidget>(),widget)||!widget.Enabled||!widget.Visible||!owner.ActiveInHierarchy||!UiLayout.IsVisible(owner))continue;
            if(widget is {Interactable:true,Kind:UiWidgetKind.Button}){buttons.Add(widget);pointerBlocked|=widget.IsHovered;}
            if(!pointerBlocked&&widget.BlocksWorldPointer)pointerBlocked=widget.Contains(Input.GameViewMousePosition,Input.GameViewSize);
        }
        Input.SetUiPointerBlocked(pointerBlocked);
        if (buttons.Count == 0)
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
            UsingKeyboardNavigation=true;
            int index = buttons.IndexOf(Focused!);
            Focus(buttons[(index + step + buttons.Count) % buttons.Count]);
        }
        if(_southPressed)UsingKeyboardNavigation=true;
    }
}
