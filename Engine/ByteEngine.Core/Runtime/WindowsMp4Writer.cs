using System.Runtime.InteropServices;

namespace ByteEngine.Core.Runtime;

/// <summary>Windows Media Foundation H.264 sink. All calls and releases belong to the encoder's MTA thread.</summary>
internal sealed unsafe class WindowsMp4Writer : IDisposable
{
    // ABI slots come from IMFAttributes, IMFSample, IMFMediaBuffer and IMFSinkWriter in the Windows SDK.
    // https://learn.microsoft.com/windows/win32/medfound/tutorial--using-the-sink-writer-to-encode-video
    private nint _writer;
    private uint _stream;
    private bool _comStarted;
    private bool _mfStarted;
    private readonly int _length;
    private uint _audioStream;
    private long _audioFrames;
    private readonly byte[] _silence = new byte[4800 * 4];

    internal WindowsMp4Writer(string path, int width, int height, int fps, bool audio = true, int bitrate = 25_000_000)
    {
        _length = checked(width * height * 4);
        try
        {
            Check(CoInitializeEx(0, 0));
            _comStarted = true;
            Check(MFStartup(0x20070, 0));
            _mfStarted = true;
            Check(MFCreateSinkWriterFromURL(path, 0, 0, out _writer));
            nint output = 0, input = 0, encoding = 0;
            try
            {
                Check(MFCreateMediaType(out output));
                Configure(output, width, height, fps, new Guid("34363248-0000-0010-8000-00aa00389b71"));
                Set32(output, new("20332624-fb0d-4d9e-bd0d-cbf6786c102e"), (uint)Math.Clamp(bitrate, 1_000_000, 50_000_000));
                uint stream;
                Check(((delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)V(_writer)[3])(_writer, output, &stream));
                _stream = stream;
                Check(MFCreateMediaType(out input));
                Configure(input, width, height, fps, new Guid("00000016-0000-0010-8000-00aa00389b71"));
                Set32(input, new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6"), (uint)(width * 4));
                Check(MFCreateAttributes(out encoding, 2));
                Set32(encoding, new("1c0608e9-370c-4710-8a58-cb6181c42423"), 0); // Explicit constant bitrate rate control.
                Set32(encoding, new("f7222374-2144-4815-b550-a37f8e12ee52"), (uint)Math.Clamp(bitrate, 1_000_000, 50_000_000));
                Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)V(_writer)[4])(_writer, _stream, input, encoding));
                if (audio) ConfigureAudio();
                Check(((delegate* unmanaged[Stdcall]<nint, int>)V(_writer)[5])(_writer));
            }
            finally
            {
                Release(encoding);
                Release(input);
                Release(output);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private static void Configure(nint type, int width, int height, int fps, Guid subtype)
    {
        SetGuid(type, new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"), new("73646976-0000-0010-8000-00aa00389b71"));
        SetGuid(type, new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"), subtype);
        Set32(type, new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd"), 2); // Progressive.
        Set64(type, new("1652c33d-d6b2-4012-b834-72030849a37d"), ((ulong)width << 32) | (uint)height);
        Set64(type, new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0"), ((ulong)fps << 32) | 1);
        Set64(type, new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6"), (1UL << 32) | 1);
    }

    internal void Write(byte[] topDownBgra, long time, long duration)
        => WriteSample(topDownBgra, 0, _length, _stream, time, duration);

    private void WriteSample(byte[] bytes, int offset, int length, uint stream, long time, long duration)
    {
        nint buffer = 0, sample = 0;
        try
        {
            Check(MFCreateMemoryBuffer(length, out buffer));
            nint pixels;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, nint, nint, int>)V(buffer)[3])(buffer, &pixels, 0, 0));
            try { Marshal.Copy(bytes, offset, pixels, length); }
            finally { Check(((delegate* unmanaged[Stdcall]<nint, int>)V(buffer)[4])(buffer)); }
            Check(((delegate* unmanaged[Stdcall]<nint, int, int>)V(buffer)[6])(buffer, length));
            Check(MFCreateSample(out sample));
            Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)V(sample)[42])(sample, buffer));
            Check(((delegate* unmanaged[Stdcall]<nint, long, int>)V(sample)[36])(sample, time));
            Check(((delegate* unmanaged[Stdcall]<nint, long, int>)V(sample)[38])(sample, Math.Max(1, duration)));
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)V(_writer)[6])(_writer, stream, sample));
        }
        finally
        {
            Release(sample);
            Release(buffer);
        }
    }

    private void ConfigureAudio()
    {
        nint output = 0, input = 0;
        try
        {
            Check(MFCreateMediaType(out output));
            AudioType(output, new("00001610-0000-0010-8000-00aa00389b71"));
            Set32(output, new("1aab75c8-cfef-451c-ab95-ac034b8e1731"), 24000); // AAC 192 kb/s.
            Set32(output, new("bfbabe79-7434-4d1c-94f0-72a3b9e17188"), 0);
            uint stream;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, uint*, int>)V(_writer)[3])(_writer, output, &stream));
            _audioStream = stream;
            Check(MFCreateMediaType(out input));
            AudioType(input, new("00000001-0000-0010-8000-00aa00389b71"));
            Set32(input, new("322de230-9eeb-43bd-ab7a-ff412251541d"), 4);
            Set32(input, new("1aab75c8-cfef-451c-ab95-ac034b8e1731"), 192000);
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)V(_writer)[4])(_writer, _audioStream, input, 0));
        }
        finally { Release(input); Release(output); }
    }

    private static void AudioType(nint type, Guid subtype)
    {
        SetGuid(type, new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f"), new("73647561-0000-0010-8000-00aa00389b71"));
        SetGuid(type, new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5"), subtype);
        Set32(type, new("37e48bf5-645e-4c5b-89de-ada9e29b696a"), 2);
        Set32(type, new("5faeeae7-0290-4c31-9e8a-c534f68d9dba"), 48000);
        Set32(type, new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669"), 16);
    }

    internal void Audio(byte[] bytes, int length, long time, long limit)
    {
        long first = time * 48000 / TimeSpan.TicksPerSecond;
        long end = Math.Min(first + length / 4, limit * 48000 / TimeSpan.TicksPerSecond);
        if (end <= _audioFrames) return;
        PadAudio(Math.Max(0, Math.Min(first, end)));
        int skip = checked((int)Math.Max(0, _audioFrames - first));
        int count = checked((int)(end - _audioFrames));
        WriteAudio(bytes, skip * 4, count);
    }

    internal void PadAudioTo(long time) => PadAudio(Math.Max(0, time * 48000 / TimeSpan.TicksPerSecond));
    private void PadAudio(long frames)
    {
        while (_audioFrames < frames) WriteAudio(_silence, 0, (int)Math.Min(4800, frames - _audioFrames));
    }

    private void WriteAudio(byte[] bytes, int offset, int frames)
    {
        if (frames <= 0) return;
        long time = _audioFrames * TimeSpan.TicksPerSecond / 48000;
        long end = (_audioFrames + frames) * TimeSpan.TicksPerSecond / 48000;
        WriteSample(bytes, offset, frames * 4, _audioStream, time, end - time);
        _audioFrames += frames;
    }

    internal void Complete() => Check(((delegate* unmanaged[Stdcall]<nint, int>)V(_writer)[11])(_writer));

    public void Dispose()
    {
        Release(_writer);
        _writer = 0;
        if (_mfStarted) { MFShutdown(); _mfStarted = false; }
        if (_comStarted) { CoUninitialize(); _comStarted = false; }
    }

    private static void** V(nint value) => *(void***)value;
    private static void Check(int result) => Marshal.ThrowExceptionForHR(result);
    private static void Release(nint value) { if (value != 0) Marshal.Release(value); }
    private static void SetGuid(nint obj, Guid key, Guid value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, Guid*, int>)V(obj)[24])(obj, &key, &value));
    private static void Set32(nint obj, Guid key, uint value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, int>)V(obj)[21])(obj, &key, value));
    private static void Set64(nint obj, Guid key, ulong value) => Check(((delegate* unmanaged[Stdcall]<nint, Guid*, ulong, int>)V(obj)[22])(obj, &key, value));

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("mfplat.dll")] private static extern int MFStartup(uint version, uint flags);
    [DllImport("mfplat.dll")] private static extern int MFShutdown();
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out nint type);
    [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out nint attributes, uint count);
    [DllImport("mfplat.dll")] private static extern int MFCreateMemoryBuffer(int size, out nint buffer);
    [DllImport("mfplat.dll")] private static extern int MFCreateSample(out nint sample);
    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)] private static extern int MFCreateSinkWriterFromURL(string path, nint stream, nint attributes, out nint writer);
}
