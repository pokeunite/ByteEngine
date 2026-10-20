using System.Collections.Concurrent;
using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Runtime;
namespace ByteEngine.Core.Media;

public interface IVideoPlayback : IDisposable
{
    double Position {get;}
    bool Playing {get;}
    bool Paused {get;}
    bool Ended {get;}
    string Error {get;}
    void Play();void Pause();void Stop();void Seek(double seconds);void Update(double delta);
    void Draw(RenderContext context,Vector2 position,Vector2 size);
}
public static class VideoHost {public static Func<string,IVideoPlayback>? Create {get;set;}}
/// <summary>Project-local video drawn as a normalized screen rectangle.</summary>
public sealed class VideoPlayer : Component
{
    public string Source {get;set;}="";
    public Vector2 ScreenPosition {get;set;}=new(.1f,.1f);
    public Vector2 ScreenSize {get;set;}=new(.8f,.8f);
    public bool PlayOnStart {get;set;}
    public bool Loop {get;set;}
    public bool UseCompanionAudio {get;set;}
    private string _loadError="";
    private IVideoPlayback? _playback;
    private string _loaded="";
    public bool IsPlaying=>_playback?.Playing==true;
    public bool IsPaused=>_playback?.Paused==true;
    public bool HasEnded=>_playback?.Ended==true;
    public string Error=>_loadError.Length>0?_loadError:_playback?.Error??"";
    public double Position=>_playback?.Position??0;
    private IVideoPlayback Ensure()
    {
        if(_playback!=null&&_loaded==Source)return _playback;
        _playback?.Dispose();_playback=null;
        string root=GameObject.Scene?.ProjectRoot??throw new InvalidOperationException("Video requires a project scene.");
        if(Path.IsPathRooted(Source)||string.IsNullOrWhiteSpace(Source))throw new ArgumentException("Choose a video path relative to the project.");
        string path=GamePackageExporter.ResolveInside(root,Source);
        if(!File.Exists(path))throw new FileNotFoundException("Video was not found.",path);
        _playback=VideoHost.Create?.Invoke(path)??new NativeVideoPlayback(path);_loaded=Source;return _playback;
    }
    public void Play(){try{Ensure().Play();_loadError="";if(UseCompanionAudio)GameObject.GetComponent<Audio.AudioSource3D>()?.Play();}catch(Exception error){_loadError=error.Message;throw;}}
    public void Pause(){_playback?.Pause();if(UseCompanionAudio)GameObject.GetComponent<Audio.AudioSource3D>()?.Pause();}
    public void Stop(){_playback?.Stop();if(UseCompanionAudio)GameObject.GetComponent<Audio.AudioSource3D>()?.Stop();}
    public void Seek(double seconds){if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));Ensure().Seek(seconds);if(UseCompanionAudio)GameObject.GetComponent<Audio.AudioSource3D>()?.Seek(seconds);}
    protected override void OnStart(){if(PlayOnStart)Play();}
    protected override void OnUpdate(){_playback?.Update(Time.DeltaTime);if(Loop&&HasEnded){Seek(0);Play();}}
    protected override void OnRender(RenderContext context)=>_playback?.Draw(context,ScreenPosition*new Vector2(context.TargetWidth,context.TargetHeight),ScreenSize*new Vector2(context.TargetWidth,context.TargetHeight));
    protected override void OnStop(){_playback?.Dispose();_playback=null;}
    protected override void OnDestroy()=>OnStop();
}

internal sealed class NativeVideoPlayback : IVideoPlayback
{
    private readonly string _path;
    private CancellationTokenSource? _cancel;
    private BlockingCollection<(double Time,int Width,int Height,byte[] Pixels)> _frames=new(2);
    private Texture2D? _texture;
    private volatile bool _eof;
    private volatile string _error="";
    private bool _visible;
    public double Position {get;private set;}
    public bool Playing {get;private set;}
    public bool Paused {get;private set;}
    public bool Ended {get;private set;}
    public string Error=>_error;
    public NativeVideoPlayback(string path)=>_path=path;
    private void Decode(double start)
    {
        _cancel?.Cancel();_cancel=new();var token=_cancel.Token;var frames=new BlockingCollection<(double,int,int,byte[])>(2);_frames=frames;_eof=false;_error="";
        Task.Run(()=>
        {
            try
            {
                using var reader=new WindowsVideoReader(_path);if(start>0)reader.Seek(start);
                while(!token.IsCancellationRequested)
                {var frame=reader.ReadFrame();if(frame==null)break;frames.Add((frame.Value.Seconds,reader.Width,reader.Height,frame.Value.Pixels),token);}
                if(!token.IsCancellationRequested)_eof=true;
            }
            catch(OperationCanceledException){}
            catch(Exception error){if(!token.IsCancellationRequested){_error=error.Message;_eof=true;}}
            finally {frames.CompleteAdding();}
        });
    }
    public void Play(){if(Ended)Seek(0);if(_cancel==null)Decode(Position);Playing=true;Paused=false;_visible=true;}
    public void Pause(){if(!Playing)return;Playing=false;Paused=true;}
    public void Stop(){_cancel?.Cancel();_cancel=null;_pending=null;_visible=false;Playing=Paused=Ended=false;Position=0;}
    public void Seek(double seconds){Position=seconds;Ended=false;_pending=null;Decode(seconds);}
    public void Update(double delta)
    {
        if(!Playing)return;
        // Do not race the playback clock ahead while the decoder is loading its first frame.
        if(_texture!=null||_frames.Count>0)Position+=Math.Max(0,delta);
        if(_pending is {} pending)
        {if(pending.Time>Position+.05)return;Upload(pending);_pending=null;}
        while(_frames.TryTake(out var frame))
        {
            if(frame.Time>Position+.05){_pending=frame;break;}
            Upload(frame);
        }
        if(_eof&&_frames.Count==0&&_pending==null){Playing=false;Ended=_error.Length==0;}
    }
    private (double Time,int Width,int Height,byte[] Pixels)? _pending;
    private void Upload((double Time,int Width,int Height,byte[] Pixels) frame)
    {if(_texture==null||_texture.Width!=frame.Width||_texture.Height!=frame.Height){_texture?.Dispose();_texture=Texture2D.FromPixels(frame.Width,frame.Height,frame.Pixels);}else _texture.UpdatePixels(frame.Pixels);}
    public void Draw(RenderContext context,Vector2 position,Vector2 size){if(_visible&&_texture!=null)context.QueueUiImage(_texture,position,size,Vector4.One);}
    public void Dispose(){Stop();_texture?.Dispose();_texture=null;}
}
