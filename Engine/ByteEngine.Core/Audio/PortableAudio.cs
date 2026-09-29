using System.Numerics;

namespace ByteEngine.Core.Audio;

/// <summary>Optional host audio backend. Desktop OpenAL is unchanged when null.</summary>
public interface IPortableAudio
{
    bool IsPlaying(Guid id);
    void Play(Guid id, AudioClip clip, AudioSource3D source);
    void Pause(Guid id);
    void Stop(Guid id);
    void Update(Guid id, AudioSource3D source);
    void Listener(Vector3 position, Vector3 forward, Vector3 up, float volume);
}
public static class PortableAudio
{
    public static IPortableAudio? Backend { get; set; }
}
