using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class OriginalPlayerSaveLoader
{
    public static GameState Load(string path, string gameDirectory, GameAssets? sharedAssets = null, ResourceService? resources = null, string? mediaToolsDirectory = null, Func<string, string>? resolveGameFile = null)
    {
        using GameAssets? ownedAssets = sharedAssets is null ? new GameAssets(gameDirectory, resources: resources) : null;
        GameAssets assets = sharedAssets ?? ownedAssets!;
        GameSession imported = new(NativePcSaveConverter.Import(File.ReadAllBytes(path), assets));
        using MovieClock clock = new(imported, gameDirectory, mediaToolsDirectory, resolveGameFile);
        using GameWorld candidate = new(imported, gameDirectory, clock, resources, resolveGameFile: resolveGameFile, sharedAssets: assets);
        candidate.EnsureField();
        if (imported.State.Scene == SceneKind.Field)
        {
            imported.SetEventState(candidate.Events.Capture());
        }
        return imported.State;
    }
}
