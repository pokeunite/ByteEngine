using System.Text.Json;
using System.Text.Json.Serialization;

namespace ByteEngine.Core.Serialization;

internal static class JsonSerialization
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
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
                JsonCommentHandling.Skip,
            IncludeFields = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

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
