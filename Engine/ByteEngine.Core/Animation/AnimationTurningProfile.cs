namespace ByteEngine.Core.Animation;

public sealed class AnimationTurnSet
{
    public string BlendSpace { get; set; } = "";
    public string Left90 { get; set; } = "";
    public string Right90 { get; set; } = "";
    public string Around180 { get; set; } = "";
}

public sealed class AnimationTurningProfile
{
    public bool Enabled { get; set; }
    public float MinimumAngle { get; set; } = 15;
    public float AroundAngle { get; set; } = 135;
    public float MaximumMoveSpeed { get; set; } = .15f;
    public float MinimumPlaybackSpeed { get; set; } = .35f;
    public float MaximumPlaybackSpeed { get; set; } = 6;
    public float BlendIn { get; set; } = .08f;
    public float BlendOut { get; set; } = .12f;
    public List<AnimationTurnSet> Sets { get; set; } = [];
    internal void Normalize()
    {
        MinimumAngle = Math.Clamp(float.IsFinite(MinimumAngle) ? MinimumAngle : 15, 5, 180);
        AroundAngle = Math.Clamp(float.IsFinite(AroundAngle) ? AroundAngle : 135, MinimumAngle, 180);
        MaximumMoveSpeed = Math.Max(float.IsFinite(MaximumMoveSpeed) ? MaximumMoveSpeed : .15f, 0);
        BlendIn = Math.Max(float.IsFinite(BlendIn) ? BlendIn : .08f, 0);
        BlendOut = Math.Max(float.IsFinite(BlendOut) ? BlendOut : .12f, 0);
        MinimumPlaybackSpeed = Math.Clamp(float.IsFinite(MinimumPlaybackSpeed) ? MinimumPlaybackSpeed : .35f, .05f, 24);
        MaximumPlaybackSpeed = Math.Clamp(float.IsFinite(MaximumPlaybackSpeed) ? MaximumPlaybackSpeed : 6, MinimumPlaybackSpeed, 24);
        Sets ??= [];
        Sets.RemoveAll(s => s == null);
        foreach (var set in Sets) { set.BlendSpace ??= ""; set.Left90 ??= ""; set.Right90 ??= ""; set.Around180 ??= ""; }
    }
}
