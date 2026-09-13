using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;
using ByteEngine.Core.InputSystem;

namespace ByteEngine.Core;

public enum Key
{
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Space,
    Up, Down, Left, Right,
    LeftShift,
    Escape
}

public enum MouseButton
{
    Left,
    Right,
    Middle
}

public static class Input
{
    private static readonly Key[] SupportedKeys = Enum.GetValues<Key>();
    private static readonly MouseButton[] SupportedMouseButtons = Enum.GetValues<MouseButton>();
    private static readonly HashSet<Key> KeysDown = new();
    private static readonly HashSet<Key> PreviousKeysDown = new();
    private static readonly HashSet<MouseButton> MouseButtonsDown = new();
    private static readonly HashSet<MouseButton> PreviousMouseButtonsDown = new();
    private static readonly RawInputSnapshot RawSnapshot = new();
    private static readonly (int Index, GamepadControl Control)[] StandardGamepadButtons =
    {
        (0, GamepadControl.South), (1, GamepadControl.East), (2, GamepadControl.West), (3, GamepadControl.North),
        (4, GamepadControl.LeftShoulder), (5, GamepadControl.RightShoulder),
        (9, GamepadControl.LeftStickButton), (10, GamepadControl.RightStickButton),
        (11, GamepadControl.DPadUp), (12, GamepadControl.DPadRight),
        (13, GamepadControl.DPadDown), (14, GamepadControl.DPadLeft),
        (7, GamepadControl.Start), (6, GamepadControl.Back)
    };

    public static Vector2 GameViewPointerNormalized { get; private set; } = new(.5f, .5f);
    public static Vector2 GameViewSize { get; private set; } = Vector2.One;
    public static bool IsPointerOverGameView { get; private set; }
    public static Vector2 GameViewMousePosition => GameViewPointerNormalized * GameViewSize;
    public static Vector2 GameViewMouseDelta { get; private set; }
    public static bool IsGameViewHovered => IsPointerOverGameView;
    public static bool IsGameViewFocused { get; private set; }
    public static bool IsGameInputCaptured { get; private set; }
    public static Vector2 MouseDelta { get; private set; }
    public static RawInputSnapshot Snapshot => RawSnapshot;
    private static Vector2 _lastGameViewDisplayPosition;
    private static bool _hadFocusedGameViewPointer;

    internal static void Update(KeyboardState keyboardState, MouseState mouseState,
        IReadOnlyList<JoystickState>? joystickStates = null)
    {
        PreviousKeysDown.Clear();
        foreach (Key key in KeysDown) PreviousKeysDown.Add(key);

        KeysDown.Clear();
        foreach (Key key in SupportedKeys)
        {
            if (keyboardState.IsKeyDown(ToOpenTkKey(key))) KeysDown.Add(key);
        }

        PreviousMouseButtonsDown.Clear();
        foreach (MouseButton button in MouseButtonsDown) PreviousMouseButtonsDown.Add(button);

        MouseButtonsDown.Clear();
        foreach (MouseButton button in SupportedMouseButtons)
        {
            if (mouseState.IsButtonDown(ToOpenTkMouseButton(button))) MouseButtonsDown.Add(button);
        }

        MouseDelta = IsGameInputCaptured
            ? new Vector2(mouseState.Delta.X, mouseState.Delta.Y)
            : Vector2.Zero;

        RawSnapshot.Clear();
        foreach (Key key in KeysDown) RawSnapshot.KeysDown.Add(key);
        foreach (MouseButton button in MouseButtonsDown) RawSnapshot.MouseButtonsDown.Add(button);
        RawSnapshot.MouseDelta = MouseDelta;
        RawSnapshot.MouseWheel = mouseState.ScrollDelta.Y;
        PopulateGamepad(RawSnapshot.Gamepad, joystickStates);
    }

    internal static void SetGameInputCaptured(bool captured)
    {
        IsGameInputCaptured = captured;
        MouseDelta = Vector2.Zero;
        GameViewMouseDelta = Vector2.Zero;
        _hadFocusedGameViewPointer = false;
    }

    public static void SetGameViewPointer(Vector2 normalizedPosition, Vector2 gameViewSize, bool isInside)
    {
        SetGameViewPointer(normalizedPosition, gameViewSize, isInside, isInside, normalizedPosition * gameViewSize);
    }

    public static void SetGameViewPointer(Vector2 normalizedPosition, Vector2 gameViewSize, bool isInside,
        bool isFocused, Vector2 displayPosition)
    {
        GameViewPointerNormalized = new Vector2(
            Math.Clamp(normalizedPosition.X, 0f, 1f),
            Math.Clamp(normalizedPosition.Y, 0f, 1f));

        GameViewSize = new Vector2(
            Math.Max(gameViewSize.X, 1f),
            Math.Max(gameViewSize.Y, 1f));

        IsPointerOverGameView = isInside;
        IsGameViewFocused = isFocused;
        GameViewMouseDelta = isInside && isFocused && _hadFocusedGameViewPointer
            ? displayPosition - _lastGameViewDisplayPosition
            : Vector2.Zero;
        _lastGameViewDisplayPosition = displayPosition;
        _hadFocusedGameViewPointer = isInside && isFocused;
    }

    public static Vector2 ConsumeGameViewMouseDelta()
    {
        Vector2 delta = GameViewMouseDelta;
        GameViewMouseDelta = Vector2.Zero;
        return delta;
    }

    public static bool IsKeyDown(Key key) => KeysDown.Contains(key);
    public static bool IsKeyPressed(Key key) => KeysDown.Contains(key) && !PreviousKeysDown.Contains(key);
    public static bool IsKeyReleased(Key key) => !KeysDown.Contains(key) && PreviousKeysDown.Contains(key);

    public static bool IsMouseButtonDown(MouseButton button) => MouseButtonsDown.Contains(button);
    public static bool IsMouseButtonPressed(MouseButton button) =>
        MouseButtonsDown.Contains(button) && !PreviousMouseButtonsDown.Contains(button);
    public static bool IsMouseButtonReleased(MouseButton button) =>
        !MouseButtonsDown.Contains(button) && PreviousMouseButtonsDown.Contains(button);

    internal static void PopulateGamepad(GamepadSnapshot target, IReadOnlyList<JoystickState>? states)
    {
        JoystickState? state = null;
        if (states != null)
            for (int i = 0; i < states.Count; i++)
            {
                JoystickState? candidate = states[i];
                if (candidate != null && (candidate.AxisCount > 0 || candidate.ButtonCount > 0))
                {
                    state = candidate;
                    break;
                }
            }
        if (state == null) return;
        float Axis(int index) => index < state.AxisCount ? state.GetAxis(index) : 0f;
        bool Button(int index) => index < state.ButtonCount && state.IsButtonDown(index);
        target.LeftStick = new Vector2(Axis(0), -Axis(1));
        target.RightStick = new Vector2(Axis(2), -Axis(3));
        target.LeftTrigger = Math.Clamp((Axis(4) + 1f) * .5f, 0f, 1f);
        target.RightTrigger = Math.Clamp((Axis(5) + 1f) * .5f, 0f, 1f);
        for (int i = 0; i < StandardGamepadButtons.Length; i++)
            if (Button(StandardGamepadButtons[i].Index)) target.ButtonsDown.Add(StandardGamepadButtons[i].Control);
    }

    private static Keys ToOpenTkKey(Key key)
    {
        if (Enum.TryParse(key.ToString(), out Keys parsed)) return parsed;
        throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported key.");
    }

    private static OpenTkMouseButton ToOpenTkMouseButton(MouseButton button) => button switch
    {
        MouseButton.Left => OpenTkMouseButton.Left,
        MouseButton.Right => OpenTkMouseButton.Right,
        MouseButton.Middle => OpenTkMouseButton.Middle,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported mouse button.")
    };
}
