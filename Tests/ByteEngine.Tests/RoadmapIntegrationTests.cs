using System.Numerics;
using System.Diagnostics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Navigation;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor;
using ByteEngine.Editor.Commands;
namespace ByteEngine.Tests;
internal static class RoadmapIntegrationTests
{
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);}
    public static void Run(string root)
    {
        string projectPath=Path.Combine(root,"Roadmap","Roadmap.byteproject");
        using var project=EditorProjectContext.Create(projectPath,Console.WriteLine);
        var scene=new Scene("Large undo");
        for(int i=0;i<5000;i++)scene.CreateGameObject("unrelated");
        var item=scene.CreateGameObject("selected");var unaffected=scene.GameObjects[0];
        var state=new EditorState{EditorScene=scene,Project=project.Project,ProjectFilePath=projectPath};state.SelectedObject=item;
        var undo=new UndoManager(project.Scenes,_=>{});state.Undo=undo;undo.Reset(state,true);
        var timer=Stopwatch.StartNew();undo.BeginObjectGesture(state,"Rename",[item.Id]);item.Name="renamed";undo.CommitGesture(state);timer.Stop();
        Check(undo.RetainedHistoryBytes<12000,"Single property history retains entire large scene");
        undo.Undo(state);Check(ReferenceEquals(state.EditorScene,scene)&&ReferenceEquals(scene.GameObjects[0],unaffected)&&item.Name=="selected","Undo replaced unrelated scene identity");
        undo.Redo(state);Check(item.Name=="renamed","Redo failed");
        undo.Execute(state,"Add object",()=>scene.CreateGameObject("added"));Guid added=scene.GameObjects.Last().Id;undo.Undo(state);Check(scene.FindGameObject(added)==null,"Object creation undo");undo.Redo(state);Check(scene.FindGameObject(added)!=null,"Object creation redo");
        var collider=item.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D());
        var body=item.AddComponent(new ByteEngine.Core.Physics.Rigidbody3D());
        undo.BeginObjectGesture(state,"Mass",[item.Id]);body.Mass=20;undo.CommitGesture(state);undo.Undo(state);
        Check(ReferenceEquals(item.GetComponent<ByteEngine.Core.Physics.Rigidbody3D>(),body)&&body.Mass!=20,"Component property undo replaces live component");undo.Redo(state);Check(body.Mass==20,"Component property redo");
        var second=item.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D{Center=new(2,0,0)});
        undo.BeginObjectGesture(state,"Remove component",[item.Id]);item.RemoveComponent(collider);undo.CommitGesture(state);undo.Undo(state);
        Check(ReferenceEquals(item.GetComponent<ByteEngine.Core.Physics.Rigidbody3D>(),body)&&item.Components.Contains(second),"Middle component undo destroys unaffected identities");undo.Redo(state);Check(item.Components.Contains(second)&&item.Components.Contains(body),"Middle component redo destroys unaffected identities");
        Console.WriteLine($"Compact undo: {timer.Elapsed.TotalMilliseconds:F2} ms for 5001 objects; retained {undo.RetainedHistoryBytes} bytes after six actions");
        var graph=new AssetDependencyGraph();Guid texture=Guid.NewGuid(),material=Guid.NewGuid(),model=Guid.NewGuid();graph.SetDependencies(material,[texture]);graph.SetDependencies(model,[material]);graph.SetDependencies(texture,[model]);Check(graph.Affected([texture]).Count==3,"Dependency cycle/transitive invalidation");graph.SetDependencies(material,[]);Check(graph.Affected([texture]).Count==1,"Removed dependency retained");
        using(var imports=new BackgroundImportQueue())
        {
            var published=new List<int>();Guid id=Guid.NewGuid();int mainThread=Environment.CurrentManagedThreadId;
            imports.Enqueue(id,()=>{Thread.Sleep(40);Check(Environment.CurrentManagedThreadId!=mainThread,"Import ran on owner thread");return 1;},value=>published.Add(value));
            imports.Enqueue(id,()=>2,value=>{Check(Environment.CurrentManagedThreadId==mainThread,"Publish ran on worker");published.Add(value);});
            var deadline=Stopwatch.StartNew();while(imports.PendingCount>0&&deadline.Elapsed.TotalSeconds<5)Thread.Sleep(5);imports.Pump();Check(published.SequenceEqual([2]),"Stale import result published");
        }
        string modelPath=Path.Combine(project.ProjectRoot,"Assets","AsyncFirst.obj");
        File.WriteAllText(modelPath,"v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");project.AssetDatabase.Scan();
        Check(project.AssetDatabase.TryGetAsset("Assets/AsyncFirst.obj",out var modelRecord)&&modelRecord!=null,"Async model registered");
        var reference=new AssetReference(modelRecord!.Guid,modelRecord.ProjectPath);
        Check(!project.Assets.RequestModel(reference,out _),"First-use request unexpectedly imports synchronously");
        var importDeadline=Stopwatch.StartNew();ByteEngine.Core.Assets.ModelAsset? ready=null;
        while(importDeadline.Elapsed.TotalSeconds<5){project.AssetDatabase.Update();if(project.Assets.RequestModel(reference,out ready))break;Thread.Sleep(5);}
        Check(ready?.Meshes.Count>0&&ReferenceEquals(ready,project.Assets.LoadModel(reference)),"First-use worker result was not published/cached");
        var cacheModel=new ByteEngine.Core.Assets.Importers.ImportedModel{Guid=Guid.NewGuid(),Materials=[new ByteEngine.Core.Assets.Importers.ImportedMaterial{Key="mat",BaseColorTexture=new ByteEngine.Core.Assets.Importers.ImportedTexture{Key="texture",EncodedData=[1,2,3,4]}}]};
        string textureCache=Path.Combine(root,"TextureCache");CookedModelStore.Save(textureCache,cacheModel);
        var hydrated=CookedModelStore.Load(textureCache,cacheModel.Guid,true).Materials.Single().BaseColorTexture!;
        Check(hydrated.EncodedData.SequenceEqual(new byte[]{1,2,3,4})&&hydrated.CookedContentHash==null,"Cache texture not hydrated before eviction");
        string cache=Path.Combine(root,"Eviction",".byteengine","ImportCache");Directory.CreateDirectory(cache);
        string old=Path.Combine(cache,new string('a',64)),recent=Path.Combine(cache,new string('b',64));Directory.CreateDirectory(old);Directory.CreateDirectory(recent);File.WriteAllBytes(Path.Combine(old,"data"),new byte[32]);File.WriteAllBytes(Path.Combine(recent,"data"),new byte[32]);Directory.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-1));
        string authored=Path.Combine(cache,"UserFiles");Directory.CreateDirectory(authored);File.WriteAllText(Path.Combine(authored,"keep.txt"),"keep");
        ModelImportCache.Prune(Path.Combine(root,"Eviction"),32);
        Check(!Directory.Exists(old)&&Directory.Exists(recent)&&File.Exists(Path.Combine(authored,"keep.txt")),"Cache LRU removes newest or non-cache files");
        Console.WriteLine("PASS first-use background import/publication, cache texture hydration and bounded LRU derived-data eviction");
        var baseBlueprint=new BlueprintDefinition{Name="Base",Root=new GameObjectData{Id=Guid.NewGuid(),Name="BaseRoot",Transform=new TransformData{LocalPosition=new Vector3Data(),LocalScale=new Vector3Data{X=1,Y=1,Z=1}}}};
        string basePath=Path.Combine(project.ProjectRoot,"Assets","Base.byteblueprint"),variantPath=Path.Combine(project.ProjectRoot,"Assets","Variant.byteblueprint");
        var serializer=new BlueprintSerializer(project.AssetDatabase);serializer.Save(baseBlueprint,basePath);project.AssetDatabase.RefreshPaths([basePath]);var asset=project.AssetDatabase.Assets.Single(a=>a.FullPath==basePath);
        var variant=serializer.CreateVariant(new AssetReference(asset.Guid,asset.ProjectPath),"Variant");variant.Root.Name="LocalRoot";variant.Root.Transform.LocalPosition!.X=2;serializer.Save(variant,variantPath);project.AssetDatabase.RefreshPaths([variantPath]);
        baseBlueprint.Root.Transform.LocalScale!.X=3;baseBlueprint.Root.Name="UpstreamRoot";serializer.Save(baseBlueprint,basePath);project.AssetDatabase.RefreshPaths([basePath]);
        var merged=serializer.Load(variantPath);Check(merged.Root.Name=="LocalRoot"&&merged.Root.Transform.LocalPosition!.X==2&&merged.Root.Transform.LocalScale!.X==3,"Variant lost override or ignored source updates");Check(merged.InheritanceConflicts.Any(p=>p.Contains("name")),"Variant conflict hidden");
        serializer.Save(merged,variantPath);Check(serializer.Load(variantPath).Root.Transform.LocalScale!.X==3,"Variant save/load changed inherited value");
        var navScene=new Scene("Navigation");var regionObject=navScene.CreateGameObject("region");var region=regionObject.AddComponent(new NavigationRegion3D{Width=12,Height=12,AgentRadius=.1f});
        var wall=navScene.CreateGameObject("wall");wall.Transform.LocalPosition=new(6,1,4);wall.AddComponent(new ByteEngine.Core.Characters.BoxCollider3D{Size=new(1,2,8)});region.Bake();
        var walker=navScene.CreateGameObject("walker");walker.Transform.LocalPosition=new(.5f,0,.5f);var agent=walker.AddComponent(new NavigationAgent3D{RegionObject=regionObject.Id,Speed=4});agent.SetDestination(new(11.5f,0,.5f));
        for(int i=0;i<1200&&agent.Moving;i++)agent.Tick(1f/60);
        Check(agent.Status==NavigationAgentStatus.Arrived&&agent.PathQueries==1&&Vector3.Distance(walker.Transform.WorldPosition,new(11.5f,0,.5f))<.2,"Agent did not navigate around static wall");
        var meshScene=new Scene("Mesh navigation");var surface=meshScene.CreateGameObject("Authored surface");var meshPoints=new List<Vector3>();var meshIndices=new List<uint>();
        for(int level=0;level<2;level++)
        {
            uint offset=(uint)meshPoints.Count;for(int z=0;z<8;z++)for(int x=0;x<8;x++)meshPoints.Add(new(x,level*3,z));
            for(int z=0;z<7;z++)for(int x=0;x<7;x++){if(x==3&&z<6)continue;uint a=offset+(uint)(z*8+x);meshIndices.AddRange([a,a+1,a+8,a+1,a+9,a+8]);}
        }
        surface.AddComponent(new ByteEngine.Core.Characters.MeshCollider3D{Vertices=meshPoints.ToArray(),Triangles=meshIndices.ToArray()});
        var meshRegionObject=meshScene.CreateGameObject("Mesh region");var meshRegion=meshRegionObject.AddComponent(new NavigationRegion3D{BakeMode=NavigationBakeMode.MeshSurface,SurfaceObject=surface.Id,AgentRadius=.1f});meshRegion.Bake();
        var meshPath=meshRegion.FindPath(new(.5f,0,.5f),new(6.5f,0,.5f));Check(meshPath.Count>0&&meshPath.Any(p=>p.Z>=6)&&meshPath.All(p=>Math.Abs(p.Y)<.001f),"Triangle navigation crosses hole or wrong height layer");
        Check(meshRegion.FindPath(new(.5f,0,.5f),new(.5f,3,.5f)).Count==0,"Disconnected stacked surfaces are connected implicitly");
        Check(meshRegion.Mesh!.AddLink(new(.5f,0,.5f),new(.5f,3,.5f))&&meshRegion.FindPath(new(.5f,0,.5f),new(6.5f,3,.5f)).Any(p=>p.Y==3),"Navmesh authored link loses elevated surface");
        var meshWalker=meshScene.CreateGameObject("Mesh walker");meshWalker.Transform.WorldPosition=new(.5f,0,.5f);var meshAgent=meshWalker.AddComponent(new NavigationAgent3D{RegionObject=meshRegionObject.Id,Speed=5});meshAgent.SetDestination(new(6.5f,0,.5f));
        for(int i=0;i<1200&&meshAgent.Moving;i++)meshAgent.Tick(1f/60);
        Check(meshAgent.Status==NavigationAgentStatus.Arrived&&meshAgent.PathQueries==1,"Agent fails authored mesh-surface route or rebuilds continuously");
        var restoredMeshScene=project.Scenes.Deserialize(project.Scenes.Serialize(meshScene));var restoredRegion=restoredMeshScene.FindGameObject(meshRegionObject.Id)!.GetComponent<NavigationRegion3D>()!;restoredRegion.Bake();Check(restoredRegion.BakeMode==NavigationBakeMode.MeshSurface&&restoredRegion.FindPath(new(.5f,0,.5f),new(6.5f,0,.5f)).Count>0,"Mesh region/source does not survive save and rebake");
        Console.WriteLine("PASS triangle navmesh hole routing, stacked surface isolation, elevated links and budgeted agent traversal");
        var canvas=navScene.CreateGameObject("canvas");canvas.AddComponent(new UiCanvas());var panel=navScene.CreateGameObject("scroll");panel.SetParent(canvas,false);panel.AddComponent(new UiWidget{Offset=Vector2.Zero,Size=new(200,100)});panel.AddComponent(new UiContainer{Padding=Vector4.Zero});var scroll=panel.AddComponent(new UiScrollContainer{ContentHeight=300});
        UiWidget? last=null;for(int i=0;i<5;i++){var row=navScene.CreateGameObject("row");row.SetParent(panel,false);last=row.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Size=new(200,50)});}
        Check(last!=null&&!last.Contains(new(10,240),new(1280,720)),"Clipped button receives click outside viewport");scroll.Reveal(last!,new(1280,720));var rect=UiLayout.Resolve(last!.GameObject,last.Anchor,last.Offset,last.Size,new(1280,720));Check(rect.Position.Y+rect.Size.Y<=100.01f&&scroll.ScrollY>0,"Controller focus did not reveal row");
        var roundtrip=project.Scenes.Deserialize(project.Scenes.Serialize(navScene));Check(roundtrip.GameObjects.Any(o=>o.GetComponent<NavigationRegion3D>()!=null)&&roundtrip.GameObjects.Any(o=>o.GetComponent<UiScrollContainer>()!=null),"Navigation/scroll scene roundtrip");
        Console.WriteLine("PASS compact property/object undo with identity preservation, dependency graph cycles/removal, background import latest-result/owner-thread publication, variant inheritance/conflicts/save, baked-clearance agent routing, scroll hit testing/focus and scene roundtrip");
    }
}
