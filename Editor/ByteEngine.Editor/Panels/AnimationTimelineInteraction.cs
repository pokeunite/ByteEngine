namespace ByteEngine.Editor.Panels;

internal static class AnimationTimelineInteraction
{
    public const float DragThreshold = 4.0f;

    public static float MoveEvent(float originalTime, float deltaPixels, float width, float duration) =>
        Clamp(originalTime + PixelsToTime(deltaPixels, width, duration), duration);

    public static (float Start, float End) MoveWindow(
        float originalStart, float originalEnd, float deltaPixels, float width, float duration)
    {
        float length = Math.Clamp(originalEnd - originalStart, 0.0f, Math.Max(duration, 0.0f));
        float start = originalStart + PixelsToTime(deltaPixels, width, duration);
        start = Math.Clamp(start, 0.0f, Math.Max(duration - length, 0.0f));
        return (start, start + length);
    }

    public static (float Start, float End) ResizeWindowStart(
        float originalStart, float originalEnd, float deltaPixels, float width, float duration)
    {
        float start = Clamp(originalStart + PixelsToTime(deltaPixels, width, duration), duration);
        return (Math.Min(start, originalEnd), Clamp(originalEnd, duration));
    }

    public static (float Start, float End) ResizeWindowEnd(
        float originalStart, float originalEnd, float deltaPixels, float width, float duration)
    {
        float end = Clamp(originalEnd + PixelsToTime(deltaPixels, width, duration), duration);
        return (Clamp(originalStart, duration), Math.Max(end, originalStart));
    }

    private static float PixelsToTime(float pixels, float width, float duration) =>
        width > 0.0001f && duration > 0.0f ? pixels / width * duration : 0.0f;

    private static float Clamp(float time, float duration) =>
        Math.Clamp(float.IsFinite(time) ? time : 0.0f, 0.0f, Math.Max(duration, 0.0f));
}