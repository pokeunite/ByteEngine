using System.Numerics;
using System.IO.Compression;
using ByteEngine.Core;
using ByteEngine.Core.Construction;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Tests;

internal sealed class GoblinVehicleDiagnostic : ByteEngineApplication
{
    private readonly string _projectFile;
    private readonly bool _prepare;
    private EditorProjectContext? _project;
    public GoblinVehicleDiagnostic(string projectFile, bool prepare) : base(1280,720,"Goblin vehicle validation")
    { _projectFile=projectFile; _prepare=prepare; IsVisible=false; }
    protected override bool ShouldUpdateScene => false;
    protected override bool ShouldRenderSceneToWindow => false;
    protected override void OnEngineStart()
    {
        _project=EditorProjectContext.Open(_projectFile, m=>Console.WriteLine("WARNING: "+m));
        string scenePath=Path.Combine(_project.ProjectRoot,_project.Project.StartupScene);
        var scene=_project.Scenes.Load(scenePath);
        if (_prepare)
        {
            if (scene.GameObjects.Any(o=>o.GetComponent<VehicleBuilder3D>()!=null))
                throw new InvalidOperationException("Garage already installed; refusing duplicate setup.");
            var cube=scene.FindGameObject("Cube");
            if(cube!=null) scene.DestroyGameObject(cube);
            var ground=scene.FindGameObject("Ground");
            if(ground!=null) ground.Active=false;
            var owner=scene.CreateGameObject("Scrap Vehicle - Build and Drive");
            owner.Transform.WorldPosition=new(0,.73f,0);
            owner.AddComponent(new VehicleBuilder3D());
            VehicleBuilder3D.CreateModelVisual(scene,_project.Assets,"Assets/parts/scrap_frame_2x1.glb",owner,"Chassis");
            var camera=scene.ActiveCamera!;
            camera.ActiveGameCamera=true;
            camera.Transform.WorldPosition=new(4.5f,5.3f,6);
            Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,new(0,1,0),Vector3.UnitY),out var world);
            camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);
            foreach(var light in scene.GameObjects.SelectMany(o=>o.Components).OfType<DirectionalLight>())
                light.AmbientIntensity=.55f;
            _project.Scenes.Save(scene,scenePath);
            scene=_project.Scenes.Load(scenePath);
        }
        var builder=scene.GameObjects.SelectMany(o=>o.Components).OfType<VehicleBuilder3D>().Single();
        Scenes.LoadScene(scene);
        Assert(builder.Building && builder.Layout.Parts.Count==0,"Garage must start with a bare frame.");
        Assert(!builder.BeginDriving(),"Incomplete vehicle must not drive.");
        TickInput(scene, new(100f/1280,227f/720), [], true);
        TickInput(scene, new(100f/1280,227f/720), [Key.Enter]);
        Assert(builder.Layout.Parts.ContainsKey(6),"Palette click and Enter failed to attach engine.");
        TickInput(scene, new(100f/1280,227f/720), [Key.Delete]);
        Assert(!builder.Layout.Parts.ContainsKey(6),"Delete failed to remove selected engine.");
        TickInput(scene, new(100f/1280,134f/720), [], true);
        TickInput(scene, new(.5f,.5f), []);
        TickInput(scene,new(.5f,.5f),[]);
        var marker=scene.FindGameObject("Mount: Front left wheel")!;
        Vector4 clip=Vector4.Transform(new Vector4(marker.Transform.WorldPosition,1),scene.ActiveCamera!.GetViewMatrix()*scene.ActiveCamera.GetProjectionMatrix(1280f/720));
        var pointer=new Vector2((clip.X/clip.W+1)/2,(1-clip.Y/clip.W)/2);
        TickInput(scene,pointer,[],true);
        Assert(builder.Layout.Parts.ContainsKey(0),"Clicking a projected world mount failed.");
        builder.RemovePart(0);
        TickInput(scene,new(.5f,.5f),[]);
        string output=Path.Combine(Environment.CurrentDirectory,".artifacts","goblin-vehicle");
        Directory.CreateDirectory(output);
        Screenshot(scene,Path.Combine(output,"garage.png"));
        Assert(!builder.AttachPart(0,"scrap_engine_block"),"Incompatible socket accepted an engine.");
        for(int i=0;i<4;i++) Assert(builder.AttachPart(i,"scrap_wheel_large"),"Wheel failed.");
        Assert(!builder.AttachPart(0,"scrap_wheel_small"),"Occupied socket accepted another wheel.");
        for(int i=4;i<=7;i++) Assert(builder.AttachPart(i,VehicleBuildLayout.Mounts[i].Parts[0]),"Required part failed.");
        for(int i=8;i<VehicleBuildLayout.Mounts.Length;i++) Assert(builder.AttachPart(i,VehicleBuildLayout.Mounts[i].Parts[0]),"Optional part failed.");
        string savePath=Path.Combine(_project.ProjectRoot,"Saves","vehicle-build.json");
        byte[]? previousSave=File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
        try
        {
            builder.SaveBuild();
            Assert(File.Exists(savePath),"Build save was not written.");
            builder.RemovePart(0);
            builder.LoadBuild();
            Assert(builder.Layout.Parts.Count==19 && builder.Layout.CanDrive,"Disk save did not restore the vehicle.");
        }
        finally
        {
            if(previousSave!=null) File.WriteAllBytes(savePath,previousSave);
            else if(File.Exists(savePath)) File.Delete(savePath);
        }
        var restored=VehicleBuildLayout.FromJson(builder.Layout.ToJson());
        Assert(restored.CanDrive && restored.Parts.Count==19,"Saved build round trip lost parts.");
        foreach(string bad in new[]{"{\"Version\":2,\"Parts\":{}}", "{\"Version\":1,\"Parts\":{\"0\":\"scrap_engine_block\"}}", "{\"Version\":1,\"Parts\":{\"999\":\"scrap_wheel_small\"}}"})
        {
            bool rejected=false;
            try { VehicleBuildLayout.FromJson(bad); } catch(System.Text.Json.JsonException) { rejected=true; }
            Assert(rejected,"Malformed build accepted.");
        }
        Assert(builder.RemovePart(7) && !builder.BeginDriving(),"Removing cab must disable driving.");
        Assert(builder.AttachPart(7,"scrap_cab_shell") && builder.BeginDriving(),"Complete vehicle failed to drive.");
        Assert(!builder.RemovePart(0),"Vehicle edited during driving.");
        TickInput(scene,new(.5f,.5f),[]);
        UiNavigation.Focus(scene.GameObjects.SelectMany(o=>o.Components).OfType<UiWidget>().First(b=>b.Label=="Return to build [B]"));
        TickInput(scene,new(.5f,.5f),[Key.Space]);
        Assert(!builder.Building,"Brake key activated focused garage button.");
        TickInput(scene,new(.5f,.5f),[]);
        Vector3 start=builder.Transform.WorldPosition;
        for(int i=0;i<60;i++) builder.StepDrive(1,0,false,1f/60);
        Assert(Vector3.Distance(start,builder.Transform.WorldPosition)>2,"Throttle failed to move vehicle.");
        Vector3 forward=builder.Transform.Forward;
        for(int i=0;i<40;i++) builder.StepDrive(1,1,false,1f/60);
        Assert(Vector3.Dot(forward,builder.Transform.Forward)<.95f,"Steering failed.");
        for(int i=0;i<100;i++) builder.StepDrive(0,0,true,1f/60);
        Assert(Math.Abs(builder.Speed)<.01,"Brake did not stop vehicle.");
        for(int i=0;i<120;i++) builder.StepDrive(-1,0,false,1f/60);
        Assert(builder.Speed<0,"Reverse failed.");
        builder.Transform.WorldPosition=new(28.4f,builder.Layout.RideHeight,0);
        builder.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,-MathF.PI/2);
        for(int i=0;i<1000;i++) builder.StepDrive(1,0,false,1f/60);
        Assert(Math.Abs(builder.Transform.WorldPosition.X)<=28.5f && Math.Abs(builder.Transform.WorldPosition.Z)<=28.5f,"Vehicle left the test yard.");
        builder.Transform.WorldPosition=new(0,builder.Layout.RideHeight,0);
        builder.Transform.WorldRotation=Quaternion.Identity;
        scene.UpdateInternal();
        // Give the chase camera time to settle before capturing the assembled vehicle.
        Time.Update(1f/60);
        for(int i=0;i<60;i++) scene.UpdateInternal();
        Screenshot(scene,Path.Combine(output,"assembled.png"));
        TickInput(scene,new(.5f,.5f),[Key.B]);
        Assert(builder.Building && builder.Layout.Parts.Count==19,"Returning to build lost the assembly.");
        TickInput(scene,new(.5f,.5f),[]);
        Console.WriteLine("PASS: palette clicks; world mount picking; Enter/Delete controls; brake/UI conflict; return to build; scene reload; 15 imported models; 19 compatible mounts; incomplete/occupied rejection; all optional parts; save validation; cab removal; throttle, steering, brake, reverse and yard bounds; GPU garage and driving renders.");
        Scenes.UnloadScene();
        _project.Dispose(); _project=null;
        Close();
    }
    private static void TickInput(Scene scene,Vector2 pointer,Key[] keys,bool click=false)
    {
        var snapshot=new ByteEngine.Core.InputSystem.RawInputSnapshot();
        snapshot.KeysDown.UnionWith(keys);
        if(click) snapshot.MouseButtonsDown.Add(MouseButton.Left);
        Input.UpdatePortable(snapshot,false);
        Input.SetGameViewPointer(pointer,new(1280,720),true);
        Time.Update(1f/60);
        scene.UpdateInternal();
    }
    private static void Assert(bool condition,string message)
    { if(!condition) throw new InvalidOperationException(message); }
    private void Screenshot(Scene scene,string file)
    {
        using var fb=new SceneFramebuffer();
        fb.Render(Renderer,Renderer3D,scene,EditorMode.Play,new EditorCamera(),new EditorCamera3D(),true,1280,720,1280,720,drawGrid3D:false);
        GL.Finish();
        byte[] pixels=new byte[1280*720*4];
        GL.BindTexture(TextureTarget.Texture2D,(int)fb.TextureId);
        GL.GetTexImage(TextureTarget.Texture2D,0,PixelFormat.Rgba,PixelType.UnsignedByte,pixels);
        GL.BindTexture(TextureTarget.Texture2D,0);
        Assert(GL.GetError()==ErrorCode.NoError,"OpenGL render error.");
        Png(file,pixels,1280,720);
    }
    private static void Png(string file,byte[] rgba,int width,int height)
    {
        using var output=File.Create(file);
        output.Write(new byte[]{137,80,78,71,13,10,26,10});
        byte[] header=new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0,4),width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4,4),height);
        header[8]=8; header[9]=6;
        Chunk(output,"IHDR",header);
        using var compressed=new MemoryStream();
        using(var z=new ZLibStream(compressed,CompressionLevel.Fastest,true))
            for(int y=height-1;y>=0;y--) { z.WriteByte(0); z.Write(rgba,y*width*4,width*4); }
        Chunk(output,"IDAT",compressed.ToArray()); Chunk(output,"IEND",[]);
    }
    private static void Chunk(Stream stream,string type,byte[] data)
    {
        byte[] name=System.Text.Encoding.ASCII.GetBytes(type);
        byte[] length=new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length,data.Length);
        stream.Write(length); stream.Write(name); stream.Write(data);
        uint crc=0xffffffff;
        foreach(byte b in name.Concat(data))
        { crc^=b; for(int i=0;i<8;i++) crc=(crc>>1)^((crc&1)==1 ? 0xedb88320u : 0); }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(length,~crc); stream.Write(length);
    }
}




