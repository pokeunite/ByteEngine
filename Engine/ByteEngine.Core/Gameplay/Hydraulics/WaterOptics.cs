using System.Numerics;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Homogeneous water-volume coefficients, in inverse metres. Independent reference
/// equations for shader validation; no render resources or game-specific values.</summary>
public static class WaterOptics
{
    public static Vector3 Transmission(Vector3 absorption,Vector3 scattering,float distance)
    {
        var total=Vector3.Max(Vector3.Zero,absorption)+Vector3.Max(Vector3.Zero,scattering);distance=Math.Max(0,distance);
        return new(MathF.Exp(-total.X*distance),MathF.Exp(-total.Y*distance),MathF.Exp(-total.Z*distance));
    }
    public static Vector3 ScatteredFraction(Vector3 absorption,Vector3 scattering,float distance)
    {
        scattering=Vector3.Max(Vector3.Zero,scattering);var total=Vector3.Max(new(.000001f),Vector3.Max(Vector3.Zero,absorption)+scattering);
        return scattering/total*(Vector3.One-Transmission(absorption,scattering,distance));
    }
}
