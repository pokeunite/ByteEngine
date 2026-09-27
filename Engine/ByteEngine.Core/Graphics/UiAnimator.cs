using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

public enum UiAnimationPreset
{
    FadeIn, FadeOut, SlideInLeft, SlideInRight, SlideInUp, SlideInDown, PopIn, Pulse
}

/// <summary>
/// Non-destructive animation overlay for a Text or Widget on this GameObject.
/// Authored offsets, sizes and colors are never rewritten by playback.
/// </summary>
public sealed class UiAnimator : Component
{
    private float _elapsed;
    public UiAnimationPreset Preset { get; set; } = UiAnimationPreset.FadeIn;
    public float Duration { get; set; } = .3f;
    public float Delay { get; set; }
    public float Distance { get; set; } = 80f;
    public bool AutoPlay { get; set; }
    public bool Loop { get; set; }
    public bool HideOnComplete { get; set; }
    public bool IsPlaying { get; private set; }
    public float Opacity { get; private set; } = 1f;
    public float Scale { get; private set; } = 1f;
    public Vector2 Offset { get; private set; }

    protected override void OnStart()
    {
        if (AutoPlay) Play();
    }

    protected override void OnUpdate() => Advance((float)Time.DeltaTime);

    public void Play()
    {
        _elapsed = 0f;
        IsPlaying = true;
        if (GameObject.GetComponent<UiText>() is { } text) text.Visible = true;
        if (GameObject.GetComponent<UiWidget>() is { } widget) widget.Visible = true;
        Sample(0f);
    }

    public void Stop()
    {
        IsPlaying = false;
        Opacity = 1f;
        Scale = 1f;
        Offset = Vector2.Zero;
    }

    public void Advance(float seconds)
    {
        if (!IsPlaying || !float.IsFinite(seconds) || seconds <= 0f) return;
        float duration = Math.Max(.001f, float.IsFinite(Duration) ? Duration : .3f);
        float delay = Math.Max(0f, float.IsFinite(Delay) ? Delay : 0f);
        _elapsed += seconds;
        if (_elapsed < delay)
        {
            Sample(0f);
            return;
        }
        float progress = Math.Clamp((_elapsed - delay) / duration, 0f, 1f);
        Sample(progress);
        if (progress < 1f) return;
        if (Loop)
        {
            _elapsed = delay;
            Sample(0f);
            return;
        }
        IsPlaying = false;
        if (HideOnComplete && Preset == UiAnimationPreset.FadeOut)
        {
            if (GameObject.GetComponent<UiText>() is { } text) text.Visible = false;
            if (GameObject.GetComponent<UiWidget>() is { } widget) widget.Visible = false;
        }
    }

    private void Sample(float progress)
    {
        float eased = progress * progress * (3f - 2f * progress);
        float distance = float.IsFinite(Distance) ? Math.Clamp(Distance, 0f, 10000f) : 80f;
        Opacity = Preset switch
        {
            UiAnimationPreset.FadeIn or UiAnimationPreset.PopIn => eased,
            UiAnimationPreset.FadeOut => 1f - eased,
            _ => 1f
        };
        Offset = Preset switch
        {
            UiAnimationPreset.SlideInLeft => new Vector2(-distance * (1f - eased), 0f),
            UiAnimationPreset.SlideInRight => new Vector2(distance * (1f - eased), 0f),
            UiAnimationPreset.SlideInUp => new Vector2(0f, -distance * (1f - eased)),
            UiAnimationPreset.SlideInDown => new Vector2(0f, distance * (1f - eased)),
            _ => Vector2.Zero
        };
        Scale = Preset switch
        {
            UiAnimationPreset.PopIn => .8f + .2f * eased,
            UiAnimationPreset.Pulse => 1f + .08f * MathF.Sin(progress * MathF.PI),
            _ => 1f
        };
    }
}
