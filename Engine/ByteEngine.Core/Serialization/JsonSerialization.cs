using System.Text.Json;

namespace ByteEngine.Core.Serialization;

internal static class JsonSerialization
{
    public static JsonSerializerOptions Options { get; } =
        new()
        {
            PropertyNamingPolicy =
                JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive =
                true,
            WriteIndented =
                true,
            AllowTrailingCommas =
                true,
            ReadCommentHandling =
                JsonCommentHandling.Skip
        };

    public static void WriteAtomic<T>(
        string filePath,
        T value)
    {
        string fullPath =
            Path.GetFullPath(
                filePath
            );

        string? directory =
            Path.GetDirectoryName(
                fullPath
            );

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory
            );
        }

        string temporaryPath =
            fullPath +
            ".tmp";

        string json =
            JsonSerializer.Serialize(
                value,
                Options
            );

        File.WriteAllText(
            temporaryPath,
            json
        );

        File.Move(
            temporaryPath,
            fullPath,
            true
        );
    }
}
