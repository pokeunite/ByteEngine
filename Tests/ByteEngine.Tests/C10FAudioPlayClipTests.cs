using ByteEngine.Core.Audio;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class C10FAudioPlayClipTests
{
    public static void Run()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "ByteEngine-C10F-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            string wavePath = Path.Combine(root, "footstep.wav");
            WriteWave(wavePath);

            Assert(
                AudioClip.TryLoadWave(wavePath, out AudioClip? clip) &&
                clip != null,
                "test WAV decodes without an audio backend");

            RegistryAndResolution(clip!);
            PlaybackBoundary(clip!);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }

    private static void RegistryAndResolution(AudioClip clip)
    {
        VisualLogicRegistry registry =
            VisualLogicRegistry.CreateDefault();

        Assert(
            registry.TryGetAction(
                "audio.play",
                out VisualActionDefinition? assigned) &&
            assigned != null,
            "audio.play remains registered");
        Assert(
            registry.TryGetAction(
                "audio.playClip",
                out VisualActionDefinition? playClip) &&
            playClip != null,
            "audio.playClip registered");

        Scene scene = new("Audio Logic");
        GameObject actor = scene.CreateGameObject("Actor");
        AudioSource3D source = actor.AddComponent(new AudioSource3D());
        List<string> warnings = new();
        EventExecutionContext context =
            new()
            {
                Globals = new VariableStore(),
                Scene = scene,
                Self = actor,
                WarningSink = warnings.Add
            };

        playClip!.Execute(
            Instruction(string.Empty, string.Empty),
            context);
        Assert(
            warnings.Any(item =>
                item.Contains("requires", StringComparison.OrdinalIgnoreCase)),
            "playClip requires a clip");

        Guid expectedGuid = Guid.NewGuid();
        AssetReference? loadedReference = null;
        int loadCount = 0;
        Guid registration =
            AudioRuntimeAssets.Configure(
                (loadedSource, reference, _) =>
                {
                    loadCount++;
                    loadedReference = reference;
                    loadedSource.ClipReference = reference;
                    loadedSource.SetClip(clip);
                    return true;
                });

        try
        {
            warnings.Clear();
            playClip.Execute(
                Instruction(
                    expectedGuid.ToString(),
                    "Assets/Audio/footstep.wav"),
                context);

            Assert(
                loadedReference?.Guid == expectedGuid &&
                loadedReference.CachedProjectPath ==
                    "Assets/Audio/footstep.wav",
                "playClip resolves stable GUID and cached path");
            Assert(
                source.ClipReference.Guid == expectedGuid &&
                source.ClipLoaded,
                "selected clip assigned before playback");

            source.SetClip(null);
            source.ClipReference = AssetReference.Empty;
            loadedReference = null;

            playClip.Execute(
                Instruction(
                    string.Empty,
                    "Assets/Audio/fallback.wav"),
                context);

            Assert(
                loadedReference?.Guid == Guid.Empty &&
                loadedReference?.CachedProjectPath ==
                    "Assets/Audio/fallback.wav",
                "path fallback resolves");

            AudioRuntimeAssets.Clear(registration);
            registration =
                AudioRuntimeAssets.Configure(
                    (_, _, warningSink) =>
                    {
                        warningSink?.Invoke("missing clip");
                        return false;
                    });

            warnings.Clear();
            source.SetClip(null);
            playClip.Execute(
                Instruction(
                    Guid.NewGuid().ToString(),
                    "Assets/Audio/deleted.wav"),
                context);

            Assert(
                !source.ClipLoaded &&
                warnings.Any(item =>
                    item.Contains("missing", StringComparison.OrdinalIgnoreCase)),
                "invalid or missing clip fails safely");

            GameObject noSource = scene.CreateGameObject("No Source");
            warnings.Clear();
            playClip.Execute(
                Instruction(
                    expectedGuid.ToString(),
                    "Assets/Audio/footstep.wav",
                    noSource),
                new EventExecutionContext
                {
                    Globals = new VariableStore(),
                    Scene = scene,
                    Self = noSource,
                    WarningSink = warnings.Add
                });

            Assert(
                warnings.Any(item =>
                    item.Contains(
                        "no AudioSource3D",
                        StringComparison.Ordinal)),
                "missing AudioSource3D fails safely");
        }
        finally
        {
            AudioRuntimeAssets.Clear(registration);
        }
    }

    private static void PlaybackBoundary(AudioClip clip)
    {
        AudioSource3D source = new GameObject("Source")
            .AddComponent(new AudioSource3D());
        AssetReference reference =
            new(Guid.NewGuid(), "Assets/Audio/footstep.wav");
        source.ClipReference = reference;
        source.SetClip(clip);

        int loads = 0;
        int stops = 0;
        int plays = 0;

        bool first =
            AudioVisualLogicPlayback.Execute(
                source,
                reference,
                null,
                (_, _, _) =>
                {
                    loads++;
                    return true;
                },
                () => false,
                () => stops++,
                () => plays++,
                () => true);

        bool repeated =
            AudioVisualLogicPlayback.Execute(
                source,
                reference,
                null,
                (_, _, _) =>
                {
                    loads++;
                    return true;
                },
                () => true,
                () => stops++,
                () => plays++,
                () => true);

        Assert(first && repeated, "loaded clip playback path succeeds");
        Assert(loads == 0, "same already-loaded clip does not reload");
        Assert(
            stops == 1 && plays == 2,
            "repeated playClip retriggers playback path");
        Assert(
            AudioVisualLogicPlayback.ReferencesSame(
                reference,
                new AssetReference(
                    reference.Guid,
                    "Assets/Renamed/footstep.wav")),
            "GUID identity survives cached-path rename");
    }

    private static VisualInstruction Instruction(
        string guid,
        string path,
        GameObject? target = null) =>
        new()
        {
            Id = "audio.playClip",
            Arguments =
            {
                ["target"] =
                    EventValue.String(
                        target == null
                            ? "Self"
                            : $"id:{target.Id}"),
                ["clipGuid"] = EventValue.String(guid),
                ["clipPath"] = EventValue.String(path)
            }
        };

    private static void WriteWave(string path)
    {
        const int sampleRate = 8000;
        byte[] pcm = new byte[16];

        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream);

        writer.Write("RIFF"u8);
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(pcm.Length);
        writer.Write(pcm);
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "FAILED C10-F audio: " + name);
        }
    }
}
