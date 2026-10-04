using GoblinScrapper.Construction;
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
        var registry=ByteEngine.Core.VisualLogic.VisualLogicRegistry.CreateDefault();
        var eventContext=new ByteEngine.Core.VisualLogic.EventExecutionContext{Scene=scene,Self=builder.GameObject,Globals=new ByteEngine.Core.Variables.VariableStore()};
        var componentSerializer=new ByteEngine.Core.Serialization.ComponentSerializer(_project!.ProjectRoot,_project.AssetDatabase,_project.Assets);
        ByteEngine.Core.Plugins.ByteEnginePluginRegistry.ApplyComponentCodecs(componentSerializer);
        var restoredControls=(VehicleBuilder3D)componentSerializer.Deserialize(componentSerializer.Serialize(builder)!)!;
        Assert(!restoredControls.UseBuiltInControls&&restoredControls.UseBuiltInPointerControls&&restoredControls.AutomaticCamera&&restoredControls.ShowWorkshopHud,"Manual control switches were lost during serialization");
        Console.WriteLine("PASS: manual control switches persist through component save/reload.");
        var template=GoblinScrapper.GoblinControlTemplate.Create();
        foreach(var instruction in template.Rules.SelectMany(r=>r.Conditions))Assert(registry.TryGetCondition(instruction.Id,out _),"Missing control condition: "+instruction.Id);
        foreach(var instruction in template.Rules.SelectMany(r=>r.Actions))Assert(registry.TryGetAction(instruction.Id,out _),"Missing control action: "+instruction.Id);
        Assert(builder.GameObject.GetComponent<ByteEngine.Core.VisualLogic.EventModuleComponent>()!=null,"Editable controls were not attached to the builder");
        var place=new ByteEngine.Core.VisualLogic.VisualInstruction{Id="bytebard.goblinscrapper.attachPart",Arguments=new(){["file"]=ByteEngine.Core.VisualLogic.EventValue.String("goblin_double_wooden_block"),["parent"]=ByteEngine.Core.VisualLogic.EventValue.Number(0),["connector"]=ByteEngine.Core.VisualLogic.EventValue.String("Front")}};
        Assert(registry.TryGetAction(place.Id,out var placeAction),"Missing connector action");placeAction!.Execute(place,eventContext);
        Assert(builder.Assembly!.Parts.Count==2&&builder.LastPlacedPartId>0,"Connector event failed");
        Assert(builder.UndoBuild()&&builder.Assembly.Parts.Count==1,"Connector event did not record undo");
        Console.WriteLine("PASS: Goblin action/condition registration and connector event execution with undo.");
        Assert(builder.Assembly!.Parts.Count==1,"Wrong start");TickInput(scene,new(.98f,.5f),[]);Assert(!builder.UseBuiltInControls,"Editable keyboard events did not take control");
        var attachmentCatalog=VehiclePartCatalog.Load(Path.Combine(_project!.ProjectRoot,"Assets","GarageUI","parts-catalog.json"));
        int suspension=builder.PlacePartAtConnector("goblin_suspension",0,"Top");
        Assert(suspension>0,"Suspension input mount rejected");
        builder.SelectBuildPart("goblin_single_wooden_block");
        for(int i=0;i<20;i++)TickInput(scene,new(.98f,.5f),[]);
        var suspensionPart=builder.Assembly.Parts[suspension];var outputSocket=attachmentCatalog["goblin_suspension"].Sockets.First(x=>x.Bone=="Moving");
        var cap=builder.Transform.WorldPosition+suspensionPart.Position+Vector3.Transform(outputSocket.Position,suspensionPart.Rotation);
        var capClip=Vector4.Transform(new Vector4(cap,1),scene.ActiveCamera!.GetViewMatrix()*scene.ActiveCamera.GetProjectionMatrix(1280f/720));
        var capPointer=new Vector2((capClip.X/capClip.W+1)/2,(1-capClip.Y/capClip.W)/2);
        TickInput(scene,capPointer,[]);
        Assert(builder.HoveredBlockId==suspension&&builder.PlacementReady,"Suspension output cannot be picked: "+builder.FreePlacementIssue);
        TickInput(scene,capPointer,[Key.Enter]);TickInput(scene,capPointer,[]);
        Assert(builder.Assembly.Parts.Values.Any(x=>x.Parent==suspension&&x.ParentBone=="Moving"),"Beam was not attached to suspension Moving output");
        Console.WriteLine("PASS: pointer picks suspension output and Enter attaches a beam to its moving section.");
        Assert(builder.UndoBuild()&&builder.UndoBuild(),"Suspension placement undo failed");
        builder.SelectBuildPart("goblin_double_wooden_block");
        Screenshot(scene,Path.Combine(output,"starting-block.png"));
        builder.OrbitBuildCamera(0,-1.2f);
        for(int i=0;i<45;i++)TickInput(scene,new(.98f,.5f),[]);
        Assert(scene.ActiveCamera!.Transform.WorldPosition.Y<builder.Transform.WorldPosition.Y,"Build camera cannot inspect underneath");
        Assert(scene.ActiveCamera.Transform.WorldPosition.Y>=.14f,"Build camera crossed below the floor");
        Screenshot(scene,Path.Combine(output,"underside-build-view.png"));
        builder.OrbitBuildCamera(0,1.2f);
        for(int i=0;i<45;i++)TickInput(scene,new(.98f,.5f),[]);
        Console.WriteLine("PASS: build camera orbits underneath without passing below the floor.");
        var camera=scene.ActiveCamera!;var clip=Vector4.Transform(new Vector4(builder.Transform.WorldPosition+new Vector3(.25f,0,0),1),camera.GetViewMatrix()*camera.GetProjectionMatrix(1280f/720));
        Vector2 pointer=new((clip.X/clip.W+1)/2,(1-clip.Y/clip.W)/2);TickInput(scene,pointer,[]);TickInput(scene,pointer,[Key.R]);TickInput(scene,pointer,[]);TickInput(scene,pointer,[],true);TickInput(scene,pointer,[]);
        Assert(builder.Assembly.Parts.Count==2,"New beam mouse placement failed: "+builder.FreePlacementIssue);Assert(builder.Assembly.Parts.Values.Single(p=>p.Id!=0).OwnConnector.Contains("Branch_0_"),"R input did not turn the beam onto its centre side connector");Assert(builder.UndoBuild()&&builder.Assembly.Parts.Count==1,"New placement undo failed");
        var catalog=VehiclePartCatalog.Load(Path.Combine(_project!.ProjectRoot,"Assets","GarageUI","parts-catalog.json"));
        // An identity physical override must preserve the rendered bind-pose bounds.
        foreach(var def in catalog.Parts.Values.Where(p=>p.ReferenceId is 2 or 40 or 46 or 60 or 28))
        {
            var probe=VehicleBuilder3D.CreateModelVisual(scene,_project.Assets,"Assets/ContraptionParts/"+def.ModelFile+".glb",builder.GameObject,"Bind pose probe");
            var rig=probe.GetComponent<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>()!;
            Assert(rig.TryGetCurrentModelBounds(out var before),"Missing bind pose bounds");
            rig.SetPhysicalBoneDeformation("Moving",Matrix4x4.Identity);
            Assert(rig.TryGetCurrentModelBounds(out var after),"Missing physical pose bounds");
            Console.WriteLine($"Bind identity {def.ReferenceId}: before={before.Minimum}/{before.Maximum} after={after.Minimum}/{after.Maximum}");
            Assert(Vector3.Distance(before.Minimum,after.Minimum)<.0001f&&Vector3.Distance(before.Maximum,after.Maximum)<.0001f,"Physical override moved the visible mesh away from its bind pose: "+def.Label);
            scene.DestroyGameObject(probe);
        }
        foreach(var def in catalog.Parts.Values)Assert(_project.Assets.LoadModel(new ByteEngine.Core.Assets.AssetReference("Assets/ContraptionParts/"+def.ModelFile+".glb")).Meshes.Count>0,"Model import failed: "+def.Label);
        builder.RestoreAssembly(ContraptionTests.Build(catalog).ToJson());TickInput(scene,new(.98f,.5f),[]);
        Screenshot(scene,Path.Combine(output,"workshop-720p.png"));Screenshot(scene,Path.Combine(output,"workshop-1080p.png"),1920,1080);Screenshot(scene,Path.Combine(output,"workshop-16x10.png"),1280,800);Screenshot(scene,Path.Combine(output,"workshop-ultrawide.png"),2560,1080);
        Assert(builder.RemoveAssemblyPart(1)&&builder.UndoBuild(),"Erase undo failed");Assert(builder.BeginDriving(),"Simulation failed");
        var masterVisual=scene.GameObjects.Single(o=>o.Name=="Master block");var machineStart=builder.Transform.WorldPosition;
        for(int i=0;i<240;i++)
        {
            TickInput(scene,new(.98f,.5f),[Key.W]);
            Assert(Vector3.Distance(masterVisual.Transform.WorldPosition,builder.Transform.WorldPosition)<.0001f,"Full game loop snapped the visible machine away from its physical position while the camera followed physics");
        }
        Assert(Vector3.Distance(machineStart,masterVisual.Transform.WorldPosition)>1&&builder.Velocity.Length()>1,"Held W did not move the visible machine");
        TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"driving.png"));TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);Assert(builder.Building,"Return failed");
        TickInput(scene,new(650f/1280,572f/720),[],true);TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"flight-palette.png"));
        var oneWheel=new VehicleAssembly(catalog);var powered=catalog.Parts.Values.First(p=>p.ReferenceId==2);
        Assert(oneWheel.AddAtSocket(powered.File,0,"Right",powered.Sockets[0].Name)>0,"Single powered wheel attachment failed");builder.RestoreAssembly(oneWheel.ToJson());Assert(builder.BeginDriving(),"Single wheel simulation failed");
        masterVisual=scene.GameObjects.Single(o=>o.Name=="Master block");var wheelVisual=scene.GameObjects.Single(o=>o.Name.EndsWith(" #1"));var wheelStart=wheelVisual.Transform.WorldPosition;
        machineStart=builder.Transform.WorldPosition;
        for(int i=0;i<180;i++)
        {
            TickInput(scene,new(.98f,.5f),[Key.W]);
            Assert(Vector3.Distance(masterVisual.Transform.WorldPosition,builder.Transform.WorldPosition)<.0001f,"Single-wheel visual diverged from physics");
        }
        var delta=masterVisual.Transform.WorldPosition-machineStart;Assert(new Vector2(delta.X,delta.Z).Length()>.1f,"Single powered wheel did not propel the visible block");
        Assert(Vector3.Distance(wheelStart,wheelVisual.Transform.WorldPosition)>.1f,"Powered wheel visual stayed behind");
        Console.WriteLine("Single-wheel visible horizontal travel: "+new Vector2(delta.X,delta.Z).Length());Screenshot(scene,Path.Combine(output,"single-wheel-fixed.png"));
        TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);Assert(builder.Building&&Vector3.Distance(masterVisual.Transform.WorldPosition,builder.Transform.WorldPosition)<.0001f,"Reset left the visible machine displaced");
        if(catalog.Revision>=3)
        {
            builder.RestoreAssembly(SteeringJointTests.Build(catalog).ToJson());TickInput(scene,new(.98f,.5f),[]);
            Screenshot(scene,Path.Combine(output,"steering-hinge-build.png"));
            Assert(builder.BeginDriving(),"Steering hinge machine cannot enter simulation");
            for(int tick=0;tick<120;tick++)TickInput(scene,new(.98f,.5f),[Key.W,Key.D]);
            Screenshot(scene,Path.Combine(output,"steering-hinge-driving.png"));
            TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);
            Assert(builder.Building,"Steering machine did not restore its build pose");
        }
        for(int recorded=0;recorded<2;recorded++)
        {
            builder.RestoreAssembly(File.ReadAllText($"Tests/ByteEngine.Tests/TestData/GoblinRecordedBuild{recorded}.json"));
            TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,$"recorded-{recorded}-build-024.png"));
            Assert(builder.BeginDriving(),"Recorded machine failed to enter simulation");
            for(int tick=0;tick<120;tick++)TickInput(scene,new(.98f,.5f),[]);
            machineStart=builder.Transform.WorldPosition;
            for(int tick=0;tick<240;tick++)TickInput(scene,new(.98f,.5f),[Key.W]);
            Assert(Vector3.Distance(machineStart,builder.Transform.WorldPosition)>2,"Recorded machine failed in full game loop");
            Screenshot(scene,Path.Combine(output,$"recorded-{recorded}-drive-024.png"));
            TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);
            Assert(builder.Building,"Recorded machine did not return to build mode");
        }
        builder.RestoreAssembly(File.ReadAllText("Tests/ByteEngine.Tests/TestData/GoblinSteeringUser025.json"));
        TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,Path.Combine(output,"user-steering-build-026.png"));
        Assert(builder.BeginDriving(),"User steering fixture cannot enter simulation");
        for(int tick=0;tick<120;tick++)TickInput(scene,new(.98f,.5f),[]);
        for(int tick=0;tick<60;tick++)TickInput(scene,new(.98f,.5f),[Key.W]);
        for(int tick=0;tick<180;tick++)TickInput(scene,new(.98f,.5f),[Key.W,Key.D]);
        var commandedForward=Vector3.Transform(-Vector3.UnitZ,builder.Transform.WorldRotation);
        Assert(commandedForward.X>.3f,"D visually steered right but the physical machine turned left");
        Screenshot(scene,Path.Combine(output,"user-steering-right-026.png"));
        var counterStart=builder.Transform.WorldRotation;
        for(int tick=0;tick<240;tick++)TickInput(scene,new(.98f,.5f),[Key.W,Key.A]);
        var counterForward=Vector3.Transform(Vector3.Transform(-Vector3.UnitZ,builder.Transform.WorldRotation),Quaternion.Inverse(counterStart));
        Assert(counterForward.X<-.15f,"Full game loop counter-steering failed");
        Screenshot(scene,Path.Combine(output,"user-steering-counter-026.png"));
        TickInput(scene,new(.98f,.5f),[Key.B]);TickInput(scene,new(.98f,.5f),[]);
        Assert(builder.Building,"User steering fixture did not return to Build");
        Console.WriteLine("PASS: held W through the full game loop moves both visible master and wheels; single-wheel and beam-frame regressions; build-pose restore.");
    }

}

