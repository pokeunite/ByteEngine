using System.Numerics;

using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public enum EventValueKind
{
    Constant,
    Reference
}

public sealed class EventValue
{
    public EventValueKind Kind { get; set; } =
        EventValueKind.Constant;

    public VariableValue Constant { get; set; } =
        VariableValue.FromNumber();

    public VariableReference? Reference { get; set; }

    public static EventValue Number(
        double value)
    {
        return new EventValue
        {
            Kind = EventValueKind.Constant,
            Constant =
                VariableValue.FromNumber(value)
        };
    }

    public static EventValue String(
        string value)
    {
        return new EventValue
        {
            Kind = EventValueKind.Constant,
            Constant =
                VariableValue.FromString(value)
        };
    }

    public static EventValue Boolean(
        bool value)
    {
        return new EventValue
        {
            Kind = EventValueKind.Constant,
            Constant =
                VariableValue.FromBoolean(value)
        };
    }

    public static EventValue Vector2(
        Vector2 value)
    {
        return new EventValue
        {
            Kind = EventValueKind.Constant,
            Constant =
                VariableValue.FromVector2(value)
        };
    }

    public static EventValue Vector3(
        Vector3 value)
    {
        return new EventValue
        {
            Kind = EventValueKind.Constant,
            Constant =
                VariableValue.FromVector3(value)
        };
    }

    public static EventValue FromReference(
        VariableReference reference)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        return new EventValue
        {
            Kind = EventValueKind.Reference,
            Reference = reference
        };
    }
}