using System.Numerics;

namespace ByteEngine.Core.Animation;

public enum AnimationLayerBlendMode { Override, Additive }
public enum AnimationBoneMaskKind { FullBody, UpperBody, LowerBody, Custom }
public enum AnimationParameterKind { Float, Bool, Trigger }
public enum AnimationComparison { Greater, GreaterOrEqual, Less, LessOrEqual, Equal, NotEqual }
public enum AnimationPoseSourceKind { Clip, BlendSpace1D, BlendSpace2D }

public sealed class AnimationBlendSample
{
    public string Clip { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class AnimationBlendSpace
{
    public string Name { get; set; } = string.Empty;
    public bool TwoDimensional { get; set; }
    public string ParameterX { get; set; } = "Speed";
    public string ParameterY { get; set; } = "Direction";
    public float MinX { get; set; }
    public float MaxX { get; set; } = 6f;
    public float MinY { get; set; } = -5f;
    public float MaxY { get; set; } = 5f;
    public List<AnimationBlendSample> Samples { get; set; } = new();

    public void Normalize()
    {
        Name ??= string.Empty;
        ParameterX ??= string.Empty;
        ParameterY ??= string.Empty;
        if (!float.IsFinite(MinX)) MinX = 0f;
        if (!float.IsFinite(MaxX) || MaxX <= MinX) MaxX = MinX + 1f;
        if (!float.IsFinite(MinY)) MinY = -5f;
        if (!float.IsFinite(MaxY) || MaxY <= MinY) MaxY = MinY + 1f;
        Samples ??= new();
        Samples.RemoveAll(sample => sample == null || !float.IsFinite(sample.X) ||
            !float.IsFinite(sample.Y) || string.IsNullOrWhiteSpace(sample.Clip));
    }
}

public sealed class AnimationBoneMask
{
    public AnimationBoneMaskKind Kind { get; set; }
    public string RootBone { get; set; } = string.Empty;
    public List<string> ExcludedBones { get; set; } = new();
    public Dictionary<string, float> BoneWeights { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        RootBone ??= string.Empty;
        ExcludedBones ??= new();
        BoneWeights ??= new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in BoneWeights.Keys.ToArray())
            BoneWeights[key] = Math.Clamp(float.IsFinite(BoneWeights[key]) ? BoneWeights[key] : 0f, 0f, 1f);
    }
}

public sealed class AnimationLayerProfile
{
    public string Name { get; set; } = "Layer";
    public bool Enabled { get; set; } = true;
    public string Clip { get; set; } = string.Empty;
    public string WeightParameter { get; set; } = string.Empty;
    public float Weight { get; set; } = 1f;
    public float BlendIn { get; set; } = .15f;
    public float BlendOut { get; set; } = .15f;
    public AnimationLayerBlendMode BlendMode { get; set; }
    public AnimationBoneMask Mask { get; set; } = new();

    public void Normalize()
    {
        Name = string.IsNullOrWhiteSpace(Name) ? "Layer" : Name.Trim();
        Clip ??= string.Empty;
        WeightParameter ??= string.Empty;
        Weight = Math.Clamp(float.IsFinite(Weight) ? Weight : 0f, 0f, 1f);
        BlendIn = Math.Max(float.IsFinite(BlendIn) ? BlendIn : 0f, 0f);
        BlendOut = Math.Max(float.IsFinite(BlendOut) ? BlendOut : 0f, 0f);
        Mask ??= new();
        Mask.Normalize();
    }
}

public sealed class AnimationSyncMarker
{
    public string Name { get; set; } = string.Empty;
    public float NormalizedTime { get; set; }
}

public sealed class AnimationSyncGroup
{
    public string Name { get; set; } = string.Empty;
    public List<string> Clips { get; set; } = new();
    public Dictionary<string, List<AnimationSyncMarker>> Markers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void Normalize()
    {
        Name ??= string.Empty;
        Clips ??= new();
        Markers ??= new(StringComparer.OrdinalIgnoreCase);
        foreach (List<AnimationSyncMarker> markers in Markers.Values)
        {
            markers.RemoveAll(marker => marker == null || string.IsNullOrWhiteSpace(marker.Name));
            foreach (AnimationSyncMarker marker in markers)
                marker.NormalizedTime = Math.Clamp(float.IsFinite(marker.NormalizedTime) ? marker.NormalizedTime : 0f, 0f, 1f);
            markers.Sort((a, b) => a.NormalizedTime.CompareTo(b.NormalizedTime));
        }
    }
}

public sealed class AnimationGraphParameter
{
    public string Name { get; set; } = string.Empty;
    public AnimationParameterKind Kind { get; set; }
    public float FloatDefault { get; set; }
    public bool BoolDefault { get; set; }
}

public sealed class AnimationGraphState
{
    public string Name { get; set; } = string.Empty;
    public AnimationPoseSourceKind SourceKind { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool Loop { get; set; } = true;
    public float PlaybackSpeed { get; set; } = 1f;
    public Vector2 EditorPosition { get; set; }
}

public sealed class AnimationGraphCondition
{
    public string Parameter { get; set; } = string.Empty;
    public AnimationComparison Comparison { get; set; }
    public float FloatValue { get; set; }
    public bool BoolValue { get; set; }
}

public sealed class AnimationGraphTransition
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public float Duration { get; set; } = .15f;
    public float ExitTime { get; set; } = -1f;
    public List<AnimationGraphCondition> Conditions { get; set; } = new();
}

public sealed class AnimationStateGraph
{
    public bool Enabled { get; set; }
    public string EntryState { get; set; } = string.Empty;
    public List<AnimationGraphParameter> Parameters { get; set; } = new();
    public List<AnimationGraphState> States { get; set; } = new();
    public List<AnimationGraphTransition> Transitions { get; set; } = new();

    public void Normalize()
    {
        EntryState ??= string.Empty;
        Parameters ??= new();
        States ??= new();
        Transitions ??= new();
        foreach (AnimationGraphState state in States)
        {
            state.Name ??= string.Empty;
            state.Source ??= string.Empty;
            state.PlaybackSpeed = Math.Max(float.IsFinite(state.PlaybackSpeed) ? state.PlaybackSpeed : 1f, 0f);
            if (!float.IsFinite(state.EditorPosition.X) || !float.IsFinite(state.EditorPosition.Y))
                state.EditorPosition = Vector2.Zero;
        }
        foreach (AnimationGraphTransition transition in Transitions)
        {
            transition.From ??= string.Empty;
            transition.To ??= string.Empty;
            transition.Duration = Math.Max(float.IsFinite(transition.Duration) ? transition.Duration : 0f, 0f);
            transition.ExitTime = float.IsFinite(transition.ExitTime) ? transition.ExitTime : -1f;
            transition.Conditions ??= new();
        }
    }
}

public sealed class AnimationIkChainProfile
{
    public string Name { get; set; } = string.Empty;
    public string RootBone { get; set; } = string.Empty;
    public string MidBone { get; set; } = string.Empty;
    public string EndBone { get; set; } = string.Empty;
    public string TargetObject { get; set; } = string.Empty;
    public Vector3 PoleOffset { get; set; } = new(0f, 0f, 1f);
    public float Weight { get; set; } = 1f;
    public bool FootGrounding { get; set; }
}
