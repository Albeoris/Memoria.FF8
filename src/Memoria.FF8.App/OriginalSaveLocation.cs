using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal static class OriginalSaveLocation
{
    public static IReadOnlyList<string> FindCandidates(string? documentsDirectory = null, string? gameDirectory = null)
    {
        string documents = documentsDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            return [];
        }
        try
        {
            GameDataEdition? edition = gameDirectory is null ? GameDataEdition.Classic : GamePlatformContextResolver.Resolve(gameDirectory).ResourceEdition;
            if (edition is null)
            {
                return [];
            }
            if (edition == GameDataEdition.Remastered)
            {
                string remasteredRoot = Path.Combine(documents, "My Games", "FINAL FANTASY VIII Remastered");
                List<string> candidates = [];
                foreach (string provider in new[] { "Steam", "GOG" })
                {
                    string providerRoot = Path.Combine(remasteredRoot, provider);
                    if (!Directory.Exists(providerRoot))
                    {
                        continue;
                    }
                    foreach (string profile in Directory.EnumerateDirectories(providerRoot).Where(path => IsNumericProfile(Path.GetFileName(path))))
                    {
                        string saves = Path.Combine(profile, "game_data", "user", "saves");
                        if (Directory.Exists(saves))
                        {
                            candidates.Add(Path.GetFullPath(saves));
                        }
                    }
                }
                return candidates.Order(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            string root = Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII Steam");
            return Directory.Exists(root) ? Directory.EnumerateDirectories(root, "user_*").Select(Path.GetFullPath).Order(StringComparer.OrdinalIgnoreCase).ToArray() : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    internal static bool IsRemasteredDirectory(string path)
    {
        DirectoryInfo saves = new(Path.GetFullPath(path));
        DirectoryInfo? user = saves.Parent;
        DirectoryInfo? data = user?.Parent;
        DirectoryInfo? profile = data?.Parent;
        DirectoryInfo? provider = profile?.Parent;
        return saves.Name.Equals("saves", StringComparison.OrdinalIgnoreCase) && user?.Name.Equals("user", StringComparison.OrdinalIgnoreCase) == true && data?.Name.Equals("game_data", StringComparison.OrdinalIgnoreCase) == true && profile is not null && IsNumericProfile(profile.Name) && provider is not null && (provider.Name.Equals("Steam", StringComparison.OrdinalIgnoreCase) || provider.Name.Equals("GOG", StringComparison.OrdinalIgnoreCase)) && provider.Parent?.Name.Equals("FINAL FANTASY VIII Remastered", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsNumericProfile(string name)
    {
        return name.Length != 0 && name.All(value => value is >= '0' and <= '9');
    }

    public static string ValidateDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("The original FF8 save folder does not exist.");
        }
        return fullPath;
    }
}
