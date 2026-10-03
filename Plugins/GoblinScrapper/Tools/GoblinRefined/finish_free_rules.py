from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleAssembly.cs');s=p.read_text()
s=s.replace('            AssemblyBox[] clearance=[bounds];','            if(file=="scrap_axle_2m") sockets=[new("SOCKET_Chassis_Centre",new(0,.31f,0),Vector3.UnitY),..sockets];\n            if(file=="scrap_steering_pivot") sockets=[..sockets,new("SOCKET_Steer_Output",new(0,-.15f,0),-Vector3.UnitY,"Steer")];\n            AssemblyBox[] clearance=[bounds];')
s=s.replace('        if(position.Length()>16)', '''        var parentBlock=Parts[parent];
        var ownBounds=Catalog[file].Bounds.Transform(position,rotation);var parentBounds=Catalog[parentBlock.File].Bounds.Transform(parentBlock.Position,parentBlock.Rotation);
        Vector3 separation=Vector3.Max(Vector3.Zero,Vector3.Max(parentBounds.Min-ownBounds.Max,ownBounds.Min-parentBounds.Max));
        if(separation.Length()>.32f)return "Part must touch its supporting structure";
        if(position.Length()>16)''')
s=s.replace('            a.Parts.Add(p.Id,p);','''            if(p.ParentBone!="Root"&&!catalog[a.Parts[p.Parent].File].Sockets.Any(s=>s.Bone==p.ParentBone))throw new JsonException("Unknown moving connection");
            if(p.OwnConnector!=""&&!catalog[p.File].Sockets.Any(s=>s.Name==p.OwnConnector))throw new JsonException("Unknown part connector");
            a.Parts.Add(p.Id,p);''')
p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreeConstruction.cs');s=p.read_text().replace('Vector3.Normalize(Vector3.Cross(from,Math.Abs(from.Y)<.9f?Vector3.UnitY:Vector3.UnitX))','Math.Abs(from.Y)<.9f?Vector3.UnitY:Vector3.UnitX').replace('or InvalidOperationException){_message="Could not load:', 'or InvalidOperationException or KeyNotFoundException){_message="Could not load:');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.Workshop.cs');s=p.read_text().replace('        for(int i=0;i<2;i++)','''        if(FreeBuilding)
        {
            var floorMaterial=new Material {BaseColor=new(.75f,.70f,.59f,1),MainTexture=Assets!.LoadTexture(new AssetReference("Assets/WorkshopMaterials/concrete-diffuse.jpg")),NormalTexture=Assets.LoadTexture(new AssetReference("Assets/WorkshopMaterials/concrete-normal.jpg")),PackedPbrTexture=Assets.LoadTexture(new AssetReference("Assets/WorkshopMaterials/concrete-arm.jpg")),PbrMapMode=MaterialPbrMapMode.Packed,DecodeColorTexturesSrgb=true,UvTiling=new(12),NormalStrength=.55f};
            foreach(var obj in _owned.Where(o=>o.Name=="Test yard floor"))obj.GetComponent<MeshRenderer>()!.Material=floorMaterial;
            var padMaterial=new Material {BaseColor=new(.8f,.76f,.67f,1),MainTexture=floorMaterial.MainTexture,NormalTexture=floorMaterial.NormalTexture,PackedPbrTexture=floorMaterial.PackedPbrTexture,PbrMapMode=MaterialPbrMapMode.Packed,DecodeColorTexturesSrgb=true,UvTiling=new(1.5f),NormalStrength=.45f};
            foreach(var obj in _owned.Where(o=>o.Name=="Build pad"))obj.GetComponent<MeshRenderer>()!.Material=padMaterial;
        }
        for(int i=0;i<2;i++)''',1);p.write_text(s)
