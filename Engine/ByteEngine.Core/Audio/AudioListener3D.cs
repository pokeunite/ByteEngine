using System.Numerics;

using ByteEngine.Core.Scene;

using OpenTK.Audio.OpenAL;

namespace ByteEngine.Core.Audio;

/// <summary>
/// World-space audio listener.
///
/// ByteEngine uses the first enabled AudioListener3D in scene hierarchy order.
/// Normally this component should be placed on the active game Camera object.
/// </summary>
public sealed class AudioListener3D
    : Component
{
    private Vector3 _previousPosition;

    private bool _hasPreviousPosition;

    private float _volume =
        1.0f;

    public override int UpdateOrder =>
        -850;

    public float Volume
    {
        get =>
            _volume;

        set =>
            _volume =
                Math.Clamp(
                    float.IsFinite(
                        value)
                        ? value
                        : 1.0f,
                    0.0f,
                    4.0f);
    }

    public bool IsActiveListener =>
        IsPrimaryListener();

    protected override void OnStart()
    {
        _previousPosition =
            Transform.WorldPosition;

        _hasPreviousPosition =
            true;

        UpdateListener();
    }

    protected override void OnUpdate()
    {
        UpdateListener();
    }

    protected override void OnStop()
    {
        _hasPreviousPosition =
            false;
    }

    private void UpdateListener()
    {
        if (!IsPrimaryListener() ||
            !AudioEngine.EnsureInitialized())
        {
            return;
        }

        Vector3 position =
            Transform.WorldPosition;

        Vector3 velocity =
            Vector3.Zero;

        float deltaTime =
            (float)Time.DeltaTime;

        if (_hasPreviousPosition &&
            deltaTime >
                0.000001f)
        {
            velocity =
                (
                    position -
                    _previousPosition
                ) /
                deltaTime;
        }

        _previousPosition =
            position;

        _hasPreviousPosition =
            true;

        Vector3 forward =
            NormalizeOr(
                Transform.Forward,
                -Vector3.UnitZ);

        Vector3 up =
            NormalizeOr(
                Transform.Up,
                Vector3.UnitY);

        AL.Listener(
            ALListener3f.Position,
            position.X,
            position.Y,
            position.Z);

        AL.Listener(
            ALListener3f.Velocity,
            velocity.X,
            velocity.Y,
            velocity.Z);

        AL.Listener(
            ALListenerf.Gain,
            Volume);

        AL.Listener(
            ALListenerfv.Orientation,
            new[]
            {
                forward.X,
                forward.Y,
                forward.Z,
                up.X,
                up.Y,
                up.Z
            });
    }

    private bool IsPrimaryListener()
    {
        ByteEngine.Core.Scene.Scene? scene =
            GameObject.Scene;

        if (scene ==
            null)
        {
            return false;
        }

        AudioListener3D? primary =
            scene.GameObjects
                .Where(
                    item =>
                        item.ActiveInHierarchy)
                .SelectMany(
                    item =>
                        item.Components
                            .OfType<AudioListener3D>())
                .FirstOrDefault(
                    listener =>
                        listener.Enabled);

        return
            ReferenceEquals(
                primary,
                this);
    }

    private static Vector3 NormalizeOr(
        Vector3 value,
        Vector3 fallback)
    {
        return
            value.LengthSquared() >
            0.000001f
                ? Vector3.Normalize(
                    value)
                : fallback;
    }
}
