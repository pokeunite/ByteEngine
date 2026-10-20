using System.Numerics;
using ByteEngine.Core.Media;
using ByteEngine.Core.Graphics;
namespace ByteEngine.Browser;

internal sealed class BrowserVideoPlayback : IVideoPlayback
{
    private static readonly Dictionary<string,BrowserVideoPlayback> Instances=new();
    private readonly Action<object> _send;
    private readonly string _id=Guid.NewGuid().ToString("N");
    private bool _requested;
    public double Position {get;private set;}
    public bool Playing {get;private set;}
    public bool Paused {get;private set;}
    public bool Ended {get;private set;}
    public string Error {get;private set;}="";
    public BrowserVideoPlayback(string path,Action<object> send)
    {_send=send;Instances[_id]=this;send(new{kind="videoLoad",id=_id,path=path.Replace("/game/","")});}
    public static void Report(string id,double seconds,bool paused,bool ended,string error)
    {if(!Instances.TryGetValue(id,out var item))return;item.Position=seconds;item.Ended=ended;item.Paused=paused&&!ended&&item._requested;item.Playing=item._requested&&!paused&&!ended&&error.Length==0;item.Error=error;}
    public void Play(){_requested=true;Playing=true;Paused=Ended=false;_send(new{kind="videoPlay",id=_id});}
    public void Pause(){Playing=false;Paused=true;_send(new{kind="videoPause",id=_id});}
    public void Stop(){_requested=false;Playing=Paused=Ended=false;Position=0;_send(new{kind="videoStop",id=_id});}
    public void Seek(double seconds){Position=seconds;Ended=false;_send(new{kind="videoSeek",id=_id,seconds});}
    public void Update(double delta){}
    public void Draw(RenderContext context,Vector2 position,Vector2 size)=>_send(new{kind="videoRect",id=_id,x=position.X/context.TargetWidth,y=position.Y/context.TargetHeight,w=size.X/context.TargetWidth,h=size.Y/context.TargetHeight});
    public void Dispose(){_send(new{kind="videoDispose",id=_id});Instances.Remove(_id);}
}
