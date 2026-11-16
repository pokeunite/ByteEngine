using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// One authored level-up card. The whole upgrade is data on this component — title,
/// description and effect numbers — plus the names of the text objects that display it,
/// so nothing about the draft is hard-coded in code.
/// </summary>
public sealed class WildlingUpgradeCard3D : Component
{
    /// <summary>1-based slot; the draft lists cards by this order.</summary>
    public int SlotNumber { get; set; } = 1;

    /// <summary>Key that picks this card during the draft.</summary>
    public Key ChoiceKey { get; set; } = Key.D1;

    public string Title { get; set; } = "Upgrade";

    public string Description { get; set; } = string.Empty;

    /// <summary>Damage bonus, fraction of base (0.15 = +15%).</summary>
    public float DamagePercent { get; set; }

    /// <summary>Fire-interval reduction (0.06 = 6% faster).</summary>
    public float CooldownPercent { get; set; }

    /// <summary>Flat max-health bonus.</summary>
    public float MaxHealthBonus { get; set; }

    /// <summary>Heal as a fraction of max health (0.25 = 25%).</summary>
    public float HealPercent { get; set; }

    [SceneObjectPicker]
    public string TitleObjectName { get; set; } = string.Empty;

    [SceneObjectPicker]
    public string BodyObjectName { get; set; } = string.Empty;

    [SceneObjectPicker]
    public string BackgroundObjectName { get; set; } = string.Empty;

    private bool _visible;

    /// <summary>Fills the authored text and makes the card visible.</summary>
    public void Show()
    {
        SetText(TitleObjectName, Title, true);
        SetText(BodyObjectName, Description, true);
        SetBackground(true);
        _visible = true;
    }

    public void Hide()
    {
        SetText(TitleObjectName, string.Empty, false);
        SetText(BodyObjectName, string.Empty, false);
        SetBackground(false);
        _visible = false;
    }

    public bool IsVisible => _visible;

    /// <summary>Applies this card's effects to the player creature.</summary>
    public void Apply(GameObject? player)
    {
        if (player == null || !_visible)
        {
            return;
        }

        if (player.GetComponent<WildlingCombat3D>() is { } combat)
        {
            combat.DamageMultiplier += MathF.Max(0f, DamagePercent);
            combat.CooldownMultiplier = Math.Clamp(
                combat.CooldownMultiplier * (1f - Math.Clamp(CooldownPercent, 0f, .9f)),
                .1f,
                3f);
        }

        if (player.GetComponent<HealthComponent>() is { } health)
        {
            if (MaxHealthBonus > 0f)
            {
                health.MaxHealth += MaxHealthBonus;
            }

            if (HealPercent > 0f)
            {
                health.Heal(health.MaxHealth * Math.Clamp(HealPercent, 0f, 1f));
            }
        }
    }

    private void SetText(string objectName, string value, bool visible)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        if (GameObject?.Scene?.FindGameObject(objectName)?.GetComponent<UiText>() is { } text)
        {
            text.Text = value;
            text.Visible = visible;
        }
    }

    private void SetBackground(bool visible)
    {
        if (string.IsNullOrWhiteSpace(BackgroundObjectName))
        {
            return;
        }

        if (GameObject?.Scene?.FindGameObject(BackgroundObjectName)?.GetComponent<UiWidget>() is { } widget)
        {
            widget.Visible = visible;
        }
    }
}
