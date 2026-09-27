using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Gameplay;
using ByteEngine.Editor;
using StbTrueTypeSharp;

namespace ByteEngine.Tests;

internal static class UiTextSystemTests
{
    public static void Run(string root)
    {
        string content = Path.Combine(root, "Assets");
        File.WriteAllBytes(Path.Combine(content, "Label.ttf"), new byte[] { 0, 1, 2, 3 });
        File.WriteAllText(Path.Combine(content, "Pixel.fnt"),
            "info face=\"Test\" size=16\ncommon lineHeight=16 base=12 scaleW=64 scaleH=64 pages=1\npage id=0 file=\"Pixel.png\"\nchars count=0\n");

        IReadOnlyList<string> words = UiTextLayout.WrapLines("hello world", 55f, value => value.Length * 10f);
        Assert(words.SequenceEqual(new[] { "hello", "world" }), "Wrapping keeps words together.");
        IReadOnlyList<string> longWord = UiTextLayout.WrapLines("abcdef", 25f, value => value.Length * 10f);
        Assert(longWord.SequenceEqual(new[] { "ab", "cd", "ef" }), "Oversized words wrap by glyph.");
        IReadOnlyList<string> paragraphs = UiTextLayout.WrapLines("first\nsecond", 0f, value => value.Length * 10f);
        Assert(paragraphs.SequenceEqual(new[] { "first", "second" }), "Explicit line breaks survive layout.");

        TestBundledFontRasterization();
        TestFontInstallation(root);
        TestUiEvents();

        using var database = new AssetDatabase(root, new[] { "Assets", "Scenes" });
        AssetRecord[] fonts = database.Assets.Where(asset => asset.Type == AssetType.Font).ToArray();
        Assert(fonts.Length == 2, "TTF and BMFont files are indexed as fonts.");
        AssetRecord vectorFont = fonts.Single(asset => asset.ProjectPath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));
        using var assets = new AssetManager(database);
        var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));

        var scene = new Scene("Text Test");
        GameObject canvas = scene.CreateGameObject("Canvas");
        canvas.AddComponent(new UiCanvas
        {
            ScaleMode = UiScaleMode.ScaleWithScreen,
            ReferenceResolution = new Vector2(1280f, 720f)
        });
        GameObject label = scene.CreateGameObject("Text");
        label.SetParent(canvas);
        label.AddComponent(new UiText
        {
            Text = "Health: 100",
            FontReference = new AssetReference(vectorFont.Guid, vectorFont.ProjectPath),
            FontSize = 40,
            Color = new Vector4(.8f, .5f, .2f, 1f),
            Anchor = UiAnchor.BottomCenter,
            Offset = new Vector2(-80f, -32f),
            WrapWidth = 300f,
            OrderInLayer = 5
        });

        GameObject barObject = scene.CreateGameObject("Health Bar");
        barObject.SetParent(canvas);
        var bar = new UiWidget
        {
            Kind = UiWidgetKind.ProgressBar,
            Anchor = UiAnchor.BottomRight,
            Offset = new Vector2(-24f, -24f),
            Size = new Vector2(200f, 30f),
            Value = 75f,
            Maximum = 100f
        };
        barObject.AddComponent(bar);
        var layout = UiLayout.Resolve(barObject, bar.Anchor, bar.Offset, bar.Size,
            new Vector2(1280f, 720f));
        Assert(layout.Position == new Vector2(1056f, 666f) &&
            layout.Size == new Vector2(200f, 30f),
            "Bottom-right UI widget positions correctly at reference resolution.");
        var scaled = UiLayout.Resolve(barObject, bar.Anchor, bar.Offset, bar.Size,
            new Vector2(1920f, 1080f));
        Assert(scaled.Position == new Vector2(1584f, 999f) &&
            scaled.Size == new Vector2(300f, 45f),
            "Canvas scales UI layout consistently with screen resolution.");

        UiCanvas canvasSettings = canvas.GetComponent<UiCanvas>()!;
        canvasSettings.SafeAreaInsets = new Vector4(10f, 20f, 30f, 40f);
        var safe = UiLayout.ResolveCanvas(canvasSettings, new Vector2(1280f, 720f));
        Assert(safe.Origin == new Vector2(10f, 20f) &&
            safe.Size == new Vector2(1240f, 660f),
            "Canvas safe-area insets keep HUD content inside screen edges.");
        canvasSettings.SafeAreaInsets = Vector4.Zero;

        GameObject panel = scene.CreateGameObject("Panel");
        panel.SetParent(canvas);
        UiWidget panelWidget = panel.AddComponent(new UiWidget
        {
            Kind = UiWidgetKind.Panel,
            Anchor = UiAnchor.Center,
            Size = new Vector2(400f, 240f)
        });
        GameObject nestedText = scene.CreateGameObject("Nested Text");
        nestedText.SetParent(panel);
        nestedText.AddComponent(new UiText());
        var panelRect = UiLayout.Resolve(panel, panelWidget.Anchor,
            panelWidget.Offset, panelWidget.Size, new Vector2(1280f, 720f));
        var textParent = UiLayout.ResolveParent(nestedText, new Vector2(1280f, 720f));
        Assert(textParent.Origin == panelRect.Position &&
            textParent.Size == panelRect.Size,
            "Nested Text anchors inside its parent Panel.");
        panelWidget.Visible = false;
        Assert(!UiLayout.IsVisible(nestedText),
            "Hiding a panel hides its nested Text.");
        panelWidget.Visible = true;
        label.GetComponent<UiText>()!.ShadowColor = new Vector4(0f, 0f, 0f, .5f);

        Scene clone = serializer.CloneForRuntime(scene);
        GameObject? copiedCanvas = clone.FindGameObject("Canvas");
        GameObject? copiedLabel = clone.FindGameObject("Text");
        UiText? text = copiedLabel?.GetComponent<UiText>();
        UiWidget? copiedBar = clone.FindGameObject("Health Bar")?.GetComponent<UiWidget>();
        Assert(copiedCanvas?.GetComponent<UiCanvas>() is
            { ScaleMode: UiScaleMode.ScaleWithScreen } &&
            copiedBar is { Kind: UiWidgetKind.ProgressBar, Value: 75f, Maximum: 100f } &&
            copiedBar.Anchor == UiAnchor.BottomRight,
            "Canvas layout and progress bar survive scene serialization.");
        Assert(copiedCanvas?.GetComponent<UiCanvas>() != null &&
            ReferenceEquals(copiedLabel?.Parent, copiedCanvas) &&
            text?.Text == "Health: 100" &&
            text.FontReference.Guid == vectorFont.Guid &&
            text.FontSize == 40 &&
            text.Color == new Vector4(.8f, .5f, .2f, 1f) &&
            text.ShadowColor == new Vector4(0f, 0f, 0f, .5f) &&
            text.Anchor == UiAnchor.BottomCenter &&
            text.Offset == new Vector2(-80f, -32f) &&
            text.WrapWidth == 300f &&
            text.OrderInLayer == 5,
            "Canvas hierarchy and Text settings survive scene serialization.");
    }

    private static void TestBundledFontRasterization()
    {
        string bundle = Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts");
        foreach (string name in BundledFontInstaller.FontFiles)
        {
            byte[] font = File.ReadAllBytes(Path.Combine(bundle, name));
            byte[] pixels = new byte[2048 * 2048];
            var chars = new StbTrueType.stbtt_bakedchar[224];
            bool baked = StbTrueType.stbtt_BakeFontBitmap(font, 0, 32f, pixels,
                2048, 2048, 32, chars.Length, chars);
            Assert(baked && chars['A' - 32].xadvance > 0f && pixels.Any(value => value != 0),
                $"Bundled font {name} rasterizes readable Latin glyphs.");
        }
    }

    private static void TestFontInstallation(string root)
    {
        string bundle = Path.Combine(root, "FontBundle");
        Directory.CreateDirectory(bundle);
        foreach (string name in BundledFontInstaller.FontFiles)
            File.WriteAllBytes(Path.Combine(bundle, name), new byte[] { 0, 1, 2, 3 });
        File.WriteAllText(Path.Combine(bundle, "CC0-LICENSE.txt"), "CC0 test fixture");

        using var project = EditorProjectContext.Create(Path.Combine(root, "FontInstall.byteproject"), _ => { });
        Assert(BundledFontInstaller.Install(project, bundle) == 3, "Built-in fonts install once.");
        Assert(BundledFontInstaller.Install(project, bundle) == 0, "Reinstall does not overwrite project fonts.");
        AssetRecord[] installed = project.AssetDatabase.Assets.Where(asset => asset.Type == AssetType.Font).ToArray();
        Assert(installed.Length == 3, "Bundled fonts appear in the project font picker.");

        AssetRecord original = installed.Single(asset => asset.ProjectPath.EndsWith("TypeLightSans.ttf", StringComparison.OrdinalIgnoreCase));
        AssetReference saved = new(original.Guid, original.ProjectPath);
        string renamed = Path.Combine(Path.GetDirectoryName(original.FullPath)!, "Renamed.ttf");
        File.Move(original.FullPath, renamed);
        File.Move(original.FullPath + ".meta", renamed + ".meta");
        project.AssetDatabase.Scan();
        Assert(FontRuntime.ResolvePath(saved) == renamed, "Font references follow GUID-preserving renames.");

        string external = Path.Combine(root, "CustomFont.ttf");
        File.WriteAllBytes(external, new byte[] { 0, 1, 2, 3 });
        var log = new EditorLog();
        IReadOnlyList<AssetRecord> imported = FontFileImport.Import(project, log,
            new[] { external }, Path.Combine(project.ProjectRoot, "Assets"));
        Assert(imported.Count == 1 && imported[0].Type == AssetType.Font &&
            imported[0].ProjectPath.EndsWith("CustomFont.ttf", StringComparison.OrdinalIgnoreCase),
            "Custom TTF imports as a project font asset.");

        string bitmap = Path.Combine(root, "CustomBitmap.fnt");
        string atlas = Path.Combine(root, "CustomBitmap.png");
        File.WriteAllText(bitmap, "common lineHeight=16 base=12 pages=1\npage id=0 file=\"CustomBitmap.png\"\n");
        File.WriteAllBytes(atlas, new byte[] { 1, 2, 3, 4 });
        IReadOnlyList<AssetRecord> bitmapImports = FontFileImport.Import(project, log,
            new[] { bitmap }, Path.Combine(project.ProjectRoot, "Assets"));
        Assert(bitmapImports.Count == 2 && bitmapImports.Any(asset => asset.Type == AssetType.Font) &&
            bitmapImports.Any(asset => asset.Type == AssetType.Texture2D),
            "Bitmap font import keeps the FNT and atlas together.");
    }

    private static void TestUiEvents()
    {
        var scene = new Scene("UI Events");
        GameObject hud = scene.CreateGameObject("HUD");
        UiText text = hud.AddComponent(new UiText());
        GameObject barObject = scene.CreateGameObject("Health Bar");
        UiWidget bar = barObject.AddComponent(new UiWidget { Kind = UiWidgetKind.ProgressBar });
        GameObject player = scene.CreateGameObject("Player");
        HealthComponent health = player.AddComponent(new HealthComponent());
        health.Damage(25f);

        var context = new EventExecutionContext
        {
            Globals = new VariableStore(), Scene = scene, Self = hud
        };
        VisualLogicRegistry registry = VisualLogicRegistry.CreateDefault();
        bool textRegistered = registry.TryGetAction("ui.setText", out VisualActionDefinition? setText);
        bool barRegistered = registry.TryGetAction("ui.setBarFromHealth", out VisualActionDefinition? setBar);
        bool belowRegistered = registry.TryGetCondition("ui.barBelow", out VisualConditionDefinition? below);
        Assert(textRegistered && barRegistered && belowRegistered,
            "UI Event Sheet actions and conditions are registered.");
        setText!.Execute(new VisualInstruction
        {
            Id = "ui.setText",
            Arguments = new()
            {
                ["target"] = EventValue.String("Self"),
                ["text"] = EventValue.String("HP 75/100")
            }
        }, context);
        setBar!.Execute(new VisualInstruction
        {
            Id = "ui.setBarFromHealth",
            Arguments = new()
            {
                ["target"] = EventValue.String("Health Bar"),
                ["source"] = EventValue.String("Player")
            }
        }, context);
        registry.TryGetAction("ui.setTextFromHealth", out VisualActionDefinition? healthText);
        Assert(healthText != null, "Health text UI action registered.");
        healthText!.Execute(new VisualInstruction
        {
            Id = "ui.setTextFromHealth",
            Arguments = new()
            {
                ["target"] = EventValue.String("Self"),
                ["source"] = EventValue.String("Player"),
                ["prefix"] = EventValue.String("HP ")
            }
        }, context);
        Assert(text.Text == "HP 75/100" && bar.Value == 75f && bar.Maximum == 100f,
            "UI nodes update text and a health bar.");
        Assert(below!.Evaluate(new VisualInstruction
        {
            Id = "ui.barBelow",
            Arguments = new()
            {
                ["target"] = EventValue.String("Health Bar"),
                ["value"] = EventValue.Number(80)
            }
        }, context), "UI conditions read progress bar values.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
