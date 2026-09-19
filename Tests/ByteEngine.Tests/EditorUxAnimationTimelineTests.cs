using System.Runtime.CompilerServices;

using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class EditorUxAnimationTimelineTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        EventDragClamps();
        WindowBodyDragPreservesDuration();
        WindowEdgesCannotCross();
    }

    private static void EventDragClamps()
    {
        AssertNear(AnimationTimelineInteraction.MoveEvent(.5f, 100f, 100f, 1f), 1f,
            "event drag clamps at clip end");
        AssertNear(AnimationTimelineInteraction.MoveEvent(.5f, -100f, 100f, 1f), 0f,
            "event drag clamps at clip start");
    }

    private static void WindowBodyDragPreservesDuration()
    {
        (float start, float end) = AnimationTimelineInteraction.MoveWindow(.2f, .5f, 100f, 100f, 1f);
        AssertNear(start, .7f, "window body clamps while moving");
        AssertNear(end - start, .3f, "window body preserves duration");

        (start, end) = AnimationTimelineInteraction.MoveWindow(.2f, .5f, -100f, 100f, 1f);
        AssertNear(start, 0f, "window body clamps at clip start");
        AssertNear(end, .3f, "window body preserves duration at clip start");
    }

    private static void WindowEdgesCannotCross()
    {
        (float start, float end) = AnimationTimelineInteraction.ResizeWindowStart(.2f, .6f, 100f, 100f, 1f);
        AssertNear(start, .6f, "left edge cannot cross right edge");
        AssertNear(end, .6f, "right edge remains stable during left resize");

        (start, end) = AnimationTimelineInteraction.ResizeWindowEnd(.2f, .6f, -100f, 100f, 1f);
        AssertNear(start, .2f, "left edge remains stable during right resize");
        AssertNear(end, .2f, "right edge cannot cross left edge");
    }

    private static void AssertNear(float actual, float expected, string message)
    {
        if (MathF.Abs(actual - expected) > .0001f)
            throw new InvalidOperationException($"UX timeline: {message}. Expected {expected}, got {actual}.");
    }
}