using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Experience pickup (GDD v0.3: XP orbs, not instant credit).
///
/// Drifts in place until the player enters <see cref="PickupRadius"/>, then homes in and
/// grants its amount to the run director. Spawned by <see cref="WildlingRun3D"/> when a
/// hostile dies; expires after <see cref="LifetimeSeconds"/>.
/// </summary>
public sealed class WildlingExperienceOrb3D : Component
{
    public float Amount { get; set; } = 1f;

    public float PickupRadius { get; set; } = 2.6f;

    public float MagnetSpeed { get; set; } = 11f;

    public float LifetimeSeconds { get; set; } = 20f;

    [SceneObjectPicker]
    public string PlayerName { get; set; } = "Fire Lizard";

    [SceneObjectPicker]
    public string RunObjectName { get; set; } = "Wildling Manager";

    private float _age;
    private float _bob;

    protected override void OnUpdate()
    {
        GameObject? orb = GameObject;
        Scene? scene = orb?.Scene;
        if (orb == null || scene == null)
        {
            return;
        }

        float delta = (float)Time.DeltaTime;
        _age += delta;

        if (_age >= MathF.Max(1f, LifetimeSeconds))
        {
            scene.DestroyGameObject(orb);
            return;
        }

        _bob += delta;

        GameObject? player = string.IsNullOrWhiteSpace(PlayerName)
            ? null
            : scene.FindGameObject(PlayerName);

        if (player == null)
        {
            return;
        }

        Vector3 toPlayer = player.Transform.WorldPosition - orb.Transform.WorldPosition;
        toPlayer.Y = 0f;
        float distance = toPlayer.Length();

        if (distance <= .55f)
        {
            Collect(scene, player);
            return;
        }

        if (distance <= MathF.Max(.5f, PickupRadius) && distance > .0001f)
        {
            Vector3 step = toPlayer / distance * MathF.Max(1f, MagnetSpeed) * delta;
            if (step.Length() > distance)
            {
                step = toPlayer;
            }

            orb.Transform.WorldPosition += step;
        }
    }

    private void Collect(Scene scene, GameObject player)
    {
        GameObject? orb = GameObject;
        if (orb == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(RunObjectName) &&
            scene.FindGameObject(RunObjectName)?.GetComponent<WildlingRun3D>() is { } run)
        {
            run.AddExperience(MathF.Max(0f, Amount));
        }

        scene.DestroyGameObject(orb);
    }
}
