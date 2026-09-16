using System.Numerics;

using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Samples importer-neutral node transform tracks.
/// </summary>
internal static class AnimationPoseSampler
{
    public static Vector3 Sample(
        ImportedVectorTrack? track,
        float time,
        Vector3 fallback)
    {
        if (track == null || track.Keys.Count == 0)
        {
            return fallback;
        }

        IReadOnlyList<ImportedVectorKey> keys = track.Keys;

        if (keys.Count == 1 || time <= keys[0].Time)
        {
            return keys[0].Value;
        }

        if (time >= keys[^1].Time)
        {
            return keys[^1].Value;
        }

        int upper = FindUpperKey(keys, time);
        ImportedVectorKey left = keys[upper - 1];
        ImportedVectorKey right = keys[upper];

        float duration = Math.Max(right.Time - left.Time, 0.000001f);
        float amount = Math.Clamp((time - left.Time) / duration, 0.0f, 1.0f);

        return track.Interpolation switch
        {
            ImportedAnimationInterpolation.Step => left.Value,
            ImportedAnimationInterpolation.CubicSpline =>
                Hermite(
                    left.Value,
                    left.OutTangent,
                    right.Value,
                    right.InTangent,
                    amount,
                    duration),
            _ => Vector3.Lerp(left.Value, right.Value, amount)
        };
    }

    public static Quaternion Sample(
        ImportedQuaternionTrack? track,
        float time,
        Quaternion fallback)
    {
        if (track == null || track.Keys.Count == 0)
        {
            return NormalizeSafe(fallback);
        }

        IReadOnlyList<ImportedQuaternionKey> keys = track.Keys;

        if (keys.Count == 1 || time <= keys[0].Time)
        {
            return NormalizeSafe(keys[0].Value);
        }

        if (time >= keys[^1].Time)
        {
            return NormalizeSafe(keys[^1].Value);
        }

        int upper = FindUpperKey(keys, time);
        ImportedQuaternionKey left = keys[upper - 1];
        ImportedQuaternionKey right = keys[upper];

        float duration = Math.Max(right.Time - left.Time, 0.000001f);
        float amount = Math.Clamp((time - left.Time) / duration, 0.0f, 1.0f);

        if (track.Interpolation == ImportedAnimationInterpolation.Step)
        {
            return NormalizeSafe(left.Value);
        }

        if (track.Interpolation == ImportedAnimationInterpolation.CubicSpline)
        {
            Vector4 cubic = Hermite(
                ToVector4(left.Value),
                ToVector4(left.OutTangent),
                ToVector4(right.Value),
                ToVector4(right.InTangent),
                amount,
                duration);

            return NormalizeSafe(
                new Quaternion(cubic.X, cubic.Y, cubic.Z, cubic.W));
        }

        return Quaternion.Slerp(
            NormalizeSafe(left.Value),
            NormalizeSafe(right.Value),
            amount);
    }

    private static int FindUpperKey(
        IReadOnlyList<ImportedVectorKey> keys,
        float time)
    {
        int low = 1;
        int high = keys.Count - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (keys[middle].Time < time)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static int FindUpperKey(
        IReadOnlyList<ImportedQuaternionKey> keys,
        float time)
    {
        int low = 1;
        int high = keys.Count - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (keys[middle].Time < time)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static Vector3 Hermite(
        Vector3 p0,
        Vector3 tangent0,
        Vector3 p1,
        Vector3 tangent1,
        float t,
        float duration)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float h00 = 2.0f * t3 - 3.0f * t2 + 1.0f;
        float h10 = t3 - 2.0f * t2 + t;
        float h01 = -2.0f * t3 + 3.0f * t2;
        float h11 = t3 - t2;

        return
            p0 * h00 +
            tangent0 * (h10 * duration) +
            p1 * h01 +
            tangent1 * (h11 * duration);
    }

    private static Vector4 Hermite(
        Vector4 p0,
        Vector4 tangent0,
        Vector4 p1,
        Vector4 tangent1,
        float t,
        float duration)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        float h00 = 2.0f * t3 - 3.0f * t2 + 1.0f;
        float h10 = t3 - 2.0f * t2 + t;
        float h01 = -2.0f * t3 + 3.0f * t2;
        float h11 = t3 - t2;

        return
            p0 * h00 +
            tangent0 * (h10 * duration) +
            p1 * h01 +
            tangent1 * (h11 * duration);
    }

    private static Vector4 ToVector4(Quaternion value) =>
        new(value.X, value.Y, value.Z, value.W);

    private static Quaternion NormalizeSafe(Quaternion value) =>
        value.LengthSquared() > 0.000001f
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;
}
