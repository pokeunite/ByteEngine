using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
namespace KarlsonDemoTool;

internal static class MilkDepotAuthor
{
    internal static void Run()
    {
        using var runtime=new DemoRuntime();runtime.Database.Scan();
        var scene=runtime.Serializer.Load(Program.Root+"/Scenes/Main.bytescene");scene.Name="Milk Depot - Karlson demo";
        var player=scene.GameObjects.Single(o=>o.Name=="FPS Character");
        var hud=scene.GameObjects.Single(o=>o.GetComponent<UiCanvas>()!=null);
        foreach(var root in scene.GameObjects.Where(o=>o.Parent==null&&o!=player&&o!=hud).ToArray())scene.DestroyGameObject(root);
        player.Transform.WorldPosition=new(-24,.08f,16);player.Transform.LocalRotation=Quaternion.Identity;
        player.AddComponent(new HealthComponent{MaxHealth=100,CurrentHealth=100});
        player.GetComponent<CharacterController3D>()!.StepHeight=.3f;
        scene.CreateGameObject("Daylight").AddComponent(new DirectionalLight{Intensity=.9f,AmbientIntensity=.18f,CastShadows=true});
        scene.CreateGameObject("Depot sky").AddComponent(new SkyEnvironment{AmbientIntensity=.35f,EnvironmentIntensity=.4f,Exposure=.85f,DrawSky=true,SkyIntensity=.5f,ZenithColor=new(.18f,.29f,.43f),HorizonColor=new(.52f,.61f,.68f)});
        using var catalog=JsonDocument.Parse(File.ReadAllText(Program.Root+"/Assets/Environment/MilkDepot/asset_catalog.json"));
        var sizes=catalog.RootElement.GetProperty("assets").EnumerateArray().ToDictionary(a=>a.GetProperty("id").GetString()!,a=>a.GetProperty("dimensions_m").EnumerateArray().Select(v=>v.GetSingle()).ToArray());
        var floors=new HashSet<(int x,int z)>();GameObject? milk=null;
        foreach(var placement in catalog.RootElement.GetProperty("placements").EnumerateArray())
        {
            string key=placement.GetProperty("asset").GetString()!;
            var p=placement.GetProperty("position").EnumerateArray().Select(v=>v.GetSingle()).ToArray();
            var position=new Vector3(p[0],p[2],-p[1]);float angle=placement.GetProperty("rotation_z").GetSingle();
            if(key=="Foundation_2m")continue;
            var obj=Model(scene,runtime,key,position,angle);
            if(key=="Floor_2m"){floors.Add(((int)position.X,(int)position.Z));continue;}
            if(key=="Milk_Bottle"){milk=obj;continue;}
            if(key is "Ammo_Pickup" or "Ammo_Case"){obj.AddComponent(new WeaponAmmoPickup3D{EquipmentObjectId=player.Id,ReserveAmount=key=="Ammo_Case"?30:18});continue;}
            if(key=="Training_Dummy")
            {obj.Layer=2;obj.AddComponent(new CapsuleCollider3D{Radius=.35f,Height=1.8f,Center=new(0,.9f,0)});obj.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Static,UseGravity=false});obj.AddComponent(new HealthComponent{MaxHealth=100,CurrentHealth=100,DestroyOnDeath=true});continue;}
            if(key.StartsWith("Stair_"))
            {
                int n=key=="Stair_1m"?5:14;float rise=n==5?1:2.5f,run=n*.3f;
                for(int i=0;i<n;i++){float h=rise*(i+1)/n;Box(obj,new(0,h/2,-(-run/2+(i+.5f)*.3f)),new(2,.0f+h,.3f));}
            }
            else if(key.StartsWith("Doorway"))
            {Box(obj,new(-1.75f,1.5f,0),new(.5f,3,.4f));Box(obj,new(1.75f,1.5f,0),new(.5f,3,.4f));Box(obj,new(0,2.8f,0),new(3,.4f,.4f));}
            else if(key=="Milk_Exit_Tower")
            {Box(obj,new(-1.85f,4,0),new(.9f,8,4.6f));Box(obj,new(1.85f,4,0),new(.9f,8,4.6f));Box(obj,new(0,5.5f,0),new(2.8f,5,4.6f));Box(obj,new(0,1.5f,-2.1f),new(3,3,.4f));}
            else if(key.StartsWith("Balcony")||key.StartsWith("Landing"))Box(obj,new(0,-.14f,0),new(2,.28f,2));
            else if(key.StartsWith("Railing"))Box(obj,new(0,.55f,0),new(2,1.1f,.09f));
            else if(key.StartsWith("Wall")||key.StartsWith("Cover")||key.StartsWith("Crate")||key.StartsWith("TrainingStep")||key.StartsWith("JumpPlatform")||key is "Weapon_Rack" or "Depot_Terminal" or "Utility_Drum" or "Exit_Door" or "Milk_Objective_Pedestal")
            {var s=sizes[key];Box(obj,new(0,s[2]/2,0),new(s[0],s[2],s[1]));}
            if(obj.Components.Any(c=>c is Collider3D))obj.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Static,UseGravity=false});
        }
        // Merge adjacent floor cells into thick static strips instead of hundreds of rigid bodies.
        foreach(var row in floors.GroupBy(p=>p.z))
        {
            var xs=row.Select(p=>p.x).Order().ToArray();int start=xs[0],end=start;
            void Floor(){var o=scene.CreateGameObject("Solid depot floor");o.Transform.WorldPosition=new((start+end)/2f,-.5f,row.Key);Box(o,Vector3.Zero,new(end-start+2,1,2));o.AddComponent(new Rigidbody3D{BodyType=RigidbodyBodyType3D.Static,UseGravity=false});}
            foreach(int x in xs.Skip(1)){if(x==end+2)end=x;else{Floor();start=end=x;}}Floor();
        }
        var status=scene.CreateGameObject("Course status");status.SetParent(hud,false);
        status.AddComponent(new UiText{Text="MILK DEPOT",Offset=new(28,58),FontSize=19,WrapWidth=1150});
        scene.CreateGameObject("Course rules").AddComponent(new MilkDepotCourse3D{PlayerObjectId=player.Id,StatusTextObjectId=status.Id,MilkObjectId=milk!.Id});
        runtime.Serializer.Save(scene,Program.Root+"/Scenes/Milk Depot.bytescene");
        var projectPath=Program.Root+"/karlson demo.byteproject";var project=System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(projectPath))!;
        Directory.CreateDirectory(Program.Root+"/.byteengine/Backups/Before Milk Depot");if(!File.Exists(Program.Root+"/.byteengine/Backups/Before Milk Depot/karlson demo.byteproject"))File.Copy(projectPath,Program.Root+"/.byteengine/Backups/Before Milk Depot/karlson demo.byteproject");
        project["startupScene"]="Scenes/Milk Depot.bytescene";File.WriteAllText(projectPath,project.ToJsonString(new(){WriteIndented=true}));
        Console.WriteLine("MILK DEPOT AUTHORED "+scene.GameObjectCount+" objects");
    }
    static void Box(GameObject owner,Vector3 center,Vector3 size)=>owner.AddComponent(new BoxCollider3D{Center=center,Size=size});
    static GameObject Model(Scene scene,DemoRuntime runtime,string key,Vector3 position,float angle)
    {
        var reference=runtime.Database.ResolveReference("Assets/Environment/MilkDepot/Models/"+key+".glb");var model=runtime.Assets.LoadModel(reference);
        var root=scene.CreateGameObject(key);root.Transform.WorldPosition=position;root.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,angle+MathF.PI);
        root.AddComponent(new ModelHierarchyInstance{Model=reference});
        var byKey=model.Nodes.ToDictionary(n=>n.Key);
        var matrices=new Dictionary<string,Matrix4x4>();
        Matrix4x4 Global(string key){if(matrices.TryGetValue(key,out var cached))return cached;var n=byKey[key];return matrices[key]=n.LocalTransform*(n.ParentKey is {} parent?Global(parent):Matrix4x4.Identity);}
        root.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,angle);
        foreach(var node in model.Nodes)
        foreach(string meshKey in node.MeshKeys)
        {
            var mesh=model.Meshes.Single(m=>m.Key==meshKey);var part=scene.CreateGameObject(key+" / "+mesh.Name);part.SetParent(root,false);
            Matrix4x4.Decompose(Global(node.Key)*Matrix4x4.CreateRotationY(MathF.PI),out var scale,out var rotation,out var translation);
            part.Transform.LocalScale=scale;part.Transform.LocalRotation=rotation;part.Transform.LocalPosition=translation;
            var material=model.Materials.Single(m=>m.Key==mesh.MaterialKey);
            part.AddComponent(new MeshRenderer{MeshReference=new ModelMeshReference(reference,meshKey),MaterialReference=new ModelMaterialReference(reference,mesh.MaterialKey!),Material=new Material{BaseColor=material.BaseColor,Roughness=material.Roughness,Metallic=material.Metallic}});
        }
        return root;
    }
}
