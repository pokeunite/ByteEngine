from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleAssembly.cs');s=p.read_text()
s=s.replace('    public string DriveRequirement=>','''    public bool HasSteeringConnection(int id)
    {
        var visited=new HashSet<int>();
        while(Parts.TryGetValue(id,out var p)&&visited.Add(id))
        {if(Parts.TryGetValue(p.Parent,out var parent)&&parent.File=="scrap_steering_pivot"&&p.ParentBone=="Steer")return true;id=p.Parent;}
        return false;
    }
    public float TotalMass=>Parts.Values.Sum(p=>p.File switch
    {
        "scrap_frame_2x1"=>75f,"scrap_frame_long"=>120f,"scrap_wheel_large"=>28f,"scrap_wheel_small"=>12f,
        "scrap_engine_block"=>110f,"scrap_cab_shell"=>80f,"scrap_track_pod"=>150f,"scrap_beam_1m"=>8f,"scrap_beam_2m"=>16f,
        "scrap_axle_2m"=>12f,"scrap_steering_pivot"=>15f,"scrap_suspension_piston"=>10f,_=>45f
    });
    public string DriveRequirement=>''')
s=s.replace('p.File.StartsWith("scrap_wheel_")&&HasAncestor(p.Parent,"scrap_steering_pivot")','p.File.StartsWith("scrap_wheel_")&&HasSteeringConnection(p.Id)')
p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreeConstruction.cs');s=p.read_text().replace('MaximumSpeed*(.85f+.15f*engines)/(1+Math.Max(0,Assembly.Parts.Count-10)*.025f)','MaximumSpeed*Math.Clamp(450f*engines/Math.Max(1,Assembly.TotalMass),.45f,1.5f)');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreePresentation.cs');s=p.read_text().replace('Assembly.Parts.Count+" connected parts"','Assembly.Parts.Count+" parts   /   "+Assembly.TotalMass.ToString("0")+" kg"');p.write_text(s)
p=Path('Tests/ByteEngine.Tests/FreeVehicleAssemblyTests.cs');s=p.read_text().replace('        string json=six.ToJson();','''        Check(six.TotalMass>600,"Part mass has no effect");
        var fixedAxle=two.Parts[axle];two.Parts[axle]=fixedAxle with {ParentBone="Root"};Check(!two.CanDrive,"Steering upper cap pretended to turn its children");two.Parts[axle]=fixedAxle;
        string json=six.ToJson();''');p.write_text(s)
