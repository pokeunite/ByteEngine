from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text().replace('            foreach(var ui in _buildUi) ui.Active=true;\n            RefreshGhost();','            foreach(var ui in _buildUi) ui.Active=true;\n            RefreshPalette(); RefreshGhost();').replace('_categoryButtons.Clear(); _part=0;', '_categoryButtons.Clear(); _axleBridges.Clear(); _part=0;')
s=s.replace('        obj.AddComponent(new MeshRenderer { UsePrimitive = true, Material = new Material { BaseColor=color, Roughness=.85f } });','        obj.AddComponent(new MeshRenderer { UsePrimitive = true, CastShadows=!(name.Contains("floor") || name.Contains("grid") || name=="Build pad"), Material = new Material { BaseColor=color, Roughness=.85f } });')
p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.GaragePresentation.cs');s=p.read_text().replace('new(238,210)', 'new(238,240)').replace('new(40,246)', 'new(40,276)').replace('point.X<266 && point.Y<320', 'point.X<266 && point.Y<350');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.Workshop.cs');s=p.read_text().replace('        sky.ZenithColor=', '        sky.SkyMode=SkyMode3D.Procedural;\n        sky.ZenithColor=').replace('light.ShadowSoftness=2;', 'light.ShadowSoftness=2;light.ShadowResolution=2048;light.ShadowBias=.006f;light.ShadowDistance=35;')
s=s.replace('using ByteEngine.Core.Scene;', 'using ByteEngine.Core.Scene;\nusing ByteEngine.Core.Assets;')
s=s.replace('        UpdateAxleBridges();\n        for(int side', '''        UpdateAxleBridges();
        var cabReference=new AssetReference(PartsDirectory+"/scrap_cab_shell.glb");
        var cab=Assets!.LoadModel(cabReference);
        var wood=cab.Materials.First(m=>m.Name.Contains("wood"));
        var woodMaterial=Assets.GetModelMaterial(cabReference,wood.Key);
        for(int side''')
s=s.replace('''        for(int i=0;i<18;i++)''','''        foreach(var obj in _owned.Where(o=>o.Name.Contains("workbench",StringComparison.OrdinalIgnoreCase) || o.Name=="Scrap crate"))
            if(obj.GetComponent<MeshRenderer>() is {} renderer) renderer.Material=woodMaterial;
        for(int i=0;i<18;i++)''')
p.write_text(s)
p=Path('Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.cs');s=p.read_text().replace('b.Label=="Return to build [B]"','b.Label=="BACK TO BUILD  [B]"');p.write_text(s)
p=Path('Tests/ByteEngine.Tests/VehicleDriftTests.cs');s=p.read_text();needle='        Console.WriteLine($"PASS:';pos=s.index(needle);s=s[:pos]+'''        var exclusive=new VehicleBuildLayout();exclusive.Place(19,"scrap_track_pod");
        Check(!exclusive.Place(0,"scrap_wheel_large") && !exclusive.Place(4,"scrap_axle_2m"),"A single track must block wheels and axles.");
        exclusive.Remove(19);exclusive.Place(0,"scrap_wheel_small");Check(!exclusive.Place(20,"scrap_track_pod"),"A wheel must block tracks.");
        exclusive.Remove(0);exclusive.Place(4,"scrap_axle_2m");Check(!exclusive.Place(19,"scrap_track_pod"),"An axle must block tracks.");
        Check(exclusive.SetChassis("scrap_frame_long") && exclusive.ActiveMounts[0].Position.Z<-1.4f,"Long chassis must have its own wheel mount positions.");
        Check(!exclusive.Place(24,"scrap_frame_long"),"Long chassis must never attach as an extension.");
        var savedLong=VehicleBuildLayout.FromJson(exclusive.ToJson());Check(savedLong.LongChassis && savedLong.Parts[4]=="scrap_axle_2m","Long chassis save round trip.");
        var migrated=VehicleBuildLayout.FromJson("{\\"Version\\":1,\\"Parts\\":{\\"24\\":\\"scrap_frame_long\\",\\"0\\":\\"scrap_wheel_large\\"}}");
        Check(migrated.LongChassis && !migrated.Parts.ContainsKey(24),"Legacy long extension must migrate to a base selection.");
'''+s[pos:];p.write_text(s)
