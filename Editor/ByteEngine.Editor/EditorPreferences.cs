using System.Text.Json;

namespace ByteEngine.Editor;

internal static class EditorPreferences
{
    private sealed class SettingsData
    {
        public bool Enable2DEditor { get; set; }

        public bool ShowFpsCounter { get; set; }

        public int LayoutVersion { get; set; }

        public bool BottomWorkspaceCollapsed { get; set; }

        public float AssetIconScale { get; set; } = 1.0f;

        // Debug-box visibility is editor UI state, not runtime debug enablement.
        // Keep these true by default so existing preference files (which do not
        // contain these properties yet) retain the current visible behaviour.
        public bool ShowDebugFootIk { get; set; } = true;

        public bool ShowDebugBlueprintVisibility { get; set; } = true;

        public bool ShowDebugTpsJitter { get; set; } = true;

        public bool ShowDebugWeaponRaycast { get; set; } = true;
    }

    private static readonly string DirectoryPath =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "ByteEngine",
            "Editor"
        );

    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            WriteIndented =
                true,

            PropertyNameCaseInsensitive =
                true
        };

    private static SettingsData? _settings;

    public static string LayoutPath =>
        Path.Combine(
            DirectoryPath,
            "layout.ini"
        );

    private static string LastProjectPath =>
        Path.Combine(
            DirectoryPath,
            "last-project.txt"
        );

    private static string SettingsPath =>
        Path.Combine(
            DirectoryPath,
            "preferences.json"
        );

    public static bool Enable2DEditor
    {
        get =>
            Settings.Enable2DEditor;

        set
        {
            if (Settings.Enable2DEditor ==
                value)
            {
                return;
            }

            Settings.Enable2DEditor =
                value;

            SaveSettings();
        }
    }

    public static bool BottomWorkspaceCollapsed
    {
        get => Settings.BottomWorkspaceCollapsed;
        set
        {
            if (Settings.BottomWorkspaceCollapsed == value) return;
            Settings.BottomWorkspaceCollapsed = value;
            SaveSettings();
        }
    }

    public static float AssetIconScale
    {
        get
        {
            float normalized = NormalizeAssetIconScale(Settings.AssetIconScale);
            if (Math.Abs(Settings.AssetIconScale - normalized) > 0.0001f)
                Settings.AssetIconScale = normalized;
            return normalized;
        }
        set
        {
            float normalized = NormalizeAssetIconScale(value);
            if (Math.Abs(AssetIconScale - normalized) <= 0.0001f) return;
            Settings.AssetIconScale = normalized;
            SaveSettings();
        }
    }

    public static bool ShowFpsCounter
    {
        get =>
            Settings.ShowFpsCounter;

        set
        {
            if (Settings.ShowFpsCounter ==
                value)
            {
                return;
            }

            Settings.ShowFpsCounter =
                value;

            SaveSettings();
        }
    }

    public static bool ShowDebugFootIk
    {
        get => Settings.ShowDebugFootIk;
        set
        {
            if (Settings.ShowDebugFootIk == value) return;
            Settings.ShowDebugFootIk = value;
            SaveSettings();
        }
    }

    public static bool ShowDebugBlueprintVisibility
    {
        get => Settings.ShowDebugBlueprintVisibility;
        set
        {
            if (Settings.ShowDebugBlueprintVisibility == value) return;
            Settings.ShowDebugBlueprintVisibility = value;
            SaveSettings();
        }
    }

    public static bool ShowDebugTpsJitter
    {
        get => Settings.ShowDebugTpsJitter;
        set
        {
            if (Settings.ShowDebugTpsJitter == value) return;
            Settings.ShowDebugTpsJitter = value;
            SaveSettings();
        }
    }

    public static bool ShowDebugWeaponRaycast
    {
        get => Settings.ShowDebugWeaponRaycast;
        set
        {
            if (Settings.ShowDebugWeaponRaycast == value) return;
            Settings.ShowDebugWeaponRaycast = value;
            SaveSettings();
        }
    }

    private static SettingsData Settings =>
        _settings ??=
            LoadSettings();

    public static string? ReadLastProject()
    {
        if (!File.Exists(
                LastProjectPath))
        {
            return null;
        }

        string path =
            File.ReadAllText(
                LastProjectPath
            )
            .Trim();

        return File.Exists(path)
            ? path
            : null;
    }

    public static void SaveLastProject(
        string projectFilePath)
    {
        Directory.CreateDirectory(
            DirectoryPath
        );

        File.WriteAllText(
            LastProjectPath,
            Path.GetFullPath(
                projectFilePath
            )
        );
    }

    /// <summary>
    /// Returns true once when ByteEngine's built-in default layout version
    /// changes. The caller can then rebuild the dock layout exactly once,
    /// while preserving the user's layout on normal future launches.
    /// </summary>
    public static bool EnsureLayoutVersion(
        int version)
    {
        if (Settings.LayoutVersion ==
            version)
        {
            return false;
        }

        Settings.LayoutVersion =
            version;

        SaveSettings();

        return true;
    }

    private static SettingsData LoadSettings()
    {
        try
        {
            if (!File.Exists(
                    SettingsPath))
            {
                return CreateDefaults();
            }

            SettingsData? loaded =
                JsonSerializer.Deserialize<SettingsData>(
                    File.ReadAllText(
                        SettingsPath),
                    JsonOptions);

            return loaded ??
                   CreateDefaults();
        }
        catch
        {
            /*
             * A corrupt preferences file should never stop the editor
             * launching. Fall back to safe defaults.
             */
            return CreateDefaults();
        }
    }

    private static SettingsData CreateDefaults()
    {
        return
            new SettingsData
            {
                /*
                 * ByteEngine is 3D-first by default.
                 * Users can opt into the 2D editor from Preferences.
                 */
                Enable2DEditor =
                    false,

                ShowFpsCounter =
                    false,

                LayoutVersion =
                    0,

                ShowDebugFootIk =
                    true,

                ShowDebugBlueprintVisibility =
                    true,

                ShowDebugTpsJitter =
                    true,

                ShowDebugWeaponRaycast =
                    true
            };
    }

    private static float NormalizeAssetIconScale(float value) =>
        float.IsFinite(value) && value > 0.0f
            ? Math.Clamp(value, 0.50f, 1.65f)
            : 1.0f;

    private static void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(
                DirectoryPath
            );

            string temporary =
                SettingsPath +
                ".tmp";

            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    Settings,
                    JsonOptions)
            );

            File.Move(
                temporary,
                SettingsPath,
                true
            );
        }
        catch
        {
            /*
             * Preferences are convenience data. A write failure should not
             * crash the editor or block the user from working.
             */
        }
    }
}
