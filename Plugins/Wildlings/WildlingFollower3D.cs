using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>Exploration companion states, highest priority first (Companion AI v2).</summary>
public enum WildlingFollowState
{
    Combat,
    Regroup,
    Navigate,
    Accompany,
    Settle,
    Observe
}

/// <summary>
/// Companion exploration AI v2.0.
///
/// Keep pace with a sprinting trainer, settle without fidgeting, and navigate
/// around obstacles using the trainer's actual path instead of cutting through
/// walls. Formation zones come from a smoothed ground travel direction (never
/// camera yaw or facing yaw), so orbiting the camera or turning in place cannot
/// move the party.
/// </summary>
public sealed class WildlingFollower3D : Component
{
    /// <summary>Scene object name that owns TeamManager3D.</summary>
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Side offset override in metres (0 = use the species zone).</summary>
    public float SideOffset { get; set; }

    /// <summary>Behind offset override in metres (0 = use the species zone).</summary>
    public float BehindOffset { get; set; }

    // ---- Speed model (v2: companions must outrun a sprinting trainer) ----
    public float FollowSpeedCap { get; set; } = 6.6f;

    public float CatchUpSpeedCap { get; set; } = 9f;

    public float Acceleration { get; set; } = 14f;

    public float Deceleration { get; set; } = 18f;

    /// <summary>Formation error that starts correction (hysteresis high).</summary>
    public float SlotStartError { get; set; } = 1.4f;

    /// <summary>Formation error that stops correction (hysteresis low).</summary>
    public float SlotStopError { get; set; } = .6f;

    public float CatchUpError { get; set; } = 3f;

    public float UrgentError { get; set; } = 6f;

    /// <summary>Seconds of trainer velocity used to lead the slot position.</summary>
    public float PredictiveLead { get; set; } = .2f;

    // ---- Formation heading ----
    public float TravelSpeedThreshold { get; set; } = .7f;

    public float HeadingSmoothing { get; set; } = .4f;

    // ---- Trail ----
    public float TrailSpacing { get; set; } = .5f;

    public float TrailLifetime { get; set; } = 8f;

    public int MaxTrailPoints { get; set; } = 40;

    // ---- Separation / settle / dwell ----
    public float SeparationRadius { get; set; } = 1.2f;

    public float SeparationStrength { get; set; } = .6f;

    public float SettleMinDistance { get; set; } = 1.5f;

    public float SettleMaxDistance { get; set; } = 4f;

    public float SettleCooldown { get; set; } = .8f;

    public float ObserveDwell { get; set; } = .6f;

    public float NoProgressSeconds { get; set; } = 1.3f;

    public float IdleFaceSpeed { get; set; } = 6f;

    /// <summary>Current behavior state (Combat, Regroup, Navigate, Accompany, Settle, Observe).</summary>
    public WildlingFollowState State => _state;

    /// <summary>Distance from the companion to its preferred formation point.</summary>
    public float FollowError => _slotError;

    public float DesiredSpeed => _desiredSpeed;

    public float ActualSpeed => _speed;

    /// <summary>Human readable zone, e.g. "Left flank" or "Rear".</summary>
    public string FormationZone { get; private set; } = "Rear";

    /// <summary>One-line diagnostics used by the HUD debug overlay.</summary>
    public string DebugSnapshot =>
        $"{FormationZone} {_state} err {_slotError:0.0}m spd {_speed:0.0}/{_desiredSpeed:0.0}";

    private static readonly Vector3 DefaultTravel = -Vector3.UnitZ;

    private readonly List<(Vector3 Position, double Time)> _trail = new();
    private Vector3 _travel = DefaultTravel;
    private Vector3 _leaderVelocity;
    private Vector3 _leaderPosition;
    private Vector3 _destination;
    private Vector3 _progressProbe;
    private bool _hasLeaderSample;
    private bool _hasDestination;
    private float _speed;
    private float _desiredSpeed;
    private float _slotError;
    private float _settleCooldownRemaining;
    private float _noProgressTimer;
    private float _stateDwell;
    private double _destinationTimer;
    private double _clock;
    private bool _moving;
    private WildlingFollowState _state = WildlingFollowState.Settle;

    private WildlingStats3D? Stats => GameObject?.GetComponent<WildlingStats3D>();

    private CharacterController3D? Motor => GameObject?.GetComponent<CharacterController3D>();

    protected override void OnUpdate()
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (self == null || scene == null)
        {
            return;
        }

        float delta = (float)Time.DeltaTime;

        if (self.GetComponent<HealthComponent>() is { IsDead: true })
        {
            EnterState(WildlingFollowState.Settle, delta);
            StopMoving();
            return;
        }

        // 1. Combat override: hand movement to the existing combat AI.
        if (self.GetComponent<WildlingCombat3D>() is { CombatEnabled: true })
        {
            EnterState(WildlingFollowState.Combat, delta);
            StopMoving();
            return;
        }

        TeamManager3D? team = string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : scene.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>();

        GameObject? leader = team?.Leader;
        if (leader == null || ReferenceEquals(leader, self))
        {
            StopMoving();
            return;
        }

        UpdateLeaderSample(leader, delta);
        UpdateTrail(leader);
        UpdateDestination(leader);
        UpdateState(leader, delta);
        UpdateMotion(self, team, leader, delta);
    }

    private void EnterState(WildlingFollowState state, float delta)
    {
        if (_state == state)
        {
            _stateDwell += delta;
            return;
        }

        _state = state;
        _stateDwell = 0f;
    }

    private void UpdateLeaderSample(GameObject leader, float delta)
    {
        Vector3 position = leader.Transform.WorldPosition;

        if (!_hasLeaderSample)
        {
            _leaderPosition = position;
            _hasLeaderSample = true;
            return;
        }

        Vector3 move = position - _leaderPosition;
        move.Y = 0f;
        _leaderPosition = position;
        _leaderVelocity = delta > .0001f ? move / delta : Vector3.Zero;

        float speed = _leaderVelocity.Length();

        // Formation heading only follows real travel, never facing or camera.
        if (speed < TravelSpeedThreshold)
        {
            return;
        }

        Vector3 direction = Vector3.Normalize(_leaderVelocity);
        float factor = 1f - MathF.Exp(-delta / MathF.Max(.05f, HeadingSmoothing));
        Vector3 blended = Vector3.Lerp(_travel, direction, factor);
        _travel = blended.LengthSquared() > .000001f ? Vector3.Normalize(blended) : direction;
    }

    private void UpdateTrail(GameObject leader)
    {
        _clock += Time.DeltaTime;
        float spacing = MathF.Max(.1f, TrailSpacing);

        if (_trail.Count == 0)
        {
            _trail.Add((leader.Transform.WorldPosition, _clock));
            return;
        }

        Vector3 last = _trail[^1].Position;
        if (Vector3.Distance(last, leader.Transform.WorldPosition) >= spacing)
        {
            _trail.Add((leader.Transform.WorldPosition, _clock));
        }

        double oldest = _clock - Math.Max(1f, TrailLifetime);
        while (_trail.Count > 0 && _trail[0].Time < oldest)
        {
            _trail.RemoveAt(0);
        }

        while (_trail.Count > Math.Max(2, MaxTrailPoints))
        {
            _trail.RemoveAt(0);
        }
    }

    private void UpdateDestination(GameObject leader)
    {
        _destinationTimer -= Time.DeltaTime;
        if (_hasDestination && _destinationTimer > 0.0)
        {
            return;
        }

        _destinationTimer = .3;

        Vector3 forward = _travel;
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        (float side, float behind) = SlotZone();

        // Lead the moving trainer slightly so we match pace instead of chasing.
        Vector3 predicted = leader.Transform.WorldPosition +
                            _leaderVelocity * MathF.Max(0f, PredictiveLead);

        Vector3 preferred = predicted - forward * behind + right * side;

        if (IsRouteBlocked(predicted, preferred))
        {
            preferred = TrailDestination(behind);
        }

        _destination = preferred;
        _hasDestination = true;
        _slotError = FlatDistance(GameObject!.Transform.WorldPosition, _destination);
    }

    private (float Side, float Behind) SlotZone()
    {
        (string zone, float side, float behind) = Stats?.Definition.Id switch
        {
            WildlingSpeciesDatabase.SprigletId => ("Right flank", 1.8f, .7f),
            WildlingSpeciesDatabase.ZappetId => ("Rear", 0f, 1.8f),
            WildlingSpeciesDatabase.FireLizardId => ("Left flank", -1.8f, .7f),
            _ => ("Rear", 1.2f, 1.2f)
        };

        FormationZone = zone;
        return (SideOffset != 0f ? SideOffset : side, BehindOffset != 0f ? BehindOffset : behind);
    }

    private bool IsRouteBlocked(Vector3 from, Vector3 to)
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (scene == null)
        {
            return false;
        }

        Vector3 delta = to - from;
        delta.Y = 0f;
        float distance = delta.Length();
        if (distance < .6f)
        {
            return false;
        }

        return GameplayQuery3D.SphereCast(
            scene,
            from + Vector3.UnitY * .8f,
            delta / distance,
            .35f,
            out RaycastHit3D hit,
            distance,
            ignore: self,
            layerMask: null,
            source: self,
            bypassCollisionMatrix: true,
            includeTriggers: false) &&
            !IsGround(hit.GameObject);
    }

    private Vector3 TrailDestination(float behind)
    {
        if (_trail.Count == 0)
        {
            return _leaderPosition;
        }

        float travelled = 0f;
        Vector3 previous = _trail[^1].Position;

        for (int index = _trail.Count - 1; index >= 0; index--)
        {
            Vector3 point = _trail[index].Position;
            travelled += Vector3.Distance(previous, point);

            if (travelled >= behind)
            {
                return point;
            }

            previous = point;
        }

        return _trail[0].Position;
    }

    private void UpdateState(GameObject leader, float delta)
    {
        _settleCooldownRemaining = MathF.Max(0f, _settleCooldownRemaining - delta);

        float leaderSpeed = _leaderVelocity.Length();
        bool moving = leaderSpeed >= MathF.Max(.1f, TravelSpeedThreshold);
        float toLeader = FlatDistance(GameObject!.Transform.WorldPosition, leader.Transform.WorldPosition);

        // Priority ladder: Regroup > Navigate > Accompany > Settle > Observe.
        if (_slotError >= MathF.Max(UrgentError, CatchUpError) || toLeader > UrgentError + SettleMaxDistance)
        {
            EnterState(WildlingFollowState.Regroup, delta);
            return;
        }

        if (_noProgressTimer >= MathF.Max(.6f, NoProgressSeconds))
        {
            EnterState(WildlingFollowState.Navigate, delta);
            return;
        }

        if (moving)
        {
            EnterState(WildlingFollowState.Accompany, delta);
            return;
        }

        // Trainer stopped. Keep a comfortable, reachable place instead of reseating.
        if (toLeader >= SettleMinDistance && toLeader <= SettleMaxDistance && _slotError <= SlotStartError)
        {
            _settleCooldownRemaining = MathF.Max(_settleCooldownRemaining, MathF.Max(.2f, SettleCooldown));
            EnterState(WildlingFollowState.Settle, delta);
            return;
        }

        if (_stateDwell >= MathF.Max(.4f, ObserveDwell))
        {
            EnterState(WildlingFollowState.Observe, delta);
            return;
        }

        EnterState(WildlingFollowState.Settle, delta);
    }

    private void UpdateMotion(GameObject self, TeamManager3D? team, GameObject leader, float delta)
    {
        Vector3 position = self.Transform.WorldPosition;
        float leaderDistance = FlatDistance(position, leader.Transform.WorldPosition);

        // No-progress detection: asked to move but standing still.
        if (FlatDistance(_progressProbe, position) > .05f)
        {
            _progressProbe = position;
            _noProgressTimer = 0f;
        }
        else if (_desiredSpeed > 1f)
        {
            _noProgressTimer += delta;
        }
        else
        {
            _noProgressTimer = 0f;
        }

        if (_state is WildlingFollowState.Combat)
        {
            StopMoving();
            return;
        }

        // Dead-zone hysteresis so companions do not fidget near their slot.
        if (!_moving && _slotError > MathF.Max(SlotStopError + .05f, SlotStartError))
        {
            _moving = true;
        }
        else if (_moving && _slotError <= MathF.Max(.05f, SlotStopError))
        {
            _moving = false;
        }

        bool settleHold = _state is WildlingFollowState.Settle or WildlingFollowState.Observe &&
                          _settleCooldownRemaining <= 0f &&
                          leaderDistance >= SettleMinDistance &&
                          leaderDistance <= SettleMaxDistance;

        if (settleHold)
        {
            _moving = false;
        }

        if (!_moving)
        {
            _desiredSpeed = 0f;
            _speed = MoveTowards(_speed, 0f, MathF.Max(.1f, Deceleration) * delta);
            StopMoving();
            FaceIdle(self, leader, delta);
            return;
        }

        Vector3 toDestination = _destination - position;
        toDestination.Y = 0f;
        float distance = toDestination.Length();
        if (distance < .05f)
        {
            _moving = false;
            StopMoving();
            return;
        }

        float leaderSpeed = _leaderVelocity.Length();
        bool catchUp = _state is WildlingFollowState.Regroup or WildlingFollowState.Navigate ||
                       _slotError >= CatchUpError ||
                       leaderDistance >= CatchUpError + SettleMaxDistance;

        float cap = catchUp ? CatchUpSpeedCap : FollowSpeedCap;
        float errorBeyondStop = MathF.Max(0f, _slotError - SlotStopError);

        // Proactive: match the trainer's speed plus a margin that grows with the
        // formation error, so catch-up starts before falling off camera.
        _desiredSpeed = MathF.Min(cap, MathF.Max(leaderSpeed, 0f) + errorBeyondStop * 2.2f);
        if (_desiredSpeed < .2f)
        {
            _desiredSpeed = 0f;
        }

        float rate = _desiredSpeed > _speed ? Acceleration : Deceleration;
        _speed = MoveTowards(_speed, _desiredSpeed, MathF.Max(.1f, rate) * delta);

        if (_speed <= .05f)
        {
            StopMoving();
            return;
        }

        Vector3 direction = toDestination / distance;
        direction += Separation(self, team) * MathF.Max(0f, SeparationStrength);
        if (direction.LengthSquared() <= .000001f)
        {
            StopMoving();
            return;
        }

        direction = Vector3.Normalize(direction);

        if (Motor is { } motor)
        {
            motor.MoveSpeed = MathF.Max(.1f, _speed);
            motor.Move(direction);
        }
        else
        {
            self.Transform.WorldPosition += direction * _speed * delta;
        }

        FaceDirection(self, direction);
    }

    /// <summary>Small repulsion so companions do not stack or push each other.</summary>
    private Vector3 Separation(GameObject self, TeamManager3D? team)
    {
        if (team == null || SeparationRadius <= .01f)
        {
            return Vector3.Zero;
        }

        Vector3 position = self.Transform.WorldPosition;
        Vector3 push = Vector3.Zero;

        foreach (GameObject member in team.Members())
        {
            if (ReferenceEquals(member, self))
            {
                continue;
            }

            Vector3 away = position - member.Transform.WorldPosition;
            away.Y = 0f;
            float distance = away.Length();
            if (distance < .001f || distance >= SeparationRadius)
            {
                continue;
            }

            push += Vector3.Normalize(away) * (1f - distance / SeparationRadius);
        }

        return push;
    }

    private void StopMoving()
    {
        if (Stats is { } stats && Motor is { } motor && MathF.Abs(motor.MoveSpeed - stats.MoveSpeed) > .01f)
        {
            motor.MoveSpeed = stats.MoveSpeed;
        }
    }

    private void FaceIdle(GameObject self, GameObject leader, float delta)
    {
        // Attentive idle: a slow glance toward the trainer needs no navigation.
        FaceSlowly(self, leader.Transform.WorldPosition, delta);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.Y = 0f;
        b.Y = 0f;
        return Vector3.Distance(a, b);
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + MathF.Sign(target - current) * maxDelta;
    }

    private void FaceDirection(GameObject self, Vector3 direction)
    {
        direction.Y = 0f;
        if (direction.LengthSquared() <= .000001f)
        {
            return;
        }

        direction = Vector3.Normalize(direction);
        Vector3 euler = self.Transform.EulerAngles;
        euler.Y = MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI;
        self.Transform.EulerAngles = euler;
    }

    private void FaceSlowly(GameObject self, Vector3 target, float delta)
    {
        Vector3 toTarget = target - self.Transform.WorldPosition;
        toTarget.Y = 0f;
        if (toTarget.LengthSquared() <= .25f)
        {
            return;
        }

        toTarget = Vector3.Normalize(toTarget);
        float wanted = MathF.Atan2(-toTarget.X, -toTarget.Z) * 180f / MathF.PI;
        Vector3 euler = self.Transform.EulerAngles;
        float difference = wanted - euler.Y;

        while (difference > 180f)
        {
            difference -= 360f;
        }

        while (difference < -180f)
        {
            difference += 360f;
        }

        float step = MathF.Max(1f, IdleFaceSpeed) * 12f * delta;
        euler.Y += Math.Clamp(difference, -step, step);
        self.Transform.EulerAngles = euler;
    }

    private static bool IsGround(GameObject gameObject)
    {
        for (GameObject? current = gameObject; current != null; current = current.Parent)
        {
            if (current.GetComponent<GroundSurface>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
