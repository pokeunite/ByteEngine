using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Serialization;

/// <summary>
/// Single source of truth for the mapping between component runtime types and
/// their serialized names.
///
/// The component serializer populates this registry as it registers codecs, so
/// the editor metadata registry, the inspector property pipeline and the
/// browser contract can all validate against exactly the same set instead of
/// maintaining parallel hand-written lists. Plugin codecs and aliases flow
/// through the same path.
/// </summary>
public static class ComponentTypeRegistry
{
    private static readonly object Gate =
        new();

    private static readonly Dictionary<Type, string> CanonicalNameByType =
        new();

    private static readonly Dictionary<string, Type> ByName =
        new(
            StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Aliases =
        new(
            StringComparer.OrdinalIgnoreCase);

    private static readonly List<Type> Ordered =
        new();

    private static readonly HashSet<(Type Type, string Name)> Seen =
        new();

    /// <summary>Component types with a registered codec, in registration order.</summary>
    public static IReadOnlyList<Type> RegisteredTypes
    {
        get
        {
            lock (Gate)
            {
                return Ordered.ToArray();
            }
        }
    }

    /// <summary>Canonical serialized names, one per registered type.</summary>
    public static IReadOnlyCollection<string> CanonicalNames
    {
        get
        {
            lock (Gate)
            {
                return CanonicalNameByType.Values
                    .Distinct(
                        StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }

    /// <summary>Alias -> canonical name.</summary>
    public static IReadOnlyDictionary<string, string> RegisteredAliases
    {
        get
        {
            lock (Gate)
            {
                return new Dictionary<string, string>(
                    Aliases,
                    StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public static void Register(
        Type type,
        string name)
    {
        ArgumentNullException.ThrowIfNull(
            type);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            name);

        if (!typeof(Component).IsAssignableFrom(
                type))
        {
            throw new ArgumentException(
                $"'{type.FullName}' is not a Component type.",
                nameof(type));
        }

        lock (Gate)
        {
            // Serializers are constructed repeatedly (editor, player, tests),
            // so the same type/name pair is registered many times. Only the
            // first registration may change canonical names or add an alias.
            if (!Seen.Add(
                    (type, name)))
            {
                return;
            }

            if (CanonicalNameByType.TryGetValue(
                    type,
                    out string? previous) &&
                !string.Equals(
                    previous,
                    name,
                    StringComparison.Ordinal))
            {
                // Registering a type twice is how plugin compatibility names
                // are introduced today (for example the namespaced Desert
                // Terrain name followed by the stable short name). Keep the
                // last registration canonical and expose the earlier name as
                // an alias.
                Aliases[previous] = name;
            }

            CanonicalNameByType[type] = name;
            ByName[name] = type;

            if (!Ordered.Contains(
                    type))
            {
                Ordered.Add(
                    type);
            }
        }
    }

    public static void RegisterAlias(
        string alias,
        string canonical)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            alias);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            canonical);

        lock (Gate)
        {
            Aliases[alias] = canonical;
        }
    }

    public static bool IsRegistered(
        Type type)
    {
        lock (Gate)
        {
            return CanonicalNameByType.ContainsKey(
                type);
        }
    }

    public static string NameOf(
        Type type)
    {
        lock (Gate)
        {
            return CanonicalNameByType.TryGetValue(
                    type,
                    out string? name)
                ? name
                : type.Name;
        }
    }

    public static bool TryResolve(
        string name,
        out Type type)
    {
        lock (Gate)
        {
            if (ByName.TryGetValue(
                    name,
                    out type!))
            {
                return true;
            }

            if (Aliases.TryGetValue(
                    name,
                    out string? canonical) &&
                ByName.TryGetValue(
                    canonical,
                    out type!))
            {
                return true;
            }

            type = null!;
            return false;
        }
    }
}
