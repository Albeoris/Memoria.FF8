using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Selects storage only at the application composition boundary.</summary>
public static class GameDataSourceFactory
{
    public static IResourceSource OpenTextureAuthoring(string gameDirectory, string? modelSourceDirectory = null, string? resourceLanguage = null)
    {
        IResourceSource Source(string directory, string? language)
        {
            if (File.Exists(Path.Combine(directory, "main.zzz")))
            {
                var source = new Remastered.RemasteredGameDataSource(directory, Memoria.FF8.Platform.GameResourceLanguages.Normalize(language) ?? "en");
                source.ConfigureTextureAuthoringCache();
                return source;
            }
            ClassicGameDataSource classic = new(directory);
            classic.ConfigureTextureAuthoringCache();
            return classic;
        }
        IResourceSource primary = Source(gameDirectory, resourceLanguage);
        if (string.IsNullOrWhiteSpace(modelSourceDirectory))
        {
            return primary;
        }
        try
        {
            return new SelectedModelResourceSource(primary, Source(modelSourceDirectory, null));
        }
        catch
        {
            primary.Dispose();
            throw;
        }
    }

    public static IResourceSource Open(string gameDirectory)
    {
        return File.Exists(Path.Combine(gameDirectory, "main.zzz")) ? new Remastered.RemasteredGameDataSource(gameDirectory) : new ClassicGameDataSource(gameDirectory);
    }

    public static IResourceSource Open(string gameDirectory, string? modelSourceDirectory, string? resourceLanguage = null)
    {
        string? language = Memoria.FF8.Platform.GameResourceLanguages.Normalize(resourceLanguage);
        IResourceSource primary = File.Exists(Path.Combine(gameDirectory, "main.zzz")) ? new Remastered.RemasteredGameDataSource(gameDirectory, language ?? "en") : new ClassicGameDataSource(gameDirectory);
        if (string.IsNullOrWhiteSpace(modelSourceDirectory))
        {
            return primary;
        }
        try
        {
            return new SelectedModelResourceSource(primary, Open(modelSourceDirectory));
        }
        catch
        {
            primary.Dispose();
            throw;
        }
    }
}
