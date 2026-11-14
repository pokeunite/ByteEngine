using System.Numerics;

namespace Wildlings;

/// <summary>Elements used by the prototype trio.</summary>
public enum WildlingElement
{
    Fire,
    Grass,
    Electric,
    Stone
}

/// <summary>
/// Data-driven creature definition. Values mirror Wildling Adventures GDD v0.2
/// (prototype tuning targets) so balance lives in one place instead of in code.
/// </summary>
public readonly record struct WildlingSpecies(
    string Id,
    string DisplayName,
    WildlingElement Element,
    float MaxHealth,
    float MoveSpeed,
    float BasicDamage,
    float BasicInterval,
    float AttackRange,
    float ProjectileSpeed,
    float SpecialDamage,
    float SpecialRadius,
    float SpecialCooldown,
    float UtilityCooldown,
    Vector4 ProjectileColor);

public static class WildlingSpeciesDatabase
{
    public const string FireLizardId = "fire_lizard";
    public const string SprigletId = "spriglet";
    public const string ZappetId = "zappet";

    private static readonly Dictionary<string, WildlingSpecies> Table = new(StringComparer.OrdinalIgnoreCase)
    {
        [FireLizardId] = new WildlingSpecies(
            FireLizardId, "Fire Lizard", WildlingElement.Fire,
            MaxHealth: 100f, MoveSpeed: 5.5f,
            BasicDamage: 12f, BasicInterval: .55f, AttackRange: 12f, ProjectileSpeed: 18f,
            SpecialDamage: 35f, SpecialRadius: 2.25f, SpecialCooldown: 6f, UtilityCooldown: 8f,
            ProjectileColor: new Vector4(.98f, .42f, .12f, 1f)),

        [SprigletId] = new WildlingSpecies(
            SprigletId, "Spriglet", WildlingElement.Grass,
            MaxHealth: 110f, MoveSpeed: 5f,
            BasicDamage: 8f, BasicInterval: .65f, AttackRange: 9f, ProjectileSpeed: 17f,
            SpecialDamage: 16f, SpecialRadius: 2.3f, SpecialCooldown: 7f, UtilityCooldown: 12f,
            ProjectileColor: new Vector4(.42f, .84f, .34f, 1f)),

        [ZappetId] = new WildlingSpecies(
            ZappetId, "Zappet", WildlingElement.Electric,
            MaxHealth: 95f, MoveSpeed: 6f,
            BasicDamage: 9f, BasicInterval: .45f, AttackRange: 10f, ProjectileSpeed: 20f,
            SpecialDamage: 24f, SpecialRadius: 3f, SpecialCooldown: 6.5f, UtilityCooldown: 8f,
            ProjectileColor: new Vector4(1f, .88f, .28f, 1f))
    };

    public static IReadOnlyCollection<string> Ids => Table.Keys;

    public static WildlingSpecies Default => Table[FireLizardId];

    public static bool TryGet(string? id, out WildlingSpecies species)
    {
        if (!string.IsNullOrWhiteSpace(id) && Table.TryGetValue(id.Trim(), out species))
        {
            return true;
        }

        species = Default;
        return false;
    }

    public static WildlingSpecies Get(string? id) => TryGet(id, out WildlingSpecies species) ? species : Default;
}
