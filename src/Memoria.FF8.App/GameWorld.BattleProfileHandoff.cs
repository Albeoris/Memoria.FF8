using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private sealed record PreparedBattleProfileHandoff(GameState ExpectedState, long CatalogGeneration, GameState State, GameplaySaveComponentDocument Document);

    private PreparedBattleProfileHandoff PrepareBattleProfileHandoff(GameWorld completedSource, int callbacks, byte nativeResult)
    {
        ArgumentNullException.ThrowIfNull(completedSource);
        if (ReferenceEquals(completedSource, this) || !completedSource.legacyGameplayProjection || completedSource.session.State.Scene != SceneKind.Field || completedSource.session.State.Battle is not null || completedSource.session.State.PendingBattleEncounter is not null || completedSource.session.State.FieldId != session.State.FieldId)
        {
            throw new InvalidOperationException("Battle profile handoff requires an isolated completed logical field return.");
        }
        GameState expected = session.State;
        long generation = assets.Resources.CatalogGeneration;
        GameplaySaveFieldContinuation continuation = completedSource.Events.CaptureCompletedBattleGameplayContinuation(callbacks, nativeResult);
        GameState source = completedSource.session.PrepareSnapshot();
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.Field, source.FieldId, source.PreviousFieldId, continuation.GetLeaderPosition(), null);
        GameplaySaveComponentDocument document = CaptureGameplayDocument(source, resume);
        document = document with { Progression = document.Progression!.WithFieldContinuation(continuation), AudioPresentation = source.AudioPresentation };
        document.Validate();
        GuardianForceAbilityProfile? profile = source.CharacterVitals?.GuardianForceProfile;
        Func<NativeCharacterVitals, NativeCharacterVitals>? prepareDefinitions;
        if (modelOverrides is GameResourceHost selected)
        {
            prepareDefinitions = initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected);
        }
        else
        {
            prepareDefinitions = profile is null ? null : initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, profile.AppliedLayers);
        }
        GameState restored = GameplaySaveSessionLoader.Restore(document, assets.GameDirectory, assets, resources, modelOverrides, readGameplayPolicy, prepareDefinitions);
        GameSession verificationSession = new(restored);
        using GameWorld verification = new(verificationSession, assets.GameDirectory, resources: resources, sharedAssets: assets, readSceneMods: readSceneMods, modelOverrides: modelOverrides, readGameplayPolicy: readGameplayPolicy, applyEncounterMods: applyEncounterMods);
        verification.EnsureField();
        string image = assets.NativeRuntimeProfile?.ExecutableHash ?? throw new NotSupportedException("Battle profile handoff requires supported selected resources.");
        GameState staged = verificationSession.PrepareSnapshot();
        if (verification.Events.NativeRuntimeProfile?.ExecutableHash != image || staged.NativeBattleMemory?.ColdExecutableSha256 != image || staged.NativeBattleMemory.ModelCpuResources?.Cpu.EffectiveLayout.ExecutableHash != image)
        {
            throw new InvalidDataException("Battle profile handoff did not reconstruct the selected field and battle allocation domains.");
        }
        if (!ReferenceEquals(session.State, expected) || assets.Resources.CatalogGeneration != generation)
        {
            throw new InvalidOperationException("Battle profile handoff source or selected resources changed during preparation.");
        }
        return new(expected, generation, staged, document);
    }

    private void PublishBattleProfileHandoff(PreparedBattleProfileHandoff prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (!ReferenceEquals(session.State, prepared.ExpectedState) || assets.Resources.CatalogGeneration != prepared.CatalogGeneration || prepared.State.EventState is not { } checkpoint || prepared.State.Scene != SceneKind.Field || prepared.State.Battle is not null || prepared.State.PendingBattleEncounter is not null)
        {
            throw new InvalidOperationException("Prepared battle profile handoff no longer matches its live owner or selected resources.");
        }
        session.Restore(prepared.State);
        battle = null;
        worldScene?.Dispose();
        worldScene = null;
        // Force the exact reconstructed allocation into the existing host before its next callback.
        LoadField(prepared.State.FieldId, prepared.State.FieldArrival, checkpoint);
    }
}
