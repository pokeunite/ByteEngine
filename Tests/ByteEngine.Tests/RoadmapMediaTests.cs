using System.Numerics;
using System.Diagnostics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
namespace ByteEngine.Tests;
internal static class RoadmapMediaTests
{
 static void Check(bool test,string name){if(!test)throw new Exception(name);}
 public static void Run(string root)
 {
  string wave=Path.Combine(root,"long.wav");const int rate=22050,seconds=180;
  using(var file=File.Create(wave))using(var w=new BinaryWriter(file)){int bytes=rate*seconds*2;w.Write("RIFF"u8);w.Write(36+bytes);w.Write("WAVEfmt "u8);w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write("data"u8);w.Write(bytes);for(int i=0;i<rate*seconds;i++)w.Write((short)(Math.Sin(i*2*Math.PI*220/rate)*100));}
  using(var reader=new StreamingPcmReader(wave)){Check(Math.Abs(reader.DurationSeconds-seconds)<.001&&reader.WorkingBufferBytes<500000,"Stream duration/bounded buffers");short[] samples=new short[65536];Check(reader.Read(samples)==samples.Length,"Stream first read");short first=samples[1];reader.Rewind();reader.Read(samples);Check(samples[1]==first,"Stream rewind");}
  string ogg=Path.Combine(Environment.CurrentDirectory,"Designs","GarageMenus","Music","Searching.ogg");
  if(File.Exists(ogg))using(var reader=new StreamingPcmReader(ogg)){short[] samples=new short[4096];Check(reader.DurationSeconds>1&&reader.Read(samples)>0&&reader.WorkingBufferBytes<500000,"Compressed Ogg streaming");reader.Rewind();Check(reader.Read(samples)>0,"Ogg rewind");}
  AudioMixer.Reset();Check(AudioEngine.EnsureInitialized(),"OpenAL initialization: "+AudioEngine.LastError);
  var scene=new Scene("Streaming audio");var source=scene.CreateGameObject("music").AddComponent(new AudioSource3D{Bus=AudioBus.Music,Loop=true,Spatial=false});source.SetStreamingClip(wave);source.Play();Check(source.IsPlaying&&source.StreamingBufferBytes>0&&source.StreamingBufferBytes<1200000,"Native stream playback/bounded memory");AudioMixer.SetVolume(AudioBus.Master,.5f);AudioMixer.SetVolume(AudioBus.Music,.4f);source.Volume=.5f;source.UpdateInternal();
  int nativeSource=(int)typeof(AudioSource3D).GetField("_source",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(source)!;
  OpenTK.Audio.OpenAL.AL.GetSource(nativeSource,OpenTK.Audio.OpenAL.ALSourcef.Gain,out float gain);Check(Math.Abs(gain-.1f)<.0001f,"Bus gain does not reach active OpenAL source");
  var fresh=scene.CreateGameObject("fresh gain").AddComponent(new AudioSource3D{Bus=AudioBus.Music,Spatial=false,Volume=.5f});fresh.SetStreamingClip(wave);fresh.Play();
  int freshSource=(int)typeof(AudioSource3D).GetField("_source",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(fresh)!;
  OpenTK.Audio.OpenAL.AL.GetSource(freshSource,OpenTK.Audio.OpenAL.ALSourcef.Gain,out gain);Check(Math.Abs(gain-.1f)<.0001f,"Bus gain does not reach new OpenAL source");fresh.Stop();scene.DestroyGameObject(fresh.GameObject);AudioMixer.SetVolume(AudioBus.Master,1);AudioMixer.SetVolume(AudioBus.Music,1);
  source.Pause();Check(!source.IsPlaying,"Stream pause");source.Play();Check(source.IsPlaying,"Stream resume");source.Stop();Check(!source.IsPlaying,"Stream stop");
  AudioMixer.MaximumVoices=1;source.Priority=0;source.Play();var important=scene.CreateGameObject("priority").AddComponent(new AudioSource3D{Priority=10,Spatial=false});important.SetStreamingClip(wave);important.Play();Check(!source.IsPlaying&&important.IsPlaying,"Voice budget/priority stealing");important.Stop();source.Stop();scene.DestroyGameObject(important.GameObject);scene.DestroyGameObject(source.GameObject);AudioMixer.Reset();AudioMixer.Duck(AudioBus.Music,.25f,1,.25f);Check(AudioMixer.EffectiveVolume(AudioBus.Music,1)==.25f,"Music ducking");AudioMixer.Reset();
  var palette=new UiThemePalette{Primary=new(.2f,.3f,.4f,1)};string theme=Path.Combine(root,"test.uitheme");palette.Save(theme);Check(UiThemePalette.Load(theme).Primary==palette.Primary,"Theme asset roundtrip");
  string fingerprint=ImportFingerprint.Create(wave,new{quality=1});Check(fingerprint!=ImportFingerprint.Create(wave,new{quality=2}),"Importer settings cache key");
  var grid=new NavigationGrid(8,3);for(int z=0;z<3;z++)grid.SetCell(3,z,true);Check(grid.FindPath(new(.5f,0,.5f),new(7.5f,0,.5f)).Count==0,"Disconnected navigation");grid.AddLink(new(2.5f,0,.5f),new(4.5f,0,.5f));Check(grid.FindPath(new(.5f,0,.5f),new(7.5f,0,.5f)).Count>0,"Authored traversal link");
  string assets=Path.Combine(root,"ManyAssets");Directory.CreateDirectory(assets);for(int i=0;i<10000;i++)File.WriteAllText(Path.Combine(assets,i+".txt"),"a");using(var db=new AssetDatabase(root,["ManyAssets"])){var timer=Stopwatch.StartNew();File.WriteAllText(Path.Combine(assets,"9000.txt"),"changed");db.RefreshPaths(["ManyAssets/9000.txt"]);timer.Stop();Check(db.LastRefreshFileCount==1&&!db.LastRefreshWasFullScan,"Single asset edit rescans 10000 assets");Console.WriteLine($"10000 assets, one edit: {timer.Elapsed.TotalMilliseconds:F2} ms; visited {db.LastRefreshFileCount} file");}
  AudioEngine.Shutdown();
  Console.WriteLine("PASS native bounded music playback/pause/resume/stop, voice priorities, ducking, theme assets, settings fingerprints, traversal links and 10000-asset refresh");
 }
 public static void Render()
 {
  using var window=new NativeWindow(new NativeWindowSettings{ClientSize=new(128,128),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core});window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
  using var renderer=new Renderer3D();using var mesh=new Mesh(new float[]{-.4f,-.4f,0,0,0,1,0,0,.4f,-.4f,0,0,0,1,1,0,0,.4f,0,0,0,1,.5f,1},new uint[]{0,1,2});var material=new Material{BaseColor=new(.7f,.25f,.1f,1),Shading=MaterialShadingMode.Unlit};var lighting=new RenderLighting3D(null,null,1,0,0);var instances=new List<RenderSubmission>();
  for(int i=0;i<64;i++){var matrix=Matrix4x4.CreateScale(.2f)*Matrix4x4.CreateTranslation(-.875f+(i%8)*.25f,-.875f+(i/8)*.25f,0);instances.Add(new(mesh,material,matrix,mesh.LocalBounds,RenderQueue3D.Opaque,false,false,false,0,i));}
  byte[] Draw(bool batched){GL.Viewport(0,0,128,128);GL.ClearColor(0,0,0,1);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);if(batched)renderer.Draw(mesh,material,Matrix4x4.Identity,Matrix4x4.Identity,Matrix4x4.Identity,lighting,null,Array.Empty<RenderPointShadow3D>(),false,null,instances);else foreach(var item in instances)renderer.Draw(mesh,material,item.ModelMatrix,Matrix4x4.Identity,Matrix4x4.Identity,lighting);GL.Finish();byte[] pixels=new byte[128*128*4];GL.ReadPixels(0,0,128,128,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);return pixels;}
  using(var profiler=new RenderPassProfiler())
  {
      for(int frame=0;frame<6;frame++){using(profiler.Begin("Game","Geometry",128,128,true))Draw(true);using(profiler.Begin("Scene","Geometry",64,64,true))GL.Clear(ClearBufferMask.ColorBufferBit);using(profiler.Begin("Asset preview","Geometry",32,32,true))GL.Clear(ClearBufferMask.ColorBufferBit);GL.Finish();}
      Directory.CreateDirectory(".artifacts");GraphicsDiagnostics.ExportCapture(Path.GetFullPath(".artifacts/roadmap-gpu-capture.json"),new Scene("GPU regression"));
      Check(GraphicsDiagnostics.Passes.Count(p=>p.Pass=="Geometry"&&p.GpuMs.HasValue)>=3,"Viewport-attributed delayed GPU profiles missing");
  }
  var separate=Draw(false);var grouped=Draw(true);Check(separate.Where((v,i)=>Math.Abs(v-grouped[i])>1).Count()==0,"Instanced rendering differs from individual objects");Check(grouped.Where((v,i)=>i%4!=3&&v>10).Count()>500,"Instanced render is blank");Check(RenderBatcher.Build(instances).Count==1,"64 opaque objects fail to batch");Check(GL.GetError()==ErrorCode.NoError,"Instancing GL errors");
  var sw=Stopwatch.StartNew();for(int i=0;i<20;i++)Draw(false);double before=sw.Elapsed.TotalMilliseconds;sw.Restart();for(int i=0;i<20;i++)Draw(true);Console.WriteLine($"GPU {GL.GetString(StringName.Renderer)}: 64 objects -> 1 main draw; 20 synchronized frames individual={before:F2}ms instanced={sw.Elapsed.TotalMilliseconds:F2}ms; pixels agree within 1 channel value");
  const int side=24;var gridVertices=new float[side*side*8];var gridIndices=new List<uint>();
  for(int y=0;y<side;y++)for(int x=0;x<side;x++){int offset=(y*side+x)*8;gridVertices[offset]=- .8f+1.6f*x/(side-1);gridVertices[offset+1]=- .8f+1.6f*y/(side-1);gridVertices[offset+5]=1;gridVertices[offset+6]=x/(float)(side-1);gridVertices[offset+7]=y/(float)(side-1);if(x<side-1&&y<side-1){uint a=(uint)(y*side+x);gridIndices.AddRange([a,a+1,a+(uint)side,a+1,a+(uint)side+1,a+(uint)side]);}}
  using var detailed=new Mesh(gridVertices,gridIndices.ToArray());using var reduced=MeshSimplifier.Simplify(detailed,.2f);
  byte[] DrawLod(Mesh lodMesh){GL.Viewport(0,0,128,128);GL.Clear(ClearBufferMask.ColorBufferBit|ClearBufferMask.DepthBufferBit);renderer.Draw(lodMesh,material,Matrix4x4.Identity,Matrix4x4.Identity,Matrix4x4.Identity,lighting);GL.Finish();var pixels=new byte[128*128*4];GL.ReadPixels(0,0,128,128,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);return pixels;}
  var high=DrawLod(detailed);var coarse=DrawLod(reduced);int highCoverage=high.Where((v,i)=>i%4==0&&v>10).Count(),lowCoverage=coarse.Where((v,i)=>i%4==0&&v>10).Count();
  Check(reduced.IndexCount<detailed.IndexCount&&highCoverage>500&&lowCoverage>=highCoverage*.90,"Generated planar LOD loses more than 10 percent silhouette coverage");
  Console.WriteLine($"PASS generated static LOD GPU silhouette fixture: {detailed.IndexCount/3}->{reduced.IndexCount/3} triangles; coverage {lowCoverage/(float)highCoverage:P1} (budget >=90%)");
  Console.WriteLine("PASS actual GPU instancing visual/state validation");
 }
}
