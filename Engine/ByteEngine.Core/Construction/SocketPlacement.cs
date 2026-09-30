using System.Numerics;

namespace ByteEngine.Core.Construction;

/// <summary>Computes a ghost part's world transform from two mating socket frames.</summary>
public static class SocketPlacement
{
    public static Matrix4x4 Align(Matrix4x4 targetPartWorld, Matrix4x4 targetSocketLocal,
        Matrix4x4 movingSocketLocal, Matrix4x4 matingRotation)
    {
        if (!Matrix4x4.Invert(movingSocketLocal, out Matrix4x4 inverse))
            throw new ArgumentException("Moving socket transform is not invertible.", nameof(movingSocketLocal));
        // System.Numerics uses row-vector transforms: local * world.
        return inverse * matingRotation * targetSocketLocal * targetPartWorld;
    }
}
