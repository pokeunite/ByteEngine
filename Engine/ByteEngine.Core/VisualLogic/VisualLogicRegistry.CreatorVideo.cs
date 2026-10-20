using ByteEngine.Core.Media;
using ByteEngine.Core.Variables;
namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterCreatorVideo(VisualLogicRegistry r)
    {
        VideoPlayer? Player(VisualInstruction i,EventExecutionContext c)=>ResolveObjectTarget(i,c)?.GetComponent<VideoPlayer>();
        Add(r,"video.play","Video","Play video",(i,c)=>Player(i,c)?.Play(),ObjectArg());
        Add(r,"video.pause","Video","Pause video",(i,c)=>Player(i,c)?.Pause(),ObjectArg());
        Add(r,"video.stop","Video","Stop video",(i,c)=>Player(i,c)?.Stop(),ObjectArg());
        Add(r,"video.seek","Video","Seek video",(i,c)=>Player(i,c)?.Seek(N(i,c,"seconds")),ObjectArg(),NumberArg("seconds","Playback seconds"));
        Test(r,"video.playing","Video","Video is playing",(i,c)=>Player(i,c)?.IsPlaying==true,ObjectArg());
        Test(r,"video.paused","Video","Video is paused",(i,c)=>Player(i,c)?.IsPaused==true,ObjectArg());
        Test(r,"video.ended","Video","Video has ended",(i,c)=>Player(i,c)?.HasEnded==true,ObjectArg());
        Test(r,"video.failed","Video","Video failed to load or decode",(i,c)=>Player(i,c)?.Error.Length>0,ObjectArg());
        Add(r,"video.readPosition","Video","Read video playback position",(i,c)=>Output(i,c,VariableValue.FromNumber(Player(i,c)?.Position??0)),ObjectArg(),TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","VideoPosition"));
        Add(r,"video.readError","Video","Read video error message",(i,c)=>Output(i,c,VariableValue.FromString(Player(i,c)?.Error??"")),ObjectArg(),TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","VideoError"));
    }
}
