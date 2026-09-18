using System.Reflection;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;

namespace ByteEngine.Tests;

internal static class C9StaticIdleRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyMissingCharacterControllerResolvesToIdle();
    }

    private static void VerifyMissingCharacterControllerResolvesToIdle()
    {
        MethodInfo? resolver =
            typeof(AnimationController).GetMethod(
                "ResolveLocomotionState",
                BindingFlags.NonPublic |
                BindingFlags.Static);

        Assert(
            resolver != null,
            "C9K: AnimationController locomotion-state resolver was not found.");

        object? result =
            resolver!.Invoke(
                null,
                new object?[]
                {
                    null,
                    4.0f
                });

        Assert(
            result is LocomotionState state &&
            state == LocomotionState.Idle,
            "C9K: a locomotion-enabled AnimationController without CharacterController3D must resolve to Idle instead of remaining in bind/T-pose.");
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}
