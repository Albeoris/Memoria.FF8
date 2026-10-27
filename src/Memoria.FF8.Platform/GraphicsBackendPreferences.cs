using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Platform;

/// <summary>Shared backend preference in the existing UI settings document. Other settings are preserved.</summary>
public static class GraphicsBackendPreferences
{
    public static string DefaultPath => Path.Combine(ApplicationPaths.Current.Configuration, "ui-settings.json");

    public static GraphicsBackend Read(string path)
    {
        if (!File.Exists(path))
        {
            return GraphicsBackend.Auto;
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("Graphics", out JsonElement graphics) || !graphics.TryGetProperty("Backend", out JsonElement backend))
        {
            return GraphicsBackend.Auto;
        }
        return backend.Deserialize<GraphicsBackend>();
    }

    public static void Write(string path, GraphicsBackend backend)
    {
        if (!Enum.IsDefined(backend))
        {
            throw new ArgumentOutOfRangeException(nameof(backend));
        }
        JsonObject root = File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new InvalidDataException("UI preferences must be an object.") : new JsonObject();
        JsonObject graphics = root["Graphics"] as JsonObject ?? new JsonObject();
        graphics["Backend"] = backend.ToString();
        root["Graphics"] = graphics;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
