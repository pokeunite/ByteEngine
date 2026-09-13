using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using JoystickState = OpenTK.Windowing.GraphicsLibraryFramework.JoystickState;

namespace ByteEngine.Tests;

internal static class V08CInputActionsTests
{
    public static void Run(string root)
    {
        TestButtonEdges();
        TestDefaultMap();
        TestAxesAndBindings();
        TestDeadzone();
        TestEmptyJoystickSlots();
        TestEditorSeparation();
        TestProjectSerialization(root);
        TestControllerSerialization(root);
        TestPlayerControllerActions();
        InputActions.Configure(InputMap.CreateDefault());
    }

    private static void TestEmptyJoystickSlots()
    {
        var snapshot = new GamepadSnapshot();
        Input.PopulateGamepad(snapshot, new JoystickState[] { null! });
        Assert(snapshot.LeftStick == Vector2.Zero && snapshot.ButtonsDown.Count == 0,
            "empty OpenTK joystick slots are ignored safely");
    }

    private static void TestDefaultMap()
    {
        InputMap map = InputMap.CreateDefault();
        Assert(new[] { "Move", "Look", "Jump", "Fire", "Interact", "Sprint", "Pause" }.All(name => map.Find(name) != null),
            "new projects receive the complete default Input Map");
        Assert(map.Find("Move")!.Bindings.Any(binding => binding.Type == InputBindingType.Keyboard2DComposite) &&
            map.Find("Move")!.Bindings.Any(binding => binding.Type == InputBindingType.GamepadStick) &&
            map.Find("Fire")!.Bindings.Any(binding => binding.Type == InputBindingType.GamepadTrigger),
            "default map includes keyboard, mouse, and common gamepad bindings");
    }

    private static void TestButtonEdges()
    {
        var map = new InputMap();
        InputActionDefinition jump = map.Add("Jump", InputActionType.Button);
        jump.Bindings.Add(InputBinding.KeyButton(Key.Space));
        jump.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadButton, GamepadControl = GamepadControl.South });
        InputActions.Configure(map);
        var raw = new RawInputSnapshot();

        InputActions.Update(raw);
        Assert(!InputActions.IsDown("Jump") && !InputActions.WasPressed("Jump"), "idle action is up");
        raw.KeysDown.Add(Key.Space);
        InputActions.Update(raw);
        Assert(InputActions.IsDown("Jump") && InputActions.WasPressed("Jump") && !InputActions.WasReleased("Jump"), "press edge occurs once");
        InputActions.Update(raw);
        Assert(InputActions.IsDown("Jump") && !InputActions.WasPressed("Jump"), "held button does not repeat press edge");
        raw.KeysDown.Clear();
        InputActions.Update(raw);
        Assert(!InputActions.IsDown("Jump") && InputActions.WasReleased("Jump"), "release edge occurs once");
        InputActions.Update(raw);
        Assert(!InputActions.WasReleased("Jump"), "release edge clears next frame");
        raw.Gamepad.ButtonsDown.Add(GamepadControl.South);
        InputActions.Update(raw);
        Assert(InputActions.IsDown("Jump") && InputActions.WasPressed("Jump"), "gamepad binding activates the same Button action");
    }

    private static void TestAxesAndBindings()
    {
        var map = new InputMap();
        InputActionDefinition move = map.Add("Move", InputActionType.Axis2D);
        move.Bindings.Add(InputBinding.Composite(Key.W, Key.S, Key.A, Key.D));
        InputActionDefinition look = map.Add("Look", InputActionType.Axis2D);
        look.Bindings.Add(new InputBinding { Type = InputBindingType.MouseDelta });
        InputActionDefinition fire = map.Add("Fire", InputActionType.Button);
        fire.Bindings.Add(InputBinding.KeyButton(Key.F));
        fire.Bindings.Add(InputBinding.MouseButtonBinding(MouseButton.Left));
        InputActions.Configure(map);

        var raw = new RawInputSnapshot();
        raw.KeysDown.UnionWith(new[] { Key.W, Key.D });
        raw.MouseDelta = new Vector2(8f, -3f);
        raw.MouseButtonsDown.Add(MouseButton.Left);
        InputActions.Update(raw);
        Vector2 movement = InputActions.ReadAxis2D("Move");
        Assert(Near(movement, Vector2.Normalize(Vector2.One)), "W+D composite is normalized");
        Assert(Near(InputActions.ReadAxis2D("Look"), new Vector2(8f, -3f)), "mouse delta remains unbounded");
        Assert(InputActions.IsDown("Fire"), "multiple bindings resolve with OR semantics");

        raw.MouseButtonsDown.Clear();
        raw.KeysDown.Clear();
        raw.KeysDown.Add(Key.F);
        InputActions.Update(raw);
        Assert(InputActions.IsDown("Fire"), "alternate binding activates same action");
    }

    private static void TestDeadzone()
    {
        var map = new InputMap();
        InputActionDefinition move = map.Add("Move", InputActionType.Axis2D);
        move.Bindings.Add(new InputBinding { Type = InputBindingType.GamepadStick, Deadzone = .2f });
        InputActions.Configure(map);
        var raw = new RawInputSnapshot();
        raw.Gamepad.LeftStick = new Vector2(.1f, 0f);
        InputActions.Update(raw);
        Assert(InputActions.ReadAxis2D("Move") == Vector2.Zero, "stick deadzone suppresses drift");
        raw.Gamepad.LeftStick = new Vector2(.6f, 0f);
        InputActions.Update(raw);
        Assert(MathF.Abs(InputActions.ReadAxis2D("Move").X - .5f) < .0001f, "stick deadzone rescales remaining range");
    }

    private static void TestEditorSeparation()
    {
        InputMap map = InputMap.CreateDefault();
        InputActions.Configure(map);
        var raw = new RawInputSnapshot { MouseDelta = new Vector2(20f, 10f) };
        raw.KeysDown.Add(Key.W);
        InputActions.Update(raw, gameplayEnabled: false);
        Assert(InputActions.ReadAxis2D("Move") == Vector2.Zero && InputActions.ReadAxis2D("Look") == Vector2.Zero &&
            !InputActions.WasPressed("Move"), "edit mode does not leak gameplay input");
        InputActions.Update(raw, gameplayEnabled: true);
        Assert(InputActions.ReadAxis2D("Move").Y > .99f && InputActions.ReadAxis2D("Look") == raw.MouseDelta,
            "play mode resolves raw input before gameplay");
    }

    private static void TestProjectSerialization(string root)
    {
        var project = new ProjectData { Name = "Input Test", InputMap = new InputMap() };
        InputActionDefinition dash = project.InputMap.Add("Dash", InputActionType.Button);
        dash.Bindings.Add(InputBinding.KeyButton(Key.J));
        Guid stableId = dash.Id;
        Assert(project.InputMap.Rename(stableId, "Dodge"), "action rename succeeds");
        string path = Path.Combine(root, "InputActions.byteproject");
        var serializer = new ProjectSerializer();
        serializer.Save(project, path);
        ProjectData loaded = serializer.Load(path);
        InputActionDefinition? action = loaded.InputMap.Find(stableId);
        Assert(action?.DisplayName == "Dodge" && action.Bindings.Single().Key == Key.J, "input map and rebinding survive project save/load");
        Assert(!loaded.InputMap.Rename(stableId, "Move") || loaded.InputMap.Find(stableId)?.Id == stableId, "rename never changes action identity");
    }

    private static void TestControllerSerialization(string root)
    {
        string testRoot = Path.Combine(root, "InputController");
        Directory.CreateDirectory(Path.Combine(testRoot, "Assets"));
        using var database = new AssetDatabase(testRoot, new[] { "Assets" });
        using var assets = new AssetManager(database);
        var serializer = new SceneSerializer(new ComponentSerializer(testRoot, database, assets));
        var scene = new Scene("Controller");
        var player = scene.CreateGameObject("Player");
        var input = player.AddComponent(new PlayerController3D
        {
            MoveAction = new InputActionReference(Guid.NewGuid(), "Locomotion"),
            LookAction = new InputActionReference(Guid.NewGuid(), "Camera"),
            JumpAction = new InputActionReference(Guid.NewGuid(), "Leap"),
            SprintAction = new InputActionReference(Guid.NewGuid(), "Run")
        });
        PlayerController3D copy = serializer.CloneForRuntime(scene).FindGameObject("Player")!.GetComponent<PlayerController3D>()!;
        Assert(copy.MoveAction.Id == input.MoveAction.Id && copy.MoveAction.Name == "Locomotion" &&
            copy.LookAction.Id == input.LookAction.Id && copy.JumpAction.Id == input.JumpAction.Id &&
            copy.SprintAction.Id == input.SprintAction.Id, "PlayerController3D action references survive Play Mode clone");
    }

    private static void TestPlayerControllerActions()
    {
        InputMap map = InputMap.CreateDefault();
        InputActions.Configure(map);
        var scene = new Scene("Player Input Migration");
        GameObject ground = scene.CreateGameObject("Ground");
        ground.AddComponent(new GroundSurface());
        ground.AddComponent(new BoxCollider3D { Size = new Vector3(20f, 1f, 20f), Center = new Vector3(0f, -.5f, 0f) });
        GameObject player = scene.CreateGameObject("Player");
        player.Transform.WorldPosition = new Vector3(0f, 1f, 0f);
        player.AddComponent(new CapsuleCollider3D { Radius = .5f, Height = 2f });
        CharacterController3D motor = player.AddComponent(new CharacterController3D
        {
            MoveSpeed = 5f, Acceleration = 1000f, AirControl = 1f, Gravity = 0f, JumpForce = 7f
        });
        PlayerController3D controller = player.AddComponent(new PlayerController3D
        {
            MoveAction = InputActions.Reference("Move"), LookAction = InputActions.Reference("Look"),
            JumpAction = InputActions.Reference("Jump"), SprintAction = InputActions.Reference("Sprint")
        });
        scene.LoadInternal();
        var raw = new RawInputSnapshot { MouseDelta = new Vector2(10f, -4f) };
        raw.KeysDown.UnionWith(new[] { Key.W, Key.Space });
        InputActions.Update(raw, true);
        Time.Update(.016);
        scene.UpdateInternal();
        Assert(motor.Velocity.Z < 0f, "Move Action drives camera-relative movement");
        Assert(MathF.Abs(controller.ControlYaw - 1.2f) < .001f && controller.ControlPitch > 12f,
            "Look Action drives control rotation through component sensitivity");
        Assert(motor.VerticalVelocity > 0f, "Jump Action calls CharacterController3D.Jump");
    }

    private static bool Near(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < .0001f;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
