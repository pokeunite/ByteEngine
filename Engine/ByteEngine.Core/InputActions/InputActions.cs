using System.Numerics;

namespace ByteEngine.Core.InputSystem;

public readonly record struct InputActionState(
    bool Down, bool Pressed, bool Released, float Axis1D, Vector2 Axis2D, string ActiveSource);

public static class InputActions
{
    private sealed class RuntimeState
    {
        public bool Down;
        public bool Pressed;
        public bool Released;
        public float Axis1D;
        public Vector2 Axis2D;
        public string ActiveSource = "None";
    }

    private static readonly Dictionary<Guid, RuntimeState> States = new();
    private static readonly Dictionary<Guid, InputActionDefinition> DefinitionsById = new();
    private static readonly Dictionary<string, InputActionDefinition> DefinitionsByName = new(StringComparer.OrdinalIgnoreCase);
    private static InputMap _map = InputMap.CreateDefault();
    public static InputMap Map => _map;
    public static bool GameplayEnabled { get; private set; }

    public static void Configure(InputMap? map)
    {
        _map = map ?? InputMap.CreateDefault();
        _map.EnsureValid();
        States.Clear();
        DefinitionsById.Clear();
        DefinitionsByName.Clear();
        foreach (InputActionDefinition action in _map.Actions)
        {
            States[action.Id] = new RuntimeState();
            DefinitionsById[action.Id] = action;
            DefinitionsByName[action.DisplayName] = action;
        }
    }

    public static InputActionReference Reference(string name)
    {
        DefinitionsByName.TryGetValue(name, out InputActionDefinition? action);
        return action == null ? InputActionReference.Named(name) : new InputActionReference(action.Id, action.DisplayName);
    }

    public static void Update(RawInputSnapshot snapshot, bool gameplayEnabled = true)
    {
        GameplayEnabled = gameplayEnabled;
        foreach (InputActionDefinition action in _map.Actions)
        {
            if (!States.TryGetValue(action.Id, out RuntimeState? state)) States[action.Id] = state = new RuntimeState();
            bool previous = state.Down;
            state.Axis1D = 0f;
            state.Axis2D = Vector2.Zero;
            state.ActiveSource = "None";
            bool unbounded = false;

            if (gameplayEnabled)
            {
                foreach (InputBinding binding in action.Bindings)
                {
                    Vector2 value = Resolve(binding, snapshot, out string source, out bool bindingUnbounded);
                    if (value.LengthSquared() <= .000001f) continue;
                    state.ActiveSource = source;
                    unbounded |= bindingUnbounded;
                    if (action.Type == InputActionType.Axis2D) state.Axis2D += value;
                    else state.Axis1D += MathF.Abs(value.X) > .000001f ? value.X : value.Y;
                }
            }

            if (action.Type == InputActionType.Axis2D)
            {
                if (!unbounded && state.Axis2D.LengthSquared() > 1f) state.Axis2D = Vector2.Normalize(state.Axis2D);
                state.Down = state.Axis2D.LengthSquared() > .000001f;
            }
            else
            {
                state.Axis1D = Math.Clamp(state.Axis1D, -1f, 1f);
                state.Down = action.Type == InputActionType.Button ? MathF.Abs(state.Axis1D) >= .5f : MathF.Abs(state.Axis1D) > .0001f;
            }
            state.Pressed = gameplayEnabled && state.Down && !previous;
            state.Released = gameplayEnabled && !state.Down && previous;
            if (!gameplayEnabled) state.Pressed = state.Released = false;
        }
    }

    public static InputActionState Get(InputActionReference? reference) => Get(Resolve(reference));
    public static InputActionState Get(string nameOrId) => Get(Resolve(nameOrId));
    public static InputActionState Get(Guid id) => Get(DefinitionsById.GetValueOrDefault(id));
    public static bool IsDown(InputActionReference? reference) => Get(reference).Down;
    public static bool WasPressed(InputActionReference? reference) => Get(reference).Pressed;
    public static bool WasReleased(InputActionReference? reference) => Get(reference).Released;
    public static float ReadAxis1D(InputActionReference? reference) => Get(reference).Axis1D;
    public static Vector2 ReadAxis2D(InputActionReference? reference) => Get(reference).Axis2D;
    public static bool IsDown(string name) => Get(name).Down;
    public static bool WasPressed(string name) => Get(name).Pressed;
    public static bool WasReleased(string name) => Get(name).Released;
    public static float ReadAxis1D(string name) => Get(name).Axis1D;
    public static Vector2 ReadAxis2D(string name) => Get(name).Axis2D;

    public static InputActionDefinition? Resolve(InputActionReference? reference)
    {
        if (reference == null) return null;
        return reference.Id != Guid.Empty ? DefinitionsById.GetValueOrDefault(reference.Id) : Resolve(reference.Name);
    }

    public static InputActionDefinition? Resolve(string? nameOrId)
    {
        if (string.IsNullOrWhiteSpace(nameOrId)) return null;
        if (Guid.TryParse(nameOrId, out Guid id)) return DefinitionsById.GetValueOrDefault(id);
        return DefinitionsByName.GetValueOrDefault(nameOrId);
    }

    public static void WriteDiagnostics(TextWriter writer)
    {
        writer.WriteLine("Input Actions:");
        writer.WriteLine($"  Map: {_map.Name} ({_map.Id})");
        writer.WriteLine($"  Gameplay enabled: {GameplayEnabled}");
        foreach (InputActionDefinition action in _map.Actions)
        {
            InputActionState value = Get(action);
            writer.WriteLine($"  {action.Group}/{action.DisplayName} ({action.Type}, {action.Id}): down={value.Down}, pressed={value.Pressed}, released={value.Released}, axis1D={value.Axis1D:0.###}, axis2D=({value.Axis2D.X:0.###}, {value.Axis2D.Y:0.###}), source={value.ActiveSource}");
        }
    }

    private static InputActionState Get(InputActionDefinition? action)
    {
        if (action == null || !States.TryGetValue(action.Id, out RuntimeState? state)) return default;
        return new InputActionState(state.Down, state.Pressed, state.Released, state.Axis1D, state.Axis2D, state.ActiveSource);
    }

    private static Vector2 Resolve(InputBinding binding, RawInputSnapshot snapshot, out string source, out bool unbounded)
    {
        source = binding.Type.ToString();
        unbounded = false;
        Vector2 value = binding.Type switch
        {
            InputBindingType.KeyboardKey => new Vector2(snapshot.KeyDown(binding.Key) ? 1f : 0f, 0f),
            InputBindingType.MouseButton => new Vector2(snapshot.MouseDown(binding.MouseButton) ? 1f : 0f, 0f),
            InputBindingType.KeyboardAxis => new Vector2((snapshot.KeyDown(binding.PositiveKey) ? 1f : 0f) - (snapshot.KeyDown(binding.NegativeKey) ? 1f : 0f), 0f),
            InputBindingType.Keyboard2DComposite => new Vector2(
                (snapshot.KeyDown(binding.RightKey) ? 1f : 0f) - (snapshot.KeyDown(binding.LeftKey) ? 1f : 0f),
                (snapshot.KeyDown(binding.UpKey) ? 1f : 0f) - (snapshot.KeyDown(binding.DownKey) ? 1f : 0f)),
            InputBindingType.MouseDelta => snapshot.MouseDelta,
            InputBindingType.MouseWheel => new Vector2(snapshot.MouseWheel, 0f),
            InputBindingType.GamepadButton => new Vector2(snapshot.Gamepad.ButtonsDown.Contains(binding.GamepadControl) ? 1f : 0f, 0f),
            InputBindingType.GamepadStick => binding.GamepadControl == GamepadControl.RightStick ? snapshot.Gamepad.RightStick : snapshot.Gamepad.LeftStick,
            InputBindingType.GamepadTrigger => new Vector2(binding.GamepadControl == GamepadControl.RightTrigger ? snapshot.Gamepad.RightTrigger : snapshot.Gamepad.LeftTrigger, 0f),
            _ => Vector2.Zero
        };
        unbounded = binding.Type == InputBindingType.MouseDelta;
        if (binding.Type is InputBindingType.GamepadStick or InputBindingType.GamepadTrigger) value = ApplyDeadzone(value, binding.Deadzone);
        value *= binding.Scale;
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) return Vector2.Zero;
        if (!unbounded && value.LengthSquared() > 1f) value = Vector2.Normalize(value);
        return value;
    }

    private static Vector2 ApplyDeadzone(Vector2 value, float deadzone)
    {
        float length = value.Length();
        float dz = Math.Clamp(deadzone, 0f, .99f);
        if (length <= dz || length <= .000001f) return Vector2.Zero;
        float magnitude = Math.Clamp((length - dz) / (1f - dz), 0f, 1f);
        return value / length * magnitude;
    }
}
