using OpenTK.Audio.OpenAL;
using System.Runtime.InteropServices;
namespace ByteEngine.Core.Audio;
internal sealed class NativeAudioStream : IDisposable
{
    private readonly int _source;
    private readonly StreamingPcmReader _reader;
    private readonly int[] _buffers=new int[3];
    private readonly short[] _samples=new short[65536];
    private bool _playing,_paused;
    private GCHandle _pin;
    public int WorkingBufferBytes=>_reader.WorkingBufferBytes+_samples.Length*sizeof(short)+_buffers.Length*_samples.Length*sizeof(short);
    public NativeAudioStream(int source,string path)
    {
        _source=source;_reader=new StreamingPcmReader(path);
        try {_pin=GCHandle.Alloc(_samples,GCHandleType.Pinned);for(int i=0;i<_buffers.Length;i++)_buffers[i]=AL.GenBuffer();}
        catch {foreach(int buffer in _buffers)if(buffer!=0)AL.DeleteBuffer(buffer);DisposeManaged();throw;}
    }
    public void Play(bool loop)
    {
        if(_paused){_paused=false;_playing=true;AL.SourcePlay(_source);return;}
        Stop();_reader.Rewind();_playing=true;
        foreach(int buffer in _buffers)if(Fill(buffer,loop))AL.SourceQueueBuffer(_source,buffer);
        AL.SourcePlay(_source);
    }
    private bool Fill(int buffer,bool loop)
    {
        int count=_reader.Read(_samples);
        if(count==0&&loop){_reader.Rewind();count=_reader.Read(_samples);}
        if(count==0)return false;
        AL.BufferData(buffer,_reader.Channels==1?ALFormat.Mono16:ALFormat.Stereo16,_pin.AddrOfPinnedObject(),count*sizeof(short),_reader.SampleRate);return true;
    }
    public void Update(bool loop)
    {
        if(!_playing||_paused)return;
        int processed=AL.GetSource(_source,ALGetSourcei.BuffersProcessed);
        for(int i=0;i<processed;i++){int buffer=AL.SourceUnqueueBuffer(_source);if(Fill(buffer,loop))AL.SourceQueueBuffer(_source,buffer);}
        int queued=AL.GetSource(_source,ALGetSourcei.BuffersQueued);
        if(queued==0){_playing=false;return;}
        if((ALSourceState)AL.GetSource(_source,ALGetSourcei.SourceState)!=ALSourceState.Playing)AL.SourcePlay(_source);
    }
    public void Pause(){_paused=true;AL.SourcePause(_source);}
    public void Stop(){_playing=false;_paused=false;AL.SourceStop(_source);int queued=AL.GetSource(_source,ALGetSourcei.BuffersQueued);for(int i=0;i<queued;i++)AL.SourceUnqueueBuffer(_source);}
    public void DisposeManaged(){_reader.Dispose();if(_pin.IsAllocated)_pin.Free();}
    public void Dispose(){try{Stop();foreach(int buffer in _buffers)if(buffer!=0)AL.DeleteBuffer(buffer);}finally{DisposeManaged();}}
}
