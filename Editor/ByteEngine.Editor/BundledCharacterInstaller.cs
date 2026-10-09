namespace ByteEngine.Editor;

/// <summary>Copies the engine's editable character starting points into new projects.</summary>
internal static class BundledCharacterInstaller
{
    internal static readonly string[] ModelFiles =
    {
        "Mannequin.glb",
        "Mannequin-FPS-Arms.glb"
    };

    internal static readonly string[] SupportFiles =
    {
        "README.txt", "ANIMATION-CREDITS.txt", "LICENSE-CC-BY-4.0.txt",
        "LICENSE-CC0-1.0.txt", "ROKOKO-SOURCE-NOTICE.txt", "animations.json"
    };

    public static int Install(string projectRoot, string assetDirectory = "Assets", string? sourceDirectory = null)
    {
        string source = sourceDirectory ??
            Path.Combine(AppContext.BaseDirectory, "Resources", "Characters", "ByteEngine");
        string[] files = ModelFiles.Concat(SupportFiles).ToArray();
        // Validate the complete bundle before installing any of it.
        foreach (string name in files)
        {
            string file = Path.Combine(source, name);
            if (!File.Exists(file))
                throw new FileNotFoundException($"Bundled character resource is missing: {name}", file);
        }

        string destination = Path.Combine(projectRoot, assetDirectory, "Characters", "ByteEngine");
        Directory.CreateDirectory(destination);
        int added = 0;
        foreach (string name in files)
        {
            string target = Path.Combine(destination, name);
            if (File.Exists(target)) continue;
            File.Copy(Path.Combine(source, name), target, overwrite: false);
            if (name.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) added++;
        }
        return added;
    }
}
