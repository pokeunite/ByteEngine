using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Sanctuary hub (GDD v0.3 slice 3). Menu-driven, not an explorable level: the hub is a
/// UI state with its own camera and a page stack (Sanctuary, Collection, Stones, Charms,
/// Upgrades, Start Run).
///
/// Buttons are authored <see cref="UiWidget"/> objects; art buttons carry released,
/// hover and pressed images, so the pointer alone drives the menu. Start Run either
/// begins the in-scene run or loads <see cref="StartScenePath"/> when it is set; the run
/// director returns here through its own <c>ReturnScenePath</c>.
///
/// Every text object, key, page, delay and time scale is a property.
/// </summary>
public sealed class WildlingSanctuary3D : Component
{
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Comma-separated UiText/UiWidget object names shown while the hub is open.</summary>
    public string MenuTextObjectNames { get; set; } =
        "Sanctuary Coins,Sanctuary Creature,Sanctuary Stats,Sanctuary Prompt";

    [SceneObjectPicker]
    public string CoinTextObjectName { get; set; } = "Sanctuary Coins";

    [SceneObjectPicker]
    public string GemTextObjectName { get; set; } = "Sanctuary Gems";

    [SceneObjectPicker]
    public string CreatureTextObjectName { get; set; } = "Sanctuary Creature";

    [SceneObjectPicker]
    public string StatsTextObjectName { get; set; } = "Sanctuary Stats";

    public Key StartRunKey { get; set; } = Key.Enter;

    /// <summary>Authored button that opens <see cref="StartPage"/> (or starts the run when it is empty).</summary>
    [SceneObjectPicker]
    public string StartButtonObjectName { get; set; } = "Start Button";

    /// <summary>Authored button on the Start Run page that launches the run.</summary>
    [SceneObjectPicker]
    public string ConfirmStartButtonObjectName { get; set; } = "Start Run Confirm";

    /// <summary>Authored button that closes the game when clicked.</summary>
    [SceneObjectPicker]
    public string QuitButtonObjectName { get; set; } = "Quit Button";

    /// <summary>Camera that frames the sanctuary while the menu is open. Never the follow camera.</summary>
    [SceneObjectPicker]
    public string HubCameraObjectName { get; set; } = "Hub Camera";

    /// <summary>Camera that follows the driven creature once the menu closes.</summary>
    [SceneObjectPicker]
    public string GameplayCameraObjectName { get; set; } = "Camera";

    /// <summary>Project-relative scene a run loads. Empty starts the run in this scene.</summary>
    public string StartScenePath { get; set; } = "Scenes/Arena.bytescene";

    /// <summary>Page shown when the hub opens.</summary>
    public string HomePage { get; set; } = "hub";

    /// <summary>Page the Start button opens before a run launches. Empty starts immediately.</summary>
    public string StartPage { get; set; } = "startrun";

    /// <summary>
    /// Page contents: <c>page=Object A,Object B;page2=...</c>. When set, the listed objects
    /// are the only menu objects shown and <see cref="MenuTextObjectNames"/> is ignored.
    /// </summary>
    public string PageDefinitions { get; set; } = string.Empty;

    /// <summary>Navigation buttons: <c>button=page;button=page</c>.</summary>
    public string PageButtonObjectNames { get; set; } = string.Empty;

    /// <summary>Key that returns to <see cref="HomePage"/> (Escape is reserved for menus).</summary>
    public Key BackKey { get; set; } = Key.Escape;

    /// <summary>Scene time scale while the hub is open (0 freezes the arena).</summary>
    public float MenuTimeScale { get; set; }

    /// <summary>Seconds after a run ends before the hub returns.</summary>
    public float ReturnDelaySeconds { get; set; } = 3f;

    /// <summary>Open the hub as soon as the scene starts.</summary>
    public bool OpenOnStart { get; set; } = true;

    public bool IsOpen { get; private set; }

    /// <summary>Page currently on screen (<see cref="HomePage"/> when the hub is open).</summary>
    public string CurrentPage { get; private set; } = string.Empty;

    private float _returnRemaining;
    private WildlingRun3D? _run;
    private float _previousTimeScale = 1f;
    private Camera3D? _hubCamera;
    private bool _pointerOwned;

    protected override void OnStart()
    {
        _run = Manager()?.GetComponent<WildlingRun3D>();

        if (OpenOnStart)
        {
            Open();
        }
    }

    protected override void OnUpdate()
    {
        float delta = (float)Time.DeltaTime;
        _run ??= Manager()?.GetComponent<WildlingRun3D>();

        if (!IsOpen)
        {
            if (_run is { State: WildlingRunState.Failed or WildlingRunState.Cleared })
            {
                _returnRemaining -= delta;

                if (_returnRemaining <= 0f)
                {
                    Open();
                }
            }

            return;
        }

        EnsureHubCamera();

        if (Input.IsKeyDown(StartRunKey) || WidgetClicked(ConfirmStartButtonObjectName))
        {
            StartRun();
            return;
        }

        if (WidgetClicked(QuitButtonObjectName))
        {
            GameObject?.Scene?.RequestQuit();
            return;
        }

        if (RouteButtons(PageButtonObjectNames))
        {
            return;
        }

        if (WidgetClicked(StartButtonObjectName))
        {
            if (!string.IsNullOrWhiteSpace(StartPage) && HasPage(StartPage))
            {
                ShowPage(StartPage);
            }
            else
            {
                StartRun();
            }

            return;
        }

        if (Input.IsKeyDown(BackKey) && !string.Equals(CurrentPage, HomePage, StringComparison.OrdinalIgnoreCase))
        {
            ShowPage(HomePage);
        }
    }

    /// <summary>True when the named authored widget is visible and clicked this frame.</summary>
    private bool WidgetClicked(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        if (GameObject?.Scene?.FindGameObject(objectName)?.GetComponent<UiWidget>() is not { } widget)
        {
            return false;
        }

        return widget.Visible && widget.WasClicked;
    }

    /// <summary>Fires the first navigation button that was clicked this frame.</summary>
    private bool RouteButtons(string definitions)
    {
        foreach ((string button, string page) in ParsePairs(definitions))
        {
            if (WidgetClicked(button))
            {
                ShowPage(page);
                return true;
            }
        }

        return false;
    }

    /// <summary>Shows the hub, freezes the world and frames it with the sanctuary camera.</summary>
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        _returnRemaining = MathF.Max(0f, ReturnDelaySeconds);
        RequestPointer(true);

        if (Manager()?.GetComponent<WildlingArenaSpawner3D>() is { } spawner)
        {
            spawner.Spawning = false;
        }

        ActivateCamera(HubCameraObjectName);
        ShowPage(HomePage);

        if (GameObject?.Scene is { } scene)
        {
            _previousTimeScale = scene.SimulationTimeScale;
            scene.SimulationTimeScale = MathF.Max(0f, MenuTimeScale);
        }
    }

    /// <summary>Shows the hub page, hides the others and refreshes its texts.</summary>
    public void ShowPage(string page)
    {
        string target = string.IsNullOrWhiteSpace(page) ? HomePage : page.Trim();
        CurrentPage = target;

        if (string.IsNullOrWhiteSpace(PageDefinitions))
        {
            SetMenuVisible(true);
            return;
        }

        foreach ((string name, string[] objects) in ParsePages(PageDefinitions))
        {
            bool visible = string.Equals(name, target, StringComparison.OrdinalIgnoreCase);

            foreach (string objectName in objects)
            {
                SetObjectVisible(objectName, visible);
            }
        }

        RefreshTexts();
    }

    /// <summary>True when <see cref="PageDefinitions"/> declares the page.</summary>
    public bool HasPage(string page) =>
        ParsePages(PageDefinitions).Any(pair =>
            string.Equals(pair.Name, page, StringComparison.OrdinalIgnoreCase));

    /// <summary>Hides the hub, restores the run camera and begins a fresh run.</summary>
    public void StartRun()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        SetMenuVisible(false);
        SetPagesVisible(false);
        ActivateCamera(GameplayCameraObjectName);
        RequestPointer(false);

        Scene? scene = GameObject?.Scene;

        if (scene != null)
        {
            scene.SimulationTimeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
        }

        if (!string.IsNullOrWhiteSpace(StartScenePath))
        {
            try
            {
                scene?.RequestLoad(StartScenePath);
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"Wildling Sanctuary: '{StartScenePath}' is not a project-relative .bytescene path.");
            }

            return;
        }

        _run ??= Manager()?.GetComponent<WildlingRun3D>();
        _run?.Begin();
        _returnRemaining = MathF.Max(0f, ReturnDelaySeconds);
    }

    private void ActivateCamera(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || GameObject?.Scene is not { } scene)
        {
            return;
        }

        if (scene.FindGameObject(objectName)?.GetComponent<Camera3D>() is { } camera)
        {
            scene.SetActiveCamera(camera);
        }
    }

    /// <summary>
    /// Keeps the sanctuary camera active while the menu is open. TeamManager3D re-asserts the
    /// party's follow camera whenever control modes change, which would otherwise pull the view
    /// back to the Wildling camera mid-menu.
    /// </summary>
    private void EnsureHubCamera()
    {
        if (!IsOpen || string.IsNullOrWhiteSpace(HubCameraObjectName) || GameObject?.Scene is not { } scene)
        {
            return;
        }

        _hubCamera ??= scene.FindGameObject(HubCameraObjectName)?.GetComponent<Camera3D>();

        if (_hubCamera == null)
        {
            return;
        }

        if (!ReferenceEquals(scene.ActiveCamera, _hubCamera))
        {
            scene.SetActiveCamera(_hubCamera);
        }
    }

    /// <summary>
    /// Takes ownership of the pointer while the menu is open. RequestUiPointer makes the Game View
    /// drop its mouse grab so the visible cursor can be used, and releases it for gameplay.
    /// </summary>
    private void RequestPointer(bool visible)
    {
        if (_pointerOwned == visible)
        {
            return;
        }

        _pointerOwned = visible;
        Input.RequestUiPointer(this, visible);
    }

    protected override void OnDestroy() => RequestPointer(false);

    private void RefreshTexts()
    {
        WildlingProfile3D? profile = Manager()?.GetComponent<WildlingProfile3D>();
        WildlingProfileState? state = profile?.State;

        SetText(CoinTextObjectName, state == null ? "0" : $"{state.Coins:N0}");

        SetText(GemTextObjectName, state == null ? "0" : $"{state.FireLizardEssence:N0}");

        SetText(CreatureTextObjectName, state?.EquippedSpecies ?? WildlingSpeciesDatabase.FireLizardId);

        SetText(
            StatsTextObjectName,
            state == null
                ? string.Empty
                : $"Runs {state.Runs}   Clears {state.Clears}   Best kills {state.BestKills}");
    }

    private void SetMenuVisible(bool visible)
    {
        foreach (string name in Split(MenuTextObjectNames))
        {
            SetObjectVisible(name, visible);
        }

        if (visible)
        {
            RefreshTexts();
        }
    }

    private void SetPagesVisible(bool visible)
    {
        if (string.IsNullOrWhiteSpace(PageDefinitions))
        {
            return;
        }

        foreach ((string _, string[] objects) in ParsePages(PageDefinitions))
        {
            foreach (string objectName in objects)
            {
                SetObjectVisible(objectName, visible);
            }
        }
    }

    private void SetObjectVisible(string objectName, bool visible)
    {
        GameObject? element = GameObject?.Scene?.FindGameObject(objectName);

        if (element?.GetComponent<UiText>() is { } text)
        {
            text.Visible = visible;
            return;
        }

        if (element?.GetComponent<UiWidget>() is { } widget)
        {
            widget.Visible = visible;
        }
    }

    private void SetText(string objectName, string value)
    {
        if (!string.IsNullOrWhiteSpace(objectName) &&
            GameObject?.Scene?.FindGameObject(objectName)?.GetComponent<UiText>() is { } text)
        {
            text.Text = value;
        }
    }

    private static string[] Split(string value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<(string Name, string[] Objects)> ParsePages(string value)
    {
        foreach (string entry in (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int split = entry.IndexOf('=');

            if (split <= 0)
            {
                continue;
            }

            yield return (entry[..split].Trim(), Split(entry[(split + 1)..]));
        }
    }

    private static IEnumerable<(string Button, string Page)> ParsePairs(string value)
    {
        foreach (string entry in (value ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int split = entry.IndexOf('=');

            if (split <= 0)
            {
                continue;
            }

            yield return (entry[..split].Trim(), entry[(split + 1)..].Trim());
        }
    }

    private GameObject? Manager() =>
        string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName);
}
