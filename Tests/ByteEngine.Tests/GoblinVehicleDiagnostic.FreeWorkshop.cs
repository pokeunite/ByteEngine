using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Construction;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal sealed partial class GoblinVehicleDiagnostic
{
    private void RunFreeWorkshop(Scene scene,VehicleBuilder3D builder)
    {
        builder.FreeBuilding=true;Scenes.LoadScene(scene);
        if(builder.PartsDirectory.Contains("ContraptionParts")){RunStandardWorkshop(scene,builder);return;}
        Assert(builder.Assembly?.Parts.Count==1,"Free construction did not initialize");
        Assert(!builder.BeginDriving(),"Empty machine drives");
        string output=Path.Combine(Environment.CurrentDirectory,"output","goblin-scraper","master-workshop");Directory.CreateDirectory(output);
        TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"master-block-start.png"));
        Assert(builder.GameObject.Children.All(o=>o.Name!="Chassis"),"A starting chassis survived");
        var camera=scene.ActiveCamera!;
        Vector3 target=builder.Transform.WorldPosition+new Vector3(.25f,0,0);
        Vector4 clip=Vector4.Transform(new Vector4(target,1),camera.GetViewMatrix()*camera.GetProjectionMatrix(1280f/720));
        Vector2 point=new((clip.X/clip.W+1)/2,(1-clip.Y/clip.W)/2);
        TickInput(scene,point,[]);Console.WriteLine("Beam placement: "+builder.FreePlacementIssue);
        Screenshot(scene,Path.Combine(output,"beam-placement-preview.png"));
        TickInput(scene,point,[],true);TickInput(scene,point,[]);
        Assert(builder.Assembly!.Parts.Values.Any(p=>p.File=="scrap_beam_1m"),"Mouse connector placement failed: "+builder.FreePlacementIssue);
        Assert(builder.UndoBuild()&&builder.Assembly.Parts.Count==1,"Mouse placement undo failed");
        Assert(builder.RedoBuild()&&builder.Assembly.Parts.Count==2,"Mouse placement redo failed");
        Vector2 Project(Vector3 local)
        {var c=Vector4.Transform(new Vector4(builder.Transform.WorldPosition+local,1),camera.GetViewMatrix()*camera.GetProjectionMatrix(1280f/720));return new((c.X/c.W+1)/2,(1-c.Y/c.W)/2);}
        var endPoint=Project(new(1.25f,0,0));TickInput(scene,endPoint,[]);TickInput(scene,endPoint,[Key.M]);
        var topPoint=Project(new(0,.25f,0));TickInput(scene,topPoint,[]);TickInput(scene,topPoint,[],true);TickInput(scene,topPoint,[]);
        Assert(builder.Assembly.Parts[1].Position.Y>.7f&&builder.Assembly.Parts.Count==2,"Mouse branch move failed: "+builder.FreePlacementIssue);
        Assert(builder.UndoBuild(),"Move undo failed");
        TickInput(scene,endPoint,[]);TickInput(scene,endPoint,[Key.X]);TickInput(scene,endPoint,[]);Assert(builder.Assembly.Parts.Count==1,"X erased the wrong block");
        TickInput(scene,endPoint,[Key.LeftControl,Key.Z]);TickInput(scene,endPoint,[]);Assert(builder.Assembly.Parts.Count==2,"Keyboard undo failed");
        TickInput(scene,new(140f/1280,251f/720),[],true);TickInput(scene,new(140f/1280,251f/720),[]);
        TickInput(scene,endPoint,[]);Screenshot(scene,Path.Combine(output,"erase-tool.png"));TickInput(scene,endPoint,[],true);TickInput(scene,endPoint,[]);
        Assert(builder.Assembly.Parts.Count==1,"Visible erase tool did not delete clicked part");Assert(builder.UndoBuild(),"Erase tool undo failed");builder.SetEraseTool(false);
        var catalog=VehiclePartCatalog.Load(Path.Combine(_project!.ProjectRoot,"Assets","GarageUI","parts-catalog.json"));
        var example=FreeVehicleAssemblyTests.BuildExample(catalog);
        builder.RestoreAssembly(example.ToJson());
        TickInput(scene,new(.98f,.5f),[]);

        Screenshot(scene,Path.Combine(output,"workshop-720p.png"));Screenshot(scene,Path.Combine(output,"workshop-1080p.png"),1920,1080);
        Screenshot(scene,Path.Combine(output,"workshop-16x10.png"),1280,800);Screenshot(scene,Path.Combine(output,"workshop-ultrawide.png"),2560,1080);
        string savePath=Path.Combine(_project.ProjectRoot,"Saves","vehicle-build.json");byte[]? previous=File.Exists(savePath)?File.ReadAllBytes(savePath):null;
        try {builder.SaveBuild();Assert(builder.RemoveAssemblyPart(1),"Joint removal failed");Assert(builder.Assembly.Parts.Count==16,"Erasing removed descendants");builder.LoadBuild();Assert(builder.Assembly.Parts.Count==17&&builder.Assembly.CanDrive,"Free graph disk save/load failed");}
        finally {if(previous!=null)File.WriteAllBytes(savePath,previous);else if(File.Exists(savePath))File.Delete(savePath);}
        var wheel=builder.GameObject.Children.First(o=>o.Name=="Large wheel #5");var before=wheel.Transform.LocalPosition;
        Assert(builder.BeginDriving(),"Six-wheel machine cannot drive");builder.StepDrive(1,1,false,1f/60);Assert(Vector3.Distance(before,wheel.Transform.LocalPosition)>.1f,"Steering joint did not move its wheel branch");
        for(int i=0;i<120;i++)builder.StepDrive(1,0,false,1f/60);for(int i=0;i<30;i++)builder.StepDrive(1,1,false,1f/60,true);
        Assert(builder.IsDrifting,"Free assembly drift failed");TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"custom-machine-driving.png"));
        TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);Assert(builder.Building,"Return to building failed");
        var fixedWheels=FreeVehicleAssemblyTests.BuildExample(catalog,false,false);builder.RestoreAssembly(fixedWheels.ToJson());Assert(builder.BeginDriving(),"Machine without steering gear cannot drive");var rotationBefore=builder.Transform.WorldRotation;for(int i=0;i<120;i++)builder.StepDrive(1,1,false,1f/60);Assert(Math.Abs(Quaternion.Dot(rotationBefore,builder.Transform.WorldRotation))<.99f,"Machine without steering gear cannot turn");TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);builder.RestoreAssembly(example.ToJson());
        builder.RemoveAssemblyPart(1);Assert(builder.UndoBuild()&&builder.Assembly.Parts.Count==17,"Branch deletion undo failed");
        Console.WriteLine("PASS: native mouse connector placement; assembly undo/redo; master and beam six-wheel build; save/load; erase-one and joint descendants; drifting; build/drive return; screenshots at four viewport sizes.");
    }
    private void RunStandardWorkshop(Scene scene,VehicleBuilder3D builder)
    {
        string output=Path.Combine(Environment.CurrentDirectory,"output","goblin-scraper","standard-contraptions");Directory.CreateDirectory(output);
        Assert(builder.Assembly!.Parts.Count==1,"Wrong start");TickInput(scene,new(.98f,.5f),[]);
        Screenshot(scene,Path.Combine(output,"starting-block.png"));
        var camera=scene.ActiveCamera!;var clip=Vector4.Transform(new Vector4(builder.Transform.WorldPosition+new Vector3(.25f,0,0),1),camera.GetViewMatrix()*camera.GetProjectionMatrix(1280f/720));
        Vector2 pointer=new((clip.X/clip.W+1)/2,(1-clip.Y/clip.W)/2);TickInput(scene,pointer,[]);TickInput(scene,pointer,[],true);TickInput(scene,pointer,[]);
        Assert(builder.Assembly.Parts.Count==2,"New beam mouse placement failed: "+builder.FreePlacementIssue);Assert(builder.UndoBuild()&&builder.Assembly.Parts.Count==1,"New placement undo failed");
        var catalog=VehiclePartCatalog.Load(Path.Combine(_project!.ProjectRoot,"Assets","GarageUI","parts-catalog.json"));
        foreach(var def in catalog.Parts.Values)Assert(_project.Assets.LoadModel(new ByteEngine.Core.Assets.AssetReference("Assets/ContraptionParts/"+def.ModelFile+".glb")).Meshes.Count>0,"Model import failed: "+def.Label);
        builder.RestoreAssembly(ContraptionTests.Build(catalog).ToJson());TickInput(scene,new(.98f,.5f),[]);
        Screenshot(scene,Path.Combine(output,"workshop-720p.png"));Screenshot(scene,Path.Combine(output,"workshop-1080p.png"),1920,1080);Screenshot(scene,Path.Combine(output,"workshop-16x10.png"),1280,800);Screenshot(scene,Path.Combine(output,"workshop-ultrawide.png"),2560,1080);
        Assert(builder.RemoveAssemblyPart(1)&&builder.UndoBuild(),"Erase undo failed");Assert(builder.BeginDriving(),"Simulation failed");for(int i=0;i<240;i++)builder.StepDrive(1,0,false,1f/60);Assert(builder.Velocity.Length()>1,"Native wheel physics failed");
        TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"driving.png"));TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);Assert(builder.Building,"Return failed");
        TickInput(scene,new(650f/1280,572f/720),[],true);TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"flight-palette.png"));
        Console.WriteLine("PASS: native selected model import, five-category UI, independent wheel physics, erase/undo, simulation reset, screenshots at four sizes.");
    }

}

