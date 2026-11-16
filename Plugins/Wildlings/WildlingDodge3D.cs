using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Survivors dodge (GDD appendix: 3.2 m over 0.28 s, 2.5 s cooldown).
///
/// Movement is handed to the character controller as a burst, so it sweeps against
/// collision instead of teleporting. Everything is a property: key, distance, duration,
/// cooldown and whether the dodge is invulnerable.
/// </summary>
public sealed class WildlingDodge3D : Component
{
    public Key DodgeKey { get; set; } = Key.Space;

    public float Distance { get; set; } = 3.2f;

    public float DurationSeconds { get; set; } = .28f;

    public float CooldownSeconds { get; set; } = 2.5f;

    /// <summary>Grants i-frames for the duration of the dodge.</summary>
    public bool InvulnerableDuringDodge { get; set; } = true;

    /// <summary>Falls back to the creature's facing when there is no movement input.</summary>
    public bool DodgeAlongFacing { get; set; } = true;

    public float CooldownRemaining { get; private set; }

    public bool IsDodging => _remaining > 0f;

    private float _remaining;
    private HealthComponent? _health;

    protected override void OnUpdate()
    {
        float delta = (float)Time.DeltaTime;

        CooldownRemaining = MathF.Max(0f, CooldownRemaining - delta);

        if (_remaining > 0f)
        {
            _remaining = MathF.Max(0f, _remaining - delta);

            if (_remaining <= 0f)
            {
                ReleaseInvulnerability();
            }

            return;
        }

        if (!Input.IsGameInputCaptured || CooldownRemaining > 0f || !Input.IsKeyPressed(DodgeKey))
        {
            return;
        }

        Begin();
    }

    /// <summary>Starts a dodge in the input direction (or the facing when standing still).</summary>
    public void Begin()
    {
        GameObject? self = GameObject;
        if (self == null)
        {
            return;
        }

        Vector3 direction = Vector3.Zero;

        if (self.GetComponent<PlayerController3D>() is { } controller)
        {
            direction = controller.ReadDesiredMovement();
        }

        if (direction.LengthSquared() <= .0001f && DodgeAlongFacing)
        {
            direction = self.Transform.Forward;
        }

        direction.Y = 0f;

        if (direction.LengthSquared() <= .0001f)
        {
            direction = -Vector3.UnitZ;
        }

        direction = Vector3.Normalize(direction);

        float seconds = MathF.Max(.05f, DurationSeconds);
        float speed = MathF.Max(.1f, Distance) / seconds;

        if (self.GetComponent<CharacterController3D>() is { } motor)
        {
            motor.BeginBurstMotion(direction * speed, seconds);
        }
        else
        {
            self.Transform.WorldPosition += direction * Distance;
        }

        _remaining = seconds;
        CooldownRemaining = MathF.Max(0f, CooldownSeconds);

        if (InvulnerableDuringDodge)
        {
            _health = self.GetComponent<HealthComponent>();
            if (_health != null)
            {
                _health.Invulnerable = true;
            }
        }
    }

    private void ReleaseInvulnerability()
    {
        if (_health != null)
        {
            _health.Invulnerable = false;
            _health = null;
        }
    }
}
