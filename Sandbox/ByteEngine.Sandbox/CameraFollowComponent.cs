using ByteEngine.Core.Scene;

namespace ByteEngine.Sandbox;

public sealed class CameraFollowComponent
    : Component
{
    public Transform? Target { get; set; }

    protected override void OnStart()
    {
        FollowTarget();
    }

    protected override void OnUpdate()
    {
        FollowTarget();
    }

    private void FollowTarget()
    {
        if (Target == null)
        {
            return;
        }

        Transform.Position =
            Target.Position;
    }
}
