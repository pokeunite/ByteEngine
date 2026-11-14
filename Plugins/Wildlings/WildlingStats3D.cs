using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Species identity plus the stats derived from the species definition.
/// Attach one per deployed Wildling; it pushes the derived numbers into the
/// engine's HealthComponent and CharacterController3D so the rest of the engine
/// (damage, death, locomotion) keeps working unchanged.
/// </summary>
public sealed class WildlingStats3D : Component
{
    /// <summary>Species definition key, e.g. fire_lizard, spriglet, zappet.</summary>
    public string Species { get; set; } = WildlingSpeciesDatabase.FireLizardId;

    /// <summary>Individual quality band, 0.96-1.12 in the GDD. 1 = baseline.</summary>
    public float QualityMultiplier { get; set; } = 1f;

    public int Level { get; set; } = 1;

    public bool ApplyOnStart { get; set; } = true;

    public WildlingSpecies Definition => WildlingSpeciesDatabase.Get(Species);

    public float Quality => Math.Clamp(QualityMultiplier <= 0f ? 1f : QualityMultiplier, .8f, 1.2f);

    public float MaxHealth => Definition.MaxHealth * Quality;

    public float MoveSpeed => Definition.MoveSpeed;

    public float BasicDamage => Definition.BasicDamage * Quality;

    public float SpecialDamage => Definition.SpecialDamage * Quality;

    public HealthComponent? Health => GameObject?.GetComponent<HealthComponent>();

    public CharacterController3D? Motor => GameObject?.GetComponent<CharacterController3D>();

    protected override void OnStart()
    {
        if (ApplyOnStart)
        {
            ApplyToBody();
        }
    }

    /// <summary>Writes the species HP and move speed onto the engine components.</summary>
    public void ApplyToBody()
    {
        if (Health is { } health)
        {
            health.MaxHealth = MaxHealth;
            health.CurrentHealth = MaxHealth;
        }

        ApplyMoveSpeedOnly();
    }

    /// <summary>Restores only the locomotion speed, leaving current HP untouched.</summary>
    public void ApplyMoveSpeedOnly()
    {
        if (Motor is { } motor)
        {
            motor.MoveSpeed = MoveSpeed;
        }
    }
}
