using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Bond / capture window (GDD v0.3: "collecting with agency").
///
/// When a hostile drops to <see cref="BondHpFraction"/> health and the player is within
/// <see cref="InteractRange"/>, a window opens for <see cref="WindowSeconds"/>. Pressing
/// <see cref="BondKey"/> inside it captures the creature. Walking away or letting the
/// window expire closes it. Every value is a property; the prompt is an authored UiText.
/// </summary>
public sealed class WildlingBond3D : Component
{
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    [SceneObjectPicker]
    public string PlayerName { get; set; } = "Fire Lizard";

    /// <summary>Authored UiText shown while a bond is possible.</summary>
    [SceneObjectPicker]
    public string PromptObjectName { get; set; } = "Bond Prompt";

    public Key BondKey { get; set; } = Key.E;

    /// <summary>Health fraction at or below which a creature can be bonded.</summary>
    public float BondHpFraction { get; set; } = .25f;

    public float WindowSeconds { get; set; } = 8f;

    public float InteractRange { get; set; } = 6f;

    public int EssencePerCapture { get; set; } = 1;

    public int HostileLayer { get; set; } = 3;

    public string PromptText { get; set; } = "PRESS E TO BOND";

    public bool WindowOpen { get; private set; }

    public int Captures { get; private set; }

    public GameObject? Candidate { get; private set; }

    private float _remaining;

    protected override void OnUpdate()
    {
        GameObject? player = Player();
        Scene? scene = GameObject?.Scene;

        if (player == null || scene == null)
        {
            Close();
            return;
        }

        if (WindowOpen)
        {
            _remaining -= (float)Time.DeltaTime;

            bool valid = Candidate != null &&
                         Candidate.ActiveInHierarchy &&
                         Candidate.GetComponent<HealthComponent>() is { IsDead: false } &&
                         Vector3.Distance(player.Transform.WorldPosition, Candidate.Transform.WorldPosition) <= InteractRange;

            if (_remaining <= 0f || !valid)
            {
                Close();
                return;
            }

            if (Input.IsKeyDown(BondKey))
            {
                Capture(player, scene);
            }

            return;
        }

        Candidate = FindBondable(player, scene);

        if (Candidate == null)
        {
            return;
        }

        WindowOpen = true;
        _remaining = MathF.Max(.5f, WindowSeconds);
        ShowPrompt(true);
    }


    private GameObject? FindBondable(GameObject player, Scene scene)
    {
        GameObject? best = null;
        float bestDistance = MathF.Max(.5f, InteractRange);

        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.Layer != HostileLayer || !candidate.ActiveInHierarchy)
            {
                continue;
            }

            if (candidate.GetComponent<HealthComponent>() is not { IsDead: false } health)
            {
                continue;
            }

            if (health.HealthPercent > Math.Clamp(BondHpFraction, .01f, 1f))
            {
                continue;
            }

            float distance = Vector3.Distance(player.Transform.WorldPosition, candidate.Transform.WorldPosition);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = candidate;
        }

        return best;
    }

    private void Capture(GameObject player, Scene scene)
    {
        GameObject? target = Candidate;

        WindowOpen = false;
        Candidate = null;
        ShowPrompt(false);

        if (target == null)
        {
            return;
        }

        // Species-true capture: the spawned creature carries its own identity, so essence
        // goes to that species rather than the player's.
        WildlingEnemy3D? identity = target.GetComponent<WildlingEnemy3D>();
        string speciesId = identity?.SpeciesId
                           ?? player.GetComponent<WildlingStats3D>()?.Definition.Id
                           ?? WildlingSpeciesDatabase.FireLizardId;
        int essence = identity?.EssencePerCapture ?? Math.Max(0, EssencePerCapture);

        if (Manager()?.GetComponent<WildlingProfile3D>() is { } profile)
        {
            profile.AddEssence(speciesId, Math.Max(0, essence));
            profile.Own(speciesId);
            profile.Save();
        }

        Captures++;
        scene.DestroyGameObject(target);
    }

    private void Close()
    {
        WindowOpen = false;
        Candidate = null;
        ShowPrompt(false);
    }

    private void ShowPrompt(bool visible)
    {
        if (string.IsNullOrWhiteSpace(PromptObjectName))
        {
            return;
        }

        if (GameObject?.Scene?.FindGameObject(PromptObjectName)?.GetComponent<UiText>() is { } text)
        {
            text.Text = PromptText;
            text.Visible = visible;
        }
    }

    private GameObject? Player()
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(PlayerName) && scene.FindGameObject(PlayerName) is { } named)
        {
            return named;
        }

        return Manager()?.GetComponent<TeamManager3D>()?.ActiveMember;
    }

    private GameObject? Manager() =>
        string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName);
}
