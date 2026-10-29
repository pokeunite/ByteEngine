using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ByteEngine.Core;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Core.InputSystem;

static class RuntimePerformanceChecks
{
    public static void Run()
    {
        void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS: "+name);}
        var buffers=new FrameTransferBuffers(64*1024);var source=Enumerable.Range(0,4096).Select(i=>(byte)i).ToArray();
        int a=buffers.Copy(source),b=buffers.Copy(source);source[0]=17;
        Check(buffers[a][0]==0&&!ReferenceEquals(buffers[a],buffers[b]),"frame buffers isolate asset data and simultaneous handles");
        for(int i=0;i<20;i++){buffers.BeginFrame();buffers.Copy(source);buffers.Copy(source);}
        long created=buffers.CreatedBytes,start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<200;i++){buffers.BeginFrame();buffers.Copy(source);buffers.Copy(source);}
        Check(buffers.CreatedBytes==created&&GC.GetAllocatedBytesForCurrentThread()-start<4096,"warmed frame transfers allocate no per-frame byte arrays");
        buffers.BeginFrame();buffers.Allocate(100000,out _);buffers.BeginFrame();Check(buffers.RetainedBytes<=64*1024,"frame buffer retention remains bounded after oversized frames");

        var data=new GameObjectData {Id=Guid.NewGuid(),Name="Source",Tags=[Guid.NewGuid()],ParentSocket="Hand",AttachmentScaleRule=AttachmentTransformRule.KeepWorld,
            Transform=new(){LocalPosition=new(){X=2,Y=3,Z=4},LocalScale=new(){X=1,Y=2,Z=3}},AttachmentOffset=new(){Rotation=17},
            Components=[new(){Type="Fixture",Properties=new JsonObject{["nested"]=new JsonObject{["number"]=42}}}],Variables=[new(){Name="HP",Value=VariableValue.FromNumber(100)}]};
        var copy=data.DeepClone();Check(JsonSerializer.Serialize(data)==JsonSerializer.Serialize(copy),"typed blueprint clone preserves the complete serialized object");
        copy.Transform.LocalPosition!.X=99;copy.Components[0].Properties["nested"]!["number"]=99;copy.Tags.Clear();
        Check(data.Transform.LocalPosition!.X==2&&data.Components[0].Properties["nested"]!["number"]!.GetValue<int>()==42&&data.Tags.Count==1,"blueprint instances cannot mutate their shared template");

        var grid=new NavigationGrid(64,64);var first=grid.FindPath(Vector3.Zero,new(63,0,63));
        Check(first.Count==64,"dense A* retains the diagonal shortest path");
        for(int i=0;i<10;i++)grid.FindPath(Vector3.Zero,new(63,0,63));
        start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)grid.FindPath(Vector3.Zero,new(63,0,63));
        Check(GC.GetAllocatedBytesForCurrentThread()-start<400000,"A* reuses search state instead of allocating maps and queues each query");
        grid.SetCell(1,1,true);var changed=grid.FindPath(Vector3.Zero,new(63,0,63));
        Check(changed.Count>first.Count&&first.Count==64&&changed.All(p=>p.X!=1.5f||p.Z!=1.5f),"path workspaces observe live obstacles and preserve earlier path ownership");
        Check(grid.FindPath(Vector3.Zero,new(63,0,63),1).Count==0,"path search still respects visited-node budgets");
        var sparse=new NavigationGrid(513,513);Check(sparse.FindPath(Vector3.Zero,new(2,0,2)).Count==3,"large grids use the bounded sparse fallback");

        using var loop=new PortableGameLoop();var scene=new Scene("Render cache invalidation");scene.CreateGameObject("Camera").AddComponent(new Camera3D());
        var parent=scene.CreateGameObject("LOD parent");var one=scene.CreateGameObject("One");one.SetParent(parent,false);one.AddComponent(new MeshRenderer{UsePrimitive=true});
        var two=scene.CreateGameObject("Two");two.SetParent(parent,false);two.AddComponent(new MeshRenderer{UsePrimitive=true});
        var sink=new RenderSink();loop.Scenes.LoadScene(scene);
        void Tick()=>loop.Tick(.016,new RawInputSnapshot(),320,200,new(.5f),true,false,sink);
        Tick();Check(sink.Items.Length==2,"render cache begins with both authored meshes");
        var group=parent.AddComponent(new MeshLodGroup{Distances=new(100,200)});Tick();Check(sink.Items.Length==1,"adding an ancestor LOD invalidates cached absence");
        group.Enabled=false;Tick();Check(sink.Items.Length==2,"cached LOD ancestry observes live enabled state");
        one.Transform.LocalPosition=new(10,0,0);Tick();Check(sink.Items.Any(s=>Math.Abs(s.WorldBounds.Center.X-10)<.01f),"cached bounds follow live transform changes");
        group.Enabled=true;one.SetParent(null,true);Tick();Check(sink.Items.Length==2,"reparenting refreshes render ancestry and maintains world placement");
    }
    sealed class RenderSink:IRenderFrameSink
    {
        public RenderSubmission[] Items=[];
        public void Draw3D(RenderView3D? view,RenderLighting3D lighting,RenderEnvironment3D env,IReadOnlyList<RenderSubmission> items)=>Items=items.ToArray();
        public void DrawText(string value,string? font,int size,Vector2 position,Vector4 color,float wrap,UiAnchor anchor){}
        public void DrawQuad(Vector2 position,Vector2 size,Vector4 color){}
        public void DrawImage(Texture2D texture,Vector2 position,Vector2 size,Vector4 color){}
    }
}
