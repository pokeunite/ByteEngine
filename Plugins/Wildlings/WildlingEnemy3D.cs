using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Runtime identity stamped on a spawned hostile: what it is, and what capturing it is
/// worth. Read by the bond component so capture is species-true.
/// </summary>
public sealed class WildlingEnemy3D : Component
{
    public string SpeciesId { get; set; } = "cinderling";

    public string DisplayName { get; set; } = "Cinderling";

    public int EssencePerCapture { get; set; } = 1;

    public bool IsGuardian { get; set; }
}
