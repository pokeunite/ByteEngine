using ByteEngine.Core.Assets;

namespace ByteEngine.Editor;

internal static class BundledFontInstaller
{
    internal static readonly string[] FontFiles =
    {
        "TypeLightSans.ttf",
        "OSerif-Regular.ttf",
        "PublicPixel.ttf"
    };

    public static int Install(EditorProjectContext project, string? sourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        string source = sourceDirectory ??
            Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts");
        string assetRoot = project.ResolveProjectPath(project.Project.AssetDirectory);
        string destination = Path.Combine(assetRoot, "Fonts", "ByteEngine");
        foreach (string name in FontFiles.Append("CC0-LICENSE.txt"))
        {
            if (!File.Exists(Path.Combine(source, name)))
                throw new FileNotFoundException($"Bundled font resource is missing: {name}", Path.Combine(source, name));
        }

        Directory.CreateDirectory(destination);
        int added = 0;
        foreach (string name in FontFiles.Append("CC0-LICENSE.txt"))
        {
            string target = Path.Combine(destination, name);
            if (File.Exists(target)) continue;
            File.Copy(Path.Combine(source, name), target, overwrite: false);
            if (name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) added++;
        }
        project.AssetDatabase.Scan();
        return added;
    }
}
