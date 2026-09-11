namespace ByteEngine.Core.VisualLogic;

public static class EventModuleTemplates
{
    public static EventModuleDefinition CreateEmpty(
        string name)
    {
        return new EventModuleDefinition
        {
            Id =
                Guid.NewGuid(),

            Version =
                1,

            Name =
                string.IsNullOrWhiteSpace(name)
                    ? "Event Module"
                    : name.Trim()
        };
    }

    public static EventModuleDefinition CreateCharacterMovement(
        string name)
    {
        var module =
            new EventModuleDefinition
            {
                Id =
                    Guid.NewGuid(),

                Version =
                    1,

                Name =
                    string.IsNullOrWhiteSpace(name)
                        ? "CharacterMovement"
                        : name.Trim(),

                RequiredComponents =
                    new List<string>
                    {
                        "CharacterController3D"
                    }
            };

        /*
         * W
         * →
         * Move Forward +1
         */
        module.Rules.Add(
            new EventRuleDefinition
            {
                Conditions =
                    new List<VisualInstruction>
                    {
                        KeyHeld(
                            "W"
                        )
                    },

                Actions =
                    new List<VisualInstruction>
                    {
                        CharacterMoveForward(
                            1.0
                        )
                    }
            }
        );

        /*
         * S
         * →
         * Move Forward -1
         */
        module.Rules.Add(
            new EventRuleDefinition
            {
                Conditions =
                    new List<VisualInstruction>
                    {
                        KeyHeld(
                            "S"
                        )
                    },

                Actions =
                    new List<VisualInstruction>
                    {
                        CharacterMoveForward(
                            -1.0
                        )
                    }
            }
        );

        /*
         * A
         * →
         * Move Right -1
         */
        module.Rules.Add(
            new EventRuleDefinition
            {
                Conditions =
                    new List<VisualInstruction>
                    {
                        KeyHeld(
                            "A"
                        )
                    },

                Actions =
                    new List<VisualInstruction>
                    {
                        CharacterMoveRight(
                            -1.0
                        )
                    }
            }
        );

        /*
         * D
         * →
         * Move Right +1
         */
        module.Rules.Add(
            new EventRuleDefinition
            {
                Conditions =
                    new List<VisualInstruction>
                    {
                        KeyHeld(
                            "D"
                        )
                    },

                Actions =
                    new List<VisualInstruction>
                    {
                        CharacterMoveRight(
                            1.0
                        )
                    }
            }
        );

        /*
         * Space Pressed
         * Is Grounded
         * →
         * Jump
         */
        module.Rules.Add(
            new EventRuleDefinition
            {
                Conditions =
                    new List<VisualInstruction>
                    {
                        KeyPressed(
                            "Space"
                        ),

                        new VisualInstruction
                        {
                            Id =
                                "character.isGrounded"
                        }
                    },

                Actions =
                    new List<VisualInstruction>
                    {
                        new VisualInstruction
                        {
                            Id =
                                "character.jump"
                        }
                    }
            }
        );

        return module;
    }

    private static VisualInstruction KeyHeld(
        string key)
    {
        return new VisualInstruction
        {
            Id =
                "input.keyHeld",

            Arguments =
                new Dictionary<string, EventValue>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["key"] =
                        EventValue.String(
                            key
                        )
                }
        };
    }

    private static VisualInstruction KeyPressed(
        string key)
    {
        return new VisualInstruction
        {
            Id =
                "input.keyPressed",

            Arguments =
                new Dictionary<string, EventValue>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["key"] =
                        EventValue.String(
                            key
                        )
                }
        };
    }

    private static VisualInstruction CharacterMoveForward(
        double amount)
    {
        return new VisualInstruction
        {
            Id =
                "character.moveForward",

            Arguments =
                new Dictionary<string, EventValue>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["amount"] =
                        EventValue.Number(
                            amount
                        )
                }
        };
    }

    private static VisualInstruction CharacterMoveRight(
        double amount)
    {
        return new VisualInstruction
        {
            Id =
                "character.moveRight",

            Arguments =
                new Dictionary<string, EventValue>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["amount"] =
                        EventValue.Number(
                            amount
                        )
                }
        };
    }
}