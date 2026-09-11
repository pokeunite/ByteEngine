using OpenTK.Windowing.GraphicsLibraryFramework;

using OpenTkMouseButton =
    OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;

namespace ByteEngine.Core;

public enum Key
{
    W,
    A,
    S,
    D,

    Q,
    E,
    F,

    Space,

    Up,
    Down,
    Left,
    Right,

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
    private static readonly Key[] SupportedKeys =
        Enum.GetValues<Key>();

    private static readonly MouseButton[] SupportedMouseButtons =
        Enum.GetValues<MouseButton>();

    private static readonly HashSet<Key> KeysDown =
        new();

    private static readonly HashSet<Key> PreviousKeysDown =
        new();

    private static readonly HashSet<MouseButton> MouseButtonsDown =
        new();

    private static readonly HashSet<MouseButton> PreviousMouseButtonsDown =
        new();

    internal static void Update(
        KeyboardState keyboardState,
        MouseState mouseState)
    {
        PreviousKeysDown.Clear();

        foreach (Key key in KeysDown)
        {
            PreviousKeysDown.Add(
                key);
        }

        KeysDown.Clear();

        foreach (Key key in SupportedKeys)
        {
            if (keyboardState.IsKeyDown(
                    ToOpenTkKey(
                        key)))
            {
                KeysDown.Add(
                    key);
            }
        }

        PreviousMouseButtonsDown.Clear();

        foreach (MouseButton button
                 in MouseButtonsDown)
        {
            PreviousMouseButtonsDown.Add(
                button);
        }

        MouseButtonsDown.Clear();

        foreach (MouseButton button
                 in SupportedMouseButtons)
        {
            if (mouseState.IsButtonDown(
                    ToOpenTkMouseButton(
                        button)))
            {
                MouseButtonsDown.Add(
                    button);
            }
        }
    }

    public static bool IsKeyDown(
        Key key)
    {
        return KeysDown.Contains(
            key);
    }

    public static bool IsKeyPressed(
        Key key)
    {
        return
            KeysDown.Contains(
                key) &&
            !PreviousKeysDown.Contains(
                key);
    }

    public static bool IsKeyReleased(
        Key key)
    {
        return
            !KeysDown.Contains(
                key) &&
            PreviousKeysDown.Contains(
                key);
    }

    public static bool IsMouseButtonDown(
        MouseButton button)
    {
        return MouseButtonsDown.Contains(
            button);
    }

    public static bool IsMouseButtonPressed(
        MouseButton button)
    {
        return
            MouseButtonsDown.Contains(
                button) &&
            !PreviousMouseButtonsDown.Contains(
                button);
    }

    public static bool IsMouseButtonReleased(
        MouseButton button)
    {
        return
            !MouseButtonsDown.Contains(
                button) &&
            PreviousMouseButtonsDown.Contains(
                button);
    }

    private static Keys ToOpenTkKey(
        Key key)
    {
        return key switch
        {
            Key.W => Keys.W,
            Key.A => Keys.A,
            Key.S => Keys.S,
            Key.D => Keys.D,

            Key.Q => Keys.Q,
            Key.E => Keys.E,
            Key.F => Keys.F,

            Key.Space => Keys.Space,

            Key.Up => Keys.Up,
            Key.Down => Keys.Down,
            Key.Left => Keys.Left,
            Key.Right => Keys.Right,

            Key.LeftShift => Keys.LeftShift,

            Key.Escape => Keys.Escape,

            _ => throw new ArgumentOutOfRangeException(
                nameof(key),
                key,
                "Unsupported key.")
        };
    }

    private static OpenTkMouseButton ToOpenTkMouseButton(
        MouseButton button)
    {
        return button switch
        {
            MouseButton.Left =>
                OpenTkMouseButton.Left,

            MouseButton.Right =>
                OpenTkMouseButton.Right,

            MouseButton.Middle =>
                OpenTkMouseButton.Middle,

            _ => throw new ArgumentOutOfRangeException(
                nameof(button),
                button,
                "Unsupported mouse button.")
        };
    }
}
