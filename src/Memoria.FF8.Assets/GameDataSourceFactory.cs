using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Selects storage only at the application composition boundary.</summary>
public static class GameDataSourceFactory
{
    public static IResourceSource Open(string gameDirectory)
    {
        return File.Exists(Path.Combine(gameDirectory, "main.zzz")) ? new Remastered.RemasteredGameDataSource(gameDirectory) : new ClassicGameDataSource(gameDirectory);
    }

    public static IResourceSource Open(string gameDirectory, string? modelSourceDirectory)
    {
        IResourceSource primary = Open(gameDirectory);
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
