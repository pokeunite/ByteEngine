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

/// <summary>How the travelling party arranges itself behind the trainer.</summary>
public enum WildlingFormation
{
    /// <summary>
    /// Follow the authored ring (WildlingSlotMarker3D) whose SlotIndex matches this
    /// companion. Move the ring in the scene view to place the creature: the ring's
    /// world position is the slot, so its offset from the trainer is the distance kept.
    /// </summary>
    Marker,

    /// <summary>
    /// Horizontal line abreast: slot 1 in the middle, slot 2 to the right, slot 3 to
    /// the left, all at the same depth. The line is a single rank, so the party reads
    /// as one row when the trainer looks back.
    /// </summary>
    Line,

    /// <summary>Legacy per-species zones (Fire Lizard left, Spriglet right, Zappet rear).</summary>
    Zones
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

    /// <summary>Formation layout used while the party travels.</summary>
    public WildlingFormation Formation { get; set; } = WildlingFormation.Marker;

    /// <summary>
    /// Party slot (1-3) that decides the side of the line. 0 resolves the slot from
    /// TeamManager3D's slot names, so members follow their team order automatically.
    /// </summary>
    public int SlotIndex { get; set; }

    /// <summary>Lateral gap between neighbouring companions in the line, in metres.</summary>
    public float LineSpacing { get; set; } = 1.7f;

    /// <summary>Depth of the whole line behind the trainer, in metres.</summary>
    public float LineBehind { get; set; } = 1.6f;

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

    /// <summary>Turn rate while travelling (degrees per second).</summary>
    public float FaceTurnSpeed { get; set; } = 540f;

    /// <summary>Idle glances never turn further than this from the current facing.</summary>
    public float IdleGlanceMaxAngle { get; set; } = 75f;

    /// <summary>Seconds the travel facing is held after stopping (no instant turn-around).</summary>
    public float FacingHoldSeconds { get; set; } = .6f;

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
    private bool _facingInitialized;
    private float _facingYaw;
    private float _facingHoldRemaining;
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
    private WildlingSlotMarker3D? _marker;
    private int _markerSlot;

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
        UpdateDestination(leader, team);
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

    private void UpdateDestination(GameObject leader, TeamManager3D? team)
    {
        int slot = ResolveSlot(team);

        if (Formation == WildlingFormation.Marker && MarkerTarget(slot) is { } marker)
        {
            // The ring is authored in the scene view and parented to the trainer, so its
            // world position already carries both the distance and the side. Refresh every
            // frame: dragging the ring in the editor moves the creature immediately.
            _destination = marker.Transform.WorldPosition;
            _hasDestination = true;
            _slotError = FlatDistance(GameObject!.Transform.WorldPosition, _destination);
            return;
        }

        _destinationTimer -= Time.DeltaTime;
        if (!_hasDestination || _destinationTimer <= 0.0)
        {
            _destinationTimer = .3;

            Vector3 forward = _travel;
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
            (float side, float behind) = SlotZone(slot);

            // Predictive lead compensates for our own reaction time. It must never
            // push the slot past the trainer, otherwise companions run in front.
            float leaderSpeed = _leaderVelocity.Length();
            float leadDistance = MathF.Min(leaderSpeed * MathF.Max(0f, PredictiveLead), MathF.Max(0f, behind));
            Vector3 predicted = leader.Transform.WorldPosition;
            if (leadDistance > .001f && leaderSpeed > .001f)
            {
                predicted += _leaderVelocity / leaderSpeed * leadDistance;
            }

            Vector3 preferred = predicted - forward * behind + right * side;

            if (IsRouteBlocked(predicted, preferred))
            {
                preferred = TrailDestination(behind);
            }

            _destination = preferred;
            _hasDestination = true;
        }

        // Re-evaluate every frame (not just when the destination refreshes) so the
        // braking curve reacts immediately instead of overshooting the slot.
        _slotError = FlatDistance(GameObject!.Transform.WorldPosition, _destination);
    }

    /// <summary>
    /// Ring authored for this slot, cached because the marker does not change at
    /// runtime. Returns null when the scene has no matching ring, in which case the
    /// computed formation is used instead.
    /// </summary>
    private WildlingSlotMarker3D? MarkerTarget(int slot)
    {
        if (_marker != null && _markerSlot == slot)
        {
            return _marker;
        }

        _markerSlot = slot;
        _marker = null;

        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return null;
        }

        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.GetComponent<WildlingSlotMarker3D>() is { } marker &&
                marker.SlotIndex == slot)
            {
                _marker = marker;
                FormationZone = $"Ring {slot}";
                break;
            }
        }

        return _marker;
    }

    private (float Side, float Behind) SlotZone(int slot)
    {
        if (Formation == WildlingFormation.Line)
        {
            // Horizontal line abreast: 1 centre, 2 right, 3 left, equal depth.
            float spacing = MathF.Max(.4f, LineSpacing);
            (string lineZone, float lineSide) = slot switch
            {
                2 => ("Line right", spacing),
                3 => ("Line left", -spacing),
                _ => ("Line centre", 0f)
            };

            FormationZone = lineZone;
            return (
                SideOffset != 0f ? SideOffset : lineSide,
                BehindOffset != 0f ? BehindOffset : MathF.Max(0f, LineBehind));
        }

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

    /// <summary>
    /// Party slot for this companion. An explicit <see cref="SlotIndex"/> wins;
    /// otherwise the member's position in TeamManager3D's slot list is used, so the
    /// line keeps its authored order (1 = Fire Lizard, 2 = Spriglet, 3 = Zappet).
    /// </summary>
    private int ResolveSlot(TeamManager3D? team)
    {
        if (SlotIndex is >= 1 and <= 3)
        {
            return SlotIndex;
        }

        GameObject? self = GameObject;
        if (team == null || self == null)
        {
            return 0;
        }

        for (int slot = 1; slot <= 3; slot++)
        {
            if (ReferenceEquals(team.Resolve(slot), self))
            {
                return slot;
            }
        }

        return 0;
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

        // Dead-zone hysteresis so companions do not fidget near their slot - but only
        // while the trainer is stationary. A moving slot is a moving target: stopping
        // at it would produce the "run, stop, wait for distance, run" stutter.
        float leaderSpeed = _leaderVelocity.Length();
        bool travelling = leaderSpeed >= MathF.Max(.1f, TravelSpeedThreshold);

        if (travelling)
        {
            _moving = true;
        }
        else if (!_moving && _slotError > MathF.Max(SlotStopError + .05f, SlotStartError))
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
        if (distance < .01f)
        {
            _moving = false;
            StopMoving();
            return;
        }

        bool catchUp = _state is WildlingFollowState.Regroup or WildlingFollowState.Navigate ||
                       _slotError >= CatchUpError ||
                       leaderDistance >= CatchUpError + SettleMaxDistance;

        float cap = catchUp ? CatchUpSpeedCap : FollowSpeedCap;
        float errorBeyondStop = MathF.Max(0f, _slotError - SlotStopError);

        // Braking curve: only plan a speed we can shed before reaching the slot, so
        // companions glide in instead of shooting past it and waiting.
        float arriveLimit = MathF.Sqrt(2f * MathF.Max(1f, Deceleration) * errorBeyondStop);

        // Overtake guard: outside catch-up, never exceed the trainer's pace by more
        // than the error requires. This is what stops them running past the player.
        float overtakeLimit = catchUp
            ? MathF.Max(CatchUpSpeedCap, leaderSpeed)
            : leaderSpeed + Math.Clamp(errorBeyondStop, .6f, 3f);

        _desiredSpeed = MathF.Min(cap, MathF.Min(overtakeLimit, arriveLimit));

        // While the trainer travels, hold station: never drop below their pace, or the
        // party falls behind and has to sprint back.
        if (travelling)
        {
            _desiredSpeed = Math.Clamp(MathF.Max(_desiredSpeed, leaderSpeed), 0f, cap);
        }

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

        Vector3 heading = toDestination / distance;
        Vector3 direction = heading + Separation(self, team) * MathF.Max(0f, SeparationStrength);
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

        // Face the route, not the separation-adjusted vector, so neighbours nudging
        // each other cannot make the visual heading oscillate.
        FaceDirection(self, heading, delta);
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
        // Hold the travel facing briefly after stopping, then allow at most a small
        // glance toward the trainer. Never a 180° turn-around on the spot.
        if (_facingHoldRemaining > 0f)
        {
            _facingHoldRemaining = MathF.Max(0f, _facingHoldRemaining - delta);
            return;
        }

        Vector3 toLeader = leader.Transform.WorldPosition - self.Transform.WorldPosition;
        toLeader.Y = 0f;
        if (toLeader.LengthSquared() <= .25f)
        {
            return;
        }

        float difference = ShortestAngle(YawOf(Vector3.Normalize(toLeader)) - CurrentYaw(self));
        if (MathF.Abs(difference) > Math.Clamp(IdleGlanceMaxAngle, 0f, 170f))
        {
            return;
        }

        Turn(self, difference, MathF.Max(1f, IdleFaceSpeed) * 6f * delta);
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

    private void FaceDirection(GameObject self, Vector3 direction, float delta)
    {
        direction.Y = 0f;
        if (direction.LengthSquared() <= .000001f)
        {
            return;
        }

        float difference = ShortestAngle(YawOf(Vector3.Normalize(direction)) - CurrentYaw(self));
        Turn(self, difference, MathF.Max(90f, FaceTurnSpeed) * delta);
        _facingHoldRemaining = MathF.Max(.05f, FacingHoldSeconds);
    }

    /// <summary>Local yaw in degrees, seeded once from the authored facing.</summary>
    private float CurrentYaw(GameObject self)
    {
        if (!_facingInitialized)
        {
            _facingYaw = YawOf(self.Transform.Forward);
            _facingInitialized = true;
        }

        return _facingYaw;
    }

    /// <summary>
    /// Rotates about world up only. Uses a quaternion so the Euler round-trip cannot
    /// introduce pitch/roll drift or per-frame jitter.
    /// </summary>
    private void Turn(GameObject self, float difference, float maxStep)
    {
        float step = Math.Clamp(difference, -MathF.Abs(maxStep), MathF.Abs(maxStep));
        if (MathF.Abs(step) < .01f)
        {
            return;
        }

        _facingYaw = ShortestAngle(CurrentYaw(self) + step);
        self.Transform.WorldRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, _facingYaw * MathF.PI / 180f);
    }

    private static float YawOf(Vector3 direction) => MathF.Atan2(-direction.X, -direction.Z) * 180f / MathF.PI;

    private static float ShortestAngle(float degrees)
    {
        while (degrees > 180f)
        {
            degrees -= 360f;
        }

        while (degrees < -180f)
        {
            degrees += 360f;
        }

        return degrees;
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
