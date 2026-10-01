using System.Numerics;
using System.Text.Json;

namespace ByteEngine.Core.Construction;

public sealed record VehicleMount(string Name, Vector3 Position, string[] Parts);

/// <summary>Version-one garage layout. Each mount accepts exactly one compatible part.</summary>
public sealed class VehicleBuildLayout
{
    public static readonly string[] PartNames =
    ["Large wheel", "Small wheel", "Axle", "Engine", "Cab", "Armor", "Ram", "Saw", "Weapon mount",
        "Steering pivot", "Suspension", "Short beam", "Long beam", "Corner joint"];
    public static readonly string[] PartFiles =
    ["scrap_wheel_large", "scrap_wheel_small", "scrap_axle_2m", "scrap_engine_block", "scrap_cab_shell",
        "scrap_armor_plate", "scrap_ram_wedge", "scrap_saw_disc", "scrap_weapon_mount", "scrap_steering_pivot",
        "scrap_suspension_piston", "scrap_beam_1m", "scrap_beam_2m", "scrap_corner_joint"];
    public static readonly VehicleMount[] Mounts =
    [
        new("Front left wheel", new(-1.02f,-.15f,-.75f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Front right wheel", new(1.02f,-.15f,-.75f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Rear left wheel", new(-1.02f,-.15f,.75f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Rear right wheel", new(1.02f,-.15f,.75f), ["scrap_wheel_large","scrap_wheel_small"]),
        new("Front axle", new(0,-.15f,-.75f), ["scrap_axle_2m"]),
        new("Rear axle", new(0,-.15f,.75f), ["scrap_axle_2m"]),
        new("Engine bay", new(0,.44f,.56f), ["scrap_engine_block"]),
        new("Driver cab", new(0,.59f,-.47f), ["scrap_cab_shell"]),
        new("Left armor", new(-.64f,.42f,.15f), ["scrap_armor_plate"]),
        new("Right armor", new(.64f,.42f,.15f), ["scrap_armor_plate"]),
        new("Front bumper", new(0,.1f,-1.38f), ["scrap_ram_wedge"]),
        new("Front cutter", new(0,.13f,-1.9f), ["scrap_saw_disc"]),
        new("Roof mount", new(0,1.23f,-.47f), ["scrap_weapon_mount"]),
        new("Steering column", new(0,.18f,-.75f), ["scrap_steering_pivot"]),
        new("Left suspension", new(-.47f,.48f,.75f), ["scrap_suspension_piston"]),
        new("Right suspension", new(.47f,.48f,.75f), ["scrap_suspension_piston"]),
        new("Left rail", new(-.67f,.08f,0), ["scrap_beam_1m","scrap_beam_2m"]),
        new("Right rail", new(.67f,.08f,0), ["scrap_beam_1m","scrap_beam_2m"]),
        new("Rear bracket", new(-.4f,.14f,.92f), ["scrap_corner_joint"])
    ];

    private readonly Dictionary<int,string> _parts = new();
    public IReadOnlyDictionary<int,string> Parts => _parts;
    public bool CanPlace(int mount, string part) => mount >= 0 && mount < Mounts.Length &&
        !_parts.ContainsKey(mount) && Mounts[mount].Parts.Contains(part, StringComparer.Ordinal);
    public bool Place(int mount, string part)
    {
        if (!CanPlace(mount, part)) return false;
        _parts.Add(mount, part);
        return true;
    }
    public bool Remove(int mount) => _parts.Remove(mount);
    public void Clear() => _parts.Clear();
    public string DriveRequirement => Enumerable.Range(0,4).Any(i => !_parts.ContainsKey(i)) ? "Add all four wheels" :
        !_parts.ContainsKey(4) || !_parts.ContainsKey(5) ? "Add front and rear axles" :
        !_parts.ContainsKey(6) ? "Add an engine" : !_parts.ContainsKey(7) ? "Add the driver cab" : "Ready to drive";
    public bool CanDrive => DriveRequirement == "Ready to drive";
    public float RideHeight => _parts.Values.Contains("scrap_wheel_large") ? .73f : .53f;
    public string ToJson() => JsonSerializer.Serialize(new Snapshot(1, new(_parts)), new JsonSerializerOptions { WriteIndented = true });
    public static VehicleBuildLayout FromJson(string json)
    {
        var snapshot = JsonSerializer.Deserialize<Snapshot>(json) ?? throw new JsonException("Empty vehicle build.");
        if (snapshot.Version != 1 || snapshot.Parts == null) throw new JsonException("Unsupported vehicle build.");
        var layout = new VehicleBuildLayout();
        foreach (var part in snapshot.Parts)
            if (!layout.Place(part.Key, part.Value)) throw new JsonException("Invalid vehicle mount or incompatible part.");
        return layout;
    }
    public sealed record Snapshot(int Version, Dictionary<int,string> Parts);
}

