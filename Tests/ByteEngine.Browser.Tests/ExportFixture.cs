using System.Numerics;
using System.Text.Json;
using System.IO.Compression;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

internal static class ExportFixture
{
    public static GamePackageResult Run(string runtime)
    {
        string parent = Path.GetFullPath(Path.Combine(".artifacts", "browser-tests", Guid.NewGuid().ToString("N")));
        string root = Path.Combine(parent, "Source");
        Directory.CreateDirectory(Path.Combine(root, "Assets")); Directory.CreateDirectory(Path.Combine(root, "Scenes"));
        string projectFile = Path.Combine(root, "Game.byteproject");
        var project = new ProjectData { Name = "Web Export Test" };
        new ProjectSerializer().Save(project, projectFile);
        File.WriteAllBytes(Path.Combine(root, "Assets", "pixel.png"), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZ1cAAAAASUVORK5CYII="));
        CreateSkin(Path.Combine(root, "Assets", "Animated.gltf"));
        CreateWave(Path.Combine(root, "Assets", "beep.wav"));
        Guid modelId;
        using (var db = new AssetDatabase(root, ["Assets","Scenes"]))
        using (var assets = new AssetManager(db))
        {
            db.TryGetAsset("Assets/pixel.png", out var image);
            db.TryGetAsset("Assets/Animated.gltf", out var model);
            db.TryGetAsset("Assets/beep.wav", out var wave);
            modelId = model!.Guid;
            var material = new MaterialAsset { Name = "Texture" };
            material.Standard.BaseColor = new(.3f,.7f,1,1);
            material.Standard.BaseColorTexture = new(image!.Guid,image.ProjectPath);
            MaterialAssetSerializer.Save(Path.Combine(root, "Assets", "Texture.bmat"), material); db.Scan();
            db.TryGetAsset("Assets/Texture.bmat", out var mat);
            var scene = new Scene("Browser Project");
            var camera = scene.CreateGameObject("Camera");
            camera.Transform.LocalPosition = new(0,.5f,4);
            camera.AddComponent(new Camera3D { ActiveGameCamera = true });
            camera.AddComponent(new AudioListener3D());
            var character = scene.CreateGameObject("Animated model");
            character.AddComponent(new ModelHierarchyInstance { Model = new(model.Guid,model.ProjectPath),
                MaterialOverride = new(mat!.Guid,mat.ProjectPath) });
            character.AddComponent(new AnimationController { Idle = "Bend", Walk = "Bend", Run = "Bend" });
            var cube = scene.CreateGameObject("Textured cube");
            cube.Transform.LocalPosition = new(1,0,0);
            cube.AddComponent(new MeshRenderer { UsePrimitive = true, MaterialAssetReference = new(mat.Guid,mat.ProjectPath) });
            var canvas = scene.CreateGameObject("Canvas"); canvas.AddComponent(new UiCanvas());
            var text = scene.CreateGameObject("Text"); text.SetParent(canvas,false);
            text.AddComponent(new UiText { Text = "Exported UI\nShared game runtime", FontSize = 24 });
            var panel = scene.CreateGameObject("Panel"); panel.SetParent(canvas,false);
            panel.AddComponent(new UiWidget { Kind = UiWidgetKind.Image, ImageReference = new(image.Guid,image.ProjectPath) });
            var sound = scene.CreateGameObject("Sound");
            sound.AddComponent(new AudioSource3D { ClipReference = new(wave!.Guid,wave.ProjectPath), PlayOnStart = true, Spatial = false });
            var codecs = new ComponentSerializer(root,db,assets,null);
            AnimationSerializationRegistrar.Register(codecs); AudioSerializationRegistrar.Register(codecs);
            new SceneSerializer(codecs,project.Classification).Save(scene,Path.Combine(root,project.StartupScene));
            db.Scan();
        }
        var before = Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.GetLastWriteTimeUtc);
        var result = WebGamePackageExporter.Export(projectFile,runtime,Path.Combine(parent,"Builds"),project.StartupScene);
        string site = Path.Combine(result.Directory,"site");
        using var archive = ZipFile.OpenRead(result.Executable);
        if (archive.GetEntry("index.html")==null || archive.GetEntry("web-game.json")==null)
            throw new Exception("HTML ZIP must have root entry page and manifest.");
        var cooked = CookedModelStore.Load(Path.Combine(site,"Content"),modelId);
        if (cooked.Skeleton==null || cooked.Animations.Count!=1 || cooked.Meshes.Count!=1)
            throw new Exception("Cooked skeleton/animation/mesh lost.");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(site,"web-game.json")));
        if (!manifest.RootElement.GetProperty("files").EnumerateArray().Any(e=>e.GetProperty("path").GetString()!.EndsWith(".ttf")))
            throw new Exception("Default font missing.");
        if(before.Any(p=>File.GetLastWriteTimeUtc(p.Key)!=p.Value))throw new Exception("Export changed source content.");
        bool rejected=false;
        try { WebGamePackageExporter.Export(projectFile,runtime,Path.Combine(root,"Assets","Build"),project.StartupScene); }
        catch(InvalidOperationException) { rejected=true; }
        if(!rejected)throw new Exception("Unsafe output not rejected.");
        Console.WriteLine("Web export ZIP, source protection, cooked animation/skeleton, fonts and safe paths passed.");
        Console.WriteLine("WEB_TEST_SITE="+site);
        return result;
    }

    private static void CreateWave(string path)
    {
        using var stream = File.Create(path); using var w = new BinaryWriter(stream);
        const int rate=8000,count=8000;
        w.Write("RIFF"u8);w.Write(36+count*2);w.Write("WAVE"u8);w.Write("fmt "u8);w.Write(16);
        w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);
        w.Write("data"u8);w.Write(count*2);for(int i=0;i<count;i++)w.Write((short)(Math.Sin(i*2*Math.PI*440/rate)*2000));
    }
    private static void CreateSkin(string path)
    {
        using var bytes = new MemoryStream(); using var w = new BinaryWriter(bytes);
        var views = new List<object>(); var accessors = new List<object>();
        int Add(float[] values,string type,int count,float[]? min=null,float[]? max=null)
        {
            int offset=(int)bytes.Position;foreach(float v in values)w.Write(v);
            int view=views.Count;views.Add(new {buffer=0,byteOffset=offset,byteLength=values.Length*4});
            int index=accessors.Count;accessors.Add(new {bufferView=view,componentType=5126,count,type,min,max});return index;
        }
        int positions=Add([-.5f,0,0,.5f,0,0,0,1,0],"VEC3",3,[-.5f,0,0],[.5f,1,0]);
        int normals=Add([0,0,1,0,0,1,0,0,1],"VEC3",3), uv=Add([0,0,1,0,.5f,1],"VEC2",3);
        int jointOffset=(int)bytes.Position;foreach(ushort v in new ushort[]{0,0,0,0,0,0,0,0,1,0,0,0})w.Write(v);
        int jointView=views.Count;views.Add(new{buffer=0,byteOffset=jointOffset,byteLength=24});
        int joints=accessors.Count;accessors.Add(new{bufferView=jointView,componentType=5123,count=3,type="VEC4"});
        int weights=Add([1,0,0,0,1,0,0,0,1,0,0,0],"VEC4",3);
        int inverse=Add([1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1,
            1,0,0,0,0,1,0,0,0,0,1,0,0,-.5f,0,1],"MAT4",2);
        int times=Add([0,1],"SCALAR",2,[0],[1]);
        int rotations=Add([0,0,0,1,0,0,.7071068f,.7071068f],"VEC4",2);
        var gltf=new {
            asset=new{version="2.0"},scene=0,scenes=new[]{new{nodes=new[]{0}}},
            nodes=new object[]{new{name="Root",children=new[]{1,3}},new{name="Hip",children=new[]{2}},
                new{name="Tip",translation=new[]{0,.5f,0}},new{name="Mesh",mesh=0,skin=0}},
            skins=new[]{new{joints=new[]{1,2},skeleton=1,inverseBindMatrices=inverse}},
            meshes=new[]{new{name="AnimatedTriangle",primitives=new[]{new{
                attributes=new{POSITION=positions,NORMAL=normals,TEXCOORD_0=uv,JOINTS_0=joints,WEIGHTS_0=weights},mode=4}}}},
            animations=new[]{new{name="Bend",samplers=new[]{new{input=times,output=rotations,interpolation="LINEAR"}},
                channels=new[]{new{sampler=0,target=new{node=2,path="rotation"}}}}},
            buffers=new[]{new{byteLength=bytes.Length,uri="data:application/octet-stream;base64,"+Convert.ToBase64String(bytes.ToArray())}},
            bufferViews=views,accessors
        };
        File.WriteAllText(path,JsonSerializer.Serialize(gltf,new JsonSerializerOptions{DefaultIgnoreCondition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull}));
    }
}
