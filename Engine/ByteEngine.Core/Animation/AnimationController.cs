using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Animation;

public enum LocomotionState
{
    Idle,
    Walk,
    Run,
    Jump,
    Fall,
    Land
}

public sealed class AnimationController : Component
{
    public string Idle { get; set; } = "Idle";
    public string Walk { get; set; } = "Walk";
    public string Run { get; set; } = "Run";
    public string Jump { get; set; } = "Jump";
    public string Fall { get; set; } = "Fall";
    public string Land { get; set; } = "Land";
    public float RunThreshold { get; set; } = 4f;
    public LocomotionState State { get; private set; }

    protected override void OnUpdate()
    {
        CharacterController3D? controller = GameObject.GetComponent<CharacterController3D>();
        if (controller == null) return;

        State = controller.JustLanded
            ? LocomotionState.Land
            : controller.IsFalling
                ? LocomotionState.Fall
                : !controller.IsGrounded
                    ? LocomotionState.Jump
                    : controller.Speed < .05f
                        ? LocomotionState.Idle
                        : controller.Speed >= RunThreshold
                            ? LocomotionState.Run
                            : LocomotionState.Walk;
    }
}
