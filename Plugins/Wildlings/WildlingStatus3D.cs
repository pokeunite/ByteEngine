using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Shared status effects from GDD chapter 16: Burning (3 dmg/s for 3 s,
/// refresh instead of stack), Root (movement lock, attacks still allowed) and
/// a slow used by bosses. Works on both SimpleEnemyAI3D hostiles and
/// player-driven characters.
/// </summary>
public sealed class WildlingStatus3D : Component
{
    private SimpleEnemyAI3D? _enemyAi;
    private CharacterController3D? _motor;
    private float _baseEnemySpeed;
    private float _baseMotorSpeed;
    private bool _speedsCached;

    private float _burnDamagePerSecond;
    private float _burnRemaining;
    private float _rootRemaining;
    private float _slowPercent;
    private float _slowRemaining;

    public bool IsRooted => _rootRemaining > 0f;

    public float BurnRemainingSeconds => _burnRemaining;

    public float SlowRemainingSeconds => _slowRemaining;

    public void ApplyBurning(float damagePerSecond, float seconds)
    {
        if (damagePerSecond <= 0f || seconds <= 0f)
        {
            return;
        }

        // Reapplication refreshes the same Burning effect; it never stacks.
        _burnDamagePerSecond = MathF.Max(_burnDamagePerSecond, damagePerSecond);
        _burnRemaining = MathF.Max(_burnRemaining, seconds);
    }

    public void ApplyRoot(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        CacheSpeeds();
        _rootRemaining = MathF.Max(_rootRemaining, seconds);
        RefreshMovement();
    }

    public void ApplySlow(float percent, float seconds)
    {
        if (percent <= 0f || seconds <= 0f)
        {
            return;
        }

        CacheSpeeds();
        _slowPercent = Math.Clamp(MathF.Max(_slowPercent, percent), 0f, .8f);
        _slowRemaining = MathF.Max(_slowRemaining, seconds);
        RefreshMovement();
    }

    protected override void OnUpdate()
    {
        float delta = (float)Time.DeltaTime;
        if (delta <= 0f)
        {
            return;
        }

        if (_rootRemaining > 0f || _slowRemaining > 0f || _burnRemaining > 0f)
        {
            if (_burnRemaining > 0f && _burnDamagePerSecond > 0f)
            {
                float tick = MathF.Min(delta, _burnRemaining);
                _burnRemaining -= tick;
                GameObject?.GetComponent<HealthComponent>()?.Damage(_burnDamagePerSecond * tick);
                if (_burnRemaining <= 0f)
                {
                    _burnDamagePerSecond = 0f;
                }
            }

            if (_rootRemaining > 0f)
            {
                _rootRemaining = MathF.Max(0f, _rootRemaining - delta);
            }

            if (_slowRemaining > 0f)
            {
                _slowRemaining = MathF.Max(0f, _slowRemaining - delta);
                if (_slowRemaining <= 0f)
                {
                    _slowPercent = 0f;
                }
            }

            RefreshMovement();
        }
    }

    private void CacheSpeeds()
    {
        if (_speedsCached || GameObject is not { } owner)
        {
            return;
        }

        _enemyAi = owner.GetComponent<SimpleEnemyAI3D>();
        _motor = owner.GetComponent<CharacterController3D>();
        _baseEnemySpeed = _enemyAi?.MoveSpeed ?? 0f;
        _baseMotorSpeed = _motor?.MoveSpeed ?? 0f;
        _speedsCached = true;
    }

    private void RefreshMovement()
    {
        CacheSpeeds();

        float multiplier = _rootRemaining > 0f ? 0f : 1f - (_slowRemaining > 0f ? _slowPercent : 0f);

        if (_enemyAi != null)
        {
            _enemyAi.MoveSpeed = _baseEnemySpeed * multiplier;
        }

        if (_motor != null)
        {
            _motor.MoveSpeed = _baseMotorSpeed * multiplier;
        }
    }
}
