using System.Buffers.Binary;
using System.Text;
using NVorbis;
namespace ByteEngine.Core.Audio;
/// <summary>Bounded sequential PCM16 decoder for WAV and Ogg Vorbis. WAV payloads are never read wholesale.</summary>
public sealed class StreamingPcmReader : IDisposable
{
    private readonly FileStream? _wave;
    private readonly VorbisReader? _vorbis;
    private long _start,_end;
    private int _bits;
    private readonly byte[] _bytes=new byte[131072];
    private readonly float[] _floats=new float[65536];
    public int Channels {get;}
    public int SampleRate {get;}
    public double DurationSeconds {get;}
    public int WorkingBufferBytes=>_bytes.Length+_floats.Length*sizeof(float);
    public StreamingPcmReader(string path)
    {
        if(Path.GetExtension(path).Equals(".ogg",StringComparison.OrdinalIgnoreCase))
        {
            _vorbis=new VorbisReader(path);Channels=_vorbis.Channels;SampleRate=_vorbis.SampleRate;DurationSeconds=_vorbis.TotalTime.TotalSeconds;
            if(Channels is <1 or >2){_vorbis.Dispose();throw new InvalidDataException("Streaming supports mono/stereo Ogg Vorbis.");}return;
        }
        _wave=File.OpenRead(path);
        try
        {
            using var reader=new BinaryReader(_wave,Encoding.ASCII,true);
            if(new string(reader.ReadChars(4))!="RIFF"){throw new InvalidDataException("Expected RIFF WAV.");}
            reader.ReadUInt32();if(new string(reader.ReadChars(4))!="WAVE")throw new InvalidDataException("Expected WAVE.");
            int channels=0,rate=0,bits=0;bool pcm=false;
            while(_wave.Position+8<=_wave.Length)
            {
                string kind=new string(reader.ReadChars(4));uint length=reader.ReadUInt32();long position=_wave.Position;
                if(length>_wave.Length-position)throw new InvalidDataException("Truncated WAV chunk.");
                if(kind=="fmt ")
                {if(length<16)throw new InvalidDataException("Invalid WAV format.");pcm=reader.ReadUInt16()==1;channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadUInt32();reader.ReadUInt16();bits=reader.ReadUInt16();}
                if(kind=="data"){_start=position;_end=position+length;}
                _wave.Position=position+length+(length&1);
            }
            if(!pcm||channels is <1 or >2||rate is <1000 or >384000||bits is not (8 or 16)||_end<=_start)throw new InvalidDataException("Streaming requires mono/stereo PCM8/PCM16 WAV.");
            Channels=channels;SampleRate=rate;_bits=bits;DurationSeconds=(_end-_start)/(double)(rate*channels*(bits/8));_wave.Position=_start;
        }
        catch{_wave.Dispose();throw;}
    }
    public void Rewind(){if(_vorbis!=null)_vorbis.SamplePosition=0;else _wave!.Position=_start;}
    public int Read(short[] samples)
    {
        int count=Math.Min(samples.Length,_floats.Length);count-=count%Channels;
        if(_vorbis!=null)
        {
            int read=_vorbis.ReadSamples(_floats,0,count);
            for(int i=0;i<read;i++)samples[i]=(short)Math.Clamp((int)(_floats[i]*32767),short.MinValue,short.MaxValue);return read;
        }
        int bytes=Math.Min(count*(_bits/8),(int)Math.Min(int.MaxValue,_end-_wave!.Position));bytes-=bytes%(Channels*(_bits/8));
        _wave.ReadExactly(_bytes.AsSpan(0,bytes));int result=bytes/(_bits/8);
        for(int i=0;i<result;i++)samples[i]=_bits==16?BinaryPrimitives.ReadInt16LittleEndian(_bytes.AsSpan(i*2,2)):(short)((_bytes[i]-128)<<8);
        return result;
    }
    public void Dispose(){_vorbis?.Dispose();_wave?.Dispose();}
}
