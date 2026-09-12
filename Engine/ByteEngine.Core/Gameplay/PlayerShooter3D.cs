using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class PlayerShooter3D : Component
{
    public bool Automatic { get; set; } = true;
    public override int UpdateOrder => -90;

    protected override void OnUpdate()
    {
        bool fire = Automatic ? Input.IsMouseButtonDown(MouseButton.Left) : Input.IsMouseButtonPressed(MouseButton.Left);
        if (fire) AttachedGameObject?.GetComponent<ProjectileLauncher3D>()?.Fire();
    }
}
