using System.Numerics;
using System.Text;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Minimal combat HUD from GDD chapter 24: three party lines with HP, the
/// active creature's cooldowns and a control reminder. Creates its own UiCanvas
/// when the scene has none, so it works in a greybox scene.
/// </summary>
public sealed class WildlingHud3D : Component
{
    private static readonly AssetReference HudFont = new(
        new Guid("24e72d33-ada4-4155-bc1c-fa9275ea176b"),
        "Assets/Fonts/ByteEngine/PublicPixel.ttf");

    /// <summary>Scene object name that owns TeamManager3D.</summary>
    public string TeamObjectName { get; set; } = "Wildling Manager";

    public int FontSize { get; set; } = 16;

    public bool ShowControls { get; set; } = true;

    private UiText? _partyText;
    private UiText? _actionText;
    private UiText? _hintText;
    private double _refreshRemaining;

    protected override void OnStart() => EnsureUi();

    protected override void OnUpdate()
    {
        _refreshRemaining -= Time.DeltaTime;
        if (_refreshRemaining > 0.0)
        {
            return;
        }

        _refreshRemaining = .1;
        EnsureUi();
        RefreshParty();
        RefreshActions();
    }

    private void EnsureUi()
    {
        GameObject? self = GameObject;
        Scene? scene = self?.Scene;
        if (scene == null || _partyText != null)
        {
            return;
        }

        GameObject? canvasObject = null;
        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.GetComponent<UiCanvas>() != null)
            {
                canvasObject = candidate;
                break;
            }
        }

        if (canvasObject == null)
        {
            canvasObject = scene.CreateGameObject("Wildling HUD");
            canvasObject.AddComponent(new UiCanvas());
        }

        _partyText = CreateLine(scene, canvasObject, "Party HUD", new Vector2(24f, 18f));
        _actionText = CreateLine(scene, canvasObject, "Action HUD", new Vector2(24f, 96f));
        _hintText = CreateLine(scene, canvasObject, "Hint HUD", new Vector2(24f, 148f));

        if (_hintText != null)
        {
            _hintText.FontSize = Math.Max(10, FontSize - 4);
            _hintText.Color = new Vector4(.78f, .83f, .74f, .95f);
            _hintText.Text = ShowControls
                ? "WASD move   Mouse look   Space jump   T command the wildlings"
                : string.Empty;
            _hintText.Visible = ShowControls;
        }
    }

    private UiText CreateLine(Scene scene, GameObject parent, string name, Vector2 offset)
    {
        GameObject line = scene.CreateGameObject(name);
        line.SetParent(parent, false);

        return line.AddComponent(new UiText
        {
            Text = name,
            FontReference = HudFont,
            FontSize = FontSize,
            Anchor = UiAnchor.TopLeft,
            Offset = offset,
            Color = new Vector4(.95f, .96f, .9f, 1f),
            ShadowColor = new Vector4(0f, 0f, 0f, .85f),
            ShadowOffset = new Vector2(2f, 2f),
            Visible = true
        });
    }

    private void RefreshParty()
    {
        if (_partyText == null)
        {
            return;
        }

        TeamManager3D? team = string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>();

        if (team == null)
        {
            _partyText.Text = "No TeamManager3D linked";
            return;
        }

        StringBuilder builder = new();

        for (int slot = 1; slot <= 3; slot++)
        {
            GameObject? member = team.Resolve(slot);
            if (member == null)
            {
                builder.AppendLine($"{slot}  <empty slot>");
                continue;
            }

            WildlingStats3D? stats = member.GetComponent<WildlingStats3D>();
            HealthComponent? health = member.GetComponent<HealthComponent>();
            string name = stats?.Definition.DisplayName ?? member.Name;
            string marker = slot == team.ActiveSlot ? ">" : " ";
            string value = health == null
                ? "--"
                : health.IsDead
                    ? "DOWN"
                    : $"{MathF.Ceiling(health.CurrentHealth):0}/{MathF.Ceiling(health.MaxHealth):0}";

            builder.AppendLine($"{marker} {slot} {name,-12} {value,9}");
        }

        _partyText.Text = builder.ToString().TrimEnd();
    }

    private void RefreshActions()
    {
        if (_actionText == null)
        {
            return;
        }

        TeamManager3D? team = string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>();

        GameObject? active = team?.ActiveMember;
        WildlingCombat3D? combat = active?.GetComponent<WildlingCombat3D>();
        WildlingStats3D? stats = active?.GetComponent<WildlingStats3D>();

        if (team is { InCombat: false })
        {
            _actionText.Text = "EXPLORE   wildlings follow the trainer   T: take command";
            return;
        }

        if (combat == null || stats == null)
        {
            _actionText.Text = string.Empty;
            return;
        }

        (string basic, string special, string utility) = Names(stats.Definition.Element);

        _actionText.Text =
            $"COMBAT   {basic} {combat.BasicCooldownRemaining:0.0}s   " +
            $"{special} {combat.SpecialCooldownRemaining:0.0}s   " +
            $"{utility} {combat.UtilityCooldownRemaining:0.0}s   1/2/3 switch";
    }

    private static (string Basic, string Special, string Utility) Names(WildlingElement element) => element switch
    {
        WildlingElement.Fire => ("Firebolt", "Fireball", "Flame Dash"),
        WildlingElement.Grass => ("Thorn Shot", "Root Burst", "Nature's Mend"),
        WildlingElement.Electric => ("Spark Bolt", "Arc Jolt", "Static Hop"),
        _ => ("Strike", "Burst", "Shift")
    };
}
