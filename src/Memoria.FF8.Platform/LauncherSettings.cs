using System.Text.Json;

namespace Memoria.FF8.Platform;

public sealed record LauncherSettings(string? LastGameDirectory = null, bool AchievementsLocalOnly = false, string? ResourceLanguage = null, string? ModelSourceDirectory = null, bool CloseAfterLaunch = false, string? Ff7GameDirectory = null, string Appearance = "System")
{
    public ModInstallationLocation ModLocation { get; init; }
    public string? CustomModsDirectory { get; init; }
    public string[]? PreviousCustomModsDirectories { get; init; }

    public static string DefaultPath => ApplicationPaths.Current.LauncherSettings;

    public LauncherSettings WithModLocation(ModInstallationLocation location, string? customDirectory)
    {
        if (location == ModInstallationLocation.Custom)
        {
            customDirectory = ApplicationInstallation.NormalizeCustomModsDirectory(customDirectory);
        }
        else
        {
            customDirectory = CustomModsDirectory;
        }
        return this with
        {
            ModLocation = location,
            CustomModsDirectory = customDirectory,
            PreviousCustomModsDirectories = (CustomModsDirectory is null ? PreviousCustomModsDirectories ?? [] : (PreviousCustomModsDirectories ?? []).Prepend(CustomModsDirectory))
                .Where(path => !ApplicationInstallation.ModPathComparer.Equals(path, customDirectory))
                .Distinct(ApplicationInstallation.ModPathComparer).ToArray()
        };
    }

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
