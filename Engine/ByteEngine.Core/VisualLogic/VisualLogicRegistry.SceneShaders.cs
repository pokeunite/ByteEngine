using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterSceneShaders(VisualLogicRegistry registry)
    {
        SkyEnvironment? Environment(VisualInstruction i,EventExecutionContext c)=>
            ResolveObjectTarget(i,c,false)?.GetComponent<SkyEnvironment>() ??
            c.Scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components).OfType<SkyEnvironment>().FirstOrDefault(e=>e.Enabled);
        registry.RegisterAction(new VisualActionDefinition{
            Id="graphics.setPixelArt",Category="Graphics / Built-in Shaders",DisplayName="Set Pixel Art Shader",
            Arguments=[BoolArg("enabled","Enable pixel art",true),NumberArg("pixelSize","Pixel size (1–32 px)",4),NumberArg("levels","Colour levels per channel (2–32)",8),NumberArg("dither","Ordered dithering (0–1)",.35)],
            Execute=(i,c)=>{
                var environment=Environment(i,c);
                if(environment==null){c.WarningSink?.Invoke("Add a Sky Environment to use built-in scene shaders.");return;}
                environment.ShaderStyle=EventValueResolver.GetBoolean(i,"enabled",c,true)?BuiltInSceneShader.PixelArt:BuiltInSceneShader.Standard;
                double size=EventValueResolver.GetNumber(i,"pixelSize",c,4),levels=EventValueResolver.GetNumber(i,"levels",c,8);
                environment.PixelSize=double.IsFinite(size)?(int)Math.Clamp(size,1,32):4;
                environment.PixelColorLevels=double.IsFinite(levels)?(int)Math.Clamp(levels,2,32):8;
                environment.PixelDither=(float)EventValueResolver.GetNumber(i,"dither",c,.35);
            }});
        registry.RegisterCondition(new VisualConditionDefinition{Id="graphics.pixelArtEnabled",Category="Graphics / Built-in Shaders",DisplayName="Pixel Art Shader Is Enabled",Evaluate=(i,c)=>Environment(i,c)?.ShaderStyle==BuiltInSceneShader.PixelArt});
    }
}
