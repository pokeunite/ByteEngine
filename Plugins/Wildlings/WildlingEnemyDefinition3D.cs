using System.Numerics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Authored enemy kind (GDD v0.3 enemy variety). Place one of these on a data-only scene
/// object and list its name in the spawner's definitions; every stat, colour and the
/// species identity used for capture lives here, not in code.
/// </summary>
public sealed class WildlingEnemyDefinition3D : Component
{
    /// <summary>Species identity used by capture/collection (e.g. <c>cinderling</c>).</summary>
    public string SpeciesId { get; set; } = "cinderling";

    public string DisplayName { get; set; } = "Cinderling";

    public float Health { get; set; } = 30f;

    public float MoveSpeed { get; set; } = 2.6f;

    public float Damage { get; set; } = 6f;

    public float AttackCooldownSeconds { get; set; } = 1.2f;

    public float AttackRange { get; set; } = 1.9f;

    public float DetectionRange { get; set; } = 40f;

    public float Scale { get; set; } = 1.2f;

    public Vector4 Color { get; set; } = new(.95f, .35f, .15f, 1f);

    public int EssencePerCapture { get; set; } = 1;

    /// <summary>Relative chance of this kind being picked when a spawn rolls.</summary>
    public int Weight { get; set; } = 1;
}
