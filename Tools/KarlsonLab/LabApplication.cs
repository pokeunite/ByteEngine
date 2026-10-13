using System.Numerics;
using System.IO.Compression;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using Karlson.Plugin;
using OpenTK.Windowing.Common;
using OpenTK.Graphics.OpenGL4;
namespace KarlsonLab;
internal sealed class LabApplication(bool smoke):ByteEngineApplication(1280,720,"Karlson / Byte Lab")
{
    LabRuntime? _runtime;int _frame;string _path="Scenes/MainMenu.bytescene";string? _capture;readonly List<double> _frameTimes=[];
    protected override bool CloseOnEscape=>false;
    protected override void OnLoad(){base.OnLoad();if(smoke)IsVisible=false;_runtime=new();Scenes.SceneFactory=LoadCourse;Scenes.RestartSceneFactory=()=>LoadCourse(_path);Scenes.LoadScene(LoadCourse(_path));}
    Scene LoadCourse(string path){_path=path;return _runtime!.Load(path);}
    protected override void OnEngineUpdate()
    {
        var scene=Scenes.ActiveScene;var director=scene?.FindComponent<KarlsonDirector3D>();
        if(director?.Playing==true){if(!IsGameInputCaptured)CaptureGameInput();}else ReleaseGameInput();
        if(!smoke)return;_frame++;
        if(_frame==15){_capture="menu.png";}
        if(_frame==30){Scenes.LoadScene(LoadCourse("Scenes/Course01.bytescene"));CaptureGameInput();}
        if(_frame==50){_capture="orientation.png";}
        if(_frame==60){var player=scene!.FindComponent<ParkourPlayer3D>()!;player.Equip(scene.GameObjects.Select(o=>o.GetComponent<KarlsonWeapon3D>()).First(w=>w!=null&&w.GameObject.Parent==null)!);}
        if(_frame==65)_capture="weapon-equipped.png";
        if(_frame==70){director?.Pause();_capture="pause.png";}
        if(_frame==85){Scenes.LoadScene(LoadCourse("Scenes/Course02.bytescene"));CaptureGameInput();}
        if(_frame==105)_capture="wall-run.png";
        if(_frame==115){Scenes.LoadScene(LoadCourse("Scenes/Course03.bytescene"));CaptureGameInput();}
        if(_frame==135)_capture="grapple-course.png";
        if(_frame==145){Scenes.LoadScene(LoadCourse("Scenes/Course04.bytescene"));CaptureGameInput();}
        if(_frame==165)_capture="milk-run.png";
        if(_frame==175){director?.Complete();_capture="results.png";}
        if(_frame>180&&director?.Mode!=CourseMode.Playing)director?.Begin();
        if(_frame>190)_frameTimes.Add(scene?.LastFrameMetrics.UpdateMs??0);
        if(_frame==250){File.WriteAllText(Program.Root+"/Preview/render-validation.json",JsonSerializer.Serialize(new{nativeFrames=_frame,meanUpdateMs=_frameTimes.Count>0?_frameTimes.Average():0,objects=scene?.GameObjectCount,captures=Directory.GetFiles(Program.Root+"/Preview","*.png").Length,gpu=GL.GetString(StringName.Renderer),scope="Native OpenGL render and authored UI capture; CPU update sample is diagnostic, not a gameplay benchmark"},new JsonSerializerOptions{WriteIndented=true}));Close();}
    }
    protected override void OnEngineRender()
    {
        if(_capture==null)return;var pixels=new byte[WindowWidth*WindowHeight*4];GL.ReadPixels(0,0,WindowWidth,WindowHeight,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);
        var error=GL.GetError();if(error!=ErrorCode.NoError)throw new Exception("Render capture GL error: "+error);
        Png(Program.Root+"/Preview/"+_capture,pixels,WindowWidth,WindowHeight);Console.WriteLine("CAPTURE "+_capture);_capture=null;
    }
    protected override void OnUnload(){Scenes.UnloadScene();_runtime?.Dispose();base.OnUnload();}
    static void Png(string file,byte[] rgba,int width,int height)
    {
        using var output=File.Create(file);output.Write(new byte[]{137,80,78,71,13,10,26,10});byte[] header=new byte[13];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),width);System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),height);header[8]=8;header[9]=6;Chunk(output,"IHDR",header);using var compressed=new MemoryStream();using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))for(int y=height-1;y>=0;y--){z.WriteByte(0);z.Write(rgba,y*width*4,width*4);}Chunk(output,"IDAT",compressed.ToArray());Chunk(output,"IEND",[]);
    }
    static void Chunk(Stream s,string type,byte[] data){byte[] name=System.Text.Encoding.ASCII.GetBytes(type),length=new byte[4];System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length,data.Length);s.Write(length);s.Write(name);s.Write(data);uint crc=0xffffffff;foreach(byte b in name.Concat(data)){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)==1?0xedb88320u:0);}System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length,~crc);s.Write(length);}
}
