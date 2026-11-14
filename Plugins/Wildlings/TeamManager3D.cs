using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Three-slot party plus the exploration/combat control split.
///
/// Exploration: the player drives the trainer, and the Wildlings trail it
/// passively (no shooting). Combat: the active Wildling takes player input,
/// the other two fight as companions, and the trainer hangs back.
/// Combat starts automatically when a hostile comes close (or with the toggle
/// key) and ends after a short quiet period.
/// </summary>
public sealed class TeamManager3D : Component
{
    /// <summary>Scene object name of party slot 1. Names keep the inspector readable.</summary>
    [SceneObjectPicker]
    public string SlotOneName { get; set; } = "Fire Lizard";

    [SceneObjectPicker]
    public string SlotTwoName { get; set; } = "Spriglet";

    [SceneObjectPicker]
    public string SlotThreeName { get; set; } = "Zappet";

    /// <summary>Scene object name of the trainer the player drives outside combat.</summary>
    [SceneObjectPicker]
    public string TrainerObjectName { get; set; } = "Trainer";

    public int ActiveSlot { get; set; } = 1;

    public float SwitchCooldown { get; set; } = .6f;

    public bool SwitchWithNumberKeys { get; set; } = true;

    public bool AutoSwitchOnDeath { get; set; } = true;

    public float DeathHandoverProtection { get; set; } = .35f;

    public bool StartInCombat { get; set; }

    public bool AutoCombat { get; set; } = true;

    public float CombatTriggerRange { get; set; } = 12f;

    public float CombatExitDelay { get; set; } = 4f;

    /// <summary>Layer index treated as hostile (project layer 3 is Enemy).</summary>
    public int HostileLayer { get; set; } = 3;

    public bool ToggleCombatWithKey { get; set; } = true;

    private float _cooldownRemaining;
    private float _protectionRemaining;
    private GameObject? _protectedMember;
    private float _combatExitRemaining;
    private double _hostileScanRemaining;
    private bool _hostilesNearby;

    public bool InCombat { get; private set; }

    public GameObject? Trainer => ResolveByName(TrainerObjectName);

    public GameObject? ActiveMember => Resolve(ActiveSlot);

    /// <summary>Who companions and the trainer trail: the trainer on foot, the fighter in combat.</summary>
    public GameObject? Leader => InCombat ? ActiveMember : Trainer;

    public bool IsSwitchReady => _cooldownRemaining <= 0f;

    public int LivingCount
    {
        get
        {
            int living = 0;
            for (int slot = 1; slot <= 3; slot++)
            {
                if (Resolve(slot)?.GetComponent<HealthComponent>() is { IsDead: false })
                {
                    living++;
                }
            }

            return living;
        }
    }

    protected override void OnStart()
    {
        InCombat = StartInCombat;
        _combatExitRemaining = InCombat ? CombatExitDelay : 0f;
        ApplyControl();
    }

    protected override void OnUpdate()
    {
        float delta = (float)Time.DeltaTime;
        _cooldownRemaining = MathF.Max(0f, _cooldownRemaining - delta);

        if (_protectionRemaining > 0f)
        {
            _protectionRemaining -= delta;
            if (_protectionRemaining <= 0f && _protectedMember?.GetComponent<HealthComponent>() is { } protectedHealth)
            {
                protectedHealth.Invulnerable = false;
                _protectedMember = null;
            }
        }

        if (ToggleCombatWithKey && Input.IsGameInputCaptured && Input.IsKeyPressed(Key.T))
        {
            SetCombat(!InCombat);
        }

        if (SwitchWithNumberKeys && InCombat && Input.IsGameInputCaptured)
        {
            if (Input.IsKeyPressed(Key.D1))
            {
                RequestSwitch(1);
            }
            else if (Input.IsKeyPressed(Key.D2))
            {
                RequestSwitch(2);
            }
            else if (Input.IsKeyPressed(Key.D3))
            {
                RequestSwitch(3);
            }
        }

        UpdateAutoCombat(delta);

        if (AutoSwitchOnDeath && InCombat)
        {
            CheckDeathHandover();
        }
    }

    /// <summary>Enters or leaves combat control and re-hands input, camera and AI.</summary>
    public void SetCombat(bool combat)
    {
        if (InCombat == combat)
        {
            return;
        }

        InCombat = combat;
        _combatExitRemaining = combat ? CombatExitDelay : 0f;
        ApplyControl();
    }

    /// <summary>Requests a slot change. Repeats inside the throttle are ignored.</summary>
    public void RequestSwitch(int slot)
    {
        if (slot is < 1 or > 3 || slot == ActiveSlot || _cooldownRemaining > 0f)
        {
            return;
        }

        CommandSlot(slot);
    }

    /// <summary>Event-sheet / script slot change: ignores the input throttle and may re-apply the same slot.</summary>
    public void CommandSlot(int slot)
    {
        if (slot is < 1 or > 3)
        {
            return;
        }

        GameObject? target = Resolve(slot);
        if (target == null || target.GetComponent<HealthComponent>() is { IsDead: true })
        {
            return;
        }

        ActiveSlot = slot;
        _cooldownRemaining = MathF.Max(.05f, SwitchCooldown);
        ApplyControl();
    }

    public GameObject? Resolve(int slot)
    {
        string name = slot switch
        {
            1 => SlotOneName,
            2 => SlotTwoName,
            3 => SlotThreeName,
            _ => string.Empty
        };

        return ResolveByName(name);
    }

    public GameObject? ResolveByName(string name)
    {
        Scene? scene = GameObject?.Scene;
        return scene == null || string.IsNullOrWhiteSpace(name) ? null : scene.FindGameObject(name);
    }

    public IReadOnlyList<GameObject> Members()
    {
        List<GameObject> members = new(3);
        for (int slot = 1; slot <= 3; slot++)
        {
            if (Resolve(slot) is { } member)
            {
                members.Add(member);
            }
        }

        return members;
    }

    private void UpdateAutoCombat(float delta)
    {
        if (!AutoCombat)
        {
            return;
        }

        _hostileScanRemaining -= delta;
        if (_hostileScanRemaining <= 0.0)
        {
            _hostileScanRemaining = .25;
            _hostilesNearby = AnyHostileNearby(CombatTriggerRange);
        }

        if (!InCombat)
        {
            if (_hostilesNearby)
            {
                SetCombat(true);
            }

            return;
        }

        if (_hostilesNearby)
        {
            _combatExitRemaining = MathF.Max(0f, CombatExitDelay);
            return;
        }

        _combatExitRemaining = MathF.Max(0f, _combatExitRemaining - delta);
        if (_combatExitRemaining <= 0f)
        {
            SetCombat(false);
        }
    }

    private bool AnyHostileNearby(float range)
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return false;
        }

        IReadOnlyList<GameObject> members = Members();

        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.Layer != HostileLayer || candidate.GetComponent<HealthComponent>() is not { IsDead: false })
            {
                continue;
            }

            foreach (GameObject member in members)
            {
                if (Vector3.Distance(candidate.Transform.WorldPosition, member.Transform.WorldPosition) <= range)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Applies the current mode to every actor: input owner, cameras and AI flags.</summary>
    private void ApplyControl()
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return;
        }

        bool exploring = !InCombat;

        for (int index = 1; index <= 3; index++)
        {
            GameObject? member = Resolve(index);
            if (member == null)
            {
                continue;
            }

            bool controlled = !exploring && index == ActiveSlot;

            SetEnabled<PlayerController3D>(member, controlled);

            if (member.GetComponent<WildlingCombat3D>() is { } combat)
            {
                combat.AiControlled = !controlled;
                combat.CombatEnabled = !exploring;
            }

            if (member.GetComponent<WildlingFollower3D>() is { } follower)
            {
                follower.Enabled = !controlled;
            }

            if (controlled)
            {
                // The follower temporarily raises MoveSpeed for catch-up; taking
                // control restores the species speed without touching HP.
                member.GetComponent<WildlingStats3D>()?.ApplyMoveSpeedOnly();
            }

            ApplyCamera(scene, member.GetComponent<CameraBoom3D>(), controlled);
        }

        if (Trainer is { } trainer)
        {
            SetEnabled<PlayerController3D>(trainer, exploring);
            ApplyCamera(scene, trainer.GetComponent<CameraBoom3D>(), exploring);
        }
    }

    private static void ApplyCamera(Scene scene, CameraBoom3D? boom, bool controlled)
    {
        if (boom == null)
        {
            return;
        }

        boom.Enabled = controlled;

        if (scene.FindGameObject(boom.CameraObjectId)?.GetComponent<Camera3D>() is { } camera)
        {
            camera.ActiveGameCamera = controlled;
        }
    }

    private void CheckDeathHandover()
    {
        GameObject? active = ActiveMember;
        if (active?.GetComponent<HealthComponent>() is not { IsDead: true })
        {
            return;
        }

        for (int offset = 1; offset < 3; offset++)
        {
            int slot = (ActiveSlot - 1 + offset) % 3 + 1;
            GameObject? candidate = Resolve(slot);
            if (candidate?.GetComponent<HealthComponent>() is not { IsDead: false })
            {
                continue;
            }

            ActiveSlot = slot;
            _cooldownRemaining = MathF.Max(.05f, SwitchCooldown);
            GrantProtection(candidate);
            ApplyControl();
            return;
        }
    }

    private void GrantProtection(GameObject member)
    {
        if (member.GetComponent<HealthComponent>() is not { } health || DeathHandoverProtection <= 0f)
        {
            return;
        }

        health.Invulnerable = true;
        _protectedMember = member;
        _protectionRemaining = DeathHandoverProtection;
    }

    private static void SetEnabled<T>(GameObject member, bool enabled) where T : Component
    {
        if (member.GetComponent<T>() is { } component)
        {
            component.Enabled = enabled;
        }
    }
}
