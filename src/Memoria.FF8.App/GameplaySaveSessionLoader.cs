using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

/// <summary>Loads an explicitly selected semantic gameplay document into an isolated normal-load process.</summary>
internal static class GameplaySaveSessionLoader
{
    internal static GameState Load(string path, string gameDirectory, GameAssets? sharedAssets = null, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null, Func<GameplayTuningPolicy>? readGameplayPolicy = null, Func<NativeCharacterVitals, NativeCharacterVitals>? prepareCharacterDefinitions = null, Action<string>? reportWarning = null)
    {
        using FileStream source = File.OpenRead(path);
        return Restore(GameplaySaveCodec.Read(source), gameDirectory, sharedAssets, resources, modelOverrides, readGameplayPolicy, prepareCharacterDefinitions, reportWarning: reportWarning);
    }

    internal static GameState Restore(GameplaySaveComponentDocument document, string gameDirectory, GameAssets? sharedAssets = null, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null, Func<GameplayTuningPolicy>? readGameplayPolicy = null, Func<NativeCharacterVitals, NativeCharacterVitals>? prepareCharacterDefinitions = null, NativeBattleEffectMemory? executionEnvironment = null, NativeMusicPlaybackState? musicProcessHistory = null, Action<string>? reportWarning = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Validate();
        if (document.Progression is { RuntimeOwnershipVersion: null } legacy)
        {
            const string warning = "Older save: missing presentation values use hidden bars, no camera shake and a hidden salary notification; missing music playback uses the saved scene selection and default settings. Existing saved values and gameplay progress are preserved.";
            (reportWarning ?? (message => Console.Error.WriteLine("Warning: " + message)))(warning);
            document = document with { Progression = legacy.RestoreMissingRuntimeData() };
        }
        if (document.Progression?.FieldContinuation is not null)
        {
            document = document with { Progression = document.Progression.NormalizeFieldContinuation() };
        }
        GameplaySaveSceneFacts facts = RequireSupportedReconstruction(document);
        using GameAssets? ownedAssets = sharedAssets is null ? new GameAssets(gameDirectory, resources: resources, modelOverrides: modelOverrides) : null;
        GameAssets assets = sharedAssets ?? ownedAssets!;
        ResourceService selectedResources = resources ?? assets.Resources;
        if (modelOverrides is GameResourceHost host && !host.SetGameplayDifficulty(document.Progression!.Rules.Preferences.Difficulty))
        {
            throw new InvalidOperationException(GameResourceHost.GameplayDifficultyProfilePending);
        }
        GameplayTuningPolicy? policy = readGameplayPolicy?.Invoke() ?? (modelOverrides as GameResourceHost)?.GameplayPolicy;
        Func<GameplayTuningPolicy>? selectedPolicy = policy is null ? null : () => policy;
        GameSession initializer;
        if (executionEnvironment is null)
        {
            initializer = GameWorld.CreateColdProcess(assets);
        }
        else
        {
            NativeBattleRetainedMemory cold = assets.ReadColdProcessNativeBattleMemory();
            initializer = GameSession.CreateColdProcess(cold with { EffectMemory = cold.EffectMemory! with { ExecutionStack = executionEnvironment.ExecutionStack, HeroCrtStack = executionEnvironment.HeroCrtStack } });
        }
        initializer.StartNewGame();
        if (musicProcessHistory is not null)
        {
            NativeGlobalSoundProcess sound = initializer.GlobalSoundProcess ?? throw new InvalidDataException("Same-process reconstruction requires its initialized sound owner.");
            NativeMusicPlaybackState playback = sound.MusicPlayback ?? throw new InvalidDataException("Same-process reconstruction requires initialized music playback.");
            initializer.SetGlobalSoundProcess(sound with { MusicPlayback = playback with { BattleFirstLoadB8B7D4 = musicProcessHistory.BattleFirstLoadB8B7D4 } });
        }
        GameplaySaveRules rules = document.Progression!.Rules;
        initializer.Restore(initializer.State with { GameplayPreferences = rules.Preferences, ConfiguredGameplayTuning = rules.ConfiguredTuning });
        using (MovieClock initialClock = new(initializer, gameDirectory))
        using (GameWorld initialWorld = new(initializer, gameDirectory, initialClock, selectedResources, sharedAssets: assets, modelOverrides: modelOverrides, readGameplayPolicy: selectedPolicy))
        {
            // Constructor initialization selects current definitions; no field VM is loaded or advanced.
        }
        GameState initialized = initializer.State;
        if (prepareCharacterDefinitions is not null)
        {
            initialized = initialized with { CharacterVitals = prepareCharacterDefinitions(initialized.CharacterVitals ?? throw new InvalidOperationException("Gameplay restoration requires initialized character definitions.")) };
        }
        GameState restored = GameplaySaveStateRestoration.Restore(document, initialized);
        if (restored.SavedWorld?.DormantScriptedReturn is { } scriptedEndpoint)
        {
            string reset = scriptedEndpoint.EntityType is 65 or 66 ? "body heading and camera velocity" : "camera velocity";
            (reportWarning ?? (message => Console.Error.WriteLine("Warning: " + message)))($"Saved scripted world endpoint restored. A subsequent scripted departure will use cold-process {reset} for the missing presentation values.");
        }
        if (document.Resume.Kind == GameplaySaveResumeKind.Field)
        {
            restored = restored with { FieldLoadContext = document.Progression.FieldContinuation is null ? NativeFieldLoadContext.FromGameplaySave(restored.FieldId) : NativeFieldLoadContext.FromGameplayContinuation(restored.FieldId) };
        }
        else
        {
            GameplaySaveWorldResumeMetadata metadata = GameplaySaveWorldMetadataResolver.Resolve(document, gameDirectory, selectedResources, modelOverrides);
            restored = GameplaySaveWorldResume.Complete(document, restored, metadata);
        }
        GameSession candidateSession = new(restored);
        using MovieClock clock = new(candidateSession, gameDirectory);
        using GameWorld candidate = new(candidateSession, gameDirectory, clock, selectedResources, sharedAssets: assets, modelOverrides: modelOverrides, readGameplayPolicy: selectedPolicy, gameplaySaveFacts: facts);
        candidate.EnsureField();
        if (document.Resume.Kind == GameplaySaveResumeKind.Field)
        {
            if (document.Progression.FieldContinuation is { } continuation)
            {
                NativeFieldFadeState durableFade = restored.FieldFade ?? throw new InvalidDataException("Gameplay continuation requires its saved fade words.");
                NativeFieldFadeState allocatedFade = candidateSession.FieldFade ?? throw new InvalidDataException("Gameplay continuation requires its allocated fade owner.");
                candidateSession.SetFieldFade(allocatedFade with { Saved = durableFade.Saved, MapFadeDisabled = durableFade.MapFadeDisabled });
                if (restored.WorldControls is { } controls)
                {
                    // Resource allocation performs normal field-entry resets; a continuation resumes its saved controls.
                    candidateSession.SetWorldControls(controls);
                }
                candidate.Events.RestoreGameplaySaveFieldContinuation(continuation, facts);
            }
            else
            {
                candidate.Events.InitializeGameplaySaveFieldParty(document.Progression.FieldLocation);
            }
        }
        else if (document.Progression.WorldUi is { } worldUi)
        {
            candidate.RestoreGameplayWorldUi(worldUi);
        }
        GameState snapshot = candidateSession.PrepareSnapshot();
        _ = new GameSession(snapshot);
        return snapshot with { AudioPresentation = document.AudioPresentation };
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
        if (document.Progression.FieldContinuation is { } continuation && (document.Resume.Kind != GameplaySaveResumeKind.Field || continuation.Scripts.FieldId != document.Resume.FieldId))
        {
            throw new InvalidDataException("Gameplay field continuation does not match its resume descriptor.");
        }
        document.Progression.FieldContinuation?.ValidateResume(document.Resume, facts);
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
