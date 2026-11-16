using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Survivors HUD (GDD v0.3).
///
/// The component creates no UI: it formats run state into text components that are
/// authored in the scene, so position, font, colour and size stay editable in the
/// inspector. Point <see cref="RunTextObjectName"/> at any object carrying a UiText.
/// </summary>
public sealed class WildlingHud3D : Component
{
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Authored scene object (UiText) that shows the run line.</summary>
    [SceneObjectPicker]
    public string RunTextObjectName { get; set; } = "Run HUD";

    /// <summary>Authored scene object (UiText) that shows the player health.</summary>
    [SceneObjectPicker]
    public string HpTextObjectName { get; set; } = "HP HUD";

    public bool ShowRunText { get; set; } = true;

    /// <summary>Show the boss countdown instead of the elapsed clock once it is close.</summary>
    public bool ShowBossCountdown { get; set; } = true;

    /// <summary>Appends the live locomotion/action clip to the run line (play-test diagnostics).</summary>
    public bool ShowAnimationDebug { get; set; }

    /// <summary>Seconds before the boss when the run line switches to a countdown.</summary>
    public float BossWarningSeconds { get; set; } = 45f;

    private UiText? _runText;
    private double _refreshRemaining;

    protected override void OnUpdate()
    {
        _refreshRemaining -= Time.DeltaTime;
        if (_refreshRemaining > 0.0)
        {
            return;
        }

        _refreshRemaining = .1;
        RefreshRun();
    }

    private void RefreshRun()
    {
        if (_runText == null)
        {
            _runText = string.IsNullOrWhiteSpace(RunTextObjectName)
                ? null
                : GameObject?.Scene?.FindGameObject(RunTextObjectName)?.GetComponent<UiText>();

            if (_runText == null)
            {
                return;
            }
        }

        WildlingRun3D? run = Manager()?.GetComponent<WildlingRun3D>();
        if (run == null)
        {
            _runText.Visible = false;
            return;
        }

        _runText.Visible = ShowRunText;

        HealthComponent? health = GameObject?.Scene?
            .FindGameObject(run.PlayerName)?.GetComponent<HealthComponent>();

        string vitals = health == null
            ? string.Empty
            : $"   HP {MathF.Ceiling(health.CurrentHealth):0}/{health.MaxHealth:0}";

        RefreshHealth(health);

        _runText.Color = run.State switch
        {
            WildlingRunState.Failed => new Vector4(1f, .42f, .32f, 1f),
            WildlingRunState.Cleared => new Vector4(.55f, 1f, .55f, 1f),
            _ => new Vector4(.96f, .98f, .9f, 1f)
        };

        _runText.Text = run.State switch
        {
            WildlingRunState.Failed =>
                $"RUN OVER   {run.Clock}   {run.Kills} kills   level {run.Level}",

            WildlingRunState.Cleared =>
                $"GUARDIAN DEFEATED   {run.Clock}   {run.Kills} kills   level {run.Level}{vitals}",

            WildlingRunState.BossDue =>
                $"GUARDIAN IS HERE   {run.Clock}   LVL {run.Level}   KILLS {run.Kills}{vitals}",

            _ when ShowBossCountdown && run.TimeRemaining <= MathF.Max(0f, BossWarningSeconds) =>
                $"BOSS IN {run.TimeRemaining:0}s   LVL {run.Level}   KILLS {run.Kills}{vitals}",

            _ =>
                $"TIME {run.Clock}   LVL {run.Level}   XP {run.Experience:0}/{run.ExperienceToNext:0}   KILLS {run.Kills}{vitals}"
        };

        if (ShowAnimationDebug &&
            GameObject?.Scene?.FindGameObject(run.PlayerName)?.GetComponent<AnimationController>() is { } animation)
        {
            _runText.Text += $"   [{animation.CurrentLocomotionClip} | {animation.CurrentAction}]";
        }
    }

    private GameObject? Manager() =>
        string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName);

    /// <summary>Writes the authored HP text; turns red at low health.</summary>
    private void RefreshHealth(HealthComponent? health)
    {
        if (string.IsNullOrWhiteSpace(HpTextObjectName) ||
            GameObject?.Scene?.FindGameObject(HpTextObjectName)?.GetComponent<UiText>() is not { } hp)
        {
            return;
        }

        hp.Visible = health != null;

        if (health == null)
        {
            return;
        }

        hp.Text = $"HP {MathF.Ceiling(health.CurrentHealth):0}/{health.MaxHealth:0}";

        hp.Color = health.HealthPercent <= .3f
            ? new Vector4(1f, .42f, .32f, 1f)
            : new Vector4(.96f, .98f, .9f, 1f);
    }
}
