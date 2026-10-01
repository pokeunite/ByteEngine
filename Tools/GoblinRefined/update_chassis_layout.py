from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuildLayout.cs');s=p.read_text();s=s.replace('new("Rear chassis extension", new(0,0,1), ["scrap_frame_long"])','new("Retired extension mount", new(0,0,1), [])')
start=s.index('    private readonly Dictionary<int,string> _parts');s=s[:start]+'''
    public string Chassis { get; private set; } = "scrap_frame_2x1";
    public bool LongChassis => Chassis=="scrap_frame_long";
    public VehicleMount[] ActiveMounts => GetMounts(Chassis);
    private static readonly VehicleMount[] LongMounts = Mounts.Select((m,i)=>m with
    {
        Position = m.Position + (i is 0 or 1 or 4 ? new Vector3(0,0,-.8f) :
            i is 2 or 3 or 5 or 14 or 15 or 18 or 25 or 26 ? new Vector3(0,0,.8f) :
            i is 10 or 11 or 22 or 23 ? new Vector3(0,0,-.9f) : Vector3.Zero)
    }).ToArray();
    public static VehicleMount[] GetMounts(string chassis) => chassis=="scrap_frame_long" ? LongMounts : Mounts;
    public bool SetChassis(string chassis)
    {
        if(chassis is not ("scrap_frame_2x1" or "scrap_frame_long")) return false;
        Chassis=chassis; return true;
    }
    private readonly Dictionary<int,string> _parts = new();
    public IReadOnlyDictionary<int,string> Parts => _parts;
    public bool HasAnyTracks => _parts.ContainsKey(19) || _parts.ContainsKey(20);
    public bool HasWheelRunningGear => _parts.Keys.Any(i=>i>=0 && i<=5);
    public string PlacementIssue(int mount,string part)
    {
        if(mount<0 || mount>=ActiveMounts.Length || !ActiveMounts[mount].Parts.Contains(part,StringComparer.Ordinal)) return "Choose a compatible mount.";
        if(mount<=5 && HasAnyTracks) return "Remove both track pods before adding wheel running gear.";
        if(mount is 19 or 20 && HasWheelRunningGear) return "Remove wheels and axles before adding track pods.";
        return _parts.ContainsKey(mount) ? "Mount occupied. Click to replace its part." : "";
    }
    public bool CanPlace(int mount,string part)=>PlacementIssue(mount,part)=="";
    public bool Place(int mount,string part)
    {
        if(!CanPlace(mount,part)) return false;
        _parts.Add(mount,part); return true;
    }
    public bool Remove(int mount)=>_parts.Remove(mount);
    public void Clear()=>_parts.Clear();
    public bool HasTracks => _parts.ContainsKey(19) && _parts.ContainsKey(20);
    public string DriveRequirement => !HasTracks && Enumerable.Range(0,4).Any(i => !_parts.ContainsKey(i)) ? "Add four wheels or both track pods" :
        !HasTracks && (!_parts.ContainsKey(4) || !_parts.ContainsKey(5)) ? "Add front and rear axles" :
        !_parts.ContainsKey(6) ? "Add an engine" : !_parts.ContainsKey(7) ? "Add the driver cab" : "Ready to drive";
    public bool CanDrive=>DriveRequirement=="Ready to drive";
    public float RideHeight=>_parts.Values.Contains("scrap_wheel_large") ? 1.10f : HasTracks ? .4125f : .875f;
    public string ToJson()=>JsonSerializer.Serialize(new Snapshot(2,new(_parts),Chassis),new JsonSerializerOptions { WriteIndented=true });
    public static VehicleBuildLayout FromJson(string json)
    {
        var snapshot=JsonSerializer.Deserialize<Snapshot>(json) ?? throw new JsonException("Empty vehicle build.");
        if(snapshot.Version is not (1 or 2) || snapshot.Parts==null) throw new JsonException("Unsupported vehicle build.");
        var layout=new VehicleBuildLayout();
        if(!layout.SetChassis(snapshot.Version==1 && snapshot.Parts.GetValueOrDefault(24)=="scrap_frame_long" ? "scrap_frame_long" : snapshot.Chassis)) throw new JsonException("Unknown chassis.");
        foreach(var part in snapshot.Parts)
        {
            if(snapshot.Version==1 && part.Key==24 && part.Value=="scrap_frame_long") continue;
            if(!layout.Place(part.Key,part.Value)) throw new JsonException("Invalid mount, incompatible part, or mixed wheels/tracks.");
        }
        return layout;
    }
    public sealed record Snapshot(int Version,Dictionary<int,string> Parts,string Chassis="scrap_frame_2x1");
}
''';p.write_text(s)
