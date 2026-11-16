using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Platform;

public static class EngineBuildMetadata
{
    public const string FileName = "engine-version.json";

    public static string CompatibilityVersion { get; } = typeof(EngineBuildMetadata).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "EngineCompatibilityVersion").Value!;

    public static string? ReadInstalledCompatibilityVersion(string distributionRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(distributionRoot));
        string data = root.EndsWith(".app", StringComparison.Ordinal) ? Path.Combine(root, "Contents", "Resources", "Data") : Path.Combine(root, "Data");
        string path = Path.Combine(data, FileName);
        if (!Directory.Exists(data))
        {
            path = Path.Combine(root, FileName);
        }
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length > 4096)
            {
                return null;
            }
            using JsonDocument document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("engineCompatibilityVersion", out JsonElement version) && version.ValueKind == JsonValueKind.String ? version.GetString() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
