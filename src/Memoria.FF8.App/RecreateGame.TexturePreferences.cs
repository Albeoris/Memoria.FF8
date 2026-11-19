using System.Text.Json;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private sealed record TextureToolPreferences(int Scope, bool Resolved, string? ExportDirectory, string? ImportDirectory, string? Destination, string[]? ImportFiles, Dictionary<int, bool>? SourceModes = null);
    private string TextureToolPreferencesPath => Path.Combine(globalSettings.DirectoryPath, "texture-tools.json");

    private TextureToolPreferences ReadTextureToolPreferences()
    {
        try
        {
            string path = TextureToolPreferencesPath;
            if (File.Exists(path) && new FileInfo(path).Length < 4 * 1024 * 1024)
            {
                TextureToolPreferences? saved = JsonSerializer.Deserialize<TextureToolPreferences>(File.ReadAllBytes(path));
                if (saved is not null)
                {
                    return saved with { Scope = Math.Clamp(saved.Scope, 0, 2) };
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
        return new(0, true, null, null, null, null);
    }

    private void SaveTextureToolPreferences()
    {
        if (textureBrowser is not { } browser)
        {
            return;
        }
        string path = TextureToolPreferencesPath;
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            TextureToolPreferences preferences = new(browser.Scope, browser.Resolved, textureExportDirectory, textureImportDirectory, textureDestination, textureImportFiles?.ToArray(), browser.SourceModes.ToDictionary(pair => pair.Key, pair => pair.Value));
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(preferences));
            File.Move(temporary, path, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Optional authoring preferences never block an export or import.
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
