using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Installed graphics, audio and text resources required by the battle presentation process.</summary>
public sealed class BattlePresentationResources
{
    public BattlePresentationResources(BattleDefinition source, Func<NativeBattleEffectBinding, NativeBattleCinematicModelOperands>? readCinematicModelOperands = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ReadCinematicModelOperands = readCinematicModelOperands;
        SourceGrouping = new(source);
        GuardianFormResources = source.GuardianFormResources;
        NativeDefensiveResources = source.NativeDefensiveResources;
        NativeActionCameraRules = source.NativeActionCameraRules;
        GuardianBoost = source.ActionDefinitions.Where(action => action.Ability == BattleAbility.GuardianForce &&
                action.Id is >= 0 and < 16 && action.NativeGuardianBoostAvailable.HasValue &&
                action.NativeAction is { KernelSection: 2, RawRecord.Length: 132 } native && native.KernelIndex == action.Id)
            .ToImmutableDictionary(action => checked((ushort)(action.Id + 64)), action => (action.NativeAction!.RawRecord[128], action.NativeAction.RawRecord[129], action.NativeGuardianBoostAvailable!.Value));
        EncounterId = source.EncounterId;
        StageId = source.StageId;
        UnrestrictedScanRotation = source.Tuning.UnrestrictedScanRotation;
        EncounterElevation = source.EncounterMod?.Camera?.ElevationDegrees;
        GuardianFormsEnabled = source.GuardianFormsEnabled;
        NativeGraphics = source.NativeGraphics;
        NativeModuleResources = source.NativeModuleResources;
        NativeEffectMeshes = source.NativeEffectMeshes;
        NativeEffectCatalog = source.NativeEffectCatalog;
        NativeCommonEffectTexture = source.NativeCommonEffectTexture;
        NativeEntryWaveFile = source.NativeEntryWaveFile;
        NativeVictoryFile = source.NativeVictoryFile;
        NativeStageCameraBank = source.NativeStageCameraBank;
        NativeStageSource = source.NativeStageSource;
        NativeStageCameraSelectors = source.NativeStageCameraSelectors;
        NativeStageCameraControl = source.NativeStageCameraControl;
        NativeInputButtonMap = source.NativeInputButtonMap;
        NativeRenzokukenResources = source.NativeRenzokukenResources;
        NativeGlobalMessages = source.NativeGlobalMessages.ToImmutableArray();
        NativeScanNames = source.NativeScanNames.ToImmutableDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone());
        NativeScanActorNames = source.NativeScanActorNames.ToImmutableDictionary(pair => pair.Key, pair => (byte[])pair.Value.Clone(), StringComparer.Ordinal);
        NativeMessageSpeed = source.NativeMessageSpeed;
        NativeSavedOptions = source.NativeSavedOptions;
        TriggerOptions = source.LimitProgress?.Options978;
        InitialState = source.NativeRetainedMemory is { } memory ? NativeBattleStorageSnapshots.Capture(memory) : null;
        InitialVibration = source.NativeVibrationProcess;
        InitialSound = source.NativeGlobalSoundProcess;
        InitialMusic = source.NativeMusicProcess;
        InitialGlobalRandom = source.NativeGlobalRandom;
        InitialCrtRandom = source.NativeCrtRandom;
        InitialUiRandom = source.NativeBattleUiRandom;
    }

    internal BattleSourceGrouping SourceGrouping { get; }
    internal Func<NativeBattleEffectBinding, NativeBattleCinematicModelOperands>? ReadCinematicModelOperands { get; }
    internal ImmutableArray<BattleGuardianFormResources> GuardianFormResources { get; }
    internal ImmutableArray<NativeBattleDefensiveResource> NativeDefensiveResources { get; }
    internal NativeBattleActionCameraRules? NativeActionCameraRules { get; }
    internal ImmutableDictionary<ushort, (byte First, byte Second, bool Enabled)> GuardianBoost { get; }
    internal int? EncounterId { get; }
    internal double? EncounterElevation { get; }
    internal bool GuardianFormsEnabled { get; }
    internal bool UnrestrictedScanRotation { get; }
    internal int? StageId { get; }
    internal NativeBattleGraphicsDefinition? NativeGraphics { get; }
    internal ImmutableArray<NativeBattleModuleResource> NativeModuleResources { get; }
    internal ImmutableArray<NativeBattleStaticMeshResource> NativeEffectMeshes { get; }
    internal ImmutableArray<NativeBattleEffectBinding> NativeEffectCatalog { get; }
    internal NativeBattleEffectInitializerAsset? NativeCommonEffectTexture { get; }
    internal NativeBattleArchiveFile? NativeEntryWaveFile { get; }
    internal NativeBattleArchiveFile? NativeVictoryFile { get; }
    internal NativeBattleCameraBank? NativeStageCameraBank { get; }
    internal NativeBattleStageSource? NativeStageSource { get; }
    internal ImmutableArray<byte> NativeStageCameraSelectors { get; }
    internal ImmutableArray<byte> NativeStageCameraControl { get; }
    internal ImmutableArray<byte> NativeInputButtonMap { get; }
    internal ImmutableArray<BattleNativeRenzokukenResource> NativeRenzokukenResources { get; }
    internal ImmutableArray<string?> NativeGlobalMessages { get; }
    internal ImmutableDictionary<byte, byte[]> NativeScanNames { get; }
    internal ImmutableDictionary<string, byte[]> NativeScanActorNames { get; }
    internal byte? NativeMessageSpeed { get; }
    internal uint? NativeSavedOptions { get; }
    internal ushort? TriggerOptions { get; }
    internal NativeBattleRetainedMemory? InitialState { get; }
    internal NativeVibrationProcessState? InitialVibration { get; }
    internal NativeGlobalSoundProcess? InitialSound { get; }
    internal NativeBattleMusicProcessState? InitialMusic { get; }
    internal NativeGlobalRandomState? InitialGlobalRandom { get; }
    internal NativeCrtRandomState? InitialCrtRandom { get; }
    internal NativeBattleUiRandomState? InitialUiRandom { get; }
}
