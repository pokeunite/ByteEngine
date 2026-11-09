using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Vfx;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
namespace ByteEngine.Tests;
internal static class MiningExplosionPreview
{
 public static void Run(string project,string output)
 {
  Directory.CreateDirectory(output);
  using var window=new NativeWindow(new NativeWindowSettings{ClientSize=new(800,600),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core,Title="Native mining explosion validation"});
  window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
  using var db=new AssetDatabase(project,new[]{"Assets"},readOnly:true);using var assets=new AssetManager(db);var token=AnimationRuntimeAssets.Configure(assets);
  try{
   using var renderer=new Renderer3D();using var ui=new Renderer2D();
   var scene=new Scene("Cartoon explosion preview");var player=scene.CreateGameObject("Explosion").AddComponent(new VfxPlayer{PlayOnStart=false,DistanceQuality=false});
   foreach(string name in new[]{"Mine blast","Mortar impact"}){
    var definition=VfxEffectSerializer.Load(Path.Combine(project,"Assets/VFX/Mining",name+".bvfx"));
    if(definition.Loop||definition.Layers.Count!=5||definition.ParticleBudget>32)throw new Exception("Explosion budget or playback changed");
    var reference=definition.Layers[0].Texture;using(var texture=new Texture2D(Path.Combine(project,reference.CachedProjectPath!))){if(texture.Width!=2527||texture.Height!=2280)throw new Exception("Unexpected source sheet dimensions");}
    player.SetDefinition(definition);player.Size=name=="Mine blast"?1.8f:2.3f;player.Prewarm();
    if(player.Status!="Ready")throw new Exception(player.Status);
    foreach(float time in new[]{.04f,.16f,.38f,.72f,1.1f}){
     player.Play();for(int i=0;i<(int)MathF.Ceiling(time*60);i++)player.Advance(1f/60);
     var context=new RenderContext(ui,renderer,scene,800,600,viewMatrix3D:Matrix4x4.CreateLookAt(new(0,3.5f,8),new(0,.8f,0),Vector3.UnitY),projectionMatrix3D:Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/3,800f/600,.05f,100),prepareEnvironmentLighting3D:false,renderShadows3D:false);
     GL.BindFramebuffer(FramebufferTarget.Framebuffer,0);GL.Viewport(0,0,800,600);GL.ClearColor(.17f,.085f,.045f,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);
     context.Begin3DFrame();player.RenderEditorInternal(context);context.Flush3D();GL.Finish();
     if(GL.GetError()!=ErrorCode.NoError||player.RenderedParticles<1||player.ActiveParticles>32)throw new Exception("Explosion render or capacity failure");
     byte[] pixels=new byte[800*600*4];GL.ReadPixels(0,0,800,600,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);
     using var png=File.Create(Path.Combine(output,$"{name}-{time:F2}.png"));GameScreenshots.WritePng(png,pixels,800,600);
    }
    for(int i=0;i<120;i++)player.Advance(1f/60);if(player.IsPlaying)throw new Exception("Explosion did not finish");player.Stop(true);
   }
   player.DestroyInternal();Console.WriteLine("PASS native mine/mortar: source alpha atlas loaded, five rendered phases each, bounded particles and one-shot completion");
  }finally{AnimationRuntimeAssets.Clear(token);}
 }
}
