using System.Runtime.CompilerServices;

namespace ByteEngine.Tests;

internal static class C10MilestoneTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        C10AnimationEventsTests.Run();
        C10AnimationWindowTests.Run();
        C10AnimationMetadataTests.Run();
        C10EAnimationTimelineEditorTests.Run();
        C10FAnimationVisualLogicTests.Run();
        C10FAnimationSignalAuthoringResolverTests.Run();
        C10FAudioPlayClipTests.Run();
        C10GAnimationMilestoneClosureTests.Run();
    }
}

