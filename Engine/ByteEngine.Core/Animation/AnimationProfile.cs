using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Animation;

/// <summary>
/// ByteEngine's single animation authoring asset.
///
/// The goal is intentionally simple: one AnimationController points at one
/// AnimationProfile. Rig, locomotion, actions and procedural animation settings
/// all live here instead of being scattered across unrelated components.
///
/// C8 establishes the profile/data foundation. Later animation milestones add
/// runtime/editor behaviour to the existing sections without changing the
/// character-facing architecture.
/// </summary>
public sealed class AnimationProfile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string Name { get; set; } = "Animation Profile";

    public AnimationRigProfile Rig { get; set; } = new();

    public AnimationLocomotionProfile Locomotion { get; set; } = new();

    public List<AnimationActionProfile> Actions { get; set; } = new();

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
                1);

        Name =
            string.IsNullOrWhiteSpace(Name)
                ? "Animation Profile"
                : Name.Trim();

        Rig ??=
            new AnimationRigProfile();

        Locomotion ??=
            new AnimationLocomotionProfile();

        Actions ??=
            new List<AnimationActionProfile>();

        Procedural ??=
            new AnimationProceduralProfile();

        Locomotion.Normalize();

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
/// The two authoring paths planned for ByteEngine animation.
///
/// Generic keeps the imported skeleton exactly as authored.
/// Humanoid will use ByteEngine's canonical human-bone mapping/retargeting
/// foundation introduced in C9.
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

    internal void Normalize()
    {
        Name =
            string.IsNullOrWhiteSpace(Name)
                ? "New Action"
                : Name.Trim();

        Clip ??=
            string.Empty;

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
/// Home for automatic pose-adjustment features. The flags intentionally do not
/// implement IK/aim yet; they establish the single place those systems belong
/// when C14-C17 arrive.
/// </summary>
public sealed class AnimationProceduralProfile
{
    public bool AimEnabled { get; set; }

    public bool LookAtEnabled { get; set; }

    public bool FootIkEnabled { get; set; }

    public bool HandIkEnabled { get; set; }
}
