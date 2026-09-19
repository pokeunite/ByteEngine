using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Editor.Panels;

/// <summary>
/// Editor-owned working copy for one model animation timeline. No disk or GPU
/// work occurs here, which keeps normalization and dirty behavior headless-testable.
/// </summary>
internal sealed class AnimationTimelineDocument
{
    public Guid ModelGuid { get; }
    public string AnimationKey { get; }
    public string AnimationName { get; private set; } = string.Empty;
    public float Duration { get; private set; }
    public List<AnimationEventMarker> Events { get; } = new();
    public List<AnimationWindow> Windows { get; } = new();
    public bool IsDirty { get; private set; }

    public AnimationTimelineDocument(
        Guid modelGuid,
        ImportedAnimation animation)
    {
        ModelGuid = modelGuid;
        AnimationKey = animation.Key;
        Reload(animation);
    }

    public void Reload(ImportedAnimation animation)
    {
        if (!string.Equals(animation.Key, AnimationKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Cannot reload a timeline document from a different animation key.");

        AnimationName = animation.Name;
        Duration = Math.Max(animation.Duration, 0.0f);
        Events.Clear();
        Events.AddRange(animation.Events.Select(CloneAndClamp));
        Windows.Clear();
        Windows.AddRange(animation.Windows.Select(CloneAndNormalize));
        Sort();
        IsDirty = false;
    }

    public AnimationEventMarker AddEvent(float time)
    {
        var marker = new AnimationEventMarker
        {
            Id = Guid.NewGuid(),
            Name = "New Event",
            Time = ClampTime(time)
        };
        Events.Add(marker);
        Sort();
        IsDirty = true;
        return marker;
    }

    public AnimationWindow AddWindow(float time)
    {
        float start = ClampTime(time);
        float defaultLength = Math.Min(0.15f, Math.Max(Duration * 0.1f, 0.01f));
        float end = ClampTime(start + defaultLength);
        if (end <= start && start > 0.0f)
        {
            start = ClampTime(start - defaultLength);
            end = ClampTime(time);
        }

        var window = new AnimationWindow
        {
            Id = Guid.NewGuid(),
            Name = "New Window",
            StartTime = Math.Min(start, end),
            EndTime = Math.Max(start, end)
        };
        Windows.Add(window);
        Sort();
        IsDirty = true;
        return window;
    }

    public bool DeleteEvent(Guid id)
    {
        int removed = Events.RemoveAll(item => item.Id == id);
        IsDirty |= removed > 0;
        return removed > 0;
    }

    public bool DeleteWindow(Guid id)
    {
        int removed = Windows.RemoveAll(item => item.Id == id);
        IsDirty |= removed > 0;
        return removed > 0;
    }

    public void MarkDirty()
    {
        NormalizeAll();
        Sort();
        IsDirty = true;
    }

    public void ApplyTo(ImportedAnimation animation)
    {
        if (!string.Equals(animation.Key, AnimationKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Cannot apply timeline metadata to a different animation key.");

        NormalizeAll();
        Sort();
        animation.Events.Clear();
        animation.Events.AddRange(Events.Select(item => item.Clone()));
        animation.Windows.Clear();
        animation.Windows.AddRange(Windows.Select(item => item.Clone()));
    }

    public void MarkSaved() => IsDirty = false;

    public float ClampTime(float time) =>
        float.IsFinite(time)
            ? Math.Clamp(time, 0.0f, Duration)
            : 0.0f;

    public static ImportedAnimation? ResolveAnimation(
        ModelAsset model,
        string animationKey) =>
        model.Animations.FirstOrDefault(
            animation => string.Equals(animation.Key, animationKey, StringComparison.Ordinal));

    private AnimationEventMarker CloneAndClamp(AnimationEventMarker source)
    {
        AnimationEventMarker clone = source.Clone();
        clone.Time = ClampTime(clone.Time);
        clone.Name = string.IsNullOrWhiteSpace(clone.Name) ? "Event" : clone.Name;
        clone.Payload ??= string.Empty;
        return clone;
    }

    private AnimationWindow CloneAndNormalize(AnimationWindow source)
    {
        AnimationWindow clone = source.Clone();
        Normalize(clone);
        return clone;
    }

    private void NormalizeAll()
    {
        foreach (AnimationEventMarker marker in Events)
        {
            marker.Time = ClampTime(marker.Time);
            marker.Name = string.IsNullOrWhiteSpace(marker.Name) ? "Event" : marker.Name.Trim();
            marker.Payload ??= string.Empty;
        }

        foreach (AnimationWindow window in Windows)
            Normalize(window);
    }

    private void Normalize(AnimationWindow window)
    {
        float first = ClampTime(window.StartTime);
        float second = ClampTime(window.EndTime);
        window.StartTime = Math.Min(first, second);
        window.EndTime = Math.Max(first, second);
        window.Name = string.IsNullOrWhiteSpace(window.Name) ? "Window" : window.Name.Trim();
        window.Payload ??= string.Empty;
    }

    private void Sort()
    {
        Events.Sort((left, right) => left.Time.CompareTo(right.Time));
        Windows.Sort((left, right) =>
        {
            int start = left.StartTime.CompareTo(right.StartTime);
            return start != 0 ? start : left.EndTime.CompareTo(right.EndTime);
        });
    }
}
