using ByteEngine.Core.Diagnostics;
using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;
using System.Text.Json;
namespace GoblinScrapper.Construction;

public sealed record FreePartSocket(string Name, Vector3 Position, Vector3 Normal, string Bone="Root");
public sealed record AssemblyBox(Vector3 Min,Vector3 Max)
{
    public AssemblyBox Transform(Vector3 position,Quaternion rotation)
    {
        Vector3 min=new(float.MaxValue),max=new(float.MinValue);
        for(int i=0;i<8;i++) {var p=position+Vector3.Transform(new Vector3((i&1)==0?Min.X:Max.X,(i&2)==0?Min.Y:Max.Y,(i&4)==0?Min.Z:Max.Z),rotation);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
        return new(min,max);
    }
    public bool Intersects(AssemblyBox b,float tolerance=.045f)=>Min.X<b.Max.X-tolerance && Max.X>b.Min.X+tolerance && Min.Y<b.Max.Y-tolerance && Max.Y>b.Min.Y+tolerance && Min.Z<b.Max.Z-tolerance && Max.Z>b.Min.Z+tolerance;
}
public sealed record AssemblyPartDefinition(string File,string Description,AssemblyBox Bounds,FreePartSocket[] Sockets,AssemblyBox[] Clearance)
{
 public float LinearDrag {get;init;}public float AngularDrag {get;init;}
 public Vector3[]? WheelHull {get;init;}
 public string Label {get;init;}=""; public string Group {get;init;}=""; public string Kind {get;init;}=""; public string ModelFile {get;init;}=""; public int ReferenceId {get;init;}=-1; public float Mass {get;init;}=10; public AssemblyBox? RootBounds {get;init;} public AssemblyBox? MovingBounds {get;init;} public AssemblyBox? MovingCollision {get;init;} public Vector3 Pivot {get;init;} public Vector3 Axis {get;init;}=Vector3.UnitX;
}
public sealed class VehiclePartCatalog
{
    public bool Standard {get;private set;}
    public int Revision {get;private set;} = 2;
    public VehiclePartCatalog? Legacy {get;private set;}
    public Dictionary<string,AssemblyPartDefinition> Parts {get;}=new(StringComparer.Ordinal);
    public AssemblyPartDefinition this[string file]=>Parts[file];
    public static VehiclePartCatalog Load(string path)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));var catalog=new VehiclePartCatalog();catalog.Standard=doc.RootElement.TryGetProperty("kit",out var kit)&&kit.GetString()!.Contains("standard",StringComparison.OrdinalIgnoreCase);
        catalog.Revision=doc.RootElement.TryGetProperty("geometry_revision",out var revision)?revision.GetInt32():2;
        if(doc.RootElement.TryGetProperty("legacy_catalog",out var legacy))
        {
            string legacyName=legacy.GetString()??"";
            if(legacyName!=Path.GetFileName(legacyName))throw new JsonException("Legacy catalogue must be in the same folder.");
            string legacyPath=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,legacyName);
            if(File.Exists(legacyPath))catalog.Legacy=Load(legacyPath);
        }
        Vector3 Vec(JsonElement a)=>new(a[0].GetSingle(),a[1].GetSingle(),a[2].GetSingle());
        foreach(var p in doc.RootElement.GetProperty("parts").EnumerateArray())
        {
            string file=Path.GetFileNameWithoutExtension(p.GetProperty("file").GetString()!);
            var b=p.GetProperty("bounds_blender_z_up");var low=Vec(b[0]);var high=Vec(b[1]);
            var bounds=new AssemblyBox(new(low.X,low.Z,-high.Y),new(high.X,high.Z,-low.Y));
            var sockets=p.GetProperty("sockets").EnumerateArray().Select(s=>new FreePartSocket(s.GetProperty("name").GetString()!,Vec(s.GetProperty("position")),Vec(s.GetProperty("normal")),s.GetProperty("animation_bone").GetString()??"Root")).ToArray();
            if(file.StartsWith("scrap_beam_"))sockets=[..sockets,..sockets.Where(s=>s.Normal.Y>.9f).Select(s=>new FreePartSocket(s.Name+"_Underside",new(s.Position.X,-s.Position.Y,s.Position.Z),-Vector3.UnitY))];
            if(file=="scrap_axle_2m") sockets=[new("SOCKET_Chassis_Centre",new(0,.31f,0),Vector3.UnitY),..sockets];
            if(file=="scrap_steering_pivot") sockets=[..sockets,new("SOCKET_Steer_Output",new(0,-.15f,0),-Vector3.UnitY,"Steer")];
            AssemblyBox[] clearance=[bounds];
            if(file is "scrap_frame_2x1" or "scrap_frame_long")
            {
                float length=file=="scrap_frame_long"?1.9f:1;
                clearance=[new(new(-.61f,-.1f,-length),new(-.39f,.14f,length)),new(new(.39f,-.1f,-length),new(.61f,.14f,length)),new(new(-.5f,-.1f,-length),new(.5f,.1f,-length+.18f)),new(new(-.5f,-.1f,length-.18f),new(.5f,.1f,length))];
            }
            AssemblyBox? Box(string key)=>p.TryGetProperty(key,out var value)?new(Vec(value[0]),Vec(value[1])):null;
            if(catalog.Standard)clearance=new[]{Box("root_bounds_game"),Box("moving_bounds_game")}.Where(b=>b!=null).Cast<AssemblyBox>().ToArray();
            string Text(string key)=>p.TryGetProperty(key,out var value)?value.GetString()??"":"";
            if(file=="goblin_starting_block") {file=VehicleAssembly.MasterBlock;sockets=sockets.Select(x=>x with {Name=x.Normal.X>.9f?"Right":x.Normal.X<-.9f?"Left":x.Normal.Y>.9f?"Top":x.Normal.Y<-.9f?"Bottom":x.Normal.Z>.9f?"Rear":"Front"}).ToArray();}
            catalog.Parts.Add(file,new(file,Text("description"),bounds,sockets,clearance) {Label=Text("label"),Group=Text("group"),Kind=Text("kind"),ModelFile=Path.GetFileNameWithoutExtension(Text("file")),ReferenceId=p.TryGetProperty("reference_id",out var rid)?rid.GetInt32():-1,Mass=p.TryGetProperty("mass",out var mass)?mass.GetSingle():10,RootBounds=Box("root_bounds_game"),MovingBounds=Box("moving_bounds_game"),MovingCollision=Box("moving_collider_game"),Pivot=p.TryGetProperty("pivot_game",out var pivot)?Vec(pivot):Vector3.Zero,Axis=p.TryGetProperty("axis_game",out var axis)?Vec(axis):Vector3.UnitX});
        }
        var masterBounds=new AssemblyBox(new(-.25f),new(.25f));
        if(!catalog.Parts.ContainsKey(VehicleAssembly.MasterBlock))catalog.Parts.Add("goblin_master_block",new("goblin_master_block","The protected heart of your machine",masterBounds,[
            new("Front",new(0,0,-.25f),-Vector3.UnitZ),new("Rear",new(0,0,.25f),Vector3.UnitZ),
            new("Left",new(-.25f,0,0),-Vector3.UnitX),new("Right",new(.25f,0,0),Vector3.UnitX),
            new("Top",new(0,.25f,0),Vector3.UnitY),new("Bottom",new(0,-.25f,0),-Vector3.UnitY)],[masterBounds]));
        Stream? ReferenceStream(string name)
        {
            if(!catalog.Standard||catalog.Revision<3)return null;
            string local=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!,name);
            return File.Exists(local)?File.OpenRead(local):typeof(VehiclePartCatalog).Assembly.GetManifestResourceStream("GoblinScrapper."+name);
        }
        using var bodyReference=ReferenceStream("block-body-reference.json");
        if(bodyReference!=null)
        {
            using var bodyDoc=JsonDocument.Parse(bodyReference);
            foreach(var entry in catalog.Parts.ToArray())
                if(bodyDoc.RootElement.GetProperty("blocks").TryGetProperty(entry.Value.ReferenceId.ToString(),out var body))
                {
                    float mass=body.GetProperty("mass_game").GetSingle(),linear=body.GetProperty("linear_drag").GetSingle(),angular=body.GetProperty("angular_drag").GetSingle();
                    if(!float.IsFinite(mass)||mass<=0||!float.IsFinite(linear)||linear<0||!float.IsFinite(angular)||angular<0)throw new JsonException("Invalid block body reference");
                    catalog.Parts[entry.Key]=entry.Value with{Mass=mass,LinearDrag=linear,AngularDrag=angular};
                }
        }
        using var wheelReference=ReferenceStream("wheel-physics-reference.json");
        if(wheelReference!=null)
        {
            using var wheelDoc=JsonDocument.Parse(wheelReference);
            foreach(var entry in catalog.Parts.ToArray())
            {
                if(!wheelDoc.RootElement.TryGetProperty(entry.Value.ReferenceId.ToString(),out var wheel))continue;
                var points=wheel.GetProperty("points_game").EnumerateArray().Select(Vec).ToArray();
                if(points.Length is <4 or >256||points.Any(v=>!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z)))throw new JsonException("Invalid wheel collider reference");
                var min=points.Aggregate(new Vector3(float.MaxValue),Vector3.Min);var max=points.Aggregate(new Vector3(float.MinValue),Vector3.Max);
                catalog.Parts[entry.Key]=entry.Value with{WheelHull=points,MovingCollision=new AssemblyBox(min,max),LinearDrag=entry.Value.ReferenceId==46?0:.1f,AngularDrag=.05f};
            }
        }
        return catalog;
    }
}
public sealed record AssemblyPart(int Id,string File,int Parent,Vector3 Position,Quaternion Rotation,string ParentBone="Root",string ParentConnector="",string OwnConnector="");
/// <summary>Unlimited reusable parts, spatial clearance and explicit mechanical ancestry, independent of vehicle hardpoints.</summary>
public sealed class VehicleAssembly
{
    public VehiclePartCatalog Catalog {get;}
    public Dictionary<int,AssemblyPart> Parts {get;}=new();
    private int _nextId=1;
    public VehicleAssembly(VehiclePartCatalog catalog,string chassis="goblin_master_block") {Catalog=catalog;Parts[0]=new(0,chassis,-1,Vector3.Zero,Quaternion.Identity);}
    public const string MasterBlock="goblin_master_block";
    public bool IsConnected(int id)=>HasAncestor(id,MasterBlock);
    private bool PrimarySocketOccupied(int id,string socket,int ignore=-1)=>Parts.Values.Any(p=>p.Id!=ignore&&p.Parent==id&&p.ParentConnector==socket)||(id!=0&&Parts[id].Parent>=0&&Parts[id].OwnConnector==socket);
    public bool IsSocketOccupied(int id,string socket,int ignore=-1)
    {
        if(PrimarySocketOccupied(id,socket,ignore))return true;if(!Catalog.Standard)return false;
        var part=Parts[id];var a=Catalog[part.File].Sockets.First(s=>s.Name==socket);var position=part.Position+Vector3.Transform(a.Position,part.Rotation);var normal=Vector3.Transform(a.Normal,part.Rotation);
        return Parts.Values.Where(p=>p.Id!=id&&p.Id!=ignore).Any(p=>Catalog[p.File].Sockets.Any(b=>Vector3.Distance(position,p.Position+Vector3.Transform(b.Position,p.Rotation))<.025f&&Vector3.Dot(normal,Vector3.Transform(b.Normal,p.Rotation))<-.995f));
    }
    public bool HasTracks=>Parts.Values.Any(p=>p.File=="scrap_track_pod"&&IsConnected(p.Id));
    public bool HasAncestor(int id,params string[] types)
    {
        var visited=new HashSet<int>();while(Parts.TryGetValue(id,out var p)&&visited.Add(id)) {if(types.Contains(p.File))return true;id=p.Parent;} return false;
    }
    public bool HasSteeringConnection(int id)
    {
        var visited=new HashSet<int>();
        while(Parts.TryGetValue(id,out var p)&&visited.Add(id))
        {if(Parts.TryGetValue(p.Parent,out var parent)&&parent.File=="scrap_steering_pivot"&&p.ParentBone=="Steer")return true;id=p.Parent;}
        return false;
    }
    public float TotalMass=>Catalog.Standard?Parts.Values.Sum(p=>Catalog[p.File].Mass):Parts.Values.Where(p=>IsConnected(p.Id)).Sum(p=>p.File switch
    {
        "goblin_master_block"=>8f,"scrap_frame_2x1"=>75f,"scrap_frame_long"=>120f,"scrap_wheel_large"=>28f,"scrap_wheel_small"=>12f,
        "scrap_engine_block"=>110f,"scrap_cab_shell"=>80f,"scrap_track_pod"=>150f,"scrap_beam_1m"=>8f,"scrap_beam_2m"=>16f,
        "scrap_axle_2m"=>12f,"scrap_steering_pivot"=>15f,"scrap_suspension_piston"=>10f,_=>45f
    });
    public string DriveRequirement=>Catalog.Standard?"Ready to drive":!Parts.Values.Any(p=>p.File=="scrap_engine_block"&&IsConnected(p.Id))?"Connect an engine":!Parts.Values.Any(p=>p.File=="scrap_cab_shell"&&IsConnected(p.Id))?"Connect a driver cab":HasTracks?(Parts.Values.Count(p=>p.File=="scrap_track_pod"&&IsConnected(p.Id))<2?"Connect two track pods":"Ready to drive"):Parts.Values.Count(p=>p.File.StartsWith("scrap_wheel_")&&IsConnected(p.Id))<2?"Connect at least two wheels":"Ready to drive";
    public bool CanDrive=>DriveRequirement=="Ready to drive";
    public float RideHeight=>Math.Max(.5f,-Parts.Values.Where(p=>IsConnected(p.Id)).SelectMany(p=>Catalog[p.File].Clearance.Select(b=>b.Transform(p.Position,p.Rotation).Min.Y)).Min()+.03f);
    public string PlacementIssue(string file,int parent,Vector3 position,Quaternion rotation,string connector="",int ignore=-1)
    {
        if(!Catalog.Parts.ContainsKey(file)||!Parts.ContainsKey(parent))return "Attach to the connected machine";
        if(!Finite(position)||!Finite(new(rotation.X,rotation.Y,rotation.Z))||!float.IsFinite(rotation.W)||Math.Abs(rotation.LengthSquared()-1)>.01f)return "Invalid placement transform";
        if(position.Length()>16)return "Build area is 32 metres wide";
        if(Parts.Count>=256)return "Workshop limit: 256 parts";

        if((file.StartsWith("scrap_wheel_")||file=="scrap_axle_2m")&&HasTracks)return "Remove track pods before adding wheels or axles";
        if(file=="scrap_track_pod"&&Parts.Values.Any(p=>p.File.StartsWith("scrap_wheel_")||p.File=="scrap_axle_2m"))return "Remove wheels and axles before adding tracks";
        if(connector==""||!Catalog[Parts[parent].File].Sockets.Any(s=>s.Name==connector))return "Choose an available connector";
        if(IsSocketOccupied(parent,connector,ignore))return "Connection already occupied";
        var parentBlock=Parts[parent];
        var ownBounds=Catalog[file].Bounds.Transform(position,rotation);var parentBounds=Catalog[parentBlock.File].Bounds.Transform(parentBlock.Position,parentBlock.Rotation);
        Vector3 separation=Vector3.Max(Vector3.Zero,Vector3.Max(parentBounds.Min-ownBounds.Max,ownBounds.Min-parentBounds.Max));
        if(separation.Length()>.32f)return "Part must touch its supporting structure";
        foreach(var other in Parts.Values)
        {
            if(other.Id==ignore)continue;
            // Mechanical flange and hub joints deliberately share their connector volume.
            bool hub=(Catalog.Standard&&other.Parent==ignore&&(Catalog[other.File].Kind is "wheel" or "gear" or "caster" or "skate" or "flywheel"||other.ParentBone!="Root"))||(Catalog.Standard&&other.Id==parent&&(Catalog[file].Kind is "wheel" or "gear" or "caster" or "skate" or "flywheel"||Catalog[other.File].Sockets.Any(s=>s.Name==connector&&s.Bone!="Root")))||(other.Id==parent && (file.StartsWith("scrap_wheel_")||other.File=="scrap_steering_pivot"||other.File=="scrap_suspension_piston"))||(other.Parent==ignore&&(other.File.StartsWith("scrap_wheel_")||file=="scrap_steering_pivot"||file=="scrap_suspension_piston"));
            if(hub)continue;
            bool meeting=Catalog.Standard&&Catalog[file].Sockets.Any(a=>Catalog[other.File].Sockets.Any(b=>!IsSocketOccupied(other.Id,b.Name,ignore)&&Vector3.Distance(position+Vector3.Transform(a.Position,rotation),other.Position+Vector3.Transform(b.Position,other.Rotation))<.025f&&Vector3.Dot(Vector3.Transform(a.Normal,rotation),Vector3.Transform(b.Normal,other.Rotation))<-.995f));
            foreach(var a in Catalog[file].Clearance)foreach(var b in Catalog[other.File].Clearance)
                if(a.Transform(position,rotation).Intersects(b.Transform(other.Position,other.Rotation),other.Id==parent||other.Parent==ignore||meeting?.13f:.045f))return "Not enough space: overlaps "+DisplayName(other.File);
        }
        return "";
    }
    public int Add(string file,int parent,Vector3 position,Quaternion rotation,string parentBone="Root",string parentConnector="",string ownConnector="")
    {
        string issue=PlacementIssue(file,parent,position,rotation,parentConnector);
        if(issue=="")issue=ConnectionIssue(file,parent,position,rotation,parentBone,parentConnector,ownConnector);
        if(issue!=""){if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("PLACE REJECT",$"file={file} parent={parent} target={parentConnector} source={ownConnector} position={position} rotation={rotation} reason={issue}");return -1;}
        int id=_nextId++;Parts.Add(id,new(id,file,parent,position,rotation,parentBone,parentConnector,ownConnector));if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("PLACE",$"id={id} file={file} parent={parent} bone={parentBone} target={parentConnector} source={ownConnector} position={position} rotation={rotation}");return id;
    }
    public int[] Descendants(int id)
    {
        var found=new HashSet<int>{id};bool changed;do {changed=false;foreach(var p in Parts.Values)if(found.Contains(p.Parent)&&found.Add(p.Id))changed=true;}while(changed);return found.OrderByDescending(i=>i).ToArray();
    }
    public string ConnectionIssue(string file,int parent,Vector3 position,Quaternion rotation,string bone,string target,string source)
    {
        if(!Parts.TryGetValue(parent,out var support)||!Catalog.Parts.TryGetValue(file,out var definition))return "Missing support";
        var a=Catalog[support.File].Sockets.FirstOrDefault(s=>s.Name==target);var b=definition.Sockets.FirstOrDefault(s=>s.Name==source);
        if(a==null||b==null||a.Bone!=bone)return "Unknown attachment point";
        var targetPosition=support.Position+Vector3.Transform(a.Position,support.Rotation);
        if(Vector3.Distance(targetPosition,position+Vector3.Transform(b.Position,rotation))>.025f)return "Connectors must meet";
        if(Vector3.Dot(Vector3.Transform(a.Normal,support.Rotation),Vector3.Transform(b.Normal,rotation))>-.995f)return "Connector faces must oppose each other";
        return "";
    }
    public (Vector3 Position,Quaternion Rotation,string Bone) Snap(int parent,string target,string file,string source,int twist=0)
    {
        var p=Parts[parent];var a=Catalog[p.File].Sockets.First(s=>s.Name==target);var b=Catalog[file].Sockets.First(s=>s.Name==source);
        var normal=Vector3.Transform(a.Normal,p.Rotation);var from=Vector3.Normalize(b.Normal);var to=-normal;float dot=Vector3.Dot(from,to);
        Quaternion alignment=dot>.9999f?Quaternion.Identity:dot<-.9999f?Quaternion.CreateFromAxisAngle(Math.Abs(from.Y)<.9f?Vector3.UnitY:Vector3.UnitX,MathF.PI):Quaternion.Normalize(new Quaternion(Vector3.Cross(from,to),1+dot));
        var rotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(normal,twist*MathF.PI/180)*alignment);
        return (p.Position+Vector3.Transform(a.Position,p.Rotation)-Vector3.Transform(b.Position,rotation),rotation,a.Bone);
    }
    public int AddAtSocket(string file,int parent,string target,string source,int twist=0)
    {var pose=Snap(parent,target,file,source,twist);return Add(file,parent,pose.Position,pose.Rotation,pose.Bone,target,source);}
    public string MoveAtSocket(int id,int parent,string target,string source,int twist=0,bool preview=false)
    {
        if(id==0||!Parts.ContainsKey(id)||!Parts.ContainsKey(parent)||Descendants(id).Contains(parent))return "Choose a connector outside this branch";
        var branch=Descendants(id);var originals=branch.ToDictionary(i=>i,i=>Parts[i]);var old=Parts[id];
        var pose=Snap(parent,target,old.File,source,twist);
        var bind=Matrix4x4.CreateFromQuaternion(old.Rotation)*Matrix4x4.CreateTranslation(old.Position);Matrix4x4.Invert(bind,out var inverse);
        var delta=inverse*Matrix4x4.CreateFromQuaternion(pose.Rotation)*Matrix4x4.CreateTranslation(pose.Position);
        foreach(int child in branch)
        {var p=Parts[child];var matrix=Matrix4x4.CreateFromQuaternion(p.Rotation)*Matrix4x4.CreateTranslation(p.Position)*delta;Matrix4x4.Decompose(matrix,out _,out var rotation,out var position);Parts[child]=p with {Position=position,Rotation=rotation};}
        Parts[id]=Parts[id] with {Parent=parent,ParentBone=pose.Bone,ParentConnector=target,OwnConnector=source};
        string issue="";
        foreach(int child in branch)
        {var p=Parts[child];issue=PlacementIssue(p.File,p.Parent,p.Position,p.Rotation,p.ParentConnector,p.Id);if(issue!="")break;}
        if(issue!=""||preview)foreach(var pair in originals)Parts[pair.Key]=pair.Value;
        if(!preview&&ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("MOVE",$"id={id} parent={parent} target={target} source={source} twist={twist} result={issue} branch={string.Join(",",branch)}");
        return issue;
    }
    public bool Remove(int id)
    {
        if(id==0||!Parts.Remove(id)){if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("DELETE REJECT",$"id={id}");return false;}
        if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("DELETE",$"id={id} detached={string.Join(",",Parts.Values.Where(p=>p.Parent==id).Select(p=>p.Id))}");
        foreach(var child in Parts.Values.Where(p=>p.Parent==id).ToArray())Parts[child.Id]=child with {Parent=-1,ParentBone="Root",ParentConnector="",OwnConnector=""};
        return true;
    }
    public string ToJson()=>JsonSerializer.Serialize(new AssemblySnapshot(5,Parts.Values.OrderBy(p=>p.Id).ToArray(),Catalog.Revision),Options);
    public static VehicleAssembly FromJson(VehiclePartCatalog catalog,string json)
    {
        if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("RESTORE",$"catalogRevision={catalog.Revision} json={json}");
        var snapshot=JsonSerializer.Deserialize<AssemblySnapshot>(json,Options)??throw new JsonException("Empty build");
        if(snapshot.Version is not (4 or 5)||snapshot.Blocks==null||snapshot.Blocks.Length is <1 or >256)throw new JsonException("This machine uses an older chassis builder. Rebuild from the master block; your old file is preserved.");
        var root=snapshot.Blocks.SingleOrDefault(p=>p.Id==0)??throw new JsonException("Missing master block");
        if(root.Parent!=-1||root.File!=MasterBlock||root.Position!=Vector3.Zero||root.Rotation!=Quaternion.Identity)throw new JsonException("Invalid master block");
        var a=new VehicleAssembly(catalog);
        foreach(var p in snapshot.Blocks.Where(p=>p.Id!=0).OrderBy(p=>p.Id))
        {
            if(p.Id<1||p.Id>1000000||a.Parts.ContainsKey(p.Id)||!catalog.Parts.ContainsKey(p.File)||!Finite(p.Position)||!float.IsFinite(p.Rotation.LengthSquared())||Math.Abs(p.Rotation.LengthSquared()-1)>.01f||p.Position.Length()>16)throw new JsonException("Invalid block");
            a.Parts.Add(p.Id,p);a._nextId=Math.Max(a._nextId,p.Id+1);
        }
        foreach(var part in a.Parts.Values.Where(p=>p.Id!=0))
            if(part.Parent>=0 && !a.Parts.ContainsKey(part.Parent))throw new JsonException("Missing parent block");
        if(snapshot.CatalogVersion!=catalog.Revision && catalog.Legacy is {} previous)
        {
            var oldAssembly=new VehicleAssembly(previous);
            foreach(var part in snapshot.Blocks.Where(p=>p.Id!=0))oldAssembly.Parts[part.Id]=part;
            var complete=new HashSet<int>{0};var visiting=new HashSet<int>();
            void Resnap(int id)
            {
                if(complete.Contains(id))return;
                if(!visiting.Add(id))throw new JsonException("Cyclic saved assembly");
                var part=a.Parts[id];
                if(part.Parent>=0)
                {
                    Resnap(part.Parent);
                    var original=oldAssembly.Snap(part.Parent,part.ParentConnector,part.File,part.OwnConnector);
                    var relative=Quaternion.Normalize(part.Rotation*Quaternion.Inverse(original.Rotation));
                    var oldParent=oldAssembly.Parts[part.Parent];
                    var socket=previous[oldParent.File].Sockets.Single(s=>s.Name==part.ParentConnector);
                    var normal=Vector3.Transform(socket.Normal,oldParent.Rotation);
                    float angle=2*MathF.Atan2(Vector3.Dot(new(relative.X,relative.Y,relative.Z),normal),relative.W);
                    int twist=(int)MathF.Round(angle*180/MathF.PI);
                    var pose=a.Snap(part.Parent,part.ParentConnector,part.File,part.OwnConnector,twist);
                    a.Parts[id]=part with {Position=pose.Position,Rotation=pose.Rotation,ParentBone=pose.Bone};
                }
                visiting.Remove(id);complete.Add(id);
            }
            foreach(int id in a.Parts.Keys.ToArray())Resnap(id);
        }
        foreach(var p in a.Parts.Values.Where(p=>p.Id!=0))
        {
            if(p.Parent==-1){if(p.ParentConnector!=""||p.OwnConnector!="")throw new JsonException("Invalid detached block");continue;}
            if(!a.Parts.ContainsKey(p.Parent)||a.Descendants(p.Id).Contains(p.Parent)||a.PlacementIssue(p.File,p.Parent,p.Position,p.Rotation,p.ParentConnector,p.Id)!=""||a.ConnectionIssue(p.File,p.Parent,p.Position,p.Rotation,p.ParentBone,p.ParentConnector,p.OwnConnector)!="")throw new JsonException($"Invalid connection or clearance for block {p.Id} ({p.File}): {a.PlacementIssue(p.File,p.Parent,p.Position,p.Rotation,p.ParentConnector,p.Id)} / {a.ConnectionIssue(p.File,p.Parent,p.Position,p.Rotation,p.ParentBone,p.ParentConnector,p.OwnConnector)}");
        }
        return a;
    }
    public static string DisplayName(string file) {int i=Array.IndexOf(VehicleBuildLayout.PartFiles,file);return i>=0?VehicleBuildLayout.PartNames[i]:file==MasterBlock?"Master block":System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(file.Replace("goblin_","").Replace("_"," "));}
    private static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    private static readonly JsonSerializerOptions Options=new(){IncludeFields=true,WriteIndented=true};
    public sealed record AssemblySnapshot(int Version,AssemblyPart[] Blocks,int CatalogVersion=2);
}
