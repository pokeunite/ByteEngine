namespace ByteEngine.Core.Animation;

/// <summary>Compact optional graph state machine. Clip playback remains the default path.</summary>
public sealed class AnimationStateRuntime
{
    private readonly AnimationStateGraph _definition;
    private readonly Dictionary<string, Value> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AnimationGraphState> _states = new(StringComparer.OrdinalIgnoreCase);
    private string _current = string.Empty;
    private string _previous = string.Empty;
    private float _transitionTime;
    private float _transitionDuration;

    private struct Value
    {
        public AnimationParameterKind Kind;
        public float Float;
        public bool Bool;
    }

    public string CurrentState => _current;
    public string PreviousState => _previous;
    public float TransitionDuration => _transitionDuration;
    public bool TryGetFloat(string name, out float value)
    {
        value = 0f;
        if (!_values.TryGetValue(name, out Value data) || data.Kind != AnimationParameterKind.Float)
            return false;
        value = data.Float;
        return true;
    }
    public float BlendAlpha => _transitionDuration <= 1e-6f ? 1f :
        Math.Clamp(_transitionTime / _transitionDuration, 0f, 1f);
    public AnimationGraphState? Current => _states.GetValueOrDefault(_current);

    public AnimationStateRuntime(AnimationStateGraph definition)
    {
        _definition = definition;
        foreach (AnimationGraphParameter parameter in definition.Parameters)
            if (!string.IsNullOrWhiteSpace(parameter.Name))
                _values[parameter.Name] = new Value { Kind = parameter.Kind,
                    Float = parameter.FloatDefault, Bool = parameter.BoolDefault };
        foreach (AnimationGraphState state in definition.States)
            if (!string.IsNullOrWhiteSpace(state.Name))
                _states[state.Name] = state;
        _current = _states.ContainsKey(definition.EntryState) ? definition.EntryState :
            definition.States.FirstOrDefault()?.Name ?? string.Empty;
    }

    public bool SetFloat(string name, float value)
    {
        if (!float.IsFinite(value) || !_values.TryGetValue(name, out Value data) ||
            data.Kind != AnimationParameterKind.Float) return false;
        data.Float = value;
        _values[name] = data;
        return true;
    }

    public bool SetBool(string name, bool value)
    {
        if (!_values.TryGetValue(name, out Value data) ||
            data.Kind != AnimationParameterKind.Bool) return false;
        data.Bool = value;
        _values[name] = data;
        return true;
    }

    public bool Trigger(string name)
    {
        if (!_values.TryGetValue(name, out Value data) ||
            data.Kind != AnimationParameterKind.Trigger) return false;
        data.Bool = true;
        _values[name] = data;
        return true;
    }

    public bool Step(float deltaTime, float normalizedPhase)
    {
        _transitionTime += Math.Max(float.IsFinite(deltaTime) ? deltaTime : 0f, 0f);
        if (_transitionTime >= _transitionDuration) _previous = string.Empty;
        foreach (AnimationGraphTransition transition in _definition.Transitions)
        {
            if (!string.Equals(transition.From, _current, StringComparison.OrdinalIgnoreCase) ||
                !_states.ContainsKey(transition.To) ||
                (transition.ExitTime >= 0f && normalizedPhase < transition.ExitTime) ||
                !Matches(transition.Conditions)) continue;
            _previous = _current;
            _current = transition.To;
            _transitionTime = 0f;
            _transitionDuration = transition.Duration;
            foreach (AnimationGraphCondition condition in transition.Conditions)
                if (_values.TryGetValue(condition.Parameter, out Value value) &&
                    value.Kind == AnimationParameterKind.Trigger)
                {
                    value.Bool = false;
                    _values[condition.Parameter] = value;
                }
            return true;
        }
        return false;
    }

    private bool Matches(IReadOnlyList<AnimationGraphCondition> conditions)
    {
        foreach (AnimationGraphCondition condition in conditions)
        {
            if (!_values.TryGetValue(condition.Parameter, out Value value)) return false;
            if (value.Kind == AnimationParameterKind.Float)
            {
                bool match = condition.Comparison switch
                {
                    AnimationComparison.Greater => value.Float > condition.FloatValue,
                    AnimationComparison.GreaterOrEqual => value.Float >= condition.FloatValue,
                    AnimationComparison.Less => value.Float < condition.FloatValue,
                    AnimationComparison.LessOrEqual => value.Float <= condition.FloatValue,
                    AnimationComparison.Equal => MathF.Abs(value.Float - condition.FloatValue) <= 1e-5f,
                    AnimationComparison.NotEqual => MathF.Abs(value.Float - condition.FloatValue) > 1e-5f,
                    _ => false
                };
                if (!match) return false;
            }
            else if (value.Kind == AnimationParameterKind.Trigger)
            {
                if (!value.Bool) return false;
            }
            else if (value.Bool != condition.BoolValue) return false;
        }
        return true;
    }
}
