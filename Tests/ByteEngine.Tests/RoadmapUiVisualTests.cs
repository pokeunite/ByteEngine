using System.Numerics;
using System.IO.Compression;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
namespace ByteEngine.Tests;
internal static class RoadmapUiVisualTests
{
 public static void Run()
 {
  string output=Path.GetFullPath(".artifacts/roadmap-ui-visual");Directory.CreateDirectory(output);
  using var window=new NativeWindow(new NativeWindowSettings{ClientSize=new(1280,720),StartVisible=false,API=ContextAPI.OpenGL,APIVersion=new(3,3),Profile=ContextProfile.Core});window.Context.MakeCurrent();GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
  using var renderer=new Renderer2D();renderer.Initialize(1280,720);using var renderer3D=new Renderer3D();using var framebuffer=new SceneFramebuffer();
  var scene=new Scene("Responsive UI preview");var canvas=scene.CreateGameObject("Canvas");canvas.AddComponent(new UiCanvas());canvas.AddComponent(new UiTheme());
  var panel=scene.CreateGameObject("Scroll panel");panel.SetParent(canvas,false);panel.AddComponent(new UiWidget{Size=new(600,260),Offset=new(40,40)});var scroll=panel.AddComponent(new UiScrollContainer{ContentHeight=600});panel.AddComponent(new UiContainer{Kind=UiContainerKind.Column,Spacing=new(12),Padding=new(16)});
  var labels=new[]{"Recover the stranded vehicle","Gestrandetes Fahrzeug bergen und zur Werkstatt zurückbringen","Récupérer le véhicule immobilisé et retourner à l’atelier","Accept and deploy","Return to workshop","Completed contracts"};var buttons=new List<UiWidget>();
  foreach(var label in labels){var obj=scene.CreateGameObject(label);obj.SetParent(panel,false);buttons.Add(obj.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Size=new(550,70),Label=label,FontSize=24,MinimumFontSize=12,AutoFitLabel=true,ThemeKey="primary"}));}
  foreach(var size in new[]{(1280,720),(1920,1080),(800,600),(1600,600),(720,1280)})
  {
   for(int frame=0;frame<2;frame++)framebuffer.RenderGame(renderer,renderer3D,scene,EditorMode.Edit,size.Item1,size.Item2,1280,720,uiOnly:true);
   GL.Finish();var pixels=new byte[size.Item1*size.Item2*4];GL.BindTexture(TextureTarget.Texture2D,(int)framebuffer.TextureId);GL.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);GL.BindTexture(TextureTarget.Texture2D,0);
   if(GL.GetError()!=ErrorCode.NoError||pixels.Where((v,i)=>i%4==0&&v>100).Count()<100)throw new Exception("UI authoring preview is blank or GL error");
   var viewport=new Vector2(size.Item1,size.Item2);var rect=UiLayout.Resolve(buttons[0].GameObject,buttons[0].Anchor,buttons[0].Offset,buttons[0].Size,viewport);
   if(!buttons[0].Contains(rect.Position+rect.Size*.5f,viewport))throw new Exception("Preview and gameplay hit-test disagree");
   Png(Path.Combine(output,$"ui-{size.Item1}x{size.Item2}.png"),pixels,size.Item1,size.Item2);
  }
  ByteEngine.Core.Input.SetGameViewPointer(new(.95f,.95f),new(1280,720),true);UiNavigation.Focus(null);
  for(int press=0;press<buttons.Count;press++)
  {
   var released=new ByteEngine.Core.InputSystem.RawInputSnapshot();ByteEngine.Core.Input.UpdatePortable(released,false);ByteEngine.Core.Time.Update(1d/60);UiNavigation.Update(scene);
   var down=new ByteEngine.Core.InputSystem.RawInputSnapshot();down.Gamepad.ButtonsDown.Add(ByteEngine.Core.InputSystem.GamepadControl.DPadDown);ByteEngine.Core.Input.UpdatePortable(down,false);ByteEngine.Core.Time.Update(1d/60);UiNavigation.Update(scene);
  }
  if(scroll.ScrollY<=0||UiNavigation.Focused!=buttons[^1])throw new Exception("Controller focus does not reveal last row");
  var activate=new ByteEngine.Core.InputSystem.RawInputSnapshot();activate.Gamepad.ButtonsDown.Add(ByteEngine.Core.InputSystem.GamepadControl.South);ByteEngine.Core.Input.UpdatePortable(activate,false);ByteEngine.Core.Time.Update(1d/60);UiNavigation.Update(scene);if(!buttons[^1].WasClicked)throw new Exception("Controller confirm does not activate focused button");UiNavigation.Focus(null);
  Console.WriteLine("PASS actual UI authoring framebuffer at five sizes, long German/French labels, shared hit testing and focus reveal; screenshots saved");
 }
    private static void Png(string file,byte[] rgba,int width,int height)
    {
        using var output=File.Create(file);output.Write(new byte[]{137,80,78,71,13,10,26,10});byte[] header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),width);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),height);header[8]=8;header[9]=6;Chunk(output,"IHDR",header);
        using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))for(int y=height-1;y>=0;y--){z.WriteByte(0);z.Write(rgba,y*width*4,width*4);}Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);
    }
    private static void Chunk(Stream s,string type,byte[] data)
    {
        byte[] name=System.Text.Encoding.ASCII.GetBytes(type),length=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length,data.Length);s.Write(length);s.Write(name);s.Write(data);uint crc=0xffffffff;foreach(byte b in name.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length,~crc);s.Write(length);
    }
}
