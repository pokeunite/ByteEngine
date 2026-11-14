using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Exploration follower for the Wildling Adventures v0.3 handoff.
///
/// Slots come from a stable *travel* heading (never camera or facing yaw), each
/// follower keeps a loose preferred zone with start/settle hysteresis, catches
/// up when far behind, and follows a breadcrumb trail when the direct line to
/// its slot is blocked. While the leader stands still nothing repositions, so
/// orbiting the camera can never make the party run in circles.
/// </summary>
public sealed class WildlingFollower3D : Component
{
    /// <summary>Scene object name that owns TeamManager3D.</summary>
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Side offset override in metres (0 = use the species slot).</summary>
    public float SideOffset { get; set; }

    /// <summary>Behind offset override in metres (0 = use the species slot).</summary>
    public float BehindOffset { get; set; }

    public float StartDistance { get; set; } = 1.8f;

    public float SettleDistance { get; set; } = 0.7f;

    public float FollowSpeed { get; set; } = 5f;

    public float CatchUpSpeed { get; set; } = 7f;

    public float CatchUpDistance { get; set; } = 7f;

    public float DestinationInterval { get; set; } = .3f;

    /// <summary>Seconds for the travel heading to converge on a new direction.</summary>
    public float HeadingSmoothing { get; set; } = .4f;

    public float TrailSpacing { get; set; } = .4f;

    public int MaxTrailPoints { get; set; } = 24;

    /// <summary>Speed of the idle head/body turn toward the leader.</summary>
    public float IdleFaceSpeed { get; set; } = 6f;

    private static readonly Vector3 DefaultTravel = -Vector3.UnitZ;

    private readonly List<Vector3> _trail = new();
    private Vector3 _travel = DefaultTravel;
    private Vector3 _destination;
    private Vector3 _lastLeaderPosition;
    private Vector3 _lastTrailSample;
    private Vector3 _lastPosition;
    private Vector3 _blockedProbe;
    private bool _hasLeaderSample;
    private bool _hasDestination;
    private bool _moving;
    private bool _travelling;
    private double _destinationTimer;
    private float _blockedSeconds;
    private float _stuckSeconds;

    /// <summary>Debug state: Idle, Follow, CatchUp, PathFollow or Combat.</summary>
    public string State { get; private set; } = "Idle";

    public Vector3 PreferredDestination => _destination;

    public Vector3 TravelHeading => _travel;

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

        if (self.GetComponent<HealthComponent>() is { IsDead: true })
        {
            RestoreSpeed();
            State = "Idle";
            return;
        }

        // Combat AI owns the creature while fighting; exploration follow yields.
        if (self.GetComponent<WildlingCombat3D>() is { CombatEnabled: true })
        {
            RestoreSpeed();
            State = "Combat";
            return;
        }

        TeamManager3D? team = string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : scene.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>();

        GameObject? leader = team?.Leader;
        if (leader == null || ReferenceEquals(leader, self))
        {
            RestoreSpeed();
            State = "Idle";
            return;
        }

        float delta = (float)Time.DeltaTime;
        UpdateTravel(leader, delta);
        UpdateDestination(leader);
        UpdateMovement(self, leader, delta);
    }

    private void UpdateTravel(GameObject leader, float delta)
    {
        Vector3 leaderPosition = leader.Transform.WorldPosition;

        if (!_hasLeaderSample)
        {
            _lastLeaderPosition = leaderPosition;
            _lastTrailSample = leaderPosition;
            _hasLeaderSample = true;
            return;
        }

        Vector3 move = leaderPosition - _lastLeaderPosition;
        move.Y = 0f;
        float distance = move.Length();
        _lastLeaderPosition = leaderPosition;

        // 0.0015 m per frame ignores sub-millimetre jitter and in-place turns.
        _travelling = distance > .0015f && distance / MathF.Max(delta, .0001f) > .35f;

        if (!_travelling)
        {
            return;
        }

        Vector3 direction = Vector3.Normalize(move);
        float factor = 1f - MathF.Exp(-delta / MathF.Max(.05f, HeadingSmoothing));
        Vector3 blended = Vector3.Lerp(_travel, direction, factor);
        _travel = blended.LengthSquared() > .000001f ? Vector3.Normalize(blended) : direction;

        if (Vector3.Distance(leaderPosition, _lastTrailSample) >= MathF.Max(.05f, TrailSpacing))
        {
            _trail.Add(leaderPosition);
            _lastTrailSample = leaderPosition;

            while (_trail.Count > Math.Max(2, MaxTrailPoints))
            {
                _trail.RemoveAt(0);
            }
        }
    }

    private void UpdateDestination(GameObject leader)
    {
        _destinationTimer -= Time.DeltaTime;
        if (_hasDestination && _destinationTimer > 0.0)
        {
            return;
        }

        _destinationTimer = Math.Max(.05f, DestinationInterval);

        (float side, float behind) = SlotOffsets();
        Vector3 forward = _travel;
        Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
        Vector3 leaderPosition = leader.Transform.WorldPosition;
        Vector3 preferred = leaderPosition - forward * behind + right * side;

        /*
         * A wall between the leader and the loose slot would make the creature
         * grind against geometry, so fall back to the corridor the trainer
         * actually walked (breadcrumb trail).
         */
        if (IsDirectRouteBlocked(leaderPosition, preferred))
        {
            preferred = TrailDestination(behind);
        }

        _destination = preferred;
        _hasDestination = true;
    }

    private (float Side, float Behind) SlotOffsets()
    {
        // Fire Lizard left, Spriglet right, Zappet further back (handoff 02).
        (float side, float behind) = Stats?.Definition.Id switch
        {
            WildlingSpeciesDatabase.SprigletId => (1.8f, .7f),
            WildlingSpeciesDatabase.ZappetId => (0f, 1.8f),
            WildlingSpeciesDatabase.FireLizardId => (-1.8f, .7f),
            _ => (1.2f, 1.2f)
        };

        return (SideOffset != 0f ? SideOffset : side, BehindOffset != 0f ? BehindOffset : behind);
    }

    private bool IsDirectRouteBlocked(Vector3 from, Vector3 to)
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

        Vector3 origin = from + Vector3.UnitY * .8f;

        return GameplayQuery3D.SphereCast(
            scene,
            origin,
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
            return _destination;
        }

        // Walk back along the recorded path until the target distance is reached.
        float travelled = 0f;
        Vector3 previous = _trail[^1];

        for (int index = _trail.Count - 1; index >= 0; index--)
        {
            Vector3 point = _trail[index];
            travelled += Vector3.Distance(previous, point);

            if (travelled >= behind)
            {
                return point;
            }

            previous = point;
        }

        return _trail[0];
    }

    private void UpdateMovement(GameObject self, GameObject leader, float delta)
    {
        Vector3 position = self.Transform.WorldPosition;
        Vector3 toDestination = _destination - position;
        toDestination.Y = 0f;
        float slotDistance = toDestination.Length();

        // Hysteresis: start when clearly displaced, settle once close enough.
        if (!_moving && slotDistance > MathF.Max(SettleDistance + .05f, StartDistance))
        {
            _moving = true;
            _blockedSeconds = 0f;
            _stuckSeconds = 0f;
            _blockedProbe = position;
        }
        else if (_moving && slotDistance <= MathF.Max(.05f, SettleDistance))
        {
            _moving = false;
        }

        if (!_moving)
        {
            RestoreSpeed();
            State = "Idle";
            FaceSlowly(self, leader.Transform.WorldPosition, delta);
            return;
        }

        // Blocked handling: re-route after 0.6 s, settle after 2 s.
        if (Vector3.Distance(_blockedProbe, position) > .05f)
        {
            _blockedProbe = position;
            _stuckSeconds = 0f;
            _blockedSeconds = 0f;
        }
        else
        {
            _stuckSeconds += delta;
        }

        if (_stuckSeconds > .6f && _blockedSeconds == 0f)
        {
            _blockedSeconds = _stuckSeconds;
            (float side, float behind) = SlotOffsets();
            Vector3 route = TrailDestination(behind);
            _destination = Vector3.Distance(route, position) > .05f
                ? route
                : leader.Transform.WorldPosition;
        }

        if (_stuckSeconds > 2f)
        {
            _moving = false;
            RestoreSpeed();
            State = "Idle";
            return;
        }

        if (slotDistance < .05f)
        {
            return;
        }

        Vector3 direction = toDestination / slotDistance;
        float leaderDistance = Vector3.Distance(leader.Transform.WorldPosition, position);
        bool catchUp = leaderDistance > CatchUpDistance;

        State = _blockedSeconds > 0f ? "PathFollow" : catchUp ? "CatchUp" : "Follow";

        Move(self, direction, catchUp ? CatchUpSpeed : FollowSpeed);
        FaceDirection(self, direction);
    }

    private void Move(GameObject self, Vector3 direction, float speed)
    {
        if (Motor is { } motor)
        {
            motor.MoveSpeed = MathF.Max(.1f, speed);
            motor.Move(direction);
            return;
        }

        self.Transform.WorldPosition += direction * speed * (float)Time.DeltaTime;
    }

    private void RestoreSpeed()
    {
        if (Stats is not { } stats || Motor is not { } motor)
        {
            return;
        }

        if (MathF.Abs(motor.MoveSpeed - stats.MoveSpeed) > .01f)
        {
            motor.MoveSpeed = stats.MoveSpeed;
        }
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
