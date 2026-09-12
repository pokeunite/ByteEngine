using System.Numerics;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
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
        bool> Evaluate
    { get; init; }
}

public sealed class VisualActionDefinition
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string DisplayName { get; init; }
    public string? TargetComponent { get; init; }

    public required Action<
        VisualInstruction,
        EventExecutionContext> Execute
    { get; init; }
}

public sealed class VisualLogicRegistry
{
    private const string SelfTarget = "Self";

    private readonly Dictionary<string, VisualConditionDefinition> _conditions =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, VisualActionDefinition> _actions =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<VisualConditionDefinition> Conditions =>
        _conditions.Values;

    public IReadOnlyCollection<VisualActionDefinition> Actions =>
        _actions.Values;

    public void RegisterCondition(
        VisualConditionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _conditions[definition.Id] = definition;
    }

    public void RegisterAction(
        VisualActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _actions[definition.Id] = definition;
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
        RegisterObjects(registry);
        RegisterCharacter(registry);
        RegisterTransform(registry);
        RegisterVariables(registry);
        RegisterGameplay(registry);

        return registry;
    }

    private static void RegisterSystem(
        VisualLogicRegistry registry)
    {
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "system.always",
                Category = "System",
                DisplayName = "Always",
                Evaluate = (_, _) => true
            });

        /*
         * Trigger Once is handled specially by EventModuleRuntime.
         */
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "system.triggerOnce",
                Category = "System",
                DisplayName = "Trigger Once",
                Evaluate = (_, _) => true
            });
    }

    private static void RegisterInput(
        VisualLogicRegistry registry)
    {
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "input.keyHeld",
                Category = "Input",
                DisplayName = "Key Is Held",
                Evaluate =
                    (instruction, context) =>
                        TryGetKey(
                            instruction,
                            context,
                            out Key key) &&
                        Input.IsKeyDown(key)
            });

        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "input.keyPressed",
                Category = "Input",
                DisplayName = "Key Pressed",
                Evaluate =
                    (instruction, context) =>
                        TryGetKey(
                            instruction,
                            context,
                            out Key key) &&
                        Input.IsKeyPressed(key)
            });

        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "input.keyReleased",
                Category = "Input",
                DisplayName = "Key Released",
                Evaluate =
                    (instruction, context) =>
                        TryGetKey(
                            instruction,
                            context,
                            out Key key) &&
                        Input.IsKeyReleased(key)
            });
    }

    private static void RegisterGameplay(VisualLogicRegistry registry)
    {
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "health.isDead",
            Category = "Gameplay",
            DisplayName = "Health Is Dead",
            TargetComponent = nameof(HealthComponent),
            Evaluate = (instruction, context) => ResolveObjectTarget(instruction, context, false)?.GetComponent<HealthComponent>()?.IsDead == true
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "health.percentAtMost",
            Category = "Gameplay",
            DisplayName = "Health Percent <= Value",
            TargetComponent = nameof(HealthComponent),
            Evaluate = (instruction, context) => ResolveObjectTarget(instruction, context, false)?.GetComponent<HealthComponent>() is { } health &&
                health.HealthPercent <= EventValueResolver.GetNumber(instruction, "value", context, 1.0)
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "arena.won",
            Category = "Gameplay",
            DisplayName = "Arena Won",
            TargetComponent = nameof(ArenaGameManager),
            Evaluate = (instruction, context) => ResolveObjectTarget(instruction, context, false)?.GetComponent<ArenaGameManager>()?.GameState == ArenaGameState.Won
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "arena.lost",
            Category = "Gameplay",
            DisplayName = "Arena Lost",
            TargetComponent = nameof(ArenaGameManager),
            Evaluate = (instruction, context) => ResolveObjectTarget(instruction, context, false)?.GetComponent<ArenaGameManager>()?.GameState == ArenaGameState.Lost
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "health.damage",
            Category = "Gameplay",
            DisplayName = "Damage Object",
            TargetComponent = nameof(HealthComponent),
            Execute = (instruction, context) => ResolveObjectTarget(instruction, context)?.GetComponent<HealthComponent>()?
                .Damage((float)EventValueResolver.GetNumber(instruction, "amount", context, 10.0))
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "health.heal",
            Category = "Gameplay",
            DisplayName = "Heal Object",
            TargetComponent = nameof(HealthComponent),
            Execute = (instruction, context) => ResolveObjectTarget(instruction, context)?.GetComponent<HealthComponent>()?
                .Heal((float)EventValueResolver.GetNumber(instruction, "amount", context, 10.0))
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "projectile.fire",
            Category = "Gameplay",
            DisplayName = "Fire Projectile",
            TargetComponent = nameof(ProjectileLauncher3D),
            Execute = (instruction, context) => ResolveObjectTarget(instruction, context)?.GetComponent<ProjectileLauncher3D>()?.Fire()
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "arena.restart",
            Category = "Gameplay",
            DisplayName = "Restart Arena",
            TargetComponent = nameof(ArenaGameManager),
            Execute = (instruction, context) => ResolveObjectTarget(instruction, context)?.GetComponent<ArenaGameManager>()?.Restart()
        });
    }

    private static void RegisterObjects(
        VisualLogicRegistry registry)
    {
        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "object.exists",
                Category = "Object",
                DisplayName = "Object Exists",
                Evaluate =
                    (instruction, context) =>
                        ResolveObjectTarget(
                            instruction,
                            context,
                            warnIfMissing: false) != null
            });

        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "object.isActive",
                Category = "Object",
                DisplayName = "Object Is Active",
                Evaluate =
                    (instruction, context) =>
                        ResolveObjectTarget(
                            instruction,
                            context,
                            warnIfMissing: false)?
                            .ActiveInHierarchy == true
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "object.destroySelf",
                Category = "Object",
                DisplayName = "Destroy Self",
                Execute =
                    (_, context) =>
                        context.Scene.DestroyGameObject(
                            context.Self)
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "object.destroy",
                Category = "Object",
                DisplayName = "Destroy Object",
                Execute =
                    (instruction, context) =>
                    {
                        GameObject? target =
                            ResolveObjectTarget(
                                instruction,
                                context);

                        if (target != null)
                        {
                            context.Scene.DestroyGameObject(
                                target);
                        }
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "object.setActive",
                Category = "Object",
                DisplayName = "Set Object Active",
                Execute =
                    (instruction, context) =>
                    {
                        GameObject? target =
                            ResolveObjectTarget(
                                instruction,
                                context);

                        if (target == null)
                        {
                            return;
                        }

                        target.Active =
                            EventValueResolver.GetBoolean(
                                instruction,
                                "active",
                                context,
                                true);
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
                Id = "character.moveForward",
                Category = "Character Controller 3D",
                DisplayName = "Move Forward",
                TargetComponent = nameof(CharacterController3D),
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

                        controller.MoveForward(
                            (float)EventValueResolver.GetNumber(
                                instruction,
                                "amount",
                                context,
                                1.0));
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "character.moveRight",
                Category = "Character Controller 3D",
                DisplayName = "Move Right",
                TargetComponent = nameof(CharacterController3D),
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

                        controller.MoveRight(
                            (float)EventValueResolver.GetNumber(
                                instruction,
                                "amount",
                                context,
                                1.0));
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "character.jump",
                Category = "Character Controller 3D",
                DisplayName = "Jump",
                TargetComponent = nameof(CharacterController3D),
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
                Id = "character.setVelocity",
                Category = "Character Controller 3D",
                DisplayName = "Set Velocity",
                TargetComponent = nameof(CharacterController3D),
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

                        controller.SetVelocity(
                            EventValueResolver.GetVector3(
                                instruction,
                                "velocity",
                                context));
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "character.addImpulse",
                Category = "Character Controller 3D",
                DisplayName = "Add Impulse",
                TargetComponent = nameof(CharacterController3D),
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

                        controller.AddImpulse(
                            EventValueResolver.GetVector3(
                                instruction,
                                "impulse",
                                context));
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
            Id = id,
            Category = "Character Controller 3D",
            DisplayName = displayName,
            TargetComponent = nameof(CharacterController3D),
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
                Id = "transform.setPosition",
                Category = "Transform",
                DisplayName = "Set Position",
                TargetComponent = "Transform",
                Execute =
                    (instruction, context) =>
                    {
                        GameObject? target =
                            ResolveObjectTarget(
                                instruction,
                                context);

                        if (target == null)
                        {
                            return;
                        }

                        target.Transform.WorldPosition =
                            EventValueResolver.GetVector3(
                                instruction,
                                "position",
                                context,
                                target.Transform.WorldPosition);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "transform.move",
                Category = "Transform",
                DisplayName = "Move",
                TargetComponent = "Transform",
                Execute =
                    (instruction, context) =>
                    {
                        GameObject? target =
                            ResolveObjectTarget(
                                instruction,
                                context);

                        if (target == null)
                        {
                            return;
                        }

                        target.Transform.WorldPosition +=
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

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "transform.setRotation",
                Category = "Transform",
                DisplayName = "Set Rotation",
                TargetComponent = "Transform",
                Execute =
                    (instruction, context) =>
                    {
                        GameObject? target =
                            ResolveObjectTarget(
                                instruction,
                                context);

                        if (target == null)
                        {
                            return;
                        }

                        target.Transform.EulerAngles =
                            EventValueResolver.GetVector3(
                                instruction,
                                "rotation",
                                context,
                                target.Transform.EulerAngles);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "transform.rotateBy",
                Category = "Transform",
                DisplayName = "Rotate By",
                TargetComponent = "Transform",
                Execute =
                    (instruction, context) =>
                    {
                        GameObject? target =
                            ResolveObjectTarget(
                                instruction,
                                context);

                        if (target == null)
                        {
                            return;
                        }

                        target.Transform.EulerAngles +=
                            EventValueResolver.GetVector3(
                                instruction,
                                "amount",
                                context);
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "transform.setScale",
                Category = "Transform",
                DisplayName = "Set Scale",
                TargetComponent = "Transform",
                Execute =
                    (instruction, context) =>
                    {
                        GameObject? target =
                            ResolveObjectTarget(
                                instruction,
                                context);

                        if (target == null)
                        {
                            return;
                        }

                        target.Transform.LocalScale =
                            EventValueResolver.GetVector3(
                                instruction,
                                "scale",
                                context,
                                target.Transform.LocalScale);
                    }
            });
    }

    private static VisualActionDefinition
        CreateAxisPositionAction(
            string id,
            string displayName,
            int axis)
    {
        return new VisualActionDefinition
        {
            Id = id,
            Category = "Transform",
            DisplayName = displayName,
            TargetComponent = "Transform",
            Execute =
                (instruction, context) =>
                {
                    GameObject? target =
                        ResolveObjectTarget(
                            instruction,
                            context);

                    if (target == null)
                    {
                        return;
                    }

                    Vector3 position =
                        target.Transform.WorldPosition;

                    float value =
                        (float)EventValueResolver.GetNumber(
                            instruction,
                            "value",
                            context);

                    target.Transform.WorldPosition =
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
                Id = "variable.compare",
                Category = "Variables",
                DisplayName = "Compare Variable / Value",
                Evaluate =
                    (instruction, context) =>
                    {
                        if (!instruction.Arguments.TryGetValue(
                                "left",
                                out EventValue? leftValue) ||
                            !instruction.Arguments.TryGetValue(
                                "right",
                                out EventValue? rightValue))
                        {
                            return false;
                        }

                        if (!EventValueResolver.TryResolve(
                                leftValue,
                                context,
                                out object? left) ||
                            !EventValueResolver.TryResolve(
                                rightValue,
                                context,
                                out object? right))
                        {
                            return false;
                        }

                        return Compare(
                            left,
                            right,
                            EventValueResolver.GetString(
                                instruction,
                                "operator",
                                context,
                                "=="));
                    }
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "variable.set",
                Category = "Variables",
                DisplayName = "Set Variable",
                Execute =
                    (instruction, context) =>
                    {
                        if (!TryGetVariableTarget(
                                instruction,
                                context,
                                out VariableReference? target) ||
                            !instruction.Arguments.TryGetValue(
                                "value",
                                out EventValue? value) ||
                            !EventValueResolver.TryResolve(
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
                Id = "variable.add",
                Category = "Variables",
                DisplayName = "Add To Variable",
                Execute =
                    (instruction, context) =>
                        ChangeNumberVariable(
                            instruction,
                            context,
                            1.0)
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "variable.subtract",
                Category = "Variables",
                DisplayName = "Subtract From Variable",
                Execute =
                    (instruction, context) =>
                        ChangeNumberVariable(
                            instruction,
                            context,
                            -1.0)
            });

        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "variable.toggle",
                Category = "Variables",
                DisplayName = "Toggle Boolean",
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

                        try
                        {
                            bool current =
                                Convert.ToBoolean(
                                    existing);

                            VariableResolver.TrySet(
                                target!,
                                variableContext,
                                !current);
                        }
                        catch
                        {
                            context.WarningSink?.Invoke(
                                "Toggle Boolean requires a Boolean target.");
                        }
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
                Convert.ToDouble(
                    existing) +
                amount *
                sign;

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

    private static GameObject? ResolveObjectTarget(
        VisualInstruction instruction,
        EventExecutionContext context,
        bool warnIfMissing = true)
    {
        string token =
            EventValueResolver.GetString(
                instruction,
                "target",
                context,
                SelfTarget);

        if (string.IsNullOrWhiteSpace(
                token) ||
            string.Equals(
                token,
                SelfTarget,
                StringComparison.OrdinalIgnoreCase))
        {
            return context.Self;
        }

        GameObject? target =
            null;

        if (token.StartsWith(
                "id:",
                StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(
                token[3..],
                out Guid objectId))
        {
            target =
                context.Scene.FindGameObject(
                    objectId);
        }
        else
        {
            /*
             * Name lookup is intentionally retained as a fallback
             * for manually-authored/older Event Modules.
             */
            target =
                context.Scene.FindGameObject(
                    token);
        }

        if (target == null &&
            warnIfMissing)
        {
            context.WarningSink?.Invoke(
                $"Event target '{token}' was not found in scene '{context.Scene.Name}'.");
        }

        return target;
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
                Convert.ToString(
                    left),
                Convert.ToString(
                    right),
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
                Convert.ToDouble(
                    value);

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
