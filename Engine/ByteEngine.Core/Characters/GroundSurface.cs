using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Characters;
public sealed class GroundSurface : Component
{
    public bool Walkable { get; set; } = true;
    public string SurfaceType { get; set; } = "Default";
    public float Friction { get; set; } = 1f;
}
