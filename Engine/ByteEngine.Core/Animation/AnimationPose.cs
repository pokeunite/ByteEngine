using System.Numerics;

namespace ByteEngine.Core.Animation;

/// <summary>Local TRS; rows are composed as local * parent in ByteEngine.</summary>
public readonly record struct AnimationBoneTransform(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    public static AnimationBoneTransform Interpolate(AnimationBoneTransform a, AnimationBoneTransform b, float weight)
    {
        weight = Math.Clamp(weight, 0f, 1f);
        Quaternion to = Quaternion.Dot(a.Rotation, b.Rotation) < 0f
            ? new Quaternion(-b.Rotation.X, -b.Rotation.Y, -b.Rotation.Z, -b.Rotation.W)
            : b.Rotation;
        return new(Vector3.Lerp(a.Position, b.Position, weight),
            Quaternion.Normalize(Quaternion.Slerp(a.Rotation, to, weight)),
            Vector3.Lerp(a.Scale, b.Scale, weight));
    }

    public static AnimationBoneTransform ApplyAdditive(AnimationBoneTransform basis,
        AnimationBoneTransform sample, AnimationBoneTransform reference, float weight)
    {
        weight = Math.Clamp(weight, 0f, 1f);
        if (weight <= 0f) return basis;
        Quaternion delta = Quaternion.Normalize(Quaternion.Inverse(reference.Rotation) * sample.Rotation);
        if (delta.W < 0f) delta = new(-delta.X, -delta.Y, -delta.Z, -delta.W);
        return new(basis.Position + (sample.Position - reference.Position) * weight,
            Quaternion.Normalize(basis.Rotation * Quaternion.Slerp(Quaternion.Identity, delta, weight)),
            basis.Scale * Vector3.Lerp(Vector3.One, new Vector3(
                SafeRatio(sample.Scale.X, reference.Scale.X),
                SafeRatio(sample.Scale.Y, reference.Scale.Y),
                SafeRatio(sample.Scale.Z, reference.Scale.Z)), weight));
    }

    private static float SafeRatio(float value, float reference) =>
        MathF.Abs(reference) > 1e-6f && float.IsFinite(value / reference) ? value / reference : 1f;
}

/// <summary>Reusable pose storage. Buffers are sized once per skeleton.</summary>
public sealed class AnimationPose
{
    private readonly AnimationBoneTransform[] _bones;
    public int Count => _bones.Length;
    public AnimationBoneTransform this[int index] { get => _bones[index]; set => _bones[index] = value; }
    public AnimationPose(int count) => _bones = new AnimationBoneTransform[Math.Max(count, 0)];
    public void CopyFrom(AnimationPose source) => Array.Copy(source._bones, _bones, Math.Min(Count, source.Count));

    public void Blend(AnimationPose other, float weight, float[]? mask = null)
    {
        int count = Math.Min(Count, other.Count);
        for (int i = 0; i < count; i++)
            _bones[i] = AnimationBoneTransform.Interpolate(_bones[i], other._bones[i],
                weight * (mask != null && i < mask.Length ? mask[i] : 1f));
    }

    public void Additive(AnimationPose sample, AnimationPose reference, float weight, float[]? mask = null)
    {
        int count = Math.Min(Count, Math.Min(sample.Count, reference.Count));
        for (int i = 0; i < count; i++)
            _bones[i] = AnimationBoneTransform.ApplyAdditive(_bones[i], sample._bones[i], reference._bones[i],
                weight * (mask != null && i < mask.Length ? mask[i] : 1f));
    }
}

/// <summary>Allocation-free blend-space weights written into caller-owned storage.</summary>
public static class AnimationBlendWeights
{
    public static void Evaluate(AnimationBlendSpace space, float x, float y, Span<float> weights)
    {
        weights.Clear();
        int count = Math.Min(space.Samples.Count, weights.Length);
        if (count == 0) return;
        if (!space.TwoDimensional)
        {
            int below = -1, above = -1;
            for (int i = 0; i < count; i++)
            {
                float value = space.Samples[i].X;
                if (value <= x && (below < 0 || value > space.Samples[below].X)) below = i;
                if (value >= x && (above < 0 || value < space.Samples[above].X)) above = i;
            }
            if (below < 0) below = above;
            if (above < 0) above = below;
            if (below == above || MathF.Abs(space.Samples[above].X - space.Samples[below].X) < 1e-6f)
                weights[below] = 1f;
            else
            {
                float t = Math.Clamp((x - space.Samples[below].X) /
                    (space.Samples[above].X - space.Samples[below].X), 0f, 1f);
                weights[below] = 1f - t;
                weights[above] = t;
            }
            return;
        }

        // Use only the local triangle containing the input. Inverse-distance
        // blending mixed opposite directions even between two forward samples.
        int exact = -1;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            float dx = x - space.Samples[i].X, dy = y - space.Samples[i].Y;
            float distance = dx * dx + dy * dy;
            if (distance < closest) { closest = distance; exact = i; }
        }
        if (closest < 1e-8f) { weights[exact] = 1f; return; }

        int triangleA = -1, triangleB = -1, triangleC = -1;
        float bestArea = float.PositiveInfinity, bestRadius = float.PositiveInfinity;
        float bestA = 0f, bestB = 0f, bestC = 0f;
        for (int a = 0; a < count; a++)
        for (int b = a + 1; b < count; b++)
        for (int c = b + 1; c < count; c++)
        {
            AnimationBlendSample pa = space.Samples[a], pb = space.Samples[b], pc = space.Samples[c];
            float area = Cross(pb.X - pa.X, pb.Y - pa.Y, pc.X - pa.X, pc.Y - pa.Y);
            if (MathF.Abs(area) < 1e-6f) continue;
            float wb = Cross(x - pa.X, y - pa.Y, pc.X - pa.X, pc.Y - pa.Y) / area;
            float wc = Cross(pb.X - pa.X, pb.Y - pa.Y, x - pa.X, y - pa.Y) / area;
            float wa = 1f - wb - wc;
            if (wa < -1e-5f || wb < -1e-5f || wc < -1e-5f) continue;
            float size = MathF.Abs(area);
            float radius = MathF.Max(
                Vector2.DistanceSquared(new Vector2(x, y), new Vector2(pa.X, pa.Y)),
                MathF.Max(Vector2.DistanceSquared(new Vector2(x, y), new Vector2(pb.X, pb.Y)),
                    Vector2.DistanceSquared(new Vector2(x, y), new Vector2(pc.X, pc.Y))));
            if (size > bestArea + 1e-5f || (MathF.Abs(size - bestArea) <= 1e-5f &&
                radius >= bestRadius)) continue;
            bestArea = size; bestRadius = radius;
            triangleA = a; triangleB = b; triangleC = c;
            bestA = MathF.Max(0f, wa); bestB = MathF.Max(0f, wb); bestC = MathF.Max(0f, wc);
        }
        if (triangleA >= 0)
        {
            float total = bestA + bestB + bestC;
            weights[triangleA] = bestA / total;
            weights[triangleB] = bestB / total;
            weights[triangleC] = bestC / total;
            return;
        }

        // Outside the plotted hull, clamp to its nearest edge. Collinear
        // layouts also work because their outer segments are hull edges.
        int edgeA = -1, edgeB = -1;
        float edgeT = 0f, bestDistance = float.PositiveInfinity;
        for (int a = 0; a < count; a++)
        for (int b = a + 1; b < count; b++)
        {
            AnimationBlendSample pa = space.Samples[a], pb = space.Samples[b];
            float dx = pb.X - pa.X, dy = pb.Y - pa.Y;
            float lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1e-8f) continue;
            bool left = false, right = false;
            for (int c = 0; c < count; c++)
            {
                if (c == a || c == b) continue;
                AnimationBlendSample pc = space.Samples[c];
                float side = Cross(dx, dy, pc.X - pa.X, pc.Y - pa.Y);
                left |= side > 1e-5f;
                right |= side < -1e-5f;
                if (left && right) break;
            }
            if (left && right) continue;
            float t = Math.Clamp(((x - pa.X) * dx + (y - pa.Y) * dy) / lengthSquared, 0f, 1f);
            float ex = pa.X + dx * t - x, ey = pa.Y + dy * t - y;
            float distance = ex * ex + ey * ey;
            if (distance >= bestDistance) continue;
            bestDistance = distance; edgeA = a; edgeB = b; edgeT = t;
        }
        if (edgeA >= 0)
        {
            weights[edgeA] = 1f - edgeT;
            weights[edgeB] = edgeT;
        }
        else weights[exact] = 1f;
    }

    public static float PlaybackScale(float baseDuration, ReadOnlySpan<float> weights,
        ReadOnlySpan<float> durations)
    {
        if (!float.IsFinite(baseDuration) || baseDuration <= 1e-6f) return 1f;
        float weightedDuration = 0f, total = 0f;
        int count = Math.Min(weights.Length, durations.Length);
        for (int i = 0; i < count; i++)
        {
            if (!float.IsFinite(weights[i]) || weights[i] <= 0f ||
                !float.IsFinite(durations[i]) || durations[i] <= 1e-6f) continue;
            weightedDuration += weights[i] * durations[i];
            total += weights[i];
        }
        return total > 1e-6f && weightedDuration > 1e-6f
            ? Math.Clamp(baseDuration * total / weightedDuration, .1f, 8f) : 1f;
    }

    private static float Cross(float ax, float ay, float bx, float by) => ax * by - ay * bx;
}

public struct FootContactState
{
    public bool HasTarget;
    public bool Locked;
    public bool AwaitLift;
    public Vector3 Target;
    public Vector3 Normal;
    public Vector3 Anchor;
    public float Weight;
}

public static class AnimationPoseMath
{
    /// <summary>
    /// Foot grounding has no gait-phase signal. Fade it out during faster locomotion
    /// rather than forcing a running leg onto an unrelated ground contact.
    /// </summary>
    public static float FootMotionWeight(float horizontalSpeed) =>
        float.IsFinite(horizontalSpeed) ? 1f - SmoothStep(.15f, 1.75f, MathF.Abs(horizontalSpeed)) : 0f;

    /// <summary>Fade IK away from lifted feet, but keep both feet planted on different-height surfaces.</summary>
    public static float FootContactWeight(float clearance, float relativeClearance, float limbLength)
    {
        if (!float.IsFinite(clearance) || !float.IsFinite(relativeClearance) ||
            !float.IsFinite(limbLength) || limbLength <= 0f) return 0f;
        float planted = Math.Max(.02f, limbLength * .04f);
        float lifted = Math.Max(planted + .08f, limbLength * .20f);
        float relativePlanted = Math.Max(.01f, limbLength * .02f);
        float relativeLifted = Math.Max(relativePlanted + .06f, limbLength * .12f);
        float heightWeight = 1f - SmoothStep(planted, lifted, clearance);
        // Relative height alone does not make the lower foot a swing foot on stairs.
        // Only use it once that foot has visibly lifted from its own surface.
        float relativeWeight = clearance <= planted ? 1f :
            1f - SmoothStep(relativePlanted, relativeLifted, relativeClearance);
        return heightWeight * relativeWeight;
    }

    public static float FootSoleOffset(Vector3 ankleWorld, Vector3 toeWorld,
        Vector3 surfaceNormal, float manualOffset)
    {
        float projected = Vector3.Dot(ankleWorld - toeWorld, surfaceNormal);
        if (!float.IsFinite(projected) || !float.IsFinite(manualOffset)) return 0f;
        return MathF.Max(0f, MathF.Max(0f, projected) + .015f + manualOffset);
    }

    /// <summary>
    /// A planted leg needs a stable anatomical bend hint. When the animated leg is
    /// nearly straight, its knee position is a poor pole and can swing sideways
    /// as the ankle target rises onto a step. The foot-to-toe direction supplies
    /// the character's local forward direction without assuming a model axis.
    /// </summary>
    public static Vector3 FootKneePole(Vector3 hip, Vector3 knee, Vector3 ankle,
        Vector3 toe, Vector3 target)
    {
        Vector3 axis = target - hip;
        if (axis.LengthSquared() < 1e-8f) return knee;
        axis = Vector3.Normalize(axis);
        Vector3 toeBend = toe - ankle;
        toeBend -= Vector3.Dot(toeBend, axis) * axis;
        if (toeBend.LengthSquared() < 1e-5f) return knee;
        Vector3 animatedBend = knee - hip;
        animatedBend -= Vector3.Dot(animatedBend, axis) * axis;
        float limbLength = Vector3.Distance(hip, knee) + Vector3.Distance(knee, ankle);
        float bendLength = animatedBend.Length();
        Vector3 toeDirection = Vector3.Normalize(toeBend);
        if (bendLength < 1e-6f) return hip + toeDirection * limbLength;
        Vector3 animatedDirection = animatedBend / bendLength;
        float strength = SmoothStep(limbLength * .06f, limbLength * .22f, bendLength);
        float alignment = Math.Clamp((Vector3.Dot(animatedDirection, toeDirection) + .2f) / 1.2f, 0f, 1f);
        Vector3 direction = Vector3.Lerp(toeDirection, animatedDirection, strength * alignment);
        if (direction.LengthSquared() < 1e-8f) direction = toeDirection;
        return hip + Vector3.Normalize(direction) * limbLength;
    }

    public static float ExponentialAlpha(float response, float deltaTime) =>
        !float.IsFinite(response) || !float.IsFinite(deltaTime) || deltaTime <= 0f
            ? 0f : 1f - MathF.Exp(-MathF.Max(0f, response) * MathF.Min(deltaTime, .1f));

    public static Vector3 SmoothDirection(Vector3 previous, Vector3 desired,
        float deltaTime, float response)
    {
        if (desired.LengthSquared() < 1e-8f) return previous;
        desired = Vector3.Normalize(desired);
        if (previous.LengthSquared() < 1e-8f) return desired;
        previous = Vector3.Normalize(previous);
        float alpha = ExponentialAlpha(response, deltaTime);
        if (Vector3.Dot(previous, desired) < -.99f)
        {
            Vector3 axis = Vector3.Cross(previous, MathF.Abs(previous.Y) < .9f
                ? Vector3.UnitY : Vector3.UnitX);
            axis = Vector3.Normalize(axis);
            return Vector3.Normalize(Vector3.Transform(previous,
                Quaternion.CreateFromAxisAngle(axis, MathF.PI * alpha)));
        }
        Vector3 value = Vector3.Lerp(previous, desired, alpha);
        return value.LengthSquared() > 1e-8f ? Vector3.Normalize(value) : previous;
    }

    public static void AdvanceFootContact(ref FootContactState state, bool hasHit,
        Vector3 rawTarget, Vector3 rawNormal, float rawWeight, Vector3 hipWorld,
        float limbLength, float deltaTime, bool surfaceChanged = false)
    {
        float weightAlpha = ExponentialAlpha(18f, deltaTime);
        if (!hasHit)
        {
            state.Locked = false;
            state.AwaitLift = false;
            state.Weight += (0f - state.Weight) * weightAlpha;
            if (state.Weight < .005f) state.HasTarget = false;
            return;
        }
        rawWeight = Math.Clamp(float.IsFinite(rawWeight) ? rawWeight : 0f, 0f, 1f);
        if (!state.HasTarget)
        {
            state.Target = rawTarget;
            state.Normal = rawNormal;
            state.Weight = rawWeight;
            state.HasTarget = true;
        }
        if (surfaceChanged) { state.Locked = false; state.AwaitLift = false; }
        if (rawWeight <= .15f) { state.Locked = false; state.AwaitLift = false; }
        if (state.Locked && limbLength > 0f &&
            (Vector3.Distance(state.Anchor, rawTarget) > limbLength * .30f ||
             Vector3.Distance(state.Anchor, hipWorld) > limbLength * .98f))
        {
            state.Locked = false;
            state.AwaitLift = true;
        }
        if (!state.Locked && !state.AwaitLift && rawWeight >= .85f)
        {
            state.Anchor = rawTarget;
            state.Locked = true;
        }
        Vector3 target = state.Locked ? state.Anchor : rawTarget;
        state.Target = Vector3.Lerp(state.Target, target, ExponentialAlpha(20f, deltaTime));
        Vector3 normal = Vector3.Lerp(state.Normal, rawNormal, ExponentialAlpha(12f, deltaTime));
        state.Normal = normal.LengthSquared() > 1e-8f ? Vector3.Normalize(normal) : rawNormal;
        state.Weight += (rawWeight - state.Weight) * weightAlpha;
    }

    public static bool IsUsableFootGroundHit(float distance, Vector3 normal,
        float footHeight, float hitHeight, float rayDistance) =>
        float.IsFinite(distance) && distance > .001f &&
        float.IsFinite(normal.Y) && normal.Y >= .55f &&
        float.IsFinite(footHeight) && float.IsFinite(hitHeight) &&
        hitHeight - footHeight <= MathF.Max(.01f, rayDistance * .65f);

    private static float SmoothStep(float start, float end, float value)
    {
        float t = Math.Clamp((value - start) / (end - start), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
    public static Quaternion ClampAim(float yawDegrees, float pitchDegrees,
        float maxYawDegrees, float maxPitchDegrees, float weight)
    {
        float yaw = Math.Clamp(yawDegrees, -MathF.Abs(maxYawDegrees), MathF.Abs(maxYawDegrees));
        float pitch = Math.Clamp(pitchDegrees, -MathF.Abs(maxPitchDegrees), MathF.Abs(maxPitchDegrees));
        Quaternion target = Quaternion.CreateFromYawPitchRoll(yaw * MathF.PI / 180f,
            pitch * MathF.PI / 180f, 0f);
        return Quaternion.Normalize(Quaternion.Slerp(Quaternion.Identity, target, Math.Clamp(weight, 0f, 1f)));
    }

    public static Quaternion FromTo(Vector3 from, Vector3 to)
    {
        if (from.LengthSquared() < 1e-12f || to.LengthSquared() < 1e-12f) return Quaternion.Identity;
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        float dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);
        if (dot > .99999f) return Quaternion.Identity;
        if (dot < -.99999f)
        {
            Vector3 axis = Vector3.Cross(from, MathF.Abs(from.X) < .9f ? Vector3.UnitX : Vector3.UnitY);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }
        Vector3 cross = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(cross, 1f + dot));
    }

    /// <summary>Stable two-link target with pole-plane fallback; returns model-space elbow and end.</summary>
    public static bool SolveTwoBone(Vector3 root, Vector3 mid, Vector3 end, Vector3 target,
        Vector3 pole, out Vector3 solvedMid, out Vector3 solvedEnd)
    {
        solvedMid = mid;
        solvedEnd = end;
        float upper = Vector3.Distance(root, mid), lower = Vector3.Distance(mid, end);
        if (upper < 1e-6f || lower < 1e-6f || !Finite(target) || !Finite(pole)) return false;
        Vector3 travel = target - root;
        float distance = travel.Length();
        Vector3 axis = distance > 1e-6f ? travel / distance : SafeDirection(end - root);
        float epsilon = MathF.Min(1e-5f, MathF.Min(upper, lower) * .25f);
        float minimum = MathF.Abs(upper - lower) + epsilon;
        float maximum = upper + lower - epsilon;
        if (minimum > maximum) return false;
        distance = Math.Clamp(distance, minimum, maximum);
        Vector3 polePlane = pole - root;
        polePlane -= Vector3.Dot(polePlane, axis) * axis;
        if (polePlane.LengthSquared() < 1e-10f)
        {
            polePlane = mid - root;
            polePlane -= Vector3.Dot(polePlane, axis) * axis;
        }
        if (polePlane.LengthSquared() < 1e-10f)
        {
            polePlane = Vector3.Cross(axis, MathF.Abs(axis.Y) < .9f ? Vector3.UnitY : Vector3.UnitX);
        }
        polePlane = Vector3.Normalize(polePlane);
        float along = (upper * upper - lower * lower + distance * distance) / (2f * distance);
        float height = MathF.Sqrt(MathF.Max(upper * upper - along * along, 0f));
        solvedMid = root + axis * along + polePlane * height;
        solvedEnd = root + axis * distance;
        return Finite(solvedMid) && Finite(solvedEnd);
    }

    private static Vector3 SafeDirection(Vector3 value) =>
        value.LengthSquared() > 1e-12f ? Vector3.Normalize(value) : Vector3.UnitZ;
    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}

public static class AnimationSyncMath
{
    public static float MapPhase(float sourcePhase, IReadOnlyList<AnimationSyncMarker>? source,
        IReadOnlyList<AnimationSyncMarker>? target)
    {
        sourcePhase = sourcePhase - MathF.Floor(sourcePhase);
        if (source == null || target == null || source.Count < 2 || target.Count < 2)
            return sourcePhase;
        for (int i = 0; i < source.Count; i++)
        {
            AnimationSyncMarker first = source[i];
            AnimationSyncMarker second = source[(i + 1) % source.Count];
            float start = first.NormalizedTime;
            float end = i + 1 < source.Count ? second.NormalizedTime : second.NormalizedTime + 1f;
            float phase = sourcePhase < start ? sourcePhase + 1f : sourcePhase;
            if (phase > end || phase < start) continue;
            int match = -1;
            for (int j = 0; j < target.Count; j++)
                if (string.Equals(target[j].Name, first.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(target[(j + 1) % target.Count].Name, second.Name, StringComparison.OrdinalIgnoreCase))
                { match = j; break; }
            if (match < 0) return sourcePhase;
            float a = target[match].NormalizedTime;
            float b = match + 1 < target.Count ? target[match + 1].NormalizedTime :
                target[0].NormalizedTime + 1f;
            float t = (phase - start) / MathF.Max(end - start, 1e-6f);
            float result = a + (b - a) * t;
            return result - MathF.Floor(result);
        }
        return sourcePhase;
    }
}
