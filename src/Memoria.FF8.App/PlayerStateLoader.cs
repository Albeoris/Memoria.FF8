using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Reads player checkpoints with lazy access to the selected resources for historical initialization.</summary>
internal static partial class PlayerStateLoader
{
    internal static LaunchOptions Parse(string[] args)
    {
        return LaunchOptions.Parse(args, loadConfiguredState: LoadConfigured);
    }

    internal static GameState Load(string path, string? gameDirectory = null, GameAssets? sharedAssets = null, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null, Func<GameplayTuningPolicy>? readGameplayPolicy = null, Func<NativeCharacterVitals, NativeCharacterVitals>? prepareCharacterDefinitions = null)
    {
        GameplaySaveComponentDocument? document = ReadGameplayDocument(path);
        if (document is not null)
        {
            string directory = gameDirectory ?? sharedAssets?.GameDirectory ?? LaunchOptions.ConfiguredGameDirectory;
            if (sharedAssets is null && resources is null && modelOverrides is null && readGameplayPolicy is null && prepareCharacterDefinitions is null)
            {
                using GameResourceHost selected = OpenSelectedHost(new() { GameDirectory = directory });
                return GameplaySaveSessionLoader.Restore(document, directory, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected));
            }
            return GameplaySaveSessionLoader.Restore(document, directory, sharedAssets, resources, modelOverrides, readGameplayPolicy, prepareCharacterDefinitions);
        }
        GameState legacy = LoadLegacy(path, gameDirectory, sharedAssets, resources);
        return ProjectLegacyPlayerState(legacy, gameDirectory ?? sharedAssets?.GameDirectory ?? LaunchOptions.ConfiguredGameDirectory, sharedAssets, resources, modelOverrides, readGameplayPolicy, prepareCharacterDefinitions);
    }

    private static GameState LoadConfigured(string path, LaunchOptions options)
    {
        GameplaySaveComponentDocument? document = ReadGameplayDocument(path);
        GameResourceHost? selected = null;
        try
        {
            if (document is not null)
            {
                selected = OpenSelectedHost(options);
                return GameplaySaveSessionLoader.Restore(document, options.GameDirectory, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected));
            }
            GameState legacy = LoadLegacy(path, options.GameDirectory, openAssets: () =>
            {
                selected ??= OpenSelectedHost(options);
                return new(options.GameDirectory, resources: selected.Resources, modelOverrides: selected);
            });
            if (!RequiresLegacyGameplayProjection(legacy))
            {
                return legacy;
            }
            selected ??= OpenSelectedHost(options);
            return ProjectLegacyPlayerState(legacy, options.GameDirectory, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected));
        }
        finally
        {
            selected?.Dispose();
        }
    }

    private static GameResourceHost OpenSelectedHost(LaunchOptions options)
    {
        return new(options.GameDirectory, watch: false, modsDirectory: options.ModsDirectory, resourceLanguage: options.ResourceLanguage, modelSourceDirectory: options.ModelSourceDirectory);
    }

    private static GameplaySaveComponentDocument? ReadGameplayDocument(string path)
    {
        using FileStream source = File.OpenRead(path);
        if (Memoria.FF7.Runtime.CombinedSessionCodec.HasSignature(source))
        {
            return CrossoverSaveAdapter.ReadFf8(Memoria.FF7.Runtime.CombinedSessionCodec.Read(source));
        }
        return GameplaySaveCodec.HasSignature(source) ? GameplaySaveCodec.Read(source) : null;
    }

    private static GameState LoadLegacy(string path, string? gameDirectory, GameAssets? sharedAssets = null, ResourceService? resources = null, Func<GameAssets>? openAssets = null)
    {
        GameAssets? ownedAssets = null;
        try
        {
            return GameSession.Load(path, () =>
            {
                GameAssets assets = sharedAssets ?? (ownedAssets ??= openAssets?.Invoke() ?? new GameAssets(gameDirectory ?? LaunchOptions.ConfiguredGameDirectory, resources: resources));
                return assets.ReadNewGameLimitProgress();
            });
        }
        finally
        {
            ownedAssets?.Dispose();
        }
    }
}
