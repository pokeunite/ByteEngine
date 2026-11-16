using System.Diagnostics;
using System.Runtime.InteropServices;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
using ByteEngine.Editor;
using ByteEngine.Editor.Panels;

using ImGuiNET;

using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

internal static class VideoRecorderTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    internal static void Run()
    {
        string output = Path.GetFullPath(".artifacts/video-recorder");
        Directory.CreateDirectory(output);
        string settingsFile = Path.Combine(output, "preferences.json");
        var settings = VideoRecorderSettings.Load(settingsFile + ".missing");
        Check(settings.OutputDirectory == VideoRecorderSettings.DefaultOutputDirectory && settings.IncludeAudio, "Default output follows Windows Videos, with playback audio enabled");
        Check(settings.TrySave(Path.Combine(output, "chosen folder"), settingsFile, out _), "Custom output folder is writable and persisted");
        Check(VideoRecorderSettings.Load(settingsFile).OutputDirectory == settings.OutputDirectory, "Settings survive a new editor/player session");
        string previous = settings.OutputDirectory;
        string blocked = Path.Combine(output, "not-a-directory");
        File.WriteAllText(blocked, "test");
        Check(!settings.TrySave(blocked, settingsFile, out _) && settings.OutputDirectory == previous, "Invalid output folder reports an error without replacing the preference");
        using var window = new GameWindow(GameWindowSettings.Default, new NativeWindowSettings
        {
            ClientSize = new(820, 480), StartVisible = false, API = ContextAPI.OpenGL, APIVersion = new(3, 3), Profile = ContextProfile.Core
        });
        window.Context.MakeCurrent();
        GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var recorder = new VideoRecorder();
        recorder.Settings.OutputDirectory = output;
        recorder.Settings.IncludeAudio = true;
        using var capture = new VideoFrameCapture();
        recorder.Toggle();
        string tone = Path.Combine(output, "quiet-test-tone.wav");
        WriteTone(tone);
        Check(PlaySound(tone, 0, 0x20009), "Quiet test tone plays through the default Windows playback device");
        recorder.Toggle();
        Check(recorder.State == VideoRecorderState.Idle, "Shortcut toggle can cancel before the first rendered frame");
        recorder.Toggle();
        int source = GL.GenFramebuffer(), texture = GL.GenTexture();
        try
        {
            for (int frame = 0; frame < 36; frame++)
            {
                if (frame == 10) Thread.Sleep(800); // Simulate a modal settings dialog / stalled render loop while audio keeps playing.
                int width = frame < 18 ? 160 : 120, height = frame < 18 ? 90 : 180;
                GL.BindTexture(TextureTarget.Texture2D, texture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, source);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
                GL.Viewport(0, 0, width, height);
                GL.Disable(EnableCap.ScissorTest);
                GL.ClearColor(0, 0, 1, 1);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                GL.Enable(EnableCap.ScissorTest);
                GL.Scissor(0, height / 2, width, height - height / 2);
                GL.ClearColor(1, 0, 0, 1);
                GL.Clear(ClearBufferMask.ColorBufferBit);
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
                capture.Capture(recorder, source, width, height);
                GL.GetInteger(GetPName.ReadFramebufferBinding, out int read);
                GL.GetInteger(GetPName.DrawFramebufferBinding, out int draw);
                Check(read == 0 && draw == 0 && GL.IsEnabled(EnableCap.ScissorTest), "Capture preserves the caller's framebuffer and scissor state");
                Thread.Sleep(35);
            }
            var stop = recorder.StopAsync();
            Check(stop.Wait(TimeSpan.FromSeconds(20)), "Encoder drains and finalizes without blocking indefinitely");
            Check(recorder.State == VideoRecorderState.Idle, recorder.Message);
            Check(recorder.Width == 160 && recorder.Height == 90 && GL.GetError() == ErrorCode.NoError, "Resize preserves fixed recording dimensions without OpenGL errors");
            string file = recorder.LastFile!;
            Check(File.Exists(file) && new FileInfo(file).Length > 500, "A real finalized MP4 exists in the chosen folder");
            Decode(file);
            File.WriteAllText(Path.Combine(output, "recording-result.txt"), recorder.Message + "\nSkipped frames: " + recorder.DroppedFrames);
            recorder.Settings.IncludeAudio = false;
            recorder.Toggle();
            GL.Disable(EnableCap.ScissorTest);
            GL.ClearColor(0, .8f, 0, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.PixelStore(PixelStoreParameter.PackSkipRows, 2);
            GL.PixelStore(PixelStoreParameter.PackSkipPixels, 3);
            capture.Capture(recorder, 0, 820, 480);
            GL.GetInteger(GetPName.PackSkipRows, out int skipRows);
            GL.GetInteger(GetPName.PackSkipPixels, out int skipPixels);
            Check(skipRows == 2 && skipPixels == 3, "Window capture restores pixel-pack offsets and prevents out-of-bounds readback");
            GL.PixelStore(PixelStoreParameter.PackSkipRows, 0);
            GL.PixelStore(PixelStoreParameter.PackSkipPixels, 0);
            recorder.Dispose();
            Check(File.Exists(recorder.LastFile) && new FileInfo(recorder.LastFile!).Length > 100, "Application shutdown finalizes a video-only window recording");
            recorder.Settings.IncludeAudio = true;
        }
        finally
        {
            PlaySound(null, 0, 0);
            GL.Disable(EnableCap.ScissorTest);
            GL.DeleteFramebuffer(source);
            GL.DeleteTexture(texture);
        }
        using var failure = new VideoRecorder();
        failure.Settings.OutputDirectory = blocked;
        failure.Toggle();
        capture.Capture(failure, 0, 320, 240);
        failure.StopAsync().Wait(TimeSpan.FromSeconds(10));
        Check(failure.State == VideoRecorderState.Error && failure.Message.Contains("failed", StringComparison.OrdinalIgnoreCase), "Filesystem/encoder failures reach the visible error state");
        using var gui = new ImGuiController(window, Path.Combine(output, "test-layout.ini"));
        var io = ImGui.GetIO();
        io.DisplaySize = new(820, 480);
        io.DeltaTime = 1f / 60;
        var panel = new VideoRecorderPanel { IsOpen = true };
        for (int i = 0; i < 2; i++)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, 820, 480);
            GL.ClearColor(.06f, .07f, .09f, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(new(20, 20));
            panel.Draw(recorder, () => { });
            gui.Render();
        }
        var screenshot = new byte[820 * 480 * 4];
        GL.ReadPixels(0, 0, 820, 480, PixelFormat.Rgba, PixelType.UnsignedByte, screenshot);
        RoadmapUiVisualTests.Png(Path.Combine(output, "recorder-settings.png"), screenshot, 820, 480);
        Check(GL.GetError() == ErrorCode.NoError, "Recorder settings render in the native editor UI");
    }

    private static void Decode(string path)
    {
        CheckHr(CoInitializeEx(0, 0));
        CheckHr(MFStartup(0x20070, 0));
        nint attributes = 0, reader = 0, type = 0;
        try
        {
            CheckHr(MFCreateAttributes(out attributes, 1));
            Guid videoProcessing = new("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
            CheckHr(Call<Set32>(attributes, 21)(attributes, in videoProcessing, 1));
            CheckHr(MFCreateSourceReaderFromURL(path, attributes, out reader));
            CheckHr(MFCreateMediaType(out type));
            Guid major = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"), video = new("73646976-0000-0010-8000-00aa00389b71");
            Guid subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"), rgb = new("00000016-0000-0010-8000-00aa00389b71");
            CheckHr(Call<SetGuid>(type, 24)(type, in major, in video));
            CheckHr(Call<SetGuid>(type, 24)(type, in subtype, in rgb));
            const uint stream = 0xfffffffc;
            CheckHr(Call<SetMediaType>(reader, 7)(reader, stream, 0, type));
            Marshal.Release(type);
            type = 0;
            CheckHr(Call<GetMediaType>(reader, 6)(reader, stream, out type));
            Guid size = new("1652c33d-d6b2-4012-b834-72030849a37d"), strideKey = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
            CheckHr(Call<Get64>(type, 8)(type, in size, out ulong dimensions));
            Check(dimensions == ((160UL << 32) | 90), "Windows' decoder reads the expected 160 x 90 video stream");
            CheckHr(Call<Get32>(type, 7)(type, in strideKey, out uint unsignedStride));
            int stride = unchecked((int)unsignedStride), count = 0;
            long lastTime = -1;
            bool checkedFirst = false, checkedResize = false;
            for (int i = 0; i < 200; i++)
            {
                CheckHr(Call<ReadSample>(reader, 9)(reader, stream, 0, out _, out uint flags, out long time, out nint sample));
                if (sample != 0)
                {
                    try
                    {
                        Check(time > lastTime, "Encoded frame timestamps remain monotonic");
                        lastTime = time;
                        count++;
                        nint buffer = 0;
                        try
                        {
                            CheckHr(Call<GetBuffer>(sample, 41)(sample, out buffer));
                            CheckHr(Call<LockBuffer>(buffer, 3)(buffer, out nint pointer, out _, out int length));
                            try
                            {
                                var data = new byte[length];
                                Marshal.Copy(pointer, data, 0, length);
                                int Pixel(int x, int y) => (stride < 0 ? 89 - y : y) * Math.Abs(stride) + x * 4;
                                if (!checkedFirst)
                                {
                                    int top = Pixel(80, 15), bottom = Pixel(80, 75);
                                    Check(data[top + 2] > 180 && data[top] < 70 && data[bottom] > 180 && data[bottom + 2] < 70,
                                        "Decoded MP4 preserves red/blue channels and upright orientation");
                                    checkedFirst = true;
                                }
                                int side = Pixel(10, 45);
                                if (time > 7_000_000 && data[side] < 30 && data[side + 1] < 30 && data[side + 2] < 30) checkedResize = true;
                            }
                            finally { CheckHr(Call<NoArgs>(buffer, 4)(buffer)); }
                        }
                        finally { if (buffer != 0) Marshal.Release(buffer); }
                    }
                    finally { Marshal.Release(sample); }
                }
                if ((flags & 2) != 0) break;
            }
            Check(count >= 8 && lastTime >= 8_000_000, "Finalized file decodes multiple frames over real elapsed recording time");
            Check(checkedResize, "Portrait resize is letterboxed inside the existing video stream");
            Marshal.Release(reader);
            reader = 0;
            CheckHr(MFCreateSourceReaderFromURL(path, attributes, out reader));
            Marshal.Release(type);
            type = 0;
            CheckHr(MFCreateMediaType(out type));
            Guid audioMajor = new("73647561-0000-0010-8000-00aa00389b71"), pcm = new("00000001-0000-0010-8000-00aa00389b71");
            CheckHr(Call<SetGuid>(type, 24)(type, in major, in audioMajor));
            CheckHr(Call<SetGuid>(type, 24)(type, in subtype, in pcm));
            SetAudio(type, new("37e48bf5-645e-4c5b-89de-ada9e29b696a"), 2);
            SetAudio(type, new("5faeeae7-0290-4c31-9e8a-c534f68d9dba"), 48000);
            SetAudio(type, new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669"), 16);
            SetAudio(type, new("322de230-9eeb-43bd-ab7a-ff412251541d"), 4);
            SetAudio(type, new("1aab75c8-cfef-451c-ab95-ac034b8e1731"), 192000);
            const uint audioStream = 0xfffffffd;
            CheckHr(Call<SetMediaType>(reader, 7)(reader, audioStream, 0, type));
            int peak = 0, audioSamples = 0;
            long audioEnd = 0;
            for (int i = 0; i < 200; i++)
            {
                CheckHr(Call<ReadSample>(reader, 9)(reader, audioStream, 0, out _, out uint flags, out long time, out nint sample));
                if (sample != 0)
                {
                    nint buffer = 0;
                    try
                    {
                        CheckHr(Call<GetBuffer>(sample, 41)(sample, out buffer));
                        CheckHr(Call<LockBuffer>(buffer, 3)(buffer, out nint pointer, out _, out int length));
                        try
                        {
                            byte[] data = new byte[length];
                            Marshal.Copy(pointer, data, 0, length);
                            for (int offset = 0; offset + 1 < length; offset += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(data, offset)));
                            audioSamples += length / 4;
                            audioEnd = time + (long)(length / 4) * TimeSpan.TicksPerSecond / 48000;
                        }
                        finally { CheckHr(Call<NoArgs>(buffer, 4)(buffer)); }
                    }
                    finally
                    {
                        if (buffer != 0) Marshal.Release(buffer);
                        Marshal.Release(sample);
                    }
                }
                if ((flags & 2) != 0) break;
            }
            Check(peak > 50 && audioSamples > 30000, $"AAC track decodes captured playback audio (peak {peak}, stereo frames {audioSamples})");
            Check(Math.Abs(audioEnd - lastTime) < 2_000_000, "Audio/video durations remain aligned within 200 ms including AAC padding");
        }
        finally
        {
            if (type != 0) Marshal.Release(type);
            if (reader != 0) Marshal.Release(reader);
            if (attributes != 0) Marshal.Release(attributes);
            MFShutdown();
            CoUninitialize();
        }
    }

    private static void SetAudio(nint type, Guid key, uint value) => CheckHr(Call<Set32>(type, 21)(type, in key, value));

    private static void WriteTone(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        const int frames = 48000 * 3, bytes = frames * 4;
        writer.Write("RIFF"u8); writer.Write(36 + bytes); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((ushort)1); writer.Write((ushort)2); writer.Write(48000); writer.Write(192000); writer.Write((ushort)4); writer.Write((ushort)16);
        writer.Write("data"u8); writer.Write(bytes);
        for (int i = 0; i < frames; i++)
        {
            short sample = (short)(Math.Sin(i * 2 * Math.PI * 440 / 48000) * 1200);
            writer.Write(sample); writer.Write(sample);
        }
    }

    private static T Call<T>(nint obj, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size));
    private static void CheckHr(int result) => Marshal.ThrowExceptionForHR(result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Set32(nint obj, in Guid key, uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetGuid(nint obj, in Guid key, in Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Get32(nint obj, in Guid key, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Get64(nint obj, in Guid key, out ulong value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetMediaType(nint obj, uint stream, nint reserved, nint type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetMediaType(nint obj, uint stream, out nint type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ReadSample(nint obj, uint stream, uint control, out uint actual, out uint flags, out long time, out nint sample);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBuffer(nint sample, out nint buffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockBuffer(nint buffer, out nint data, out int maximum, out int length);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int NoArgs(nint obj);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("mfplat.dll")] private static extern int MFStartup(uint version, uint flags);
    [DllImport("mfplat.dll")] private static extern int MFShutdown();
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out nint type);
    [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out nint attributes, uint count);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)] private static extern int MFCreateSourceReaderFromURL(string path, nint attributes, out nint reader);
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)] private static extern bool PlaySound(string? file, nint module, uint flags);
}
