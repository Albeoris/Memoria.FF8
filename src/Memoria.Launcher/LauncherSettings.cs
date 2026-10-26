using System.Text.Json;

namespace Memoria.Launcher;

internal sealed record LauncherSettings(string? LastGameDirectory = null, bool AchievementsLocalOnly = false)
{
    public static string DefaultPath => Memoria.FF8.Platform.ApplicationPaths.Current.LauncherSettings;

    public static LauncherSettings Load(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path)) ?? new LauncherSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new LauncherSettings();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this));
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
