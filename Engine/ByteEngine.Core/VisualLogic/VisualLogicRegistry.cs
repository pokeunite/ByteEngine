using System.Numerics;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed class VisualConditionDefinition
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required string DisplayName { get; init; }

    public string? TargetComponent { get; init; }

    public required Func<
        VisualInstruction,
        EventExecutionContext,
        bool> Evaluate { get; init; }
}

public sealed class VisualActionDefinition
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required string DisplayName { get; init; }

    public string? TargetComponent { get; init; }

    public required Action<
        VisualInstruction,
        EventExecutionContext> Execute { get; init; }
}

public sealed class VisualLogicRegistry
{
    private readonly Dictionary<
        string,
        VisualConditionDefinition> _conditions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<
        string,
        VisualActionDefinition> _actions =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<VisualConditionDefinition>
        Conditions =>
            _conditions.Values;

    public IReadOnlyCollection<VisualActionDefinition>
        Actions =>
            _actions.Values;

    public void RegisterCondition(
        VisualConditionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        _conditions[definition.Id] =
            definition;
    }

    public void RegisterAction(
        VisualActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            definition);

        _actions[definition.Id] =
            definition;
    }

    public bool TryGetCondition(
        string id,
        out VisualConditionDefinition? definition)
    {
        return _conditions.TryGetValue(
            id,
            out definition);
    }

    public bool TryGetAction(
        string id,
        out VisualActionDefinition? definition)
    {
        return _actions.TryGetValue(
            id,
            out definition);
    }

    public static VisualLogicRegistry CreateDefault()
    {
        var registry =
            new VisualLogicRegistry();

        RegisterSystem(registry);
        RegisterInput(registry);
        RegisterCharacter(registry);
        RegisterTransform(registry);
        RegisterVariables(registry);

        return registry;
    }

    private static void RegisterSystem(
        VisualLogicRegistry registry)
    {
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id =
                    "system.always",

                Category =
                    "System",

                DisplayName =
                    "Always",

                Evaluate =
                    (_, _) => true
            });

        /*
         * Trigger Once is handled specially by
         * EventModuleRuntime.
         */
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id =
                    "system.triggerOnce",

                Category =
                    "System",

                DisplayName =
                    "Trigger Once",

                Evaluate =
                    (_, _) => true
            });
    }

    private static void RegisterInput(
        VisualLogicRegistry registry)
    {
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id =
                    "input.keyHeld",

                Category =
                    "Input",

                DisplayName =
                    "Key Is Held",

                Evaluate =
                    (instruction, context) =>
                    {
                        return
                            TryGetKey(
                                instruction,
                                context,
                                out Key key) &&
                            Input.IsKeyDown(key);
                    }
            });

        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id =
                    "input.keyPressed",

                Category =
                    "Input",

                DisplayName =
                    "Key Pressed",

                Evaluate =
                    (instruction, context) =>
                    {
                        return
                            TryGetKey(
                                instruction,
                                context,
                                out Key key) &&
                            Input.IsKeyPressed(key);
                    }
            });

        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id =
                    "input.keyReleased",

                Category =
                    "Input",

                DisplayName =
                    "Key Released",

                Evaluate =
                    (instruction, context) =>
                    {
                        return
                            TryGetKey(
                                instruction,
                                context,
                                out Key key) &&
                            Input.IsKeyReleased(key);
                    }
            });
    }

    private static void RegisterCharacter(
        VisualLogicRegistry registry)
    {
        registry.RegisterCondition(
            CreateCharacterCondition(
                "character.isGrounded",
                "Is Grounded",
                controller =>
                    controller.IsGrounded));

        registry.RegisterCondition(
            CreateCharacterCondition(
                "character.isFalling",
                "Is Falling",
                controller =>
                    controller.IsFalling));

        registry.RegisterCondition(
            CreateCharacterCondition(
                "character.isMoving",
                "Is Moving",
                controller =>
                    controller.IsMoving));

        registry.RegisterCondition(
            CreateCharacterCondition(
                "character.justLanded",
                "Just Landed",
                controller =>
                    controller.JustLanded));

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "character.moveForward",

                Category =
                    "Character Controller 3D",

                DisplayName =
                    "Move Forward",

                TargetComponent =
                    nameof(CharacterController3D),

                Execute =
                    (instruction, context) =>
                    {
                        CharacterController3D? controller =
                            context.Self
                                .GetComponent<CharacterController3D>();

                        if (controller == null)
                        {
                            context.WarningSink?.Invoke(
                                $"{context.Self.Name} has no CharacterController3D.");

                            return;
                        }

                        float amount =
                            (float)EventValueResolver.GetNumber(
                                instruction,
                                "amount",
                                context,
                                1.0);

                        controller.MoveForward(
                            amount);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "character.moveRight",

                Category =
                    "Character Controller 3D",

                DisplayName =
                    "Move Right",

                TargetComponent =
                    nameof(CharacterController3D),

                Execute =
                    (instruction, context) =>
                    {
                        CharacterController3D? controller =
                            context.Self
                                .GetComponent<CharacterController3D>();

                        if (controller == null)
                        {
                            context.WarningSink?.Invoke(
                                $"{context.Self.Name} has no CharacterController3D.");

                            return;
                        }

                        float amount =
                            (float)EventValueResolver.GetNumber(
                                instruction,
                                "amount",
                                context,
                                1.0);

                        controller.MoveRight(
                            amount);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "character.jump",

                Category =
                    "Character Controller 3D",

                DisplayName =
                    "Jump",

                TargetComponent =
                    nameof(CharacterController3D),

                Execute =
                    (_, context) =>
                    {
                        CharacterController3D? controller =
                            context.Self
                                .GetComponent<CharacterController3D>();

                        if (controller == null)
                        {
                            context.WarningSink?.Invoke(
                                $"{context.Self.Name} has no CharacterController3D.");

                            return;
                        }

                        controller.Jump();
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "character.setVelocity",

                Category =
                    "Character Controller 3D",

                DisplayName =
                    "Set Velocity",

                TargetComponent =
                    nameof(CharacterController3D),

                Execute =
                    (instruction, context) =>
                    {
                        CharacterController3D? controller =
                            context.Self
                                .GetComponent<CharacterController3D>();

                        if (controller == null)
                        {
                            context.WarningSink?.Invoke(
                                $"{context.Self.Name} has no CharacterController3D.");

                            return;
                        }

                        Vector3 velocity =
                            EventValueResolver.GetVector3(
                                instruction,
                                "velocity",
                                context);

                        controller.SetVelocity(
                            velocity);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "character.addImpulse",

                Category =
                    "Character Controller 3D",

                DisplayName =
                    "Add Impulse",

                TargetComponent =
                    nameof(CharacterController3D),

                Execute =
                    (instruction, context) =>
                    {
                        CharacterController3D? controller =
                            context.Self
                                .GetComponent<CharacterController3D>();

                        if (controller == null)
                        {
                            context.WarningSink?.Invoke(
                                $"{context.Self.Name} has no CharacterController3D.");

                            return;
                        }

                        Vector3 impulse =
                            EventValueResolver.GetVector3(
                                instruction,
                                "impulse",
                                context);

                        controller.AddImpulse(
                            impulse);
                    }
            });
    }

    private static VisualConditionDefinition
        CreateCharacterCondition(
            string id,
            string displayName,
            Func<CharacterController3D, bool> test)
    {
        return new VisualConditionDefinition
        {
            Id =
                id,

            Category =
                "Character Controller 3D",

            DisplayName =
                displayName,

            TargetComponent =
                nameof(CharacterController3D),

            Evaluate =
                (_, context) =>
                {
                    CharacterController3D? controller =
                        context.Self
                            .GetComponent<CharacterController3D>();

                    return
                        controller != null &&
                        test(controller);
                }
        };
    }

    private static void RegisterTransform(
        VisualLogicRegistry registry)
    {
        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "transform.setPosition",

                Category =
                    "Transform",

                DisplayName =
                    "Set Position",

                TargetComponent =
                    "Transform",

                Execute =
                    (instruction, context) =>
                    {
                        context.Self.Transform.WorldPosition =
                            EventValueResolver.GetVector3(
                                instruction,
                                "position",
                                context,
                                context.Self.Transform.WorldPosition);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "transform.move",

                Category =
                    "Transform",

                DisplayName =
                    "Move",

                TargetComponent =
                    "Transform",

                Execute =
                    (instruction, context) =>
                    {
                        context.Self.Transform.WorldPosition +=
                            EventValueResolver.GetVector3(
                                instruction,
                                "amount",
                                context);
                    }
            });

        registry.RegisterAction(
            CreateAxisPositionAction(
                "transform.setX",
                "Set X",
                0));

        registry.RegisterAction(
            CreateAxisPositionAction(
                "transform.setY",
                "Set Y",
                1));

        registry.RegisterAction(
            CreateAxisPositionAction(
                "transform.setZ",
                "Set Z",
                2));
    }

    private static VisualActionDefinition
        CreateAxisPositionAction(
            string id,
            string displayName,
            int axis)
    {
        return new VisualActionDefinition
        {
            Id =
                id,

            Category =
                "Transform",

            DisplayName =
                displayName,

            TargetComponent =
                "Transform",

            Execute =
                (instruction, context) =>
                {
                    Vector3 position =
                        context.Self.Transform.WorldPosition;

                    float value =
                        (float)EventValueResolver.GetNumber(
                            instruction,
                            "value",
                            context);

                    context.Self.Transform.WorldPosition =
                        axis switch
                        {
                            0 =>
                                new Vector3(
                                    value,
                                    position.Y,
                                    position.Z),

                            1 =>
                                new Vector3(
                                    position.X,
                                    value,
                                    position.Z),

                            _ =>
                                new Vector3(
                                    position.X,
                                    position.Y,
                                    value)
                        };
                }
        };
    }

    private static void RegisterVariables(
        VisualLogicRegistry registry)
    {
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id =
                    "variable.compare",

                Category =
                    "Variables",

                DisplayName =
                    "Compare Variable / Value",

                Evaluate =
                    (instruction, context) =>
                    {
                        if (!instruction.Arguments.TryGetValue(
                                "left",
                                out EventValue? leftValue))
                        {
                            return false;
                        }

                        if (!instruction.Arguments.TryGetValue(
                                "right",
                                out EventValue? rightValue))
                        {
                            return false;
                        }

                        if (!EventValueResolver.TryResolve(
                                leftValue,
                                context,
                                out object? left))
                        {
                            return false;
                        }

                        if (!EventValueResolver.TryResolve(
                                rightValue,
                                context,
                                out object? right))
                        {
                            return false;
                        }

                        string operation =
                            EventValueResolver.GetString(
                                instruction,
                                "operator",
                                context,
                                "==");

                        return Compare(
                            left,
                            right,
                            operation);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "variable.set",

                Category =
                    "Variables",

                DisplayName =
                    "Set Variable",

                Execute =
                    (instruction, context) =>
                    {
                        if (!TryGetVariableTarget(
                                instruction,
                                context,
                                out VariableReference? target))
                        {
                            return;
                        }

                        if (!instruction.Arguments.TryGetValue(
                                "value",
                                out EventValue? value))
                        {
                            return;
                        }

                        if (!EventValueResolver.TryResolve(
                                value,
                                context,
                                out object? resolved))
                        {
                            return;
                        }

                        VariableResolver.TrySet(
                            target!,
                            EventValueResolver.CreateVariableContext(
                                context),
                            resolved);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "variable.add",

                Category =
                    "Variables",

                DisplayName =
                    "Add To Variable",

                Execute =
                    (instruction, context) =>
                    {
                        ChangeNumberVariable(
                            instruction,
                            context,
                            1.0);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id =
                    "variable.subtract",

                Category =
                    "Variables",

                DisplayName =
                    "Subtract From Variable",

                Execute =
                    (instruction, context) =>
                    {
                        ChangeNumberVariable(
                            instruction,
                            context,
                            -1.0);
                    }
            });
    }

    private static void ChangeNumberVariable(
        VisualInstruction instruction,
        EventExecutionContext context,
        double sign)
    {
        if (!TryGetVariableTarget(
                instruction,
                context,
                out VariableReference? target))
        {
            return;
        }

        VariableResolutionContext variableContext =
            EventValueResolver.CreateVariableContext(
                context);

        if (!VariableResolver.TryGet(
                target!,
                variableContext,
                out object? existing))
        {
            return;
        }

        double amount =
            EventValueResolver.GetNumber(
                instruction,
                "amount",
                context);

        try
        {
            double result =
                Convert.ToDouble(existing) +
                amount * sign;

            VariableResolver.TrySet(
                target!,
                variableContext,
                result);
        }
        catch
        {
            context.WarningSink?.Invoke(
                "Add/Subtract Variable requires a numeric target.");
        }
    }

    private static bool TryGetVariableTarget(
        VisualInstruction instruction,
        EventExecutionContext context,
        out VariableReference? target)
    {
        if (EventValueResolver.TryGetReference(
                instruction,
                "target",
                out target))
        {
            return true;
        }

        context.WarningSink?.Invoke(
            "Variable action has no target VariableReference.");

        return false;
    }

    private static bool Compare(
        object? left,
        object? right,
        string operation)
    {
        if (TryConvertNumber(
                left,
                out double leftNumber) &&
            TryConvertNumber(
                right,
                out double rightNumber))
        {
            return operation switch
            {
                "==" =>
                    Math.Abs(
                        leftNumber -
                        rightNumber) <=
                    0.000001,

                "!=" =>
                    Math.Abs(
                        leftNumber -
                        rightNumber) >
                    0.000001,

                "<" =>
                    leftNumber <
                    rightNumber,

                "<=" =>
                    leftNumber <=
                    rightNumber,

                ">" =>
                    leftNumber >
                    rightNumber,

                ">=" =>
                    leftNumber >=
                    rightNumber,

                _ =>
                    false
            };
        }

        int comparison =
            string.Compare(
                Convert.ToString(left),
                Convert.ToString(right),
                StringComparison.OrdinalIgnoreCase);

        return operation switch
        {
            "==" =>
                comparison == 0,

            "!=" =>
                comparison != 0,

            "<" =>
                comparison < 0,

            "<=" =>
                comparison <= 0,

            ">" =>
                comparison > 0,

            ">=" =>
                comparison >= 0,

            _ =>
                false
        };
    }

    private static bool TryConvertNumber(
        object? value,
        out double number)
    {
        try
        {
            if (value == null)
            {
                number =
                    0.0;

                return false;
            }

            number =
                Convert.ToDouble(value);

            return true;
        }
        catch
        {
            number =
                0.0;

            return false;
        }
    }

    private static bool TryGetKey(
        VisualInstruction instruction,
        EventExecutionContext context,
        out Key key)
    {
        string value =
            EventValueResolver.GetString(
                instruction,
                "key",
                context);

        return Enum.TryParse(
            value,
            true,
            out key);
    }
}