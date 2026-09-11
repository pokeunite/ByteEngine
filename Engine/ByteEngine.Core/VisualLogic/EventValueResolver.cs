using System.Numerics;

using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public static class EventValueResolver
{
    public static bool TryResolve(
        EventValue value,
        EventExecutionContext context,
        out object? result)
    {
        ArgumentNullException.ThrowIfNull(
            value);

        ArgumentNullException.ThrowIfNull(
            context);

        if (value.Kind ==
            EventValueKind.Constant)
        {
            result =
                value.Constant.BoxedValue;

            return true;
        }

        if (value.Reference == null)
        {
            result =
                null;

            return false;
        }

        return VariableResolver.TryGet(
            value.Reference,
            CreateVariableContext(context),
            out result);
    }

    public static bool TryGetReference(
        VisualInstruction instruction,
        string argumentName,
        out VariableReference? reference)
    {
        reference =
            null;

        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value))
        {
            return false;
        }

        if (value.Kind !=
            EventValueKind.Reference)
        {
            return false;
        }

        if (value.Reference == null)
        {
            return false;
        }

        reference =
            value.Reference;

        return true;
    }

    public static string GetString(
        VisualInstruction instruction,
        string argumentName,
        EventExecutionContext context,
        string fallback = "")
    {
        if (!TryGetRaw(
                instruction,
                argumentName,
                context,
                out object? raw))
        {
            return fallback;
        }

        if (raw == null)
        {
            return fallback;
        }

        return Convert.ToString(raw) ??
               fallback;
    }

    public static double GetNumber(
        VisualInstruction instruction,
        string argumentName,
        EventExecutionContext context,
        double fallback = 0.0)
    {
        if (!TryGetRaw(
                instruction,
                argumentName,
                context,
                out object? raw))
        {
            return fallback;
        }

        if (raw == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToDouble(raw);
        }
        catch
        {
            return fallback;
        }
    }

    public static bool GetBoolean(
        VisualInstruction instruction,
        string argumentName,
        EventExecutionContext context,
        bool fallback = false)
    {
        if (!TryGetRaw(
                instruction,
                argumentName,
                context,
                out object? raw))
        {
            return fallback;
        }

        if (raw == null)
        {
            return fallback;
        }

        try
        {
            return Convert.ToBoolean(raw);
        }
        catch
        {
            return fallback;
        }
    }

    public static Vector3 GetVector3(
        VisualInstruction instruction,
        string argumentName,
        EventExecutionContext context,
        Vector3 fallback = default)
    {
        if (!TryGetRaw(
                instruction,
                argumentName,
                context,
                out object? raw))
        {
            return fallback;
        }

        return raw switch
        {
            Vector3 value =>
                value,

            Vector2 value =>
                new Vector3(
                    value,
                    0.0f),

            _ =>
                fallback
        };
    }

    private static bool TryGetRaw(
        VisualInstruction instruction,
        string argumentName,
        EventExecutionContext context,
        out object? result)
    {
        result =
            null;

        if (!instruction.Arguments.TryGetValue(
                argumentName,
                out EventValue? value))
        {
            return false;
        }

        return TryResolve(
            value,
            context,
            out result);
    }

    internal static VariableResolutionContext CreateVariableContext(
        EventExecutionContext context)
    {
        return new VariableResolutionContext
        {
            Globals =
                context.Globals,

            Scene =
                context.Scene,

            Self =
                context.Self
        };
    }
}