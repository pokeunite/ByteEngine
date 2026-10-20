using System.Text.Json.Serialization;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Audio;

/// <summary>Scene-wide button entry feedback. Works while simulation is paused and shares the UI audio bus.</summary>
public sealed class UiInteractionAudio : Component
{
    public string HoverSourceName { get; set; } = "";
    public float MinimumInterval { get; set; } = .075f;
    [JsonIgnore] public int HoverSequence { get; private set; }
    UiWidget? previous;
    bool pending;
    double lastSound = double.NegativeInfinity;
    protected override void OnUpdate()
    {
        var scene = GameObject.Scene!;
        UiNavigation.Update(scene);
        var candidate = scene.GameObjects.SelectMany(o => o.Components).OfType<UiWidget>()
            .Where(w => w.Enabled && w.GameObject.ActiveInHierarchy && w.Visible && w.Interactable &&
                w.Kind == UiWidgetKind.Button && UiLayout.IsVisible(w.GameObject))
            .Where(w => UiNavigation.UsingKeyboardNavigation ? w.IsFocused : w.IsHovered)
            .OrderBy(w => w.OrderInLayer).LastOrDefault();
        if (candidate != previous) { previous = candidate; pending = candidate != null; }
        if (!pending || candidate == null || Time.TotalTime - lastSound < Math.Max(0, MinimumInterval)) return;
        var source = scene.FindGameObject(HoverSourceName)?.GetComponent<AudioSource3D>();
        if (source is not { Enabled: true } || !source.GameObject.ActiveInHierarchy) return;
        source.Play();
        pending = false;
        lastSound = Time.TotalTime;
        HoverSequence++;
    }
    protected override void OnStop() { previous = null; pending = false; lastSound = double.NegativeInfinity; }
}
