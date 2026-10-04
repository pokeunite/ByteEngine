using System.Numerics;
using GoblinScrapper.Construction;
namespace ByteEngine.Tests;
internal static class AttachmentAuditTests {
 public static void Run(string project,bool requireAll=false) {
  var c=VehiclePartCatalog.Load(Path.Combine(project,"Assets","GarageUI","parts-catalog.json"));var beam=c.Parts.Values.First(p=>p.ReferenceId==15);int failures=0,connectors=0,partSuccess=0;
  foreach(var parent in c.Parts.Values.Where(p=>p.File!=VehicleAssembly.MasterBlock)) {
   int passed=0;
   foreach(var target in parent.Sockets.Skip(1)) {
    bool success=false;string last="";
    foreach(int twist in new[]{0,90,180,270}) {
     var a=new VehicleAssembly(c);a.Parts[0]=a.Parts[0] with{Position=new(-20,0,0)};
     a.Parts[1]=new(1,parent.File,-1,Vector3.Zero,Quaternion.Identity); // isolated parent, input excluded from audit
     var pose=a.Snap(1,target.Name,beam.File,beam.Sockets[0].Name,twist);last=a.PlacementIssue(beam.File,1,pose.Position,pose.Rotation,target.Name);
     if(last==""&&a.ConnectionIssue(beam.File,1,pose.Position,pose.Rotation,target.Bone,target.Name,beam.Sockets[0].Name)==""){success=true;break;}
    }
    connectors++;if(success)passed++;else{failures++;Console.WriteLine($"FAIL {parent.Label} / {target.Name}: {last}");}
   }
   if(passed>0)partSuccess++;Console.WriteLine($"PART {parent.Label}: {passed}/{parent.Sockets.Length-1} usable outgoing connectors");
  }
  Console.WriteLine($"TOTAL: {partSuccess}/{c.Parts.Count-1} parts accept a short beam; {connectors-failures}/{connectors} outgoing connectors pass.");
  int pairFailures=0,pairCount=0;
  foreach(var parent in c.Parts.Values.Where(p=>p.File!=VehicleAssembly.MasterBlock))foreach(var child in c.Parts.Values.Where(p=>p.File!=VehicleAssembly.MasterBlock)) {
   pairCount++;bool okay=false;
   foreach(var target in parent.Sockets.Skip(1)){if(okay)break;foreach(var source in child.Sockets.Where(s=>s.Bone=="Root")){if(okay)break;foreach(int twist in new[]{0,90,180,270}){
    var a=new VehicleAssembly(c);a.Parts[0]=a.Parts[0] with{Position=new(-20,0,0)};a.Parts[1]=new(1,parent.File,-1,Vector3.Zero,Quaternion.Identity);
    var pose=a.Snap(1,target.Name,child.File,source.Name,twist);
    if(a.PlacementIssue(child.File,1,pose.Position,pose.Rotation,target.Name)==""&&a.ConnectionIssue(child.File,1,pose.Position,pose.Rotation,target.Bone,target.Name,source.Name)==""){okay=true;break;}
   }}}
   if(!okay){pairFailures++;Console.WriteLine("PAIR FAIL "+parent.Label+" -> "+child.Label);}
  }
  Console.WriteLine($"PAIR MATRIX: {pairCount-pairFailures}/{pairCount} parent-child combinations have a valid placement.");
  if(requireAll&&pairFailures>0)throw new Exception("Part-pair audit failed");
  if(requireAll&&(partSuccess!=c.Parts.Count-1||failures>0))throw new Exception("Attachment audit failed");
 }
}
