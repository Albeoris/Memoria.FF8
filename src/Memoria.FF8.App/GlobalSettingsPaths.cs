namespace Memoria.FF8;

/// <summary>Host preferences belong to the user, independently of the active game or snapshot path.</summary>
internal sealed class GlobalSettingsPaths
{
    public GlobalSettingsPaths(string? localApplicationData = null, string? isolatedDirectory = null)
    {
        DirectoryPath = isolatedDirectory is null ? Path.Combine(localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Memoria", "FF8") : Path.GetFullPath(isolatedDirectory);
    }

    public string DirectoryPath { get; }
    public string UiPreferencesPath => Path.Combine(DirectoryPath, "ui-settings.json");
    public string ControlsPreferencesPath => Path.Combine(DirectoryPath, "controls.json");
    public string TripleTriadDeckHistoryPath => Path.Combine(DirectoryPath, "triple-triad-decks.json");
    public string SaveBanksPath => Path.Combine(DirectoryPath, "save-banks.json");

    public void ImportLegacyFiles(string savePath)
    {
        string legacyDirectory = Path.GetDirectoryName(Path.GetFullPath(savePath))!;
        foreach (string destination in new[] { UiPreferencesPath, ControlsPreferencesPath })
        {
            string source = Path.Combine(legacyDirectory, Path.GetFileName(destination));
            if (!File.Exists(destination) && File.Exists(source))
            {
                Directory.CreateDirectory(DirectoryPath);
                File.Copy(source, destination, overwrite: false);
            }
        }
    }
}
