namespace ByteEngine.Core.Graphics;

/// <summary>Browser presentation preferences. Never changes simulation or input timing.</summary>
public static class BrowserPresentationSettings
{
    public static GraphicsQuality Quality { get; set; } = GraphicsQuality.Balanced;
    public static string WorldResolution { get; set; } = "Auto";
}
