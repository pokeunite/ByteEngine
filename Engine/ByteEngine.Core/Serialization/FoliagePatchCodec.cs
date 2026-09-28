using System.Text.Json.Nodes;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Serialization;

internal sealed class FoliagePatchCodec : IComponentCodec
{
    public string TypeName => "FoliagePatch";
    public Type ComponentType => typeof(FoliagePatch);
    public ComponentData Serialize(Component component, ComponentSerializationContext context)
    {
        var patch = (FoliagePatch)component;
        return new ComponentData { Type = TypeName, Properties = new JsonObject
        {
            ["modelGuid"] = patch.Model.Guid.ToString(), ["modelPath"] = patch.Model.CachedProjectPath,
            ["materialGuid"] = patch.MaterialAsset.Guid.ToString(), ["materialPath"] = patch.MaterialAsset.CachedProjectPath,
            ["width"] = patch.Area.X, ["depth"] = patch.Area.Y, ["amount"] = patch.Amount, ["seed"] = patch.Seed,
            ["height"] = patch.PlantHeight, ["variation"] = patch.SizeVariation,
            ["snap"] = patch.SnapToGround, ["wind"] = patch.WindEnabled,
            ["strength"] = patch.WindStrength, ["speed"] = patch.WindSpeed,
            ["distance"] = patch.ViewDistance, ["shadows"] = patch.CastShadows,
            ["paintedMode"] = patch.UsePaintedLayout, ["brushRadius"] = patch.BrushRadius, ["paintDensity"] = patch.PaintDensity,
            ["plants"] = new JsonArray(patch.PaintedPlants.Select(p =>
                (JsonNode)new JsonArray(p.X,p.Y,p.Z,p.W)).ToArray())
        }};
    }
    public Component Deserialize(ComponentData data, ComponentSerializationContext context)
    {
        JsonObject p = data.Properties;
        AssetReference Reference(string prefix) => new(
            Guid.TryParse(p[prefix + "Guid"]?.GetValue<string>(), out Guid id) ? id : Guid.Empty,
            p[prefix + "Path"]?.GetValue<string>() ?? string.Empty);
        var result = new FoliagePatch
        {
            BrushRadius = p["brushRadius"]?.GetValue<float>() ?? 1, PaintDensity = p["paintDensity"]?.GetValue<float>() ?? 3,
            UsePaintedLayout = p["paintedMode"]?.GetValue<bool>() ?? false, Model = Reference("model"), MaterialAsset = Reference("material"),
            Area = new(p["width"]?.GetValue<float>() ?? 10, p["depth"]?.GetValue<float>() ?? 10),
            Amount = p["amount"]?.GetValue<int>() ?? 100, Seed = p["seed"]?.GetValue<int>() ?? 1,
            PlantHeight = p["height"]?.GetValue<float>() ?? 1, SizeVariation = p["variation"]?.GetValue<float>() ?? .2f,
            SnapToGround = p["snap"]?.GetValue<bool>() ?? true, WindEnabled = p["wind"]?.GetValue<bool>() ?? true,
            WindStrength = p["strength"]?.GetValue<float>() ?? 3, WindSpeed = p["speed"]?.GetValue<float>() ?? 1,
            ViewDistance = p["distance"]?.GetValue<float>() ?? 80, CastShadows = p["shadows"]?.GetValue<bool>() ?? true
        };
        if (p["plants"] is JsonArray plants)
            result.RestorePaintedPlants(plants.OfType<JsonArray>().Where(a => a.Count == 4)
                .Select(a => new System.Numerics.Vector4(a[0]!.GetValue<float>(),a[1]!.GetValue<float>(),
                    a[2]!.GetValue<float>(),a[3]!.GetValue<float>())));
        return result;
    }
}
