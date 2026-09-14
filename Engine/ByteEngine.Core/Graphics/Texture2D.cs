using OpenTK.Graphics.OpenGL4;
using StbImageSharp;

namespace ByteEngine.Core.Graphics;

public enum TextureFilter
{
    Nearest,
    Linear
}

public sealed class Texture2D
    : IDisposable
{
    private int _handle;

    private bool _disposed;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public string? FilePath { get; private set; }

    /// <summary>
    /// True when the source image was loaded through the floating-point HDR
    /// path and uploaded without first quantizing it to 8-bit color.
    /// </summary>
    public bool IsHdr { get; private set; }

    /// <summary>
    /// Increments whenever the GPU content backing this texture changes.
    /// Environment IBL uses this to rebuild processed lighting only when needed.
    /// </summary>
    internal int ContentVersion { get; private set; }

    public Texture2D(
        string filePath,
        TextureFilter filter = TextureFilter.Nearest)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"ByteEngine could not find texture: {filePath}",
                filePath
            );
        }

        LoadFile(
            filePath,
            filter,
            replacingExistingHandle: false
        );

        Console.WriteLine(
            $"Texture loaded: {Path.GetFileName(filePath)} " +
            $"({Width}x{Height}" +
            (IsHdr ? ", HDR" : string.Empty) +
            ")"
        );
    }

    private Texture2D(
        int width,
        int height,
        byte[] pixels,
        TextureFilter filter)
    {
        Width = width;
        Height = height;
        FilePath = null;
        IsHdr = false;

        UploadBytes(
            pixels,
            filter
        );
    }

    internal static Texture2D CreateMissingTexture()
    {
        const int size = 8;

        byte[] pixels =
            new byte[
                size *
                size *
                4
            ];

        for (int y = 0;
             y < size;
             y++)
        {
            for (int x = 0;
                 x < size;
                 x++)
            {
                bool magenta =
                    (
                        x < size / 2
                    ) ==
                    (
                        y < size / 2
                    );

                int index =
                    (
                        y *
                        size +
                        x
                    ) *
                    4;

                pixels[index] =
                    magenta
                        ? (byte)255
                        : (byte)20;

                pixels[index + 1] =
                    magenta
                        ? (byte)0
                        : (byte)20;

                pixels[index + 2] =
                    magenta
                        ? (byte)255
                        : (byte)20;

                pixels[index + 3] =
                    255;
            }
        }

        return new Texture2D(
            size,
            size,
            pixels,
            TextureFilter.Nearest
        );
    }

    internal static Texture2D FromEncodedBytes(
        byte[] encodedData,
        TextureFilter filter = TextureFilter.Linear)
    {
        ArgumentNullException.ThrowIfNull(encodedData);

        using var stream =
            new MemoryStream(
                encodedData,
                false);

        ImageResult image =
            ImageResult.FromStream(
                stream,
                ColorComponents.RedGreenBlueAlpha);

        return new Texture2D(
            image.Width,
            image.Height,
            image.Data,
            filter);
    }

    internal void Reload(
        string filePath,
        TextureFilter filter)
    {
        LoadFile(
            filePath,
            filter,
            replacingExistingHandle: true
        );
    }

    internal void ReloadEncoded(
        byte[] encodedData,
        TextureFilter filter = TextureFilter.Linear)
    {
        using var stream =
            new MemoryStream(
                encodedData,
                false);

        ImageResult image =
            ImageResult.FromStream(
                stream,
                ColorComponents.RedGreenBlueAlpha);

        ReplacePixels(
            image.Width,
            image.Height,
            image.Data,
            filter,
            null);
    }

    internal void ReplaceWithMissing()
    {
        const int size = 8;

        byte[] pixels =
            CreateMissingPixels(
                size);

        ReplacePixels(
            size,
            size,
            pixels,
            TextureFilter.Nearest,
            null);
    }

    private void LoadFile(
        string filePath,
        TextureFilter filter,
        bool replacingExistingHandle)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(Texture2D));
        }

        string fullPath =
            Path.GetFullPath(
                filePath);

        bool isHdr =
            string.Equals(
                Path.GetExtension(
                    fullPath),
                ".hdr",
                StringComparison.OrdinalIgnoreCase);

        int previousHandle =
            replacingExistingHandle
                ? _handle
                : 0;

        using FileStream stream =
            File.OpenRead(
                fullPath);

        if (isHdr)
        {
            ImageResultFloat image =
                ImageResultFloat.FromStream(
                    stream,
                    ColorComponents.RedGreenBlueAlpha);

            Width =
                image.Width;

            Height =
                image.Height;

            FilePath =
                fullPath;

            IsHdr =
                true;

            UploadFloats(
                image.Data,
                filter);
        }
        else
        {
            ImageResult image =
                ImageResult.FromStream(
                    stream,
                    ColorComponents.RedGreenBlueAlpha);

            Width =
                image.Width;

            Height =
                image.Height;

            FilePath =
                fullPath;

            IsHdr =
                false;

            UploadBytes(
                image.Data,
                filter);
        }

        if (previousHandle != 0)
        {
            GL.DeleteTexture(
                previousHandle);
        }

        ContentVersion++;
    }

    private void ReplacePixels(
        int width,
        int height,
        byte[] pixels,
        TextureFilter filter,
        string? filePath)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(Texture2D));
        }

        int previous =
            _handle;

        Width =
            width;

        Height =
            height;

        FilePath =
            filePath;

        IsHdr =
            false;

        UploadBytes(
            pixels,
            filter);

        if (previous != 0)
        {
            GL.DeleteTexture(
                previous);
        }

        ContentVersion++;
    }

    private static byte[] CreateMissingPixels(
        int size)
    {
        byte[] pixels =
            new byte[
                size *
                size *
                4];

        for (int y = 0;
             y < size;
             y++)
        {
            for (int x = 0;
                 x < size;
                 x++)
            {
                bool magenta =
                    (
                        x <
                        size / 2) ==
                    (
                        y <
                        size / 2);

                int index =
                    (
                        y *
                        size +
                        x) *
                    4;

                pixels[index] =
                    magenta
                        ? (byte)255
                        : (byte)20;

                pixels[index + 1] =
                    magenta
                        ? (byte)0
                        : (byte)20;

                pixels[index + 2] =
                    magenta
                        ? (byte)255
                        : (byte)20;

                pixels[index + 3] =
                    255;
            }
        }

        return pixels;
    }

    private void UploadBytes(
        byte[] pixels,
        TextureFilter filter)
    {
        _handle =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.Texture2D,
            _handle);

        ConfigurePixelStore();

        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba8,
            Width,
            Height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            pixels);

        ConfigureSampling(
            filter);

        GL.BindTexture(
            TextureTarget.Texture2D,
            0);
    }

    private void UploadFloats(
        float[] pixels,
        TextureFilter filter)
    {
        _handle =
            GL.GenTexture();

        GL.BindTexture(
            TextureTarget.Texture2D,
            _handle);

        ConfigurePixelStore();

        /*
         * RGBA16F preserves HDR range while using half the storage of
         * RGBA32F. Source data remains float on upload; the GPU performs
         * the conversion to 16-bit floating point.
         */
        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba16f,
            Width,
            Height,
            0,
            PixelFormat.Rgba,
            PixelType.Float,
            pixels);

        ConfigureSampling(
            filter);

        GL.BindTexture(
            TextureTarget.Texture2D,
            0);
    }

    private static void ConfigurePixelStore()
    {
        GL.PixelStore(
            PixelStoreParameter.UnpackAlignment,
            1);
    }

    private static void ConfigureSampling(
        TextureFilter filter)
    {
        TextureMinFilter minFilter =
            filter ==
                TextureFilter.Nearest
                ? TextureMinFilter.Nearest
                : TextureMinFilter.Linear;

        TextureMagFilter magFilter =
            filter ==
                TextureFilter.Nearest
                ? TextureMagFilter.Nearest
                : TextureMagFilter.Linear;

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter,
            (int)minFilter);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter,
            (int)magFilter);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS,
            (int)TextureWrapMode.ClampToEdge);

        GL.TexParameter(
            TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT,
            (int)TextureWrapMode.ClampToEdge);
    }

    internal void Bind(
        int slot = 0)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(Texture2D));
        }

        GL.ActiveTexture(
            (TextureUnit)(
                (int)TextureUnit.Texture0 +
                slot));

        GL.BindTexture(
            TextureTarget.Texture2D,
            _handle);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        GL.DeleteTexture(
            _handle);

        _handle =
            0;

        _disposed =
            true;
    }
}
