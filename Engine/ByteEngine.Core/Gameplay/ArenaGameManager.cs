using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public enum ArenaGameState
{
    Playing,
    Won,
    Lost
}

public sealed class ArenaGameManager : Component
{
    private readonly Dictionary<Guid, Snapshot> _snapshots =
        new();

    private bool _announced;

    private int _initialEnemyCount;

    public Guid PlayerId { get; set; }

    public string PlayerName { get; set; } =
        "Player";

    public int RemainingEnemies { get; private set; }

    public ArenaGameState GameState { get; private set; } =
        ArenaGameState.Playing;

    public override int UpdateOrder =>
        2000;

    /*
     * Draw the HUD after normal scene objects. GameObject.RenderOrder uses
     * the first non-null component render order on this manager object.
     */
    public override int? RenderOrder =>
        10000;

    protected override void OnStart()
    {
        RuntimeScene? scene =
            AttachedGameObject?
                .Scene;

        if (scene ==
            null)
        {
            return;
        }

        _snapshots.Clear();

        foreach (GameObject item
                 in scene.GameObjects.Where(
                     IsParticipant))
        {
            HealthComponent health =
                item.GetComponent<HealthComponent>()!;

            _snapshots[item.Id] =
                new Snapshot(
                    item.Transform.LocalPosition,
                    item.Transform.LocalRotation,
                    health.MaxHealth,
                    item.Active);
        }

        _initialEnemyCount =
            scene.GameObjects.Count(
                item =>
                    item.GetComponent<SimpleEnemyAI3D>() !=
                    null);

        Evaluate(
            scene);
    }

    protected override void OnUpdate()
    {
        RuntimeScene? scene =
            AttachedGameObject?
                .Scene;

        if (scene ==
            null)
        {
            return;
        }

        if (Input.IsKeyPressed(
                Key.R))
        {
            Restart(
                scene);

            return;
        }

        Evaluate(
            scene);
    }

    protected override void OnRender(
        RenderContext context)
    {
        /*
         * Keep the editor Scene View clean. Runtime HUD is only useful while
         * the scene is actually loaded in Play Mode.
         */
        if (!context.Scene.IsLoaded)
        {
            return;
        }

        context.Renderer2D.ResetCamera();

        DrawHealthBar(
            context);

        DrawEnemyPips(
            context);

        DrawStateOverlay(
            context);
    }

    public void Restart()
    {
        RuntimeScene? scene =
            AttachedGameObject?
                .Scene;

        if (scene !=
            null)
        {
            Restart(
                scene);
        }
    }

    private void Restart(
        RuntimeScene scene)
    {
        foreach ((Guid id, Snapshot snapshot)
                 in _snapshots)
        {
            GameObject? item =
                scene.FindGameObject(
                    id);

            HealthComponent? health =
                item?
                    .GetComponent<HealthComponent>();

            if (item == null ||
                health == null)
            {
                continue;
            }

            item.Active =
                snapshot.Active;

            item.Transform.LocalPosition =
                snapshot.Position;

            item.Transform.LocalRotation =
                snapshot.Rotation;

            health.MaxHealth =
                snapshot.MaxHealth;

            health.CurrentHealth =
                snapshot.MaxHealth;

            item.GetComponent<CharacterController3D>()?
                .SetVelocity(
                    Vector3.Zero);

            item.GetComponent<SimpleEnemyAI3D>()?
                .ResetCombatState();

            item.GetComponent<ProjectileLauncher3D>()?
                .ResetCooldown();
        }

        foreach (GameObject projectile
                 in scene.GameObjects
                     .Where(
                         item =>
                             item.GetComponent<Projectile3D>() !=
                             null)
                     .ToArray())
        {
            scene.DestroyGameObject(
                projectile);
        }

        GameState =
            ArenaGameState.Playing;

        _announced =
            false;

        Evaluate(
            scene);

        Console.WriteLine(
            "[ByteArena] Arena restarted.");
    }

    private void Evaluate(
        RuntimeScene scene)
    {
        GameObject? player =
            ResolvePlayer(
                scene);

        foreach (GameObject enemy
                 in scene.GameObjects.Where(
                     item =>
                         item.GetComponent<SimpleEnemyAI3D>() !=
                         null))
        {
            if (enemy.GetComponent<HealthComponent>() is
                {
                    IsDead: true
                })
            {
                enemy.Active =
                    false;
            }
        }

        RemainingEnemies =
            scene.GameObjects.Count(
                item =>
                    item.ActiveInHierarchy &&
                    item.GetComponent<SimpleEnemyAI3D>() !=
                    null &&
                    item.GetComponent<HealthComponent>() is
                    {
                        IsDead: false
                    });

        GameState =
            player?
                .GetComponent<HealthComponent>() is
            {
                IsDead: true
            }
                ? ArenaGameState.Lost
                : RemainingEnemies ==
                  0
                    ? ArenaGameState.Won
                    : ArenaGameState.Playing;

        if (GameState !=
                ArenaGameState.Playing &&
            !_announced)
        {
            Console.WriteLine(
                GameState ==
                ArenaGameState.Won
                    ? "[ByteArena] YOU WON! Press R to restart."
                    : "[ByteArena] YOU LOST! Press R to restart.");

            _announced =
                true;
        }
    }

    private void DrawHealthBar(
        RenderContext context)
    {
        GameObject? player =
            ResolvePlayer(
                context.Scene);

        HealthComponent? health =
            player?
                .GetComponent<HealthComponent>();

        float percent =
            health?
                .HealthPercent ??
            0f;

        percent =
            Math.Clamp(
                percent,
                0f,
                1f);

        const float width =
            240f;

        const float height =
            18f;

        const float left =
            24f;

        const float top =
            24f;

        Vector2 backgroundCenter =
            new(
                left +
                width *
                .5f,
                top +
                height *
                .5f);

        context.Renderer2D.DrawQuad(
            backgroundCenter,
            new Vector2(
                width +
                4f,
                height +
                4f),
            new Vector4(
                .02f,
                .02f,
                .025f,
                .9f));

        float fillWidth =
            width *
            percent;

        if (fillWidth >
            0f)
        {
            Vector2 fillCenter =
                new(
                    left +
                    fillWidth *
                    .5f,
                    top +
                    height *
                    .5f);

            Vector4 fillColor =
                percent >
                .5f
                    ? new Vector4(
                        .2f,
                        .9f,
                        .3f,
                        1f)
                    : percent >
                      .25f
                        ? new Vector4(
                            1f,
                            .7f,
                            .1f,
                            1f)
                        : new Vector4(
                            1f,
                            .2f,
                            .15f,
                            1f);

            context.Renderer2D.DrawQuad(
                fillCenter,
                new Vector2(
                    fillWidth,
                    height),
                fillColor);
        }
    }

    private void DrawEnemyPips(
        RenderContext context)
    {
        int total =
            Math.Max(
                _initialEnemyCount,
                RemainingEnemies);

        if (total <=
            0)
        {
            return;
        }

        const float size =
            14f;

        const float spacing =
            8f;

        float right =
            context.TargetWidth -
            24f;

        float y =
            32f;

        for (int index =
                 0;
             index <
             total;
             index++)
        {
            float x =
                right -
                index *
                (
                    size +
                    spacing
                ) -
                size *
                .5f;

            bool alive =
                index <
                RemainingEnemies;

            context.Renderer2D.DrawQuad(
                new Vector2(
                    x,
                    y),
                new Vector2(
                    size,
                    size),
                alive
                    ? new Vector4(
                        .95f,
                        .18f,
                        .12f,
                        1f)
                    : new Vector4(
                        .16f,
                        .16f,
                        .18f,
                        .75f));
        }
    }

    private void DrawStateOverlay(
        RenderContext context)
    {
        if (GameState ==
            ArenaGameState.Playing)
        {
            return;
        }

        Vector4 tint =
            GameState ==
            ArenaGameState.Won
                ? new Vector4(
                    .05f,
                    .55f,
                    .18f,
                    .32f)
                : new Vector4(
                    .65f,
                    .05f,
                    .05f,
                    .34f);

        Vector2 center =
            new(
                context.TargetWidth *
                .5f,
                context.TargetHeight *
                .5f);

        context.Renderer2D.DrawQuad(
            center,
            new Vector2(
                context.TargetWidth,
                context.TargetHeight),
            tint);

        /*
         * No runtime font system yet. Use a large high-contrast center card
         * so win/loss is unmissable while Console still prints the text.
         */
        Vector4 cardColor =
            GameState ==
            ArenaGameState.Won
                ? new Vector4(
                    .1f,
                    .9f,
                    .3f,
                    .92f)
                : new Vector4(
                    1f,
                    .18f,
                    .12f,
                    .92f);

        context.Renderer2D.DrawQuad(
            center,
            new Vector2(
                Math.Min(
                    360f,
                    context.TargetWidth *
                    .65f),
                72f),
            cardColor);

        /*
         * Three small white bars read visually like a state banner without
         * introducing a font renderer solely for this demo.
         */
        for (int index =
                 -1;
             index <=
             1;
             index++)
        {
            context.Renderer2D.DrawQuad(
                center +
                new Vector2(
                    index *
                    42f,
                    0f),
                new Vector2(
                    24f,
                    12f),
                Vector4.One);
        }
    }

    private GameObject? ResolvePlayer(
        RuntimeScene scene)
    {
        GameObject? player =
            PlayerId !=
                Guid.Empty
                ? scene.FindGameObject(
                    PlayerId)
                : null;

        if (player ==
                null &&
            !string.IsNullOrWhiteSpace(
                PlayerName))
        {
            player =
                scene.FindGameObject(
                    PlayerName);
        }

        return player;
    }

    private static bool IsParticipant(
        GameObject item) =>
        item.GetComponent<HealthComponent>() !=
            null &&
        (
            item.GetComponent<PlayerController3D>() !=
                null ||
            item.GetComponent<SimpleEnemyAI3D>() !=
                null
        );

    private sealed record Snapshot(
        Vector3 Position,
        Quaternion Rotation,
        float MaxHealth,
        bool Active);
}
