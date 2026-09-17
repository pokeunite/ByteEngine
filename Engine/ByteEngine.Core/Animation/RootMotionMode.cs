namespace ByteEngine.Core.Animation;

/// <summary>
/// Controls how horizontal translation authored on an animation's skeleton root
/// is handled by AnimationController.
/// </summary>
public enum RootMotionMode
{
    /// <summary>
    /// Removes locomotion travel from the rendered pose and leaves world
    /// movement entirely to gameplay/CharacterController3D.
    /// </summary>
    InPlace,

    /// <summary>
    /// Removes locomotion travel from the rendered pose and applies the
    /// extracted horizontal X/Z travel to the character GameObject.
    /// </summary>
    ApplyHorizontal
}
