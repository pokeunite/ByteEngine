using System.Buffers;

using ByteEngine.Core.Runtime;

using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics;

/// <summary>Captures only due frames into a fixed-size LDR target. Window resizes letterbox rather than corrupting the video stream.</summary>
public sealed class VideoFrameCapture : IDisposable
{
    private int _framebuffer, _texture, _width, _height;

    public void Capture(VideoRecorder recorder, int sourceFramebuffer, int width, int height)
    {
        if (!recorder.PrepareFrame(width, height, out var pixels) || pixels == null) return;
        GL.GetInteger(GetPName.ReadFramebufferBinding, out int oldRead);
        GL.GetInteger(GetPName.DrawFramebufferBinding, out int oldDraw);
        GL.GetInteger(GetPName.TextureBinding2D, out int oldTexture);
        GL.GetInteger(GetPName.PixelPackBufferBinding, out int oldPackBuffer);
        GL.GetInteger(GetPName.PackAlignment, out int oldAlignment);
        GL.GetInteger(GetPName.PackRowLength, out int oldRowLength);
        GL.GetInteger(GetPName.PackSkipRows, out int oldSkipRows);
        GL.GetInteger(GetPName.PackSkipPixels, out int oldSkipPixels);
        GL.GetInteger(GetPName.ReadBuffer, out int oldReadBuffer);
        GL.GetInteger(GetPName.DrawBuffer, out int oldDrawBuffer);
        bool scissor = GL.IsEnabled(EnableCap.ScissorTest);
        try
        {
            EnsureSize(recorder.Width, recorder.Height);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _framebuffer);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.Disable(EnableCap.ScissorTest);
            GL.ClearBuffer(ClearBuffer.Color, 0, new float[] { 0, 0, 0, 1 });
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, sourceFramebuffer);
            GL.ReadBuffer(sourceFramebuffer == 0 ? ReadBufferMode.Back : ReadBufferMode.ColorAttachment0);
            double fit = Math.Min((double)_width / width, (double)_height / height);
            int w = Math.Max(1, (int)(width * fit)), h = Math.Max(1, (int)(height * fit));
            int x = (_width - w) / 2, y = (_height - h) / 2;
            GL.BlitFramebuffer(0, 0, width, height, x, y, x + w, y + h, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _framebuffer);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, 0);
            GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
            GL.PixelStore(PixelStoreParameter.PackRowLength, 0);
            GL.PixelStore(PixelStoreParameter.PackSkipRows, 0);
            GL.PixelStore(PixelStoreParameter.PackSkipPixels, 0);
            GL.ReadPixels(0, 0, _width, _height, PixelFormat.Bgra, PixelType.UnsignedByte, pixels);
            recorder.Submit(pixels);
            pixels = null;
        }
        catch (Exception e) { recorder.Fail("Frame capture failed: " + e.Message); }
        finally
        {
            if (pixels != null) ArrayPool<byte>.Shared.Return(pixels);
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, oldRead);
            GL.ReadBuffer((ReadBufferMode)oldReadBuffer);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, oldDraw);
            GL.DrawBuffer((DrawBufferMode)oldDrawBuffer);
            GL.BindTexture(TextureTarget.Texture2D, oldTexture);
            GL.BindBuffer(BufferTarget.PixelPackBuffer, oldPackBuffer);
            GL.PixelStore(PixelStoreParameter.PackAlignment, oldAlignment);
            GL.PixelStore(PixelStoreParameter.PackRowLength, oldRowLength);
            GL.PixelStore(PixelStoreParameter.PackSkipRows, oldSkipRows);
            GL.PixelStore(PixelStoreParameter.PackSkipPixels, oldSkipPixels);
            if (scissor) GL.Enable(EnableCap.ScissorTest);
        }
    }

    private void EnsureSize(int width, int height)
    {
        if (_framebuffer != 0 && _width == width && _height == height) return;
        Dispose();
        _width = width;
        _height = height;
        _texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
        _framebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _framebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _texture, 0);
        if (GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer) != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException("Video capture framebuffer is incomplete.");
    }

    public void Dispose()
    {
        if (_framebuffer != 0) GL.DeleteFramebuffer(_framebuffer);
        if (_texture != 0) GL.DeleteTexture(_texture);
        _framebuffer = _texture = 0;
    }
}
