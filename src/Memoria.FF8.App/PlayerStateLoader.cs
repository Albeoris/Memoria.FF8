using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Reads player checkpoints with lazy access to the selected resources for historical initialization.</summary>
internal static class PlayerStateLoader
{
    internal static LaunchOptions Parse(string[] args)
    {
        return LaunchOptions.Parse(args, (path, gameDirectory) => Load(path, gameDirectory));
    }

    internal static GameState Load(string path, string? gameDirectory = null, GameAssets? sharedAssets = null, ResourceService? resources = null)
    {
        using (FileStream source = File.OpenRead(path))
        {
            if (GameplaySaveCodec.HasSignature(source))
            {
                return GameplaySaveSessionLoader.Restore(GameplaySaveCodec.Read(source), gameDirectory ?? sharedAssets?.GameDirectory ?? LaunchOptions.ConfiguredGameDirectory, sharedAssets, resources);
            }
        }
        GameAssets? ownedAssets = null;
        try
        {
            return GameSession.Load(path, () =>
            {
                GameAssets assets = sharedAssets ?? (ownedAssets ??= new GameAssets(gameDirectory ?? LaunchOptions.ConfiguredGameDirectory, resources: resources));
                return assets.ReadNewGameLimitProgress();
            });
        }
        finally
        {
            ownedAssets?.Dispose();
        }
    }
}
