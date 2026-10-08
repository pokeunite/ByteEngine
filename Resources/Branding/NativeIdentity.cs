using System.Reflection;
using OpenTK.Windowing.Common.Input;
using StbImageSharp;
namespace ByteEngine.Branding;
internal static class NativeIdentity
{
    internal static WindowIcon? LoadWindowIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ByteEngine.Window.png");
        if (stream == null) return null;
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        return new WindowIcon(new OpenTK.Windowing.Common.Input.Image(image.Width, image.Height, image.Data));
    }
}
