using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

internal static class GameplayQueryInitialOverlapRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        TangentFloorContactDoesNotBlockHorizontalCapsuleSweep();
        IntoWallContactStillBlocksHorizontalCapsuleSweep();
    }

    private static void TangentFloorContactDoesNotBlockHorizontalCapsuleSweep()
    {
        var scene =
            new Scene(
                "Capsule Tangent Floor Regression");

        GameObject ground =
            scene.CreateGameObject(
                "Ground");

        ground.Transform.LocalPosition =
            new Vector3(
                0f,
                -.5f,
                0f);

        ground.AddComponent(
            new BoxCollider3D
            {
                Size =
                    new Vector3(
                        10f,
                        1f,
                        10f)
            });

        bool blocked =
            GameplayQuery3D.CapsuleCast(
                scene,
                new Vector3(
                    0f,
                    .25f,
                    0f),
                new Vector3(
                    0f,
                    1.25f,
                    0f),
                Vector3.UnitZ,
                .25f,
                out _,
                1f,
                includeTriggers:
                    false);

        if (blocked)
        {
            throw new InvalidOperationException(
                "FAILED: a capsule tangent to the floor was blocked by a horizontal distance-zero floor contact.");
        }
    }

    private static void IntoWallContactStillBlocksHorizontalCapsuleSweep()
    {
        var scene =
            new Scene(
                "Capsule Wall Regression");

        GameObject wall =
            scene.CreateGameObject(
                "Wall");

        wall.Transform.LocalPosition =
            new Vector3(
                0f,
                .75f,
                1.25f);

        wall.AddComponent(
            new BoxCollider3D
            {
                Size =
                    new Vector3(
                        2f,
                        1.5f,
                        .5f)
            });

        bool blocked =
            GameplayQuery3D.CapsuleCast(
                scene,
                new Vector3(
                    0f,
                    .25f,
                    .75f),
                new Vector3(
                    0f,
                    1.25f,
                    .75f),
                Vector3.UnitZ,
                .25f,
                out _,
                1f,
                includeTriggers:
                    false);

        if (!blocked)
        {
            throw new InvalidOperationException(
                "FAILED: motion into a distance-zero wall contact must remain blocking.");
        }
    }
}
