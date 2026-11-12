using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static partial class PlayerStateLoader
{
    private static bool RequiresLegacyGameplayProjection(GameState state)
    {
        return state.Battle is null && state.PendingBattleEncounter is null && (state.Scene == SceneKind.Field && state.EventState is not null || state.Scene == SceneKind.WorldMap && state.World is not null);
    }

    private static GameState ProjectLegacyPlayerState(GameState saved, string directory, GameAssets? sharedAssets = null, ResourceService? resources = null, IModelOverrideSource? modelOverrides = null, Func<GameplayTuningPolicy>? readGameplayPolicy = null, Func<NativeCharacterVitals, NativeCharacterVitals>? prepareCharacterDefinitions = null)
    {
        // The standalone world snapshot contains the live transfer and its
        // retained allocation. A compact save cannot represent that boundary.
        if (!RequiresLegacyGameplayProjection(saved) || saved.World is { } world &&
            (world.ChocoboDismount is not null || world.NativeFootRuntime?.Rule == NativeFootWorldRuntimeState.ChocoboDismountRule))
        {
            return saved;
        }
        if (sharedAssets is null && resources is null && modelOverrides is null && readGameplayPolicy is null && prepareCharacterDefinitions is null)
        {
            using GameResourceHost selected = OpenSelectedHost(new() { GameDirectory = directory });
            return ProjectLegacyPlayerState(saved, directory, resources: selected.Resources, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy, prepareCharacterDefinitions: initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected));
        }
        using GameAssets? ownedAssets = sharedAssets is null ? new GameAssets(directory, resources: resources, modelOverrides: modelOverrides) : null;
        GameAssets assets = sharedAssets ?? ownedAssets!;
        System.Text.Json.JsonElement? checkpoint = saved.Scene == SceneKind.WorldMap ? saved.DepartingFieldState ?? saved.EventState : saved.EventState;
        if (checkpoint is { } field && field.TryGetProperty("Scene", out var scene) && scene.TryGetProperty("NativeFieldInitialization", out var initialization) && initialization.ValueKind == System.Text.Json.JsonValueKind.Object && initialization.TryGetProperty("ExecutableHash", out var image) && image.GetString() == assets.NativeRuntimeProfile?.ExecutableHash)
        {
            return saved;
        }
        GameSession source = new(saved);
        using GameWorld projection = new(source, directory, resources: resources, sharedAssets: assets, modelOverrides: modelOverrides, readGameplayPolicy: readGameplayPolicy, legacyGameplayProjection: true);
        projection.EnsureField();
        GameplaySaveComponentDocument document = projection.CaptureLegacyGameplayDocument();
        return GameplaySaveSessionLoader.Restore(document, directory, assets, resources, modelOverrides, readGameplayPolicy, prepareCharacterDefinitions);
    }
}
