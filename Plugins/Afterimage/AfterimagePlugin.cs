using ByteEngine.Core.Plugins;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
namespace ByteEngine.Afterimage;
public sealed class AfterimagePlugin:IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext c)
    {
        c.RegisterSimpleComponent<Afterimage3D>("Afterimage3D",new("Afterimage","Effects","Frozen animated silhouettes with bounded lifetime and count. Event sheets control emission.","ghost slow motion trail"));
        var target=new VisualArgumentDefinition("target","Animated object",VariableValue.FromString("Self")){IsObjectReference=true};
        c.RegisterAction(new(){Id="bytebard.afterimage.configure",Category="Afterimage",DisplayName="Configure afterimage trail",Arguments=[target,new("interval","Emission interval (real seconds)",VariableValue.FromNumber(.075)),new("lifetime","Fade duration (real seconds)",VariableValue.FromNumber(.38)),new("count","Maximum ghosts (1 to 8)",VariableValue.FromNumber(4)),new("distance","Minimum travel distance",VariableValue.FromNumber(.045)),new("opacity","Ghost opacity",VariableValue.FromNumber(.22))],Execute=(i,e)=>{
            var token=EventValueResolver.GetString(i,"target",e,"Self");var obj=token=="Self"?e.Self:token.StartsWith("id:")&&Guid.TryParse(token[3..],out var id)?e.Scene.FindGameObject(id):e.Scene.FindGameObject(token);
            if(obj?.GetComponent<Afterimage3D>()is {} effect){effect.Interval=(float)EventValueResolver.GetNumber(i,"interval",e);effect.Lifetime=(float)EventValueResolver.GetNumber(i,"lifetime",e);effect.MaximumGhosts=(int)EventValueResolver.GetNumber(i,"count",e);effect.MinimumDistance=(float)EventValueResolver.GetNumber(i,"distance",e);effect.Color=new(effect.Color.X,effect.Color.Y,effect.Color.Z,(float)EventValueResolver.GetNumber(i,"opacity",e));}
        }});
        c.RegisterAction(new(){Id="bytebard.afterimage.emit",Category="Afterimage",DisplayName="Enable afterimage emission",Arguments=[target,new("enabled","Emitting",VariableValue.FromBoolean(true))],Execute=(i,e)=>{
            var token=EventValueResolver.GetString(i,"target",e,"Self");var obj=token=="Self"?e.Self:token.StartsWith("id:")&&Guid.TryParse(token[3..],out var id)?e.Scene.FindGameObject(id):e.Scene.FindGameObject(token);
            if(obj?.GetComponent<Afterimage3D>() is {} effect)effect.Emitting=EventValueResolver.GetBoolean(i,"enabled",e);
        }});
        c.RegisterAction(new(){Id="bytebard.afterimage.clear",Category="Afterimage",DisplayName="Clear afterimages",Arguments=[target],Execute=(i,e)=>{
            var token=EventValueResolver.GetString(i,"target",e,"Self");var obj=token=="Self"?e.Self:token.StartsWith("id:")&&Guid.TryParse(token[3..],out var id)?e.Scene.FindGameObject(id):e.Scene.FindGameObject(token);obj?.GetComponent<Afterimage3D>()?.Clear();
        }});
    }
}
