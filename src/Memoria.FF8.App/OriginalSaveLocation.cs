namespace Memoria.FF8;

internal static class OriginalSaveLocation
{
    public static IReadOnlyList<string> FindCandidates(string? documentsDirectory = null)
    {
        string documents = documentsDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
        {
            return [];
        }
        string root = Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII Steam");
        try
        {
            return Directory.Exists(root) ? Directory.EnumerateDirectories(root, "user_*").Select(Path.GetFullPath).Order(StringComparer.OrdinalIgnoreCase).ToArray() : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
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
