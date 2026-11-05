using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Loads an explicitly selected semantic gameplay document into an isolated normal-load process.</summary>
internal static class GameplaySaveSessionLoader
{
    internal static GameState Load(string path, string gameDirectory, GameAssets? sharedAssets = null, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null, Func<GameplayTuningPolicy>? readGameplayPolicy = null)
    {
        using FileStream source = File.OpenRead(path);
        return Restore(GameplaySaveCodec.Read(source), gameDirectory, sharedAssets, resources, modelOverrides, readGameplayPolicy);
    }

    internal static GameState Restore(GameplaySaveComponentDocument document, string gameDirectory, GameAssets? sharedAssets = null, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null, Func<GameplayTuningPolicy>? readGameplayPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        GameplaySaveSceneFacts facts = RequireSupportedReconstruction(document);
        using GameAssets? ownedAssets = sharedAssets is null ? new GameAssets(gameDirectory, resources: resources, modelOverrides: modelOverrides) : null;
        GameAssets assets = sharedAssets ?? ownedAssets!;
        ResourceService selectedResources = resources ?? assets.Resources;
        GameplayTuningPolicy policy = readGameplayPolicy?.Invoke() ?? GameplayTuningPolicy.Empty;
        GameSession initializer = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
        initializer.StartNewGame();
        GameplaySaveRules rules = document.Progression!.Rules;
        initializer.Restore(initializer.State with { GameplayPreferences = rules.Preferences, ConfiguredGameplayTuning = rules.ConfiguredTuning });
        using (MovieClock initialClock = new(initializer, gameDirectory))
        using (GameWorld initialWorld = new(initializer, gameDirectory, initialClock, selectedResources, sharedAssets: assets, modelOverrides: modelOverrides, readGameplayPolicy: () => policy))
        {
            // Constructor initialization selects current definitions; no field VM is loaded or advanced.
        }
        GameState restored = GameplaySaveStateRestoration.Restore(document, initializer.State);
        if (document.Resume.Kind == GameplaySaveResumeKind.Field)
        {
            restored = restored with { FieldLoadContext = NativeFieldLoadContext.FromGameplaySave(restored.FieldId) };
        }
        else
        {
            GameplaySaveWorldResumeMetadata metadata = GameplaySaveWorldMetadataResolver.Resolve(document, gameDirectory, selectedResources, modelOverrides);
            restored = GameplaySaveWorldResume.Complete(document, restored, metadata);
        }
        GameSession candidateSession = new(restored);
        using MovieClock clock = new(candidateSession, gameDirectory);
        using GameWorld candidate = new(candidateSession, gameDirectory, clock, selectedResources, sharedAssets: assets, modelOverrides: modelOverrides, readGameplayPolicy: () => policy, gameplaySaveFacts: facts);
        candidate.EnsureField();
        if (document.Resume.Kind == GameplaySaveResumeKind.Field)
        {
            candidate.Events.InitializeGameplaySaveFieldParty(document.Progression.FieldLocation);
        }
        GameState snapshot = candidateSession.PrepareSnapshot();
        _ = new GameSession(snapshot);
        return snapshot;
    }

    private static GameplaySaveSceneFacts RequireSupportedReconstruction(GameplaySaveComponentDocument document)
    {
        if (document.Version != GameplaySaveComponentDocument.CurrentVersion || document.Progression?.SceneFacts is not { PersistentBattleMode: not null, ActiveBattleMode: not null } facts)
        {
            throw new NotSupportedException("Gameplay loading requires complete current components and current scene battle-mode words.");
        }
        if (facts.FieldId != document.Resume.FieldId)
        {
            throw new InvalidDataException("Gameplay scene facts must identify the current resume field.");
        }
        if (document.Resume.Kind == GameplaySaveResumeKind.Field)
        {
            if (facts.Origin != GameplaySaveSceneFactsOrigin.Field || facts.FieldParty[0] == 255)
            {
                throw new NotSupportedException("Gameplay field reconstruction requires current field facts and a leading field-party member.");
            }
        }
        return facts;
    }
}
