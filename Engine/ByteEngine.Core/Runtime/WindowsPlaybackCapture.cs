using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ByteEngine.Core.Runtime;

/// <summary>WASAPI shared-mode loopback of the default playback device. No microphone capture or playback-device changes.</summary>
internal sealed unsafe class WindowsPlaybackCapture : IDisposable
{
    private nint _client, _capture;
    private byte[] _packet = [];
    private bool _comStarted;
    private long _timestampCorrection;
    private bool _timestampAnchored;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort Format, Channels;
        public uint SampleRate, BytesPerSecond;
        public ushort BlockAlignment, BitsPerSample, ExtraSize;
    }

    internal WindowsPlaybackCapture()
    {
        nint enumerator = 0, device = 0;
        try
        {
            Check(CoInitializeEx(0, 0));
            _comStarted = true;
            Guid cls = new("bcde0395-e52f-467c-8e3d-c4579291692e"), iid = new("a95664d2-9614-4f35-a746-de8db63617e6");
            Check(CoCreateInstance(in cls, 0, 23, in iid, out enumerator));
            nint endpoint;
            Check(((delegate* unmanaged[Stdcall]<nint, int, int, nint*, int>)V(enumerator)[4])(enumerator, 0, 1, &endpoint));
            device = endpoint;
            Guid audioClient = new("1cb9ad4c-dbfa-4c32-b178-c2f568a703b2");
            nint client;
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, uint, nint, nint*, int>)V(device)[3])(device, &audioClient, 23, 0, &client));
            _client = client;
            var format = new WaveFormat { Format = 1, Channels = 2, SampleRate = 48000, BytesPerSecond = 192000, BlockAlignment = 4, BitsPerSample = 16 };
            // Loopback + automatic PCM conversion + high-quality resampling. Request a stable AAC-compatible format.
            Check(((delegate* unmanaged[Stdcall]<nint, int, uint, long, long, WaveFormat*, nint, int>)V(_client)[3])
                (_client, 0, 0x88020000, 5_000_000, 0, &format, 0));
            Guid captureId = new("c8adbd64-e71e-48a0-a4de-185c395cd317");
            nint capture;
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)V(_client)[14])(_client, &captureId, &capture));
            _capture = capture;
            Check(((delegate* unmanaged[Stdcall]<nint, int>)V(_client)[10])(_client));
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            if (device != 0) Marshal.Release(device);
            if (enumerator != 0) Marshal.Release(enumerator);
        }
    }

    internal void Drain(Action<byte[], int, long> write)
    {
        uint available;
        Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)V(_capture)[5])(_capture, &available));
        while (available > 0)
        {
            nint data;
            uint frames, flags;
            ulong position, qpc;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, uint*, uint*, ulong*, ulong*, int>)V(_capture)[3])
                (_capture, &data, &frames, &flags, &position, &qpc));
            try
            {
                int bytes = checked((int)frames * 4);
                if (_packet.Length < bytes) _packet = new byte[bytes];
                if ((flags & 2) != 0) _packet.AsSpan(0, bytes).Clear();
                else Marshal.Copy(data, _packet, 0, bytes);
                long now = (long)(Stopwatch.GetTimestamp() * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency);
                long time = (flags & 4) == 0 ? (long)qpc : now - frames * TimeSpan.TicksPerSecond / 48000;
                // Some virtual/remote playback drivers report unflagged future QPC timestamps.
                // Calibrate that device once against packet receipt, then preserve its sample timing.
                if (!_timestampAnchored && (flags & 4) == 0)
                {
                    if (time > now + TimeSpan.TicksPerMillisecond * 20 || time < now - TimeSpan.TicksPerSecond * 2)
                        _timestampCorrection = now - frames * TimeSpan.TicksPerSecond / 48000 - time;
                    _timestampAnchored = true;
                }
                if ((flags & 4) == 0) time += _timestampCorrection;
                write(_packet, bytes, time);
            }
            finally { Check(((delegate* unmanaged[Stdcall]<nint, uint, int>)V(_capture)[4])(_capture, frames)); }
            Check(((delegate* unmanaged[Stdcall]<nint, uint*, int>)V(_capture)[5])(_capture, &available));
        }
    }

    public void Dispose()
    {
        if (_client != 0) ((delegate* unmanaged[Stdcall]<nint, int>)V(_client)[11])(_client);
        if (_capture != 0) Marshal.Release(_capture);
        if (_client != 0) Marshal.Release(_client);
        _capture = _client = 0;
        if (_comStarted) { CoUninitialize(); _comStarted = false; }
    }

    private static void** V(nint value) => *(void***)value;
    private static void Check(int result) => Marshal.ThrowExceptionForHR(result);
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid cls, nint outer, uint context, in Guid iid, out nint instance);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
}
