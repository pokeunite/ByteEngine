using ByteEngine.Core.Gameplay;

namespace ByteEngine.Core.Animation;

public sealed partial class AnimationController
{
    private bool _turnActive;
    private float _turnElapsed, _turnDegrees, _turnClipDuration;
    private string _turnClip = "", _turnStance = "";
    private int _turnSign;
    private float _idleTurnAngle, _turnQuietTime;
    public float PendingTurnAngle => _idleTurnAngle;
    public bool TurnInPlaceEnabled { get; set; } = true;
    public string CurrentTurn => _turnActive ? _turnClip : "";

    private bool UpdateTurnInPlace()
    {
        var settings = _loadedPoseProfile?.Locomotion.Turning;
        var control = GameObject.GetComponent<TopDownController3D>();
        bool eligible = TurnInPlaceEnabled && settings?.Enabled == true && control is { Enabled: true, HasCursorAim: true, IsFacingCursor: true } &&
            State == LocomotionState.Idle && LocomotionSpeed <= settings.MaximumMoveSpeed && (!_actionActive || _maskedActionActive);
        float angle = control?.SignedAimAngleDegrees ?? 0;
        float yawSpeed = control?.YawSpeedDegrees ?? 0;
        float delta = Math.Max((float)Time.DeltaTime, 0);
        if (!_turnActive)
            _idleTurnAngle = eligible ? Math.Clamp(_idleTurnAngle + yawSpeed * delta, -360, 360) : 0;
        _turnQuietTime = Math.Abs(yawSpeed) < 3 ? _turnQuietTime + delta : 0;
        float requestedAngle = settings != null && Math.Abs(angle) >= settings.MinimumAngle ? angle : _idleTurnAngle;
        if (_turnActive && (!eligible || _turnElapsed > 8 || _renderers.All(r => !r.IsPlaying) ||
            !string.Equals(_turnStance, _selectedBlendSpace?.Name ?? "", StringComparison.OrdinalIgnoreCase) ||
            (Math.Abs(yawSpeed) > 3 && Math.Sign(yawSpeed) != _turnSign) ||
            _turnQuietTime > .14f))
        {
            _turnActive = false; _idleTurnAngle = 0; _turnQuietTime = 0;
            foreach (var renderer in _renderers) renderer.SetTurnYawRemoval(false);
            UpdateAdvancedPoseControls();
            if (!_actionActive || _maskedActionActive) ApplyObservedLocomotion(settings?.BlendOut ?? TransitionDuration, true);
            return false;
        }
        if (!_turnActive && eligible && Math.Abs(requestedAngle) >= settings!.MinimumAngle)
        {
            string stance = _selectedBlendSpace?.Name ?? "";
            var set = settings.Sets.FirstOrDefault(s => s.BlendSpace.Equals(stance, StringComparison.OrdinalIgnoreCase)) ??
                settings.Sets.FirstOrDefault(s => string.IsNullOrEmpty(s.BlendSpace));
            if (set == null) return false;
            bool around = Math.Abs(requestedAngle) >= settings.AroundAngle && !string.IsNullOrEmpty(set.Around180);
            string clip = around ? set.Around180 : requestedAngle < 0 ? set.Left90 : set.Right90;
            if (string.IsNullOrEmpty(clip)) return false;
            _turnClipDuration = _renderers.Select(r => r.ResolvedModel?.Animations.FirstOrDefault(a => a.Name == clip)?.Duration ?? 0).DefaultIfEmpty(0).Max();
            if (_turnClipDuration <= 0) return false;
            _turnDegrees = around ? 180 : 90;
            _turnActive = true; _turnElapsed = 0; _turnClip = clip; _turnStance = stance; _turnSign = Math.Sign(requestedAngle); _turnQuietTime = 0; _idleTurnAngle = 0;
            foreach (var renderer in _renderers)
            {
                renderer.SetBlendSpace(null, 0, 0, settings.BlendIn);
                renderer.SetTurnYawRemoval(true);
                renderer.Play(clip, false, settings.BlendIn);
            }
        }
        if (!_turnActive) return false;
        _turnElapsed += Math.Max((float)Time.DeltaTime, 0);
        _currentLocomotionClip = _turnClip;
        float rate = Math.Clamp(Math.Abs(yawSpeed) * _turnClipDuration / _turnDegrees, settings!.MinimumPlaybackSpeed, settings.MaximumPlaybackSpeed);
        foreach (var renderer in _renderers) renderer.Speed = rate;
        return true;
    }
}
