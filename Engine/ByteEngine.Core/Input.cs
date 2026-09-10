using OpenTK.Windowing.GraphicsLibraryFramework;

namespace ByteEngine.Core;

public enum Key
{
    W,
    A,
    S,
    D,
    Up,
    Down,
    Left,
    Right,
    Escape
}

public static class Input
{
    private static readonly Key[] SupportedKeys =
        Enum.GetValues<Key>();

    private static readonly HashSet<Key> KeysDown =
        new();

    internal static void Update(
        KeyboardState keyboardState)
    {
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

    private static Keys ToOpenTkKey(
        Key key)
    {
        return key switch
        {
            Key.W => Keys.W,
            Key.A => Keys.A,
            Key.S => Keys.S,
            Key.D => Keys.D,
            Key.Up => Keys.Up,
            Key.Down => Keys.Down,
            Key.Left => Keys.Left,
            Key.Right => Keys.Right,
            Key.Escape => Keys.Escape,
            _ => throw new ArgumentOutOfRangeException(
                nameof(key),
                key,
                "Unsupported key."
            )
        };
    }
}
