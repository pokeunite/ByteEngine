using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Level-up draft with world pause (GDD v0.3: three choices, world paused, pick one).
///
/// Cards are authored scene components (<see cref="WildlingUpgradeCard3D"/>), so the
/// pool, wording and numbers are edited in the inspector, not here. When the run
/// director levels up this pauses the scene time scale, shows the authored cards and
/// waits for their choice keys.
/// </summary>
public sealed class WildlingLevelUpDraft3D : Component
{
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Scene time scale while the draft is open (0 = world frozen).</summary>
    public float PausedTimeScale { get; set; }

    /// <summary>How many authored cards a draft offers.</summary>
    public int ChoiceCount { get; set; } = 3;

    public bool IsOpen { get; private set; }

    public int Drafts { get; private set; }

    private readonly List<WildlingUpgradeCard3D> _shown = new();
    private WildlingRun3D? _run;
    private float _previousTimeScale = 1f;

    protected override void OnStart()
    {
        _run = Manager()?.GetComponent<WildlingRun3D>();

        if (_run != null)
        {
            _run.LeveledUp += OnLeveledUp;
        }
    }

    protected override void OnDestroy()
    {
        if (_run != null)
        {
            _run.LeveledUp -= OnLeveledUp;
        }
    }

    private void OnLeveledUp(int level) => Open();

    /// <summary>Pauses the world and shows the authored cards.</summary>
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return;
        }

        _shown.Clear();

        List<WildlingUpgradeCard3D> cards = new();
        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.GetComponent<WildlingUpgradeCard3D>() is { } card)
            {
                cards.Add(card);
            }
        }

        if (cards.Count == 0)
        {
            return;
        }

        cards.Sort((left, right) => left.SlotNumber.CompareTo(right.SlotNumber));

        int count = Math.Clamp(ChoiceCount, 1, cards.Count);
        for (int index = 0; index < count; index++)
        {
            cards[index].Show();
            _shown.Add(cards[index]);
        }

        _previousTimeScale = scene.SimulationTimeScale;
        scene.SimulationTimeScale = MathF.Max(0f, PausedTimeScale);
        IsOpen = true;
        Drafts++;
    }

    protected override void OnUpdate()
    {
        if (!IsOpen)
        {
            return;
        }

        // Held keys, not single-frame presses: the draft can open mid-frame (an orb is
        // collected inside another component's update) and a press must never be missed.
        for (int index = 0; index < _shown.Count; index++)
        {
            WildlingUpgradeCard3D card = _shown[index];

            if (Input.IsKeyDown(card.ChoiceKey) || Input.IsKeyDown(SlotKey(index)))
            {
                Choose(card);
                return;
            }
        }
    }

    /// <summary>Fallback key for a slot, so a mis-authored card key cannot lock the draft.</summary>
    private static Key SlotKey(int index) => index switch
    {
        0 => Key.D1,
        1 => Key.D2,
        _ => Key.D3
    };

    private void Choose(WildlingUpgradeCard3D card)
    {
        card.Apply(Player());

        foreach (WildlingUpgradeCard3D shown in _shown)
        {
            shown.Hide();
        }

        _shown.Clear();
        IsOpen = false;

        if (GameObject?.Scene is { } scene)
        {
            scene.SimulationTimeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
        }
    }

    private GameObject? Player()
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return null;
        }

        if (_run == null)
        {
            _run = Manager()?.GetComponent<WildlingRun3D>();
        }

        string playerName = _run?.PlayerName ?? string.Empty;
        return string.IsNullOrWhiteSpace(playerName) ? null : scene.FindGameObject(playerName);
    }

    private GameObject? Manager() =>
        string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName);
}
