using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

internal static class PixelArtShaderTests
{
    internal static void Run(string root)
    {
        Directory.CreateDirectory(Path.Combine(root,"Assets/Events"));Directory.CreateDirectory(Path.Combine(root,"Scenes"));
        using var window=new NativeWindow(new NativeWindowSettings{ClientSize=new(640,400),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core});window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var renderer=new Renderer3D();using var sprites=new Renderer2D();using var post=new PostProcess3D();
        var env=new SkyEnvironment{ShaderStyle=BuiltInSceneShader.PixelArt,PixelSize=5,PixelColorLevels=7,PixelDither=.4f};
        var old=GraphicsLookSerialization.Load(new SkyEnvironment(),new System.Text.Json.Nodes.JsonObject());
        if(old.ShaderStyle!=BuiltInSceneShader.Standard)throw new Exception("Legacy scenes must retain standard rendering");
        var restored=GraphicsLookSerialization.Load(new SkyEnvironment(),GraphicsLookSerialization.Save(env));
        if(restored.PixelSize!=5||restored.PixelColorLevels!=7||restored.PixelDither!=.4f||restored.ShaderStyle!=BuiltInSceneShader.PixelArt)throw new Exception("Pixel settings roundtrip failed");
        env.PixelSize=int.MaxValue;env.PixelColorLevels=-3;env.PixelDither=float.NaN;
        if(env.PixelSize!=32||env.PixelColorLevels!=2||!float.IsFinite(env.PixelDither))throw new Exception("Invalid pixel settings must be bounded");
        env.PixelSize=4;env.PixelColorLevels=8;env.PixelDither=.35f;
        // Exact GPU evidence: blocks remain constant, colours stay quantized,
        // disabled output matches legacy, and partial edge blocks are valid.
        const int w=67,h=43;
        var rgba=new byte[w*h*4];for(int y=0;y<h;y++)for(int x=0;x<w;x++){int n=(y*w+x)*4;rgba[n]=(byte)(x*255/(w-1));rgba[n+1]=(byte)(y*255/(h-1));rgba[n+2]=(byte)((x+y)*255/(w+h-2));rgba[n+3]=255;}
        using var input=Texture2D.FromPixels(w,h,rgba);input.Bind();GL.GetInteger(GetPName.TextureBinding2D,out int inputHandle);
        byte[] Capture(GraphicsLook look,int width=w,int height=h,bool tone=true)
        {post.Render(inputHandle,0,width,height,tone,1,true,look:look);GL.Finish();var data=new byte[width*height*4];GL.ReadPixels(0,0,width,height,PixelFormat.Rgba,PixelType.UnsignedByte,data);return data;}
        var baseline=Capture(GraphicsLook.Default);var disabled=Capture(GraphicsLook.Default with{PixelSize=23,PixelColorLevels=2,PixelDither=1});if(!baseline.SequenceEqual(disabled))throw new Exception("Disabled pixel shader changed the legacy image");
        foreach(float dither in new[]{0f,1f})
        {
            var data=Capture(env.CaptureLook() with{PixelSize=4,PixelColorLevels=4,PixelDither=dither});
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<3;c++)
            {int value=data[(y*w+x)*4+c],first=data[((y/4*4)*w+x/4*4)*4+c];if(value!=first)throw new Exception("Pixel block is not constant");if(Enumerable.Range(0,4).All(level=>Math.Abs(value-level*85)>1))throw new Exception("Output colour is outside configured levels");}
        }
        var copy=Capture(env.CaptureLook(),tone:false);if(!copy.SequenceEqual(Capture(GraphicsLook.Default,tone:false)))throw new Exception("Pixel shader modified the 2D copy path");
        var scene=new Scene("Pixel art shader demo");scene.CreateGameObject("Environment – shader controls").AddComponent(env);
        env.ZenithColor=new(.08f,.15f,.24f);env.HorizonColor=new(.23f,.38f,.43f);env.GroundColor=new(.08f,.12f,.16f);env.EnvironmentLightingEnabled=false;
        var camera=scene.CreateGameObject("Isometric camera");camera.Transform.WorldPosition=new(9,8,11);Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,new(0,.4f,0),Vector3.UnitY),out var world);camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);var cam=camera.AddComponent(new Camera3D{Orthographic=true,OrthographicWidth=15,ActiveGameCamera=true});
        void Shape(string name,Vector3 position,Vector3 scale,Vector3 color,PrimitiveMeshType primitive=PrimitiveMeshType.Cube)
        {var obj=scene.CreateGameObject(name);obj.Transform.WorldPosition=position;obj.Transform.LocalScale=scale;obj.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=primitive,Material=new Material{BaseColor=new(color,1),Roughness=.9f}});}
        Shape("Floating stone island",new(0,-.55f,0),new(8,.9f,6),new(.23f,.28f,.3f));Shape("Grass",new(0,-.03f,0),new(7.9f,.18f,5.9f),new(.24f,.49f,.25f));
        for(int i=0;i<7;i++)Shape("Stepping stone "+i,new(-2.5f+i*.65f,.1f,1),new(.55f,.14f,.7f),new(.62f,.58f,.4f));
        for(int i=0;i<3;i++){float x=-2.3f+i*2.2f;Shape("Tree trunk "+i,new(x,.6f,-1.6f),new(.3f,1.3f,.3f),new(.34f,.17f,.09f));Shape("Tree canopy "+i,new(x,1.65f,-1.6f),new(1.8f,1.9f,1.7f),new(.12f+i*.035f,.36f+i*.06f,.19f),PrimitiveMeshType.Sphere);}
        Shape("Tower",new(1,.95f,-.2f),new(1.2f,2,1.2f),new(.66f,.55f,.36f));Shape("Tower top",new(1,2.1f,-.2f),new(1.5f,.25f,1.5f),new(.8f,.65f,.4f));
        for(int i=0;i<5;i++)Shape("Flower "+i,new(-2+i*.6f,.25f,2.1f),new(.22f,.32f,.22f),new(1,.25f+i*.08f,.13f),PrimitiveMeshType.Sphere);
        var sun=scene.CreateGameObject("Sun").AddComponent(new DirectionalLight{Intensity=2,AmbientIntensity=.25f,Color=new(1,.88f,.69f),CastShadows=false});sun.Transform.EulerAngles=new(50,-35,0);
        var registry=VisualLogicRegistry.CreateDefault();var self=scene.FindGameObject("Environment – shader controls")!;
        registry.TryGetAction("graphics.setPixelArt",out var action);registry.TryGetCondition("graphics.pixelArtEnabled",out var condition);
        var context=new EventExecutionContext{Self=self,Scene=scene,Globals=new VariableStore()};
        action!.Execute(new VisualInstruction{Arguments=new(){["enabled"]=EventValue.Boolean(false)}},context);if(condition!.Evaluate(new(),context))throw new Exception("Event shader disable failed");
        action.Execute(new VisualInstruction{Arguments=new(){["enabled"]=EventValue.Boolean(true)}},context);if(!condition.Evaluate(new(),context))throw new Exception("Event shader enable failed");
        var module=new EventModuleDefinition{Name="Shader comparison",Rules=[]};
        foreach(var (key,enabled) in new[]{("1",false),("2",true)})module.Rules.Add(new(){DisplayName=enabled?"2 – pixel art":"1 – standard",Conditions=[new(){Id="input.keyPressed",Arguments=new(){["key"]=EventValue.String(key)}}],Actions=[new(){Id="graphics.setPixelArt",Arguments=new(){["enabled"]=EventValue.Boolean(enabled),["pixelSize"]=EventValue.Number(4),["levels"]=EventValue.Number(8),["dither"]=EventValue.Number(.35)}}]});
        string eventPath="Assets/Events/Shader comparison.byteevents";new EventModuleSerializer().Save(module,Path.Combine(root,eventPath));self.AddComponent(new EventModuleComponent()).AddModuleReference(new(eventPath));
        using var db=new AssetDatabase(root,["Assets","Scenes"]);using var assets=new AssetManager(db);var serializer=new SceneSerializer(new ComponentSerializer(root,db,assets));serializer.Save(scene,Path.Combine(root,"Scenes/Main.bytescene"));new ProjectSerializer().Save(new ProjectData{Name="Pixel Art Shader Demo"},Path.Combine(root,"Pixel Art Shader Demo.byteproject"));
        var reload=serializer.Load(Path.Combine(root,"Scenes/Main.bytescene"));if(reload.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>().Single().ShaderStyle!=BuiltInSceneShader.PixelArt)throw new Exception("Saved scene lost pixel shader");
        scene.LoadInternal();
        using var target=new RuntimePostProcessTarget3D();
        using var comparison=new System.Drawing.Bitmap(1280,440);using var canvas=System.Drawing.Graphics.FromImage(comparison);canvas.Clear(System.Drawing.Color.FromArgb(18,23,30));using var font=new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif,14);
        for(int i=0;i<2;i++)
        {
            env.ShaderStyle=i==0?BuiltInSceneShader.Standard:BuiltInSceneShader.PixelArt;
            var render=new RenderContext(sprites,renderer,scene,640,400,viewMatrix3D:cam.GetViewMatrix(),projectionMatrix3D:cam.GetProjectionMatrix(1.6f),prepareEnvironmentLighting3D:false,renderShadows3D:false);
            GL.DepthMask(true);target.Begin(640,400);scene.RenderInternal(render);target.Present(1,640,400,true,cam.GetProjectionMatrix(1.6f),env.CaptureLook());GL.Finish();
            var data=new byte[640*400*4];GL.ReadPixels(0,0,640,400,PixelFormat.Bgra,PixelType.UnsignedByte,data);
            using var image=new System.Drawing.Bitmap(640,400,System.Drawing.Imaging.PixelFormat.Format32bppArgb);var bits=image.LockBits(new(0,0,640,400),System.Drawing.Imaging.ImageLockMode.WriteOnly,image.PixelFormat);System.Runtime.InteropServices.Marshal.Copy(data,0,bits.Scan0,data.Length);image.UnlockBits(bits);image.RotateFlip(System.Drawing.RotateFlipType.RotateNoneFlipY);canvas.DrawImageUnscaled(image,i*640,40);canvas.DrawString(i==0?"STANDARD":"PIXEL ART · 4 px · 8 levels",font,System.Drawing.Brushes.White,i*640+18,10);
        }
        string preview=Path.Combine(root,"Pixel-Art-Comparison.png");comparison.Save(preview);
        if(GL.GetError()!=ErrorCode.NoError)throw new Exception("Pixel shader GL error");
        Console.WriteLine("PASS pixel shader GPU blocks, quantization, dithering, edge blocks, disabled/2D identity, scene roundtrip and event controls. "+preview);
    }
}

