using OpenTK.Windowing.GraphicsLibraryFramework;

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

public static class Input
{
    private static readonly Key[] SupportedKeys =
        Enum.GetValues<Key>();

    private static readonly HashSet<Key> KeysDown =
        new();

    private static readonly HashSet<Key> PreviousKeysDown =
        new();

    internal static void Update(
        KeyboardState keyboardState)
    {
        PreviousKeysDown.Clear();

        foreach (Key key in KeysDown)
        {
            PreviousKeysDown.Add(key);
        }

        KeysDown.Clear();

        foreach (Key key in SupportedKeys)
        {
            if (keyboardState.IsKeyDown(
                    ToOpenTkKey(key)))
            {
                KeysDown.Add(key);
            }
        }
    }

    public static bool IsKeyDown(
        Key key)
    {
        return KeysDown.Contains(key);
    }

    public static bool IsKeyPressed(
        Key key)
    {
        return
            KeysDown.Contains(key) &&
            !PreviousKeysDown.Contains(key);
    }

    public static bool IsKeyReleased(
        Key key)
    {
        return
            !KeysDown.Contains(key) &&
            PreviousKeysDown.Contains(key);
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
}