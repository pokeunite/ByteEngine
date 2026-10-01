using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Construction;
namespace ByteEngine.Tests;
internal static class FreeVehicleAssemblyTests
{
    internal static string Socket(VehiclePartCatalog c,string file,string contains)=>c[file].Sockets.First(s=>s.Name.Contains(contains,StringComparison.Ordinal)).Name;
    internal static int Connect(VehicleAssembly a,string file,int parent,string target,string source,int twist=0)
    {var pose=a.Snap(parent,target,file,source,twist);string issue=a.PlacementIssue(file,parent,pose.Position,pose.Rotation,target);if(issue!="")throw new InvalidOperationException(file+" "+target+": "+issue);int id=a.AddAtSocket(file,parent,target,source,twist);Check(id>0,"Connector failed");return id;}
    internal static VehicleAssembly BuildExample(VehiclePartCatalog c,bool sixWheels=true,bool steering=true)
    {
        var a=new VehicleAssembly(c);
        int front=Connect(a,"scrap_beam_2m",0,"Front",Socket(c,"scrap_beam_2m","End_Rear"));
        int rear=Connect(a,"scrap_beam_2m",0,"Rear",Socket(c,"scrap_beam_2m","End_Rear"));
        int[] parents=sixWheels?[front,0,rear]:[front,rear];
        foreach(int parent in parents)
        {
            string slot=parent==0?"Bottom":Socket(c,"scrap_beam_2m","Branch_0.8_(0, 0, 1)_Underside");
            int pivot=steering?Connect(a,"scrap_steering_pivot",parent,slot,Socket(c,"scrap_steering_pivot","Chassis")):parent;
            int axle=Connect(a,"scrap_axle_2m",pivot,steering?"SOCKET_Steer_Output":slot,"SOCKET_Chassis_Centre");
            Connect(a,"scrap_wheel_large",axle,Socket(c,"scrap_axle_2m","Wheel_Left"),Socket(c,"scrap_wheel_large","Axle_hub"));
            Connect(a,"scrap_wheel_large",axle,Socket(c,"scrap_axle_2m","Wheel_Right"),Socket(c,"scrap_wheel_large","Axle_hub"));
        }
        Connect(a,"scrap_engine_block",front,Socket(c,"scrap_beam_2m","Branch_0.0_(0, 0, 1)"),Socket(c,"scrap_engine_block","Chassis"));
        Connect(a,"scrap_cab_shell",rear,Socket(c,"scrap_beam_2m","Branch_0.0_(0, 0, 1)"),Socket(c,"scrap_cab_shell","Floor_mount"));
        return a;
    }
    public static void Run(string project)
    {
        var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));var empty=new VehicleAssembly(c);
        Check(empty.Parts.Count==1&&empty.Parts[0].File==VehicleAssembly.MasterBlock,"Wrong starting block");Check(c[VehicleAssembly.MasterBlock].Sockets.Length==6,"Master must have six faces");
        Check(empty.Add("scrap_beam_1m",0,new(1,0,0),Quaternion.Identity)<0,"Surface placement survived");
        int beam=Connect(empty,"scrap_beam_1m",0,"Right",Socket(c,"scrap_beam_1m","End_Rear"));
        Check(empty.Parts[beam].Position.X>.7f,"Beam failed to extend sideways");
        Check(empty.AddAtSocket("scrap_beam_1m",0,"Right",Socket(c,"scrap_beam_1m","End_Rear"))<0,"Occupied slot accepted duplicates");
        Check(empty.IsSocketOccupied(beam,Socket(c,"scrap_beam_1m","End_Rear")),"Input socket reused");
        Check(empty.Add("scrap_beam_1m",0,new(float.NaN,0,0),Quaternion.Identity,"Root","Top",Socket(c,"scrap_beam_1m","End_Rear"))<0,"Invalid transform accepted");
        int child=Connect(empty,"scrap_beam_1m",beam,Socket(c,"scrap_beam_1m","End_Front"),Socket(c,"scrap_beam_1m","End_Rear"));
        Check(empty.MoveAtSocket(beam,0,"Top",Socket(c,"scrap_beam_1m","End_Rear"))=="","Beam move failed");Check(empty.Parts[beam].Position.Y>.7f&&empty.Parts[child].Position.Y>1.7f,"Move kept original branch transform");
        Check(empty.MoveAtSocket(beam,beam,Socket(c,"scrap_beam_1m","End_Front"),Socket(c,"scrap_beam_1m","End_Rear"))!="","Cycle accepted");
        var six=BuildExample(c);var four=BuildExample(c,false);Check(six.CanDrive&&four.CanDrive,"Beam frame cannot drive");Check(six.Parts.Values.Count(p=>p.File=="scrap_wheel_large")==6,"Six wheels failed");
        var fixedWheels=BuildExample(c,false,false);Check(fixedWheels.CanDrive,"Steering gear is still mandatory");
        string json=six.ToJson();var restored=VehicleAssembly.FromJson(c,json);Check(restored.Parts.Count==six.Parts.Count&&restored.CanDrive,"Save lost connections");
        Check(restored.Remove(1)&&restored.Parts.Count==six.Parts.Count-1,"Erase must remove only one block");Check(restored.Parts.Values.Any(p=>p.Parent==-1&&p.Id!=0),"Detached parts were discarded");VehicleAssembly.FromJson(c,restored.ToJson());Check(!restored.Remove(0),"Master can be removed");
        bool rejected=false;try{VehicleAssembly.FromJson(c,json.Replace("\"Parent\": 1","\"Parent\": 999"));}catch(JsonException){rejected=true;}Check(rejected,"Invalid parent accepted");
        Console.WriteLine("PASS: master block; connector-only beams; occupied inputs; four/six-wheel frames; moving joints; clearance; erase-one; detached-save and graph validation.");
    }
    private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}
