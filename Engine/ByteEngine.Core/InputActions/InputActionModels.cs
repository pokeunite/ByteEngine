using System.Numerics;
using System.Text.Json.Serialization;

namespace ByteEngine.Core.InputSystem;

public enum InputActionType { Button, Axis1D, Axis2D }

public enum InputBindingType
{
    KeyboardKey,
    MouseButton,
    KeyboardAxis,
    Keyboard2DComposite,
    MouseDelta,
    MouseWheel,
    GamepadButton,
    GamepadStick,
    GamepadTrigger
}

public enum GamepadControl
{
    South, East, West, North,
    LeftShoulder, RightShoulder,
    LeftStickButton, RightStickButton,
    DPadUp, DPadDown, DPadLeft, DPadRight,
    Start, Back,
    LeftStick, RightStick, LeftTrigger, RightTrigger
}

public sealed class InputActionReference
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public InputActionReference() { }
    public InputActionReference(Guid id, string name) { Id = id; Name = name ?? string.Empty; }
    public static InputActionReference Named(string name) => new(Guid.Empty, name);
    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Id.ToString() : Name;
}

public sealed class InputBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public InputBindingType Type { get; set; }
    public Key Key { get; set; } = Key.Space;
    public Key NegativeKey { get; set; } = Key.A;
    public Key PositiveKey { get; set; } = Key.D;
    public Key UpKey { get; set; } = Key.W;
    public Key DownKey { get; set; } = Key.S;
    public Key LeftKey { get; set; } = Key.A;
    public Key RightKey { get; set; } = Key.D;
    public MouseButton MouseButton { get; set; } = MouseButton.Left;
    public GamepadControl GamepadControl { get; set; } = GamepadControl.South;
    public float Deadzone { get; set; } = .15f;
    public Vector2 Scale { get; set; } = Vector2.One;

    public static InputBinding KeyButton(Key key) => new() { Type = InputBindingType.KeyboardKey, Key = key };
    public static InputBinding MouseButtonBinding(MouseButton button) => new() { Type = InputBindingType.MouseButton, MouseButton = button };
    public static InputBinding Axis(Key negative, Key positive) => new() { Type = InputBindingType.KeyboardAxis, NegativeKey = negative, PositiveKey = positive };
    public static InputBinding Composite(Key up, Key down, Key left, Key right) => new()
    {
        Type = InputBindingType.Keyboard2DComposite, UpKey = up, DownKey = down, LeftKey = left, RightKey = right
    };
}

public sealed class InputActionDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "Action";
    public string Group { get; set; } = "Gameplay";
    public InputActionType Type { get; set; }
    public List<InputBinding> Bindings { get; set; } = new();
}

public sealed class InputMap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Default";
    public List<InputActionDefinition> Actions { get; set; } = new();

    public InputActionDefinition? Find(Guid id) => id == Guid.Empty ? null : Actions.FirstOrDefault(a => a.Id == id);
    public InputActionDefinition? Find(string? name) => string.IsNullOrWhiteSpace(name) ? null :
        Actions.FirstOrDefault(a => string.Equals(a.DisplayName, name, StringComparison.OrdinalIgnoreCase));
    public InputActionDefinition? Resolve(InputActionReference? reference) => reference == null ? null :
        reference.Id != Guid.Empty ? Find(reference.Id) : Find(reference.Name);

    public InputActionDefinition Add(string displayName, InputActionType type, string group = "Gameplay")
    {
        string name = UniqueName(string.IsNullOrWhiteSpace(displayName) ? "Action" : displayName.Trim());
        var action = new InputActionDefinition { DisplayName = name, Type = type, Group = group };
        Actions.Add(action);
        return action;
    }

    public bool Rename(Guid id, string displayName)
    {
        InputActionDefinition? action = Find(id);
        if (action == null || string.IsNullOrWhiteSpace(displayName)) return false;
        string requested = displayName.Trim();
        if (Actions.Any(a => a.Id != id && string.Equals(a.DisplayName, requested, StringComparison.OrdinalIgnoreCase))) return false;
        action.DisplayName = requested;
        return true;
    }

    public bool Remove(Guid id) => Actions.RemoveAll(a => a.Id == id) > 0;

    public void EnsureValid()
    {
        if (Id == Guid.Empty) Id = Guid.NewGuid();
        Actions ??= new();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (InputActionDefinition action in Actions)
        {
            if (action.Id == Guid.Empty) action.Id = Guid.NewGuid();
            action.DisplayName = UniqueName(string.IsNullOrWhiteSpace(action.DisplayName) ? "Action" : action.DisplayName.Trim(), names);
            names.Add(action.DisplayName);
            action.Bindings ??= new();
            foreach (InputBinding binding in action.Bindings)
            {
                if (binding.Id == Guid.Empty) binding.Id = Guid.NewGuid();
                binding.Deadzone = Math.Clamp(float.IsFinite(binding.Deadzone) ? binding.Deadzone : .15f, 0f, .99f);
                if (!float.IsFinite(binding.Scale.X) || !float.IsFinite(binding.Scale.Y)) binding.Scale = Vector2.One;
            }
        }
    }

    public void EnsureGameplayDefaults()
    {
        EnsureValid();
        Ensure("Move", InputActionType.Axis2D, a =>
        {
            a.Bindings.Add(InputBinding.Composite(Key.W, Key.S, Key.A, Key.D));
            a.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadStick, GamepadControl = GamepadControl.LeftStick, Deadzone = .15f });
        });
        Ensure("Look", InputActionType.Axis2D, a =>
        {
            a.Bindings.Add(new InputBinding { Type = InputBindingType.MouseDelta });
            a.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadStick, GamepadControl = GamepadControl.RightStick, Deadzone = .15f });
        });
        Ensure("Jump", InputActionType.Button, a =>
        {
            a.Bindings.Add(InputBinding.KeyButton(Key.Space));
            a.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadButton, GamepadControl = GamepadControl.South });
        });
        Ensure("Sprint", InputActionType.Button, a => a.Bindings.Add(InputBinding.KeyButton(Key.LeftShift)));
        InputActionDefinition sprint = Find("Sprint")!;
        if (!sprint.Bindings.Any(b => b.Type == InputBindingType.GamepadButton))
            sprint.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadButton, GamepadControl = GamepadControl.LeftStickButton });
        Ensure("Fire", InputActionType.Button, a =>
        {
            a.Bindings.Add(InputBinding.MouseButtonBinding(MouseButton.Left));
            a.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadTrigger, GamepadControl = GamepadControl.RightTrigger, Deadzone = .1f });
        });
        Ensure("Interact", InputActionType.Button, a =>
        {
            a.Bindings.Add(InputBinding.KeyButton(Key.E));
            a.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadButton, GamepadControl = GamepadControl.West });
        });
        Ensure("Pause", InputActionType.Button, a =>
        {
            a.Bindings.Add(InputBinding.KeyButton(Key.Escape));
            a.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadButton, GamepadControl = GamepadControl.Start });
        });
    }

    public static InputMap CreateDefault()
    {
        var map = new InputMap();
        map.EnsureGameplayDefaults();
        return map;
    }

    private void Ensure(string name, InputActionType type, Action<InputActionDefinition> configure)
    {
        if (Find(name) != null) return;
        InputActionDefinition action = Add(name, type);
        configure(action);
    }

    private string UniqueName(string requested, HashSet<string>? reserved = null)
    {
        bool Exists(string value) => reserved != null
            ? reserved.Contains(value)
            : Actions.Any(a => string.Equals(a.DisplayName, value, StringComparison.OrdinalIgnoreCase));
        if (!Exists(requested)) return requested;
        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{requested} {suffix}";
            if (!Exists(candidate)) return candidate;
        }
    }
}

public sealed class RawInputSnapshot
{
    public HashSet<Key> KeysDown { get; } = new();
    public HashSet<MouseButton> MouseButtonsDown { get; } = new();
    public Vector2 MouseDelta { get; set; }
    public float MouseWheel { get; set; }
    public GamepadSnapshot Gamepad { get; } = new();

    public bool KeyDown(Key key) => KeysDown.Contains(key);
    public bool MouseDown(MouseButton button) => MouseButtonsDown.Contains(button);
    public void Clear()
    {
        KeysDown.Clear(); MouseButtonsDown.Clear(); MouseDelta = Vector2.Zero; MouseWheel = 0f; Gamepad.Clear();
    }
}

public sealed class GamepadSnapshot
{
    public HashSet<GamepadControl> ButtonsDown { get; } = new();
    public Vector2 LeftStick { get; set; }
    public Vector2 RightStick { get; set; }
    public float LeftTrigger { get; set; }
    public float RightTrigger { get; set; }
    public void Clear() { ButtonsDown.Clear(); LeftStick = RightStick = Vector2.Zero; LeftTrigger = RightTrigger = 0f; }
}
