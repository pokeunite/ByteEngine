from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreeConstruction.cs');s=p.read_text().replace('    private float _freeSteering;','    private float _freeSteering;\n    private Vector3 _workshopPan;');s=s.replace('_twist=(_twist+1)%4','_twist=(_twist+(Input.IsKeyDown(Key.LeftAlt)?15:90))%360').replace('_twist*MathF.PI/2','_twist*MathF.PI/180')
s=s.replace('if(Input.IsMouseButtonDown(MouseButton.Middle)){var delta=Input.GameViewMouseDelta;_orbit-=delta.X*.008f;_elevation=Math.Clamp(_elevation+delta.Y*.006f,.22f,1.1f);}', '''if(Input.IsMouseButtonDown(MouseButton.Middle))
            {
                var delta=Input.GameViewMouseDelta;
                if(Input.IsKeyDown(Key.LeftShift))_workshopPan+=(-_camera!.Transform.Right*delta.X+_camera.Transform.Up*delta.Y)*(_zoom*.0014f);
                else {_orbit-=delta.X*.008f;_elevation=Math.Clamp(_elevation+delta.Y*.006f,.22f,1.1f);}
            }''')
s=s.replace('    private void SaveAssembly()', '''    private void LoadWorkshopExample()
    {
        if(!Building)return;
        try {string json=File.ReadAllText(Path.Combine(ProjectRoot,"Saves","Examples","six-wheel-hauler.json"));VehicleAssembly.FromJson(_catalog!,json);RememberAssembly();RestoreAssembly(json);ResetPosition();_message="Example loaded. Every connection can be rebuilt.";}
        catch(Exception e)when(e is IOException or JsonException){_message="Example unavailable: "+e.Message;}
    }
    private void SaveAssembly()''');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text().replace('Vector3 target=Transform.WorldPosition+Vector3.UnitY*.5f;','Vector3 target=Transform.WorldPosition+Vector3.UnitY*.5f+(FreeBuilding && Building?_workshopPan:Vector3.Zero);').replace('Speed=0; _motion.Reset(); _rollDistance=0;','Speed=0; _motion.Reset(); _rollDistance=0;_workshopPan=Vector3.Zero;');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.Workshop.cs');s=p.read_text().replace('sky.Exposure=.82f;sky.AmbientIntensity=.28f;sky.EnvironmentIntensity=.45f','sky.Exposure=1.08f;sky.AmbientIntensity=.45f;sky.EnvironmentIntensity=.65f').replace('light.Intensity=2.0f;light.AmbientIntensity=.28f','light.Transform.WorldRotation=AlignNormals(-Vector3.UnitZ,Vector3.Normalize(new Vector3(-.4f,-1,-.6f)));light.Intensity=1.8f;light.AmbientIntensity=.45f');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreePresentation.cs');s=p.read_text().replace('new(171,170)','new(171,204)').replace('        Rule("Parts dock"','        Button("LOAD EXAMPLE",new(29,240),new(147,23),LoadWorkshopExample,true).Color=new(.22f,.21f,.17f,1);\n        Rule("Parts dock"');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.GaragePresentation.cs');s=p.read_text().replace('point.X<198 && point.Y<260','point.X<198 && point.Y<278');p.write_text(s)
p=Path('Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.FreeWorkshop.cs');s=p.read_text().replace('var example=FreeVehicleAssemblyTests.BuildExample(catalog);','var example=FreeVehicleAssemblyTests.BuildExample(catalog);\n        string examples=Path.Combine(_project.ProjectRoot,"Saves","Examples");Directory.CreateDirectory(examples);File.WriteAllText(Path.Combine(examples,"six-wheel-hauler.json"),example.ToJson());');p.write_text(s)
p=Path('Tests/ByteEngine.Tests/FreeVehicleAssemblyTests.cs');s=p.read_text().replace('        Check(two.PlacementIssue("scrap_beam_1m",0,new(float.NaN','        Check(two.PlacementIssue("scrap_beam_1m",0,new(12,0,0),Quaternion.Identity).Contains("touch"),"Disconnected floating branch accepted");\n        Check(two.PlacementIssue("scrap_beam_1m",0,new(float.NaN');p.write_text(s)
