using ByteEngine.Core.Diagnostics;

using OpenTK.Audio.OpenAL;

namespace ByteEngine.Core.Audio;

/// <summary>
/// Process-wide OpenAL device/context used by ByteEngine audio components.
///
/// The backend is initialized lazily the first time an audio clip/source/listener
/// needs it. Failure is non-fatal: ByteEngine keeps running and audio components
/// simply remain silent.
/// </summary>
public static class AudioEngine
{
    private static readonly object Sync =
        new();

    private static bool _attempted;
    private static bool _available;

    private static ALDevice _device =
        ALDevice.Null;

    private static ALContext _context =
        ALContext.Null;

    public static bool IsAvailable =>
        _available;

    public static string LastError { get; private set; } =
        string.Empty;

    public static string DeviceName { get; private set; } =
        string.Empty;

    public static bool EnsureInitialized()
    {
        lock (Sync)
        {
            if (_available)
            {
                return true;
            }

            if (_attempted)
            {
                return false;
            }

            _attempted =
                true;

            try
            {
                /*
                 * OpenTK 4.9 resolves platform OpenAL dynamically. Register
                 * its resolver before touching ALC/AL entry points.
                 */
                CrashDebugLog.Write(
                    "AudioEngine: before ALBase.RegisterOpenALResolver.");

                ALBase.RegisterOpenALResolver();

                CrashDebugLog.Write(
                    "AudioEngine: after ALBase.RegisterOpenALResolver.");

                CrashDebugLog.Write(
                    "AudioEngine: before ALC.OpenDevice.");

                _device =
                    ALC.OpenDevice(
                        null!);

                CrashDebugLog.Write(
                    $"AudioEngine: after ALC.OpenDevice deviceNull={_device == ALDevice.Null}.");

                if (_device ==
                    ALDevice.Null)
                {
                    return
                        Fail(
                            "OpenAL could not open the default audio device.");
                }

                /*
                 * A single zero is the empty, zero-terminated ALC attribute
                 * list, so the device chooses its normal output settings.
                 */
                CrashDebugLog.Write(
                    "AudioEngine: before ALC.CreateContext.");

                _context =
                    ALC.CreateContext(
                        _device,
                        new[]
                        {
                            0
                        });

                CrashDebugLog.Write(
                    $"AudioEngine: after ALC.CreateContext contextNull={_context == ALContext.Null}.");

                if (_context ==
                    ALContext.Null)
                {
                    ALC.CloseDevice(
                        _device);

                    _device =
                        ALDevice.Null;

                    return
                        Fail(
                            "OpenAL could not create an audio context.");
                }

                CrashDebugLog.Write(
                    "AudioEngine: before ALC.MakeContextCurrent.");

                if (!ALC.MakeContextCurrent(
                        _context))
                {
                    ALC.DestroyContext(
                        _context);

                    ALC.CloseDevice(
                        _device);

                    _context =
                        ALContext.Null;

                    _device =
                        ALDevice.Null;

                    return
                        Fail(
                            "OpenAL could not make the audio context current.");
                }

                CrashDebugLog.Write(
                    "AudioEngine: after ALC.MakeContextCurrent.");

                CrashDebugLog.Write(
                    "AudioEngine: before AL.DistanceModel.");

                AL.DistanceModel(
                    ALDistanceModel.InverseDistanceClamped);

                CrashDebugLog.Write(
                    "AudioEngine: before AL.DopplerFactor.");

                AL.DopplerFactor(
                    1.0f);

                CrashDebugLog.Write(
                    "AudioEngine: native initialization calls completed.");

                DeviceName =
                    ALC.GetString(
                        _device,
                        AlcGetString.DeviceSpecifier) ??
                    "Default Device";

                string version =
                    AL.Get(
                        ALGetString.Version) ??
                    "Unknown";

                string renderer =
                    AL.Get(
                        ALGetString.Renderer) ??
                    "Unknown";

                _available =
                    true;

                LastError =
                    string.Empty;

                Console.WriteLine(
                    "----------------------------------");

                Console.WriteLine(
                    $"Audio Device   : {DeviceName}");

                Console.WriteLine(
                    $"OpenAL Version : {version}");

                Console.WriteLine(
                    $"Audio Renderer : {renderer}");

                Console.WriteLine(
                    "----------------------------------");

                return true;
            }
            catch (Exception exception)
            {
                CrashDebugLog.WriteException(
                    "AudioEngine.EnsureInitialized managed exception",
                    exception);

                /*
                 * DllNotFoundException is deliberately caught here as well.
                 * Missing native OpenAL must never prevent the editor or game
                 * from starting.
                 */
                return
                    Fail(
                        $"Audio backend unavailable: {exception.Message}");
            }
        }
    }

    public static void Shutdown()
    {
        lock (Sync)
        {
            if (_context !=
                ALContext.Null)
            {
                try
                {
                    ALC.MakeContextCurrent(
                        ALContext.Null);

                    ALC.DestroyContext(
                        _context);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"Audio context shutdown failed: {exception.Message}");
                }
            }

            if (_device !=
                ALDevice.Null)
            {
                try
                {
                    ALC.CloseDevice(
                        _device);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"Audio device shutdown failed: {exception.Message}");
                }
            }

            _context =
                ALContext.Null;

            _device =
                ALDevice.Null;

            _available =
                false;

            /*
             * Allow a later ByteEngineApplication in the same process (tests,
             * tools, editor restart scenarios) to initialize audio again.
             */
            _attempted =
                false;

            DeviceName =
                string.Empty;

            LastError =
                string.Empty;
        }
    }

    private static bool Fail(
        string message)
    {
        LastError =
            message;

        _available =
            false;

        Console.Error.WriteLine(
            message);

        return false;
    }
}
