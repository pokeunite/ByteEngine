using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Variables;

public sealed class VariableResolutionContext
{
    public required VariableStore Globals { get; init; }
    public required Scene.Scene Scene { get; init; }
    public GameObject? Self { get; init; }
}

public static class VariableResolver
{
    private const BindingFlags PublicInstanceIgnoreCase =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;

    public static bool TryGet(
        VariableReference reference,
        VariableResolutionContext context,
        out object? value)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(context);
        value = null;

        VariableStore? store = ResolveStore(reference, context);
        if (store != null && store.TryGet(reference.MemberName, out VariableValue? variable))
        {
            value = variable!.BoxedValue;
            return true;
        }

        if (reference.Scope != VariableScope.Component) return false;
        object? root = ResolveComponent(reference, context);
        return root != null && TryReadPath(root, reference.MemberName, out value);
    }

    public static bool TrySet(
        VariableReference reference,
        VariableResolutionContext context,
        object? value)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(context);

        VariableStore? store = ResolveStore(reference, context);
        if (store != null && store.TryGet(reference.MemberName, out VariableValue? variable))
            return AssignVariable(variable!, value);

        if (reference.Scope != VariableScope.Component) return false;
        object? root = ResolveComponent(reference, context);
        return root != null && TryWritePath(root, reference.MemberName, value);
    }

    private static VariableStore? ResolveStore(
        VariableReference reference,
        VariableResolutionContext context) => reference.Scope switch
        {
            VariableScope.Global => context.Globals,
            VariableScope.Scene => context.Scene.Variables,
            VariableScope.Self => context.Self?.Variables,
            VariableScope.Object => ResolveObject(reference, context)?.Variables,
            _ => null
        };

    private static object? ResolveComponent(
        VariableReference reference,
        VariableResolutionContext context)
    {
        GameObject? target = ResolveObject(reference, context) ?? context.Self;
        return reference.ComponentType is "Transform"
            ? target?.Transform
            : target?.Components.FirstOrDefault(
                component => MatchesType(component.GetType(), reference.ComponentType));
    }

    private static GameObject? ResolveObject(
        VariableReference reference,
        VariableResolutionContext context) => reference.ObjectId.HasValue
            ? context.Scene.FindGameObject(reference.ObjectId.Value)
            : !string.IsNullOrWhiteSpace(reference.ObjectName)
                ? context.Scene.FindGameObject(reference.ObjectName)
                : null;

    private static bool MatchesType(Type type, string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (type.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
         type.FullName?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

    private static bool TryReadPath(object root, string path, out object? value)
    {
        object? current = root;
        foreach (string segment in SplitPath(path))
        {
            if (current == null || !TryGetMember(current.GetType(), segment, out MemberInfo? member))
            {
                value = null;
                return false;
            }

            current = GetMemberValue(current, member);
        }

        value = current;
        return true;
    }

    private static bool TryWritePath(object root, string path, object? value)
    {
        string[] segments = SplitPath(path);
        if (segments.Length == 0) return false;

        try
        {
            var parents = new List<(object Owner, MemberInfo Member)>();
            object current = root;

            for (int index = 0; index < segments.Length - 1; index++)
            {
                if (!TryGetMember(current.GetType(), segments[index], out MemberInfo? member))
                    return false;

                object? next = GetMemberValue(current, member);
                if (next == null) return false;
                parents.Add((current, member));
                current = next;
            }

            if (!TryGetMember(current.GetType(), segments[^1], out MemberInfo? leaf) ||
                !TrySetMemberValue(current, leaf, value))
                return false;

            object updatedChild = current;
            for (int index = parents.Count - 1; index >= 0; index--)
            {
                (object owner, MemberInfo member) = parents[index];
                if (GetMemberType(member).IsValueType &&
                    !TrySetMemberValue(owner, member, updatedChild))
                    return false;

                updatedChild = owner;
            }

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string[] SplitPath(string path) =>
        path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryGetMember(
        Type type,
        string name,
        [NotNullWhen(true)] out MemberInfo? member)
    {
        member = type.GetProperty(name, PublicInstanceIgnoreCase) ??
                 (MemberInfo?)type.GetField(name, PublicInstanceIgnoreCase);
        return member != null;
    }

    private static object? GetMemberValue(object owner, MemberInfo member) => member switch
    {
        PropertyInfo property => property.GetValue(owner),
        FieldInfo field => field.GetValue(owner),
        _ => null
    };

    private static Type GetMemberType(MemberInfo member) => member switch
    {
        PropertyInfo property => property.PropertyType,
        FieldInfo field => field.FieldType,
        _ => throw new ArgumentException("Unsupported member type.", nameof(member))
    };

    private static bool TrySetMemberValue(object owner, MemberInfo member, object? value)
    {
        Type memberType = GetMemberType(member);
        object? converted = ConvertValue(value, memberType);

        switch (member)
        {
            case PropertyInfo { CanWrite: true } property:
                property.SetValue(owner, converted);
                return true;
            case FieldInfo field when !field.IsInitOnly:
                field.SetValue(owner, converted);
                return true;
            default:
                return false;
        }
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value == null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (effectiveType.IsEnum)
            return value is string text
                ? Enum.Parse(effectiveType, text, true)
                : Enum.ToObject(effectiveType, value);

        return Convert.ChangeType(value, effectiveType);
    }

    private static bool AssignVariable(VariableValue variable, object? value)
    {
        try
        {
            switch (variable.Type)
            {
                case VariableType.Number:
                    variable.Number = Convert.ToDouble(value);
                    break;
                case VariableType.String:
                    variable.String = Convert.ToString(value) ?? string.Empty;
                    break;
                case VariableType.Boolean:
                    variable.Boolean = Convert.ToBoolean(value);
                    break;
                case VariableType.Vector2 when value is Vector2 vector2:
                    variable.Vector2 = vector2;
                    break;
                case VariableType.Vector3 when value is Vector3 vector3:
                    variable.Vector3 = vector3;
                    break;
                default:
                    return false;
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidCastException or FormatException or OverflowException)
        {
            return false;
        }
    }
}
