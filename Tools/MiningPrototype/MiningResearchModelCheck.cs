using System.Numerics;
using System.Reflection;
using System.Text.Json;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
internal static class MiningResearchModelCheck
{
 public static void Run()
 {
  const int width=800,height=700;string output="artifacts/research-models/native";Directory.CreateDirectory(output);
  using var window=new OpenTK.Windowing.Desktop.NativeWindow(new NativeWindowSettings{ClientSize=new(width,height),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core,Title="Research models native validation"});window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
  using var db=new AssetDatabase(Program.Root,new[]{"Assets"},readOnly:true);using var assets=new AssetManager(db);var token=AnimationRuntimeAssets.Configure(assets);
  try{
   using var renderer=new Renderer3D();using var ui=new Renderer2D();
   foreach(var item in new[]{("ResearchStation",5,"Sample","Research",.4f), ("SecurityDrone",6,"Rotor0","Hover",.13f), ("FieldBarricade",4,"Panel","Deploy",1.2f), ("MortarRound",0,"Root","",0f)})
   {
    string path="Assets/Models/Mining/Research/"+item.Item1+".glb";using var meta=JsonDocument.Parse(File.ReadAllText(Program.Root+"/"+path+".meta"));var reference=new AssetReference(meta.RootElement.GetProperty("guid").GetGuid(),path);var model=assets.LoadModel(reference);
    if(model.Meshes.Count==0||model.Skeleton?.Bones.Count<(item.Item1=="MortarRound"?1:5)||model.Animations.Count!=item.Item2)throw new Exception("Incomplete mechanical model: "+item.Item1);
    var nodes=model.Nodes.Select(n=>n.Name).ToHashSet();foreach(var clip in model.Animations)if(!clip.Name.StartsWith(item.Item1+"_")||clip.Channels.Count==0||clip.Channels.Any(c=>!nodes.Contains(c.NodeName)))throw new Exception("Foreign or missing animation target: "+clip.Name);
    var scene=new Scene(item.Item1);var light=scene.CreateGameObject("Preview key");light.Transform.WorldRotation=Quaternion.CreateFromYawPitchRoll(3.4f,-.8f,0);light.AddComponent(new DirectionalLight{Intensity=2,AmbientIntensity=.4f});var o=scene.CreateGameObject(item.Item1);var skin=o.AddComponent(new SkeletalMeshRenderer{Model=reference,Loop=false,TransitionDuration=0});
    if(item.Item2>0){if(!skin.Play(item.Item1+"_"+item.Item4,false,0))throw new Exception("Native clip cannot play");skin.Seek(0);skin.TryGetBoneModelMatrix(item.Item3,out var before);skin.Seek(item.Item5);skin.TryGetBoneModelMatrix(item.Item3,out var after);if(before==after)throw new Exception("Mechanical animation does not move: "+item.Item1);}else if(!skin.ResolveRuntimeResources())throw new Exception("Mortar round cannot resolve");
    foreach(var clip in model.Animations)
    {
     if(!skin.Play(clip.Name,false,0))throw new Exception("Clip cannot play: "+clip.Name);
     foreach(float phase in new[]{0f,.25f,.5f,.75f,1f}){
      skin.Seek(clip.Duration*phase);if(!skin.TryGetCurrentModelBounds(out var bounds)||!float.IsFinite(bounds.Minimum.X)||!float.IsFinite(bounds.Maximum.Y)||(bounds.Maximum-bounds.Minimum).Length()>8)throw new Exception("Exploded or invalid animated bounds: "+clip.Name);
     }
    }
    skin.Play(item.Item1+(item.Item1=="SecurityDrone"?"_Hover":"_Idle"),true,0);skin.Seek(.17f);skin.TryGetCurrentModelBounds(out var b);var center=(b.Minimum+b.Maximum)/2;float span=Math.Max(1,(b.Maximum-b.Minimum).Length()*.65f);
    var context=new RenderContext(ui,renderer,scene,width,height,viewMatrix3D:Matrix4x4.CreateLookAt(center+new Vector3(1.25f,1.1f,-1.8f)*span,center,Vector3.UnitY),projectionMatrix3D:Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/3,width/(float)height,.05f,100),prepareEnvironmentLighting3D:false,renderShadows3D:false);
    GL.BindFramebuffer(FramebufferTarget.Framebuffer,0);GL.Viewport(0,0,width,height);GL.ClearColor(.11f,.13f,.16f,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);typeof(RenderContext).GetMethod("Begin3DFrame",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(context,null);
    typeof(Component).GetMethod("RenderEditorInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(skin,[context]);typeof(RenderContext).GetMethod("Flush3D",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(context,null);GL.Finish();if(GL.GetError()!=ErrorCode.NoError)throw new Exception("Native model rendering failed");
    byte[] pixels=new byte[width*height*4];GL.ReadPixels(0,0,width,height,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);using(var png=File.Create(output+"/"+item.Item1+".png"))typeof(GameScreenshots).GetMethod("WritePng",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[png,pixels,width,height]);
    Console.WriteLine($"PASS native {item.Item1}: {model.Meshes.Count} material sections / {model.Skeleton!.Bones.Count} bones / {model.Animations.Count} clips / all phases finite / moving {item.Item3}");
    scene.DestroyGameObject(o);
   }
  }finally{AnimationRuntimeAssets.Clear(token);}
 }
}
