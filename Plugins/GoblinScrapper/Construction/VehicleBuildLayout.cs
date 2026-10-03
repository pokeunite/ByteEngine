using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;
using System.Text.Json;

namespace GoblinScrapper.Construction;

public sealed record VehicleMount(string Name, Vector3 Position, string[] Parts);

/// <summary>Stable mount IDs preserve existing saved builds while exposing the refined expansion kit.</summary>
public sealed class VehicleBuildLayout
{
    public static readonly string[] PartNames =
    ["Large wheel", "Small wheel", "Axle", "Engine", "Cab", "Armor", "Ram", "Saw", "Weapon mount",
        "Steering pivot", "Suspension", "Short beam", "Long beam", "Corner joint", "T connector", "Long chassis",
        "Crushing drum", "Saw module", "Battering fist", "Hammer arm", "Auger drill", "Grabber jaws", "Forked ram",
        "Catapult basket", "Swivel turret", "Track pod", "Lift mast", "Outrigger arm"];
    public static readonly string[] PartFiles =
    ["scrap_wheel_large", "scrap_wheel_small", "scrap_axle_2m", "scrap_engine_block", "scrap_cab_shell",
        "scrap_armor_plate", "scrap_ram_wedge", "scrap_saw_disc", "scrap_weapon_mount", "scrap_steering_pivot",
        "scrap_suspension_piston", "scrap_beam_1m", "scrap_beam_2m", "scrap_corner_joint", "scrap_t_connector", "scrap_frame_long",
        "scrap_crushing_drum", "scrap_saw_module", "scrap_battering_fist", "scrap_hammer_arm", "scrap_auger_drill",
        "scrap_grabber_jaws", "scrap_forked_ram", "scrap_catapult_basket", "scrap_swivel_turret", "scrap_track_pod",
        "scrap_lift_mast", "scrap_outrigger_arm"];
    public static readonly string[] FrontWeapons = ["scrap_saw_module", "scrap_battering_fist", "scrap_hammer_arm", "scrap_auger_drill", "scrap_grabber_jaws"];
    public static readonly VehicleMount[] Mounts =
    [
        new("Front left wheel", new(-1.02f,-.46f,-.65f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Front right wheel", new(1.02f,-.46f,-.65f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Rear left wheel", new(-1.02f,-.46f,.65f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Rear right wheel", new(1.02f,-.46f,.65f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Front axle", new(0,-.15f,-.65f), ["scrap_axle_2m"]),
        new("Rear axle", new(0,-.15f,.65f), ["scrap_axle_2m"]),
        new("Engine bay", new(0,.18f,.56f), ["scrap_engine_block"]),
        new("Driver cab", new(0,.18f,-.47f), ["scrap_cab_shell"]),
        new("Left armor", new(-.67f,.2f,0), ["scrap_armor_plate"]),
        new("Right armor", new(.67f,.2f,0), ["scrap_armor_plate"]),
        new("Front bumper", new(0,0,-1), ["scrap_ram_wedge","scrap_forked_ram","scrap_crushing_drum"]),
        new("Front weapon", new(0,.16f,-1.25f), ["scrap_saw_disc", ..FrontWeapons]),
        new("Roof mount", new(0,1.33f,-.47f), ["scrap_weapon_mount","scrap_swivel_turret","scrap_lift_mast"]),
        new("Steering column", new(0,-.12f,-.65f), ["scrap_steering_pivot"]),
        new("Left suspension", new(-.47f,.48f,.65f), ["scrap_suspension_piston"]),
        new("Right suspension", new(.47f,.48f,.65f), ["scrap_suspension_piston"]),
        new("Left rail", new(-.67f,.08f,0), ["scrap_beam_1m","scrap_beam_2m"]),
        new("Right rail", new(.67f,.08f,0), ["scrap_beam_1m","scrap_beam_2m"]),
        new("Rear bracket", new(-.4f,.14f,.92f), ["scrap_corner_joint","scrap_t_connector"]),
        new("Left track", new(-.67f,0,0), ["scrap_track_pod"]),
        new("Right track", new(.67f,0,0), ["scrap_track_pod"]),
        new("Roof payload", new(0,1.91f,-.47f), ["scrap_catapult_basket", ..FrontWeapons]),
        new("Left front weapon", new(-.5f,.16f,-1), FrontWeapons),
        new("Right front weapon", new(.5f,.16f,-1), FrontWeapons),
        new("Retired extension mount", new(0,0,1), []),
        new("Left outrigger", new(-.67f,0,.65f), ["scrap_outrigger_arm"]),
        new("Right outrigger", new(.67f,0,.65f), ["scrap_outrigger_arm"])
    ];

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
