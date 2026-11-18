using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal sealed record BattlePresentationEntry(
    BattleModelRoster Roster,
    NativeBattleModelAliases Aliases,
    ImmutableDictionary<BattleModelOwner, NativeBattleModelEntryFacts> Actors,
    byte? Advantage,
    ushort Flags);

internal readonly record struct BattlePresentationSharedState(
    NativeCrtRandomState? CrtRandom,
    NativeBattleUiRandomState? UiRandom,
    NativeGlobalRandomState? GlobalRandom,
    ulong GlobalRandomCalls,
    NativeBattleMusicProcessState? Music,
    NativeGlobalSoundProcess? Sound,
    NativeVibrationProcessState? Vibration);

internal sealed record BattlePresentationServices(
    Func<BattleModelRoster, BattlePresentationProcess.HostedModelRegistry?, NativeBattleModelLifecycleServices> CaptureModels,
    Func<BattlePresentationSharedState> ReadSharedState,
    Action<BattlePresentationSharedState> PublishSharedState,
    Func<NativeExamTimerState?> ReadTimer,
    Action<NativeExamTimerState?> PublishTimer,
    Func<BattleGunbladeMode> ReadGunbladeMode,
    Action<NativeBattleUiServices> PollClock,
    Action ActivateClock,
    Action PublishVitals,
    Func<HostedRenzokukenFrame?> ReadRenzokuken,
    Action<ImmutableArray<NativeBattleRenzokukenWidgetStepped>, long> PublishRenzokukenUi,
    Func<(long Frame, bool GuardianSkip)> ReadDisplayFrame,
    Func<long> ReadCallback,
    Func<BattleModelOwner, NativeBattleModelTextureBinding, NativeBattleModelTextureBinding?, NativeBattlePreparedActor> ReadPreparedActor,
    Func<BattleModelOwner, NativeBattlePreparedScanActor?> ReadPreparedScanActor,
    Func<BattleModelOwner, NativeBattlePreparedScanExtra> ReadPreparedScanExtra,
    Func<bool> ObservesActionHeaders,
    Action<BattlePresentationProcess, BattlePresentationEffectObservation> ObserveEffect,
    Action<NativeBattleGraphicsState, NativeBattleDescriptorState, int, NativeBattleDescriptorPacket> DescriptorRemoved,
    Action<BattlePresentationProcess> FrameEnded,
    Func<(NativeBattlePartyPresentation Presentation, bool[] Guardian)> ReadStackVitals,
    Func<NativeBattleActionHeaderSlot, bool> MatchesVisualEntry,
    Action<NativeBattleActionHeaderSlot> AdmitVisualEntry,
    Func<NativeBattleShotWidgetFacts?> ReadShotWidget,
    Action<NativeBattleShotWidgetUpdated> PublishShotWidget,
    Func<NativeBattleDuelWidgetFacts?> ReadDuelWidget,
    Action<NativeBattleDuelWidgetUpdated> PublishDuelWidget)
{
    internal Func<bool>? ObservesFollowupTextures { get; init; }
    internal Action<NativeBattleGraphicsState, int, NativeBattleAuxiliaryTask, NativeBattleGraphicsModel>? TextureAnimationReleased { get; init; }
}

internal readonly record struct BattlePresentationEffectObservation(
    NativeBattleOriginalEffectState Effect,
    NativeBattleActionHeaderSlot? Header,
    string? ActorId,
    long Callback,
    byte? OriginalActor,
    ushort? OriginalTargets,
    int? OriginalHeader,
    long? HeaderGeneration);
