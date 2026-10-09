using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Tests;
internal static class ReusableGameSystemsTests
{
    public static void Run(string root)
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        var scene = new Scene("Reusable HUD");
        var dial = scene.CreateGameObject("Dial"); dial.AddComponent(new UiWidget());
        var text = scene.CreateGameObject("Readout").AddComponent(new UiText());
        var speed = dial.AddComponent(new Speedometer { ReadoutObject = "Readout", DialPathPrefix = "dial-" });
        speed.SetSpeed(-10); speed.Refresh(); Check(text.Text == "36 km/h", "Speed conversion or reverse motion failed");
        Check(dial.GetComponent<UiWidget>()!.ImageReference.ProjectPath == "dial-18.png", "Dial frame selection failed");
        speed.SetSpeed(1000); speed.Refresh(); Check(dial.GetComponent<UiWidget>()!.ImageReference.ProjectPath == "dial-60.png", "Dial frame upper bound failed");
        speed.SetSpeed(10);
        speed.Unit = SpeedUnit.MilesPerHour; speed.DecimalPlaces = 1; speed.Refresh(); Check(text.Text == "22.4 mph", "MPH conversion failed");
        speed.SetSpeed(float.NaN); speed.Refresh(); Check(speed.DisplaySpeed == 0, "Invalid speed propagates into HUD");
        var marker = scene.CreateGameObject("Marker"); var widget = marker.AddComponent(new UiWidget { Size = new(8) });
        var map = marker.AddComponent(new Minimap { WorldCenter = new(100, 200), WorldSize = new(200, 400), MapOffset = new(10, 20), MapSize = new(100, 200) });
        map.SetPosition(new(100, 0, 200)); Check(widget.Offset == new Vector2(56, 116), "Minimap center mapping failed");
        Check(map.WorldToMap(new(1000, 0, -1000)) == new Vector2(110, 20), "Minimap boundary clamp failed");
        map.InvertZ = true; Check(map.WorldToMap(new(1000, 0, -1000)) == new Vector2(110, 220), "Minimap inverted axis failed");
        using var database = new AssetDatabase(root, ["Assets"]);
        using var assets = new AssetManager(database);
        var serializer = new ComponentSerializer(Path.Combine(root, "Assets"), database, assets, null);
        var restoredSpeed = (Speedometer)serializer.Deserialize(serializer.Serialize(speed)!)!;
        var restoredMap = (Minimap)serializer.Deserialize(serializer.Serialize(map)!)!;
        Check(restoredSpeed.Unit == speed.Unit && restoredSpeed.ReadoutObject == "Readout" && restoredSpeed.DialPathPrefix == "dial-", "Speedometer scene roundtrip failed");
        Check(restoredMap.WorldCenter == map.WorldCenter && restoredMap.MapSize == map.MapSize && restoredMap.InvertZ, "Minimap scene roundtrip failed");
        var saves = new GameSaveService(root); saves.Save("slots/one.json", new Dictionary<string, int> { ["coins"] = 123 });
        Check(saves.Load<Dictionary<string, int>>("slots/one.json")!["coins"] == 123, "Typed save roundtrip failed");
        saves.SaveText("slots/one.json", "replacement"); Check(saves.LoadText("slots/one.json") == "replacement", "Save replacement failed");
        bool rejected = false; try { saves.SaveText("../outside.txt", "bad"); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "Save allowed traversal outside its root");
        saves.Delete("slots/one.json"); Check(!saves.Exists("slots/one.json"), "Save deletion failed");
        Check(!Directory.GetFiles(GameSaveStorage.GetDirectory(root), "*.tmp", SearchOption.AllDirectories).Any(), "Temporary save files leaked");
        Console.WriteLine("Reusable speedometer, minimap and native save/load checks passed.");
    }
}
