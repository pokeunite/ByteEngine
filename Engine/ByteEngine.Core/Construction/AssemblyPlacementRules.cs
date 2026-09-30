using System.Numerics;

namespace ByteEngine.Core.Construction;

public readonly record struct AssemblyPlacementBody(
    Guid Id, Matrix4x4 WorldPose, Vector3 Size, float Mass);

/// <summary>Cheap conservative overlap and mass budget check before making a physical part.</summary>
public static class AssemblyPlacementRules
{
    public static bool Validate(AssemblyPlacementBody candidate,
        IReadOnlyCollection<AssemblyPlacementBody> existing,
        float maximumAssemblyMass, out string reason)
    {
        if (candidate.Id == Guid.Empty || candidate.Mass <= 0 ||
            !float.IsFinite(candidate.Mass) || candidate.Size.X <= 0 ||
            candidate.Size.Y <= 0 || candidate.Size.Z <= 0 ||
            !float.IsFinite(candidate.Size.X) || !float.IsFinite(candidate.Size.Y) ||
            !float.IsFinite(candidate.Size.Z))
        {
            reason = "Part has an invalid body size or mass.";
            return false;
        }
        float totalMass = candidate.Mass;
        foreach (AssemblyPlacementBody body in existing) totalMass += body.Mass;
        if (!float.IsFinite(totalMass) || totalMass > maximumAssemblyMass)
        {
            reason = "Machine exceeds the mass budget.";
            return false;
        }
        (Vector3 minimum, Vector3 maximum) = Bounds(candidate);
        foreach (AssemblyPlacementBody body in existing)
        {
            (Vector3 otherMin, Vector3 otherMax) = Bounds(body);
            const float tolerance = .01f;
            if (minimum.X < otherMax.X - tolerance && maximum.X > otherMin.X + tolerance &&
                minimum.Y < otherMax.Y - tolerance && maximum.Y > otherMin.Y + tolerance &&
                minimum.Z < otherMax.Z - tolerance && maximum.Z > otherMin.Z + tolerance)
            {
                reason = "Part overlaps another machine body.";
                return false;
            }
        }
        reason = string.Empty;
        return true;
    }

    private static (Vector3 Minimum, Vector3 Maximum) Bounds(AssemblyPlacementBody body)
    {
        Vector3 half = body.Size * .5f;
        Vector3 minimum = new(float.PositiveInfinity);
        Vector3 maximum = new(float.NegativeInfinity);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = Vector3.Transform(new Vector3(x * half.X,
                        y * half.Y, z * half.Z), body.WorldPose);
                    minimum = Vector3.Min(minimum, corner);
                    maximum = Vector3.Max(maximum, corner);
                }
        return (minimum, maximum);
    }
}
