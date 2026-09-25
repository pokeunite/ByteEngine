using ByteEngine.Core.Assets;
using System.Numerics;

namespace ByteEngine.Core.Animation;

/// <summary>
/// ByteEngine's single animation authoring asset.
///
/// One AnimationController points at one AnimationProfile. Rig, locomotion,
/// blend spaces, actions, layers, sync and procedural settings live here.
///
/// Runtime pose features and their editor settings live in this asset without
/// changing the character-facing AnimationController architecture.
/// </summary>
public sealed class AnimationProfile
{
    public const int CurrentVersion = 5;

    public int Version { get; set; } = CurrentVersion;

    public string Name { get; set; } = "Animation Profile";

    public AnimationRigProfile Rig { get; set; } = new();

    public AnimationLocomotionProfile Locomotion { get; set; } = new();

    public List<AnimationActionProfile> Actions { get; set; } = new();

    public List<AnimationBlendSpace> BlendSpaces { get; set; } = new();

    public List<AnimationLayerProfile> Layers { get; set; } = new();

    public List<AnimationSyncGroup> SyncGroups { get; set; } = new();

    public AnimationStateGraph StateGraph { get; set; } = new();

    public AnimationProceduralProfile Procedural { get; set; } = new();

    /// <summary>
    /// Repairs nullable/missing sections from older profile files and keeps
    /// authoring values inside safe runtime ranges.
    /// </summary>
    public void Normalize()
    {
        Version =
            Math.Max(
                Version,
                CurrentVersion);

        Name =
            string.IsNullOrWhiteSpace(Name)
                ? "Animation Profile"
                : Name.Trim();

        Rig ??=
            new AnimationRigProfile();

        Rig.ReferenceModel ??=
            AssetReference.Empty;

        Locomotion ??=
            new AnimationLocomotionProfile();

        Actions ??=
            new List<AnimationActionProfile>();

        Procedural ??=
            new AnimationProceduralProfile();
        Procedural.Normalize();

        BlendSpaces ??= new();
        Layers ??= new();
        SyncGroups ??= new();
        StateGraph ??= new();
        Locomotion.Normalize();
        foreach (AnimationBlendSpace space in BlendSpaces) space.Normalize();
        foreach (AnimationLayerProfile layer in Layers) layer.Normalize();
        foreach (AnimationSyncGroup group in SyncGroups) group.Normalize();
        StateGraph.Normalize();

        for (int index = Actions.Count - 1;
             index >= 0;
             index--)
        {
            AnimationActionProfile? action =
                Actions[index];

            if (action == null)
            {
                Actions.RemoveAt(index);
                continue;
            }

            action.Normalize();
        }
    }
}

/// <summary>
/// Rig classification for native animation and humanoid bone diagnostics.
/// </summary>
public enum AnimationRigType
{
    Generic,
    Humanoid
}

public sealed class AnimationRigProfile
{
    public AnimationRigType Type { get; set; } =
        AnimationRigType.Generic;

    /// <summary>
    /// Optional character/reference model used by editor tooling. C9 will use
    /// this for humanoid validation and bone mapping.
    /// </summary>
    public AssetReference ReferenceModel { get; set; } =
        AssetReference.Empty;

}

/// <summary>
/// Existing C5-C7 locomotion behaviour represented inside the unified profile.
/// C12 will expand this section with directional movement blending, automatic
/// sync, distance matching and pose adaptation.
/// </summary>
public sealed class AnimationLocomotionProfile
{
    public bool Enabled { get; set; } = true;

    public bool DriveFromCharacterController { get; set; } = true;

    public string Idle { get; set; } = "Idle";

    public string Walk { get; set; } = "Walk";

    public string Run { get; set; } = "Run";

    public string Jump { get; set; } = "Jump";

    public string Fall { get; set; } = "Fall";

    public string Land { get; set; } = "Land";

    public float RunThreshold { get; set; } = 4.0f;

    public float MoveThreshold { get; set; } = 0.05f;
    public float StateHysteresis { get; set; } = 0.15f;
    public bool DirectionalMovement { get; set; }
    public string BlendSpace { get; set; } = string.Empty;
    public string SyncGroup { get; set; } = string.Empty;

    public string WalkForward { get; set; } = string.Empty;
    public string WalkBackward { get; set; } = string.Empty;
    public string WalkLeft { get; set; } = string.Empty;
    public string WalkRight { get; set; } = string.Empty;
    public string RunForward { get; set; } = string.Empty;
    public string RunBackward { get; set; } = string.Empty;
    public string RunLeft { get; set; } = string.Empty;
    public string RunRight { get; set; } = string.Empty;

    public float DirectionHysteresis { get; set; } = 0.10f;
    public bool MatchPlaybackToSpeed { get; set; }
    public float WalkReferenceSpeed { get; set; } = 2.0f;
    public float RunReferenceSpeed { get; set; } = 5.0f;
    public float MinimumPlaybackRate { get; set; } = 0.70f;
    public float MaximumPlaybackRate { get; set; } = 1.40f;

    public float TransitionDuration { get; set; } = 0.15f;

    public float PlaybackSpeed { get; set; } = 1.0f;

    public RootMotionMode RootMotionMode { get; set; } =
        RootMotionMode.InPlace;

    internal void Normalize()
    {
        Idle ??= string.Empty;
        Walk ??= string.Empty;
        Run ??= string.Empty;
        Jump ??= string.Empty;
        Fall ??= string.Empty;
        Land ??= string.Empty;
        WalkForward ??= string.Empty;
        WalkBackward ??= string.Empty;
        WalkLeft ??= string.Empty;
        WalkRight ??= string.Empty;
        RunForward ??= string.Empty;
        RunBackward ??= string.Empty;
        RunLeft ??= string.Empty;
        RunRight ??= string.Empty;
        BlendSpace ??= string.Empty;
        SyncGroup ??= string.Empty;

        MoveThreshold = Math.Max(MoveThreshold, 0.0f);
        StateHysteresis = Math.Max(StateHysteresis, 0.0f);
        DirectionHysteresis = Math.Max(DirectionHysteresis, 0.0f);
        WalkReferenceSpeed = Math.Max(WalkReferenceSpeed, 0.001f);
        RunReferenceSpeed = Math.Max(RunReferenceSpeed, 0.001f);
        MinimumPlaybackRate = Math.Max(MinimumPlaybackRate, 0.0f);
        MaximumPlaybackRate = Math.Max(MaximumPlaybackRate, MinimumPlaybackRate);

        RunThreshold =
            Math.Max(
                RunThreshold,
                0.0f);

        TransitionDuration =
            Math.Max(
                TransitionDuration,
                0.0f);

        PlaybackSpeed =
            Math.Max(
                PlaybackSpeed,
                0.0f);
    }
}

/// <summary>
/// A named high-level animation action.
///
/// C8 stores the unified definition. C11 will add sections/combos, event
/// windows, layers and target-alignment behaviour without forcing those
/// settings onto Event Sheet nodes.
/// </summary>
public sealed class AnimationActionProfile
{
    public string Name { get; set; } =
        "New Action";

    public string Clip { get; set; } =
        string.Empty;

    public bool Loop { get; set; }

    public float BlendIn { get; set; } =
        0.1f;

    public float BlendOut { get; set; } =
        0.15f;

    public float PlaybackSpeed { get; set; } = 1.0f;

    public int Priority { get; set; }

    public bool Interruptible { get; set; } = true;

    public string NextAction { get; set; } = string.Empty;

    public string ComboWindow { get; set; } = string.Empty;

    internal void Normalize()
    {
        Name =
            string.IsNullOrWhiteSpace(Name)
                ? "New Action"
                : Name.Trim();

        Clip ??=
            string.Empty;

        NextAction ??= string.Empty;
        ComboWindow ??= string.Empty;
        PlaybackSpeed = Math.Max(PlaybackSpeed, 0.0f);

        BlendIn =
            Math.Max(
                BlendIn,
                0.0f);

        BlendOut =
            Math.Max(
                BlendOut,
                0.0f);
    }
}

/// <summary>
/// Authoring settings for final-pose aim, look-at and IK corrections.
/// </summary>
public sealed class AnimationProceduralProfile
{
    public bool AimEnabled { get; set; }

    public bool LookAtEnabled { get; set; }

    public bool FootIkEnabled { get; set; }

    public bool HandIkEnabled { get; set; }

    public float AimWeight { get; set; } = 1f;
    public float AimYawLimit { get; set; } = 80f;
    public float AimPitchLimit { get; set; } = 65f;
    public float AimSmoothing { get; set; } = 12f;
    public string AimBone { get; set; } = string.Empty;
    public string LookAtBone { get; set; } = string.Empty;
    public string LookAtTarget { get; set; } = string.Empty;
    public float LookAtWeight { get; set; } = 1f;
    public float FootRayDistance { get; set; } = .75f;
    public float FootOffset { get; set; }
    public float PelvisCompensation { get; set; } = .5f;
    public List<AnimationIkChainProfile> IkChains { get; set; } = new();

    public void Normalize()
    {
        AimBone ??= string.Empty;
        LookAtBone ??= string.Empty;
        LookAtTarget ??= string.Empty;
        AimWeight = Math.Clamp(float.IsFinite(AimWeight) ? AimWeight : 0f, 0f, 1f);
        LookAtWeight = Math.Clamp(float.IsFinite(LookAtWeight) ? LookAtWeight : 0f, 0f, 1f);
        AimYawLimit = Math.Clamp(float.IsFinite(AimYawLimit) ? AimYawLimit : 0f, 0f, 180f);
        AimPitchLimit = Math.Clamp(float.IsFinite(AimPitchLimit) ? AimPitchLimit : 0f, 0f, 180f);
        AimSmoothing = Math.Clamp(float.IsFinite(AimSmoothing) ? AimSmoothing : 0f, 0f, 100f);
        FootRayDistance = Math.Clamp(float.IsFinite(FootRayDistance) ? FootRayDistance : 0f, 0f, 10f);
        FootOffset = float.IsFinite(FootOffset) ? FootOffset : 0f;
        PelvisCompensation = Math.Clamp(float.IsFinite(PelvisCompensation) ? PelvisCompensation : 0f, 0f, 1f);
        IkChains ??= new();
        IkChains.RemoveAll(chain => chain == null);
        foreach (AnimationIkChainProfile chain in IkChains)
        {
            chain.Name ??= string.Empty;
            chain.RootBone ??= string.Empty;
            chain.MidBone ??= string.Empty;
            chain.EndBone ??= string.Empty;
            chain.TargetObject ??= string.Empty;
            chain.Weight = Math.Clamp(float.IsFinite(chain.Weight) ? chain.Weight : 0f, 0f, 1f);
            if (!float.IsFinite(chain.PoleOffset.X) || !float.IsFinite(chain.PoleOffset.Y) ||
                !float.IsFinite(chain.PoleOffset.Z)) chain.PoleOffset = Vector3.UnitZ;
        }
    }
}
