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
    private readonly bool _cpuOnly = OperatingSystem.IsBrowser();
    private byte[]? _pixels;
    readonly Queue<(int Version,int X,int Y,int Width,int Height)> _browserUpdates=new();
    public IReadOnlyList<(int X,int Y,int Width,int Height)> ChangedRegionsSince(int version){if(version<0||_browserUpdates.Count==0||version<_browserUpdates.Peek().Version-1)return [(0,0,Width,Height)];return _browserUpdates.Where(r=>r.Version>version).Select(r=>(r.X,r.Y,r.Width,r.Height)).ToArray();}
    void TrackBrowserRegion(int x,int y,int width,int height){if(!_cpuOnly)return;_browserUpdates.Enqueue((ContentVersion,x,y,width,height));while(_browserUpdates.Count>256)_browserUpdates.Dequeue();}
    public ReadOnlyMemory<byte> PixelData => _pixels ?? ReadOnlyMemory<byte>.Empty;
    public TextureFilter Filter { get; private set; }
    public bool CpuOnly => _cpuOnly;
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
    public int ContentVersion { get; private set; }

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

    /// <summary>Update a same-size dynamic RGBA map without recreating the GPU texture.</summary>
    public void UpdatePixels(byte[] pixels){if(_disposed)throw new ObjectDisposedException(nameof(Texture2D));if(pixels.Length!=Width*Height*4)throw new ArgumentException("RGBA map size differs",nameof(pixels));if(!_cpuOnly){Bind();GL.TexSubImage2D(TextureTarget.Texture2D,0,0,0,Width,Height,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);}_pixels=pixels;ContentVersion++;TrackBrowserRegion(0,0,Width,Height);}
    /// <summary>Upload only an RGBA rectangle from a full-sized CPU backing map. No staging allocation or GPU readback.</summary>
    public void UpdateRegion(byte[] pixels,int x,int y,int width,int height)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(pixels.Length!=checked(Width*Height*4)||x<0||y<0||width<=0||height<=0||x>Width-width||y>Height-height)throw new ArgumentException("Invalid texture update region");
        if(!_cpuOnly){
            Bind();GL.GetInteger(GetPName.UnpackRowLength,out int rowLength);GL.GetInteger(GetPName.UnpackSkipPixels,out int skipX);GL.GetInteger(GetPName.UnpackSkipRows,out int skipY);
            try{GL.PixelStore(PixelStoreParameter.UnpackRowLength,Width);GL.PixelStore(PixelStoreParameter.UnpackSkipPixels,x);GL.PixelStore(PixelStoreParameter.UnpackSkipRows,y);GL.TexSubImage2D(TextureTarget.Texture2D,0,x,y,width,height,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);}
            finally{GL.PixelStore(PixelStoreParameter.UnpackRowLength,rowLength);GL.PixelStore(PixelStoreParameter.UnpackSkipPixels,skipX);GL.PixelStore(PixelStoreParameter.UnpackSkipRows,skipY);}
        }
        _pixels=pixels;ContentVersion++;TrackBrowserRegion(x,y,width,height);
    }
    public static Texture2D FromPixels(int width, int height, byte[] pixels, TextureFilter filter = TextureFilter.Linear)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4))
            throw new ArgumentException("Expected width * height * 4 RGBA bytes.", nameof(pixels));
        return new Texture2D(width, height, pixels, filter);
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

    static (int Width,int Height,byte[] Data) BrowserImage(int width,int height,byte[] pixels){if(!OperatingSystem.IsBrowser()||Math.Max(width,height)<=1024)return(width,height,pixels);float scale=1024f/Math.Max(width,height);int w=Math.Max(1,(int)(width*scale)),h=Math.Max(1,(int)(height*scale));var output=new byte[w*h*4];for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels.AsSpan(((y*height/h)*width+x*width/w)*4,4).CopyTo(output.AsSpan((y*w+x)*4,4));return(w,h,output);}

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

        var resized=BrowserImage(image.Width,image.Height,image.Data);
        return new Texture2D(resized.Width,resized.Height,resized.Data,filter);
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

            Width=image.Width;Height=image.Height;

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

            var resized=BrowserImage(image.Width,image.Height,image.Data);
            Width=resized.Width;Height=resized.Height;

            FilePath =
                fullPath;

            IsHdr =
                false;

            UploadBytes(resized.Data,filter);
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
        Filter = filter;
        if (_cpuOnly) { Filter=filter;_pixels = pixels; return; }
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
        Filter = filter;
        if (_cpuOnly)
        {
            _pixels = pixels.Select(p => (byte)Math.Clamp((float.IsFinite(p) ? p : 0) * 255f, 0, 255)).ToArray();
            return;
        }
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

    public void EnableWorldSampling()
    {
        if (_cpuOnly || _disposed) return;
        GL.BindTexture(TextureTarget.Texture2D, _handle);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.BindTexture(TextureTarget.Texture2D, 0);
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

        if (_handle != 0) GL.DeleteTexture(
            _handle);
        _pixels = null;

        _handle =
            0;

        _disposed =
            true;
    }
}
