using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
namespace ByteEngine.Tests;
internal static class WebPlatformTests
{
 public static void Run(string root){
  int checks=0;void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
  BrowserRenderContract.ValidateCoverage();Check(BrowserRenderContract.MaterialProperties.Split(' ').Length==typeof(Material).GetProperties().Count(),"Every public material property has a browser contract decision");
  string runtime=Path.Combine(root,"WebRuntime");Directory.CreateDirectory(runtime);foreach(string file in new[]{"renderer.js","water.js","graphics.js","main.js","content.js","audio.js","frame-wire.js","performance.js","_framework/blazor.boot.json"}){string p=Path.Combine(runtime,file);Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,"fixture");}BrowserRenderContract.PrepareRuntime(runtime);BrowserRenderContract.ValidateRuntime(runtime);Check(true,"A coherent runtime validates");
  File.AppendAllText(Path.Combine(runtime,"renderer.js"),"changed");bool rejected=false;try{BrowserRenderContract.ValidateRuntime(runtime);}catch(InvalidOperationException){rejected=true;}Check(rejected,"A mixed renderer is rejected before export");
  Check(File.ReadAllText(Path.Combine(runtime,"shared-render.js")).Contains("byteGravityWave"),"Shared shader generation carries the native wave implementation");
  var water=BrowserWaterParameters.Capture(new Material{WaterSurface=true,WaterColumn=new(3,1,.8f,0),WaterMotion=new(9,2,.1f,.5f),WaterAbsorption=new(.8f,.2f,.1f,.3f)},17)!;
  Check(water.depthTexture==17&&water.column[0]==3&&water.motion[0]==9&&water.absorption[3]==.3f,"Typed native water parameters survive browser transport");
  Check(PlatformCapabilities.InspectJson("{\"type\":\"MeshRenderer\",\"properties\":{\"polygonMode\":\"Line\"}}",RuntimePlatform.Browser).Any(i=>i.BlocksExport),"Unsupported wireframe blocks export");
  Check(PlatformCapabilities.InspectJson("{\"type\":\"SkyEnvironment\",\"properties\":{\"environmentLightingEnabled\":true,\"graphicsLook\":{\"ao\":0.5,\"bloom\":0.2}}}",RuntimePlatform.Browser).Count==3,"Environment lighting, AO and bloom differences are declared");
  Check(PlatformCapabilities.InspectJson("{\"type\":\"PointLight\",\"properties\":{\"castShadows\":false}}",RuntimePlatform.Browser).Count==0,"Unshadowed point lights do not produce false warnings");
  byte[] gray=WebTextureCooker.Png(2,1,[0,0,0,255,255,255,255,255]);var color=StbImageSharp.ImageResult.FromMemory(WebTextureCooker.Optimize(gray,1,WebTextureRole.Color),StbImageSharp.ColorComponents.RedGreenBlueAlpha);Check(color.Data[0]>=187&&color.Data[0]<=188,"Colour reduction filters in linear light");
  byte[] alpha=WebTextureCooker.Png(2,1,[255,0,0,255,0,0,0,0]);var edge=StbImageSharp.ImageResult.FromMemory(WebTextureCooker.Optimize(alpha,1,WebTextureRole.Color),StbImageSharp.ColorComponents.RedGreenBlueAlpha);Check(edge.Data[0]==255&&edge.Data[3]==127,"Transparent foliage edges preserve colour without dark halos");
  Check(ReferenceEquals(gray,WebTextureCooker.Optimize(gray,1,WebTextureRole.UserInterface)),"UI textures are never resized by material limits");
  byte[] normal=WebTextureCooker.Png(2,1,[255,128,128,255,128,255,128,255]);var nm=StbImageSharp.ImageResult.FromMemory(WebTextureCooker.Optimize(normal,1,WebTextureRole.Normal),StbImageSharp.ColorComponents.RedGreenBlueAlpha);float length=new System.Numerics.Vector3(nm.Data[0]/127.5f-1,nm.Data[1]/127.5f-1,nm.Data[2]/127.5f-1).Length();Check(Math.Abs(length-1)<.02,"Reduced normal maps remain normalized");
  File.WriteAllText(Path.Combine(root,"web-platform-tests.json"),JsonSerializer.Serialize(new{pass=true,checks,contract=BrowserRenderContract.Hash}));Console.WriteLine("WEB PLATFORM: "+checks+" checks passed");
 }
}

