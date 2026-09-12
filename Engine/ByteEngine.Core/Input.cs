using System.Numerics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTkMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace ByteEngine.Core;

public enum Key
{
    W, A, S, D,
    Q, E, F, R,
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

    public static Vector2 GameViewPointerNormalized { get; private set; } = new(.5f, .5f);
    public static Vector2 GameViewSize { get; private set; } = Vector2.One;
    public static bool IsPointerOverGameView { get; private set; }
    public static Vector2 GameViewMousePosition => GameViewPointerNormalized * GameViewSize;
    public static Vector2 GameViewMouseDelta { get; private set; }
    public static bool IsGameViewHovered => IsPointerOverGameView;
    public static bool IsGameViewFocused { get; private set; }
    private static Vector2 _lastGameViewDisplayPosition;
    private static bool _hadFocusedGameViewPointer;

    internal static void Update(KeyboardState keyboardState, MouseState mouseState)
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

    private static Keys ToOpenTkKey(Key key) => key switch
    {
        Key.W => Keys.W,
        Key.A => Keys.A,
        Key.S => Keys.S,
        Key.D => Keys.D,
        Key.Q => Keys.Q,
        Key.E => Keys.E,
        Key.F => Keys.F,
        Key.R => Keys.R,
        Key.Space => Keys.Space,
        Key.Up => Keys.Up,
        Key.Down => Keys.Down,
        Key.Left => Keys.Left,
        Key.Right => Keys.Right,
        Key.LeftShift => Keys.LeftShift,
        Key.Escape => Keys.Escape,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported key.")
    };

    private static OpenTkMouseButton ToOpenTkMouseButton(MouseButton button) => button switch
    {
        MouseButton.Left => OpenTkMouseButton.Left,
        MouseButton.Right => OpenTkMouseButton.Right,
        MouseButton.Middle => OpenTkMouseButton.Middle,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unsupported mouse button.")
    };
}
