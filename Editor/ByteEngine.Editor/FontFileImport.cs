using System.Text.RegularExpressions;
using ByteEngine.Core.Assets;

namespace ByteEngine.Editor;

internal static class FontFileImport
{
    public static IReadOnlyList<AssetRecord> Import(
        EditorProjectContext project, EditorLog log, IEnumerable<string> paths, string destinationDirectory)
    {
        var records = new List<AssetRecord>();
        var importer = new ExternalAssetImporter(project, log);
        foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".ttf" or ".otf")
            {
                records.AddRange(importer.Import(new[] { path }, destinationDirectory));
                continue;
            }

            if (extension != ".fnt")
            {
                log.Warning($"Unsupported font file '{Path.GetFileName(path)}'. Choose TTF, OTF or text BMFont FNT.");
                continue;
            }

            try
            {
                string source = File.ReadAllText(path);
                Match page = Regex.Match(source, @"(?m)^page\s+id=0\s+file=""([^""]+)""");
                if (!page.Success || Regex.IsMatch(source, @"(?m)^page\s+id=[1-9]"))
                    throw new InvalidDataException("Only single-page text BMFont files are supported.");
                string imageName = page.Groups[1].Value;
                if (imageName != Path.GetFileName(imageName) ||
                    Path.GetExtension(imageName).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".tga" or ".bmp"))
                    throw new InvalidDataException("The BMFont atlas must be an image beside the .fnt file.");
                string imagePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, imageName);
                if (!File.Exists(imagePath))
                    throw new FileNotFoundException("The BMFont atlas image is missing.", imagePath);

                string stem = Path.GetFileNameWithoutExtension(path);
                string folder = Path.Combine(destinationDirectory, stem + "-BitmapFont");
                for (int suffix = 2; Directory.Exists(folder); suffix++)
                    folder = Path.Combine(destinationDirectory, stem + "-BitmapFont-" + suffix);
                records.AddRange(importer.Import(new[] { path, imagePath }, folder));
            }
            catch (Exception exception)
            {
                log.Error($"Could not import bitmap font '{Path.GetFileName(path)}': {exception.Message}");
            }
        }
        return records;
    }
}
