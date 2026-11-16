using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Floating damage numbers.
///
/// The popups are **authored** UiText objects listed in <see cref="PoolObjectNames"/> — font,
/// size, anchor and colour come from the scene. This component only subscribes to every
/// <see cref="HealthComponent.Damaged"/> in the scene, projects the victim to screen space and
/// animates one pooled text per hit (rise + fade). Numbers are formatted with
/// <see cref="Format"/>, so even the text is data.
/// </summary>
public sealed class WildlingDamagePopup3D : Component
{
    /// <summary>Comma-separated UiText object names used as the popup pool (parented to a UiCanvas).</summary>
    public string PoolObjectNames { get; set; } = string.Empty;

    /// <summary>Number format; <c>{0}</c> is the damage amount.</summary>
    public string Format { get; set; } = "{0:0}";

    /// <summary>Screen-space rise speed in reference pixels per second.</summary>
    public float LiftSpeed { get; set; } = 52f;

    /// <summary>Horizontal jitter applied per popup, in reference pixels.</summary>
    public float Spread { get; set; } = 10f;

    public float LifetimeSeconds { get; set; } = .85f;

    /// <summary>Portion of the lifetime spent fading out.</summary>
    public float FadeSeconds { get; set; } = .35f;

    /// <summary>Height above the victim's origin where the number starts, in metres.</summary>
    public float WorldHeightOffset { get; set; } = 1.1f;

    public Vector4 EnemyColor { get; set; } = new(1f, .94f, .62f, 1f);

    /// <summary>Colour used when the player creature itself is hit.</summary>
    public Vector4 PlayerColor { get; set; } = new(1f, .42f, .36f, 1f);

    /// <summary>Also pop numbers when the player takes damage.</summary>
    public bool ShowPlayerDamage { get; set; } = true;

    /// <summary>How often new HealthComponents are discovered, in seconds.</summary>
    public float RescanSeconds { get; set; } = .5f;

    private sealed class Popup
    {
        public UiText Text = null!;
        public float Life;
        public Vector2 Screen;
        public Vector4 Color;
    }

    private readonly List<Popup> _pool = new();
    private readonly HashSet<HealthComponent> _watched = new();
    private float _rescanRemaining;
    private int _rotation;

    protected override void OnStart()
    {
        BuildPool();
        Rescan();
    }

    /// <summary>Resolves the authored texts; missing names simply leave the pool empty.</summary>
    private void BuildPool()
    {
        _pool.Clear();

        foreach (string name in (PoolObjectNames ?? string.Empty)
                     .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (GameObject?.Scene?.FindGameObject(name)?.GetComponent<UiText>() is not { } text)
            {
                continue;
            }

            text.Visible = false;
            _pool.Add(new Popup { Text = text });
        }
    }

    protected override void OnUpdate()
    {
        float delta = (float)Time.DeltaTime;

        _rescanRemaining -= delta;
        if (_rescanRemaining <= 0f)
        {
            _rescanRemaining = MathF.Max(.1f, RescanSeconds);
            Rescan();
        }

        if (_pool.Count == 0)
        {
            return;
        }

        float scale = MathF.Max(.01f, CanvasScale());

        foreach (Popup popup in _pool)
        {
            if (popup.Life <= 0f)
            {
                continue;
            }

            popup.Life -= delta;

            if (popup.Life <= 0f)
            {
                popup.Text.Visible = false;
                continue;
            }

            popup.Screen.Y -= MathF.Max(0f, LiftSpeed) * delta / scale;

            float fade = MathF.Max(.01f, FadeSeconds);
            float alpha = popup.Life < fade ? Math.Clamp(popup.Life / fade, 0f, 1f) : 1f;

            popup.Text.Anchor = UiAnchor.TopLeft;
            popup.Text.Offset = popup.Screen;
            popup.Text.Color = popup.Color with { W = popup.Color.W * alpha };
            popup.Text.Visible = true;
        }
    }
    /// <summary>Registers every health component once; their damage events drive the popups.</summary>
    private void Rescan()
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return;
        }

        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.GetComponent<HealthComponent>() is not { } health || !_watched.Add(health))
            {
                continue;
            }

            health.Damaged += OnDamaged;
        }
    }

    private void OnDamaged(HealthComponent health, float amount)
    {
        if (amount <= 0f || _pool.Count == 0 || health.GameObject == null)
        {
            return;
        }

        bool isPlayer = health.GameObject.GetComponent<WildlingStats3D>() != null ||
                        health.GameObject.GetComponent<WildlingCombat3D>() != null;

        if (isPlayer && !ShowPlayerDamage)
        {
            return;
        }

        Camera3D? camera = GameObject?.Scene?.ActiveCamera;
        Vector3 world = health.GameObject.Transform.WorldPosition + new Vector3(0f, WorldHeightOffset, 0f);

        if (camera == null || !TryProject(camera, world, out Vector2 screen))
        {
            return;
        }

        Popup popup = Lease();
        float scale = MathF.Max(.01f, CanvasScale());
        float jitter = MathF.Max(0f, Spread) * ((_rotation % 3) - 1) * .5f;
        _rotation++;

        popup.Screen = new Vector2(screen.X / scale + jitter, screen.Y / scale);
        popup.Life = MathF.Max(.05f, LifetimeSeconds);
        popup.Color = isPlayer ? PlayerColor : EnemyColor;
        popup.Text.Text = string.Format(Format, MathF.Round(amount));
        popup.Text.Anchor = UiAnchor.TopLeft;
        popup.Text.Offset = popup.Screen;
        popup.Text.Color = popup.Color;
        popup.Text.Visible = true;
    }

    /// <summary>Free slot, else the one closest to expiring.</summary>
    private Popup Lease()
    {
        Popup? oldest = null;

        foreach (Popup popup in _pool)
        {
            if (popup.Life <= 0f)
            {
                return popup;
            }

            if (oldest == null || popup.Life < oldest.Life)
            {
                oldest = popup;
            }
        }

        return oldest ?? _pool[0];
    }

    /// <summary>Reference-pixel scale of the owning UiCanvas (offsets are authored in those units).</summary>
    private float CanvasScale()
    {
        if (_pool.Count == 0 ||
            !UiLayout.TryGetCanvas(_pool[0].Text.GameObject, out UiCanvas? canvas) || canvas == null)
        {
            return 1f;
        }

        return UiLayout.ResolveCanvas(canvas, Input.GameViewSize).Scale;
    }

    /// <summary>World point → screen pixels through the active camera.</summary>
    private static bool TryProject(Camera3D camera, Vector3 world, out Vector2 screen)
    {
        screen = default;
        Vector2 viewport = Input.GameViewSize;

        if (viewport.X < 4f || viewport.Y < 4f)
        {
            return false;
        }

        Matrix4x4 view = camera.GetViewMatrix();
        Matrix4x4 projection = camera.GetProjectionMatrix(viewport.X / MathF.Max(1f, viewport.Y));
        Vector4 clip = Vector4.Transform(new Vector4(world, 1f), view * projection);

        if (clip.W <= .0001f)
        {
            return false;
        }

        float x = clip.X / clip.W;
        float y = clip.Y / clip.W;

        if (x < -1.5f || x > 1.5f || y < -1.5f || y > 1.5f)
        {
            return false;
        }

        screen = new Vector2((x * .5f + .5f) * viewport.X, (.5f - y * .5f) * viewport.Y);
        return true;
    }

    protected override void OnDestroy()
    {
        foreach (HealthComponent health in _watched)
        {
            health.Damaged -= OnDamaged;
        }

        _watched.Clear();
    }
}

