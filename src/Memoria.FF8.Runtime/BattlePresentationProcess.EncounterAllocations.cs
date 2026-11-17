using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private HostedEncounterLoadCheckpoint? hostedEncounterLoadCheckpoint;

    internal sealed record HostedEncounterLoadCheckpoint(
        BattleEncounterAllocationState Batch, HostedModelRegistry Registry, NativeBattleGraphicsState Graphics,
        NativeBattleActionPacketsState Packets, ImmutableArray<HostedControlPacket> Controls,
        NativeBattleModelCpuResources? Cpu, NativeBattleModelTextureCache? TextureCache, NativeBattleEffectMemory? Memory,
        NativeBattleFileStaging? Staging, NativeBattleFileIoState? FileIo, int? IoResult, NativeBattleModelLoader? Loader,
        NativeBattleCameraState? Camera, NativeBattleStageState? Stage);

    internal HostedEncounterLoadCheckpoint? PrepareHostedEncounterLoad(BattleEncounterAllocationState? batch, PreparedHostedDispatch dispatch)
    {
        if (batch is not { Phase: BattleEncounterAllocationPhase.Loading, LoadingIndex: 0 } ||
            hostedEncounterLoadCheckpoint is not null || dispatch.Registry is not { } registry ||
            !registry.Replacement.Loads.Keys.OfType<BattleEnemyActorModel>().Any(owner => owner.Actor.ActorId == batch.ActorIds[0]))
        {
            return null;
        }
        if (dispatch.Action is not null || dispatch.Completion is not null || NativeModelLoader?.Task is not null)
        {
            throw new InvalidOperationException("An attached encounter batch requires exclusive ownership of its native model load.");
        }
        // The summon action is already complete. Save its acknowledged packets together with the
        // resource graph that existed before the first member's model registry was published.
        return NativeBattleStorageSnapshots.CapturePrivate(new HostedEncounterLoadCheckpoint(
            batch, registry.Original, dispatch.Graphics, dispatch.Acknowledgment?.AcknowledgedPackets ?? nativeActionPackets,
            hostedControlPackets, NativeModelCpuResources, NativeModelTextureCache, NativeEffectMemory, NativeFileStaging,
            NativeFileIo, NativeEffectIoResult, NativeModelLoader, NativeCamera, NativeStage));
    }

    internal void PublishHostedEncounterLoad(HostedEncounterLoadCheckpoint? checkpoint)
    {
        if (checkpoint is not null)
        {
            hostedEncounterLoadCheckpoint = checkpoint;
        }
    }

    internal void CompleteHostedEncounterLoad()
    {
        hostedEncounterLoadCheckpoint = null;
    }

    internal bool RollbackHostedEncounterLoad()
    {
        if (hostedEncounterLoadCheckpoint is not { } saved)
        {
            return false;
        }
        HostedEncounterLoadCheckpoint checkpoint = NativeBattleStorageSnapshots.Restore(saved);
        NativeBattleGraphicsState current = RequireNativeGraphics();
        NativeGraphics = checkpoint.Graphics with
        {
            Callback = current.Callback,
            PresentationSubticks = current.PresentationSubticks,
            PresentationHalfSubtick = current.PresentationHalfSubtick
        };
        hostedModelRegistry = checkpoint.Registry;
        nativeActionPackets = checkpoint.Packets;
        hostedControlPackets = checkpoint.Controls;
        NativeModelCpuResources = checkpoint.Cpu;
        NativeModelTextureCache = checkpoint.TextureCache;
        NativeEffectMemory = checkpoint.Memory;
        NativeFileStaging = checkpoint.Staging;
        NativeFileIo = checkpoint.FileIo;
        NativeEffectIoResult = checkpoint.IoResult;
        NativeModelLoader = checkpoint.Loader;
        NativeCamera = checkpoint.Camera;
        NativeStage = checkpoint.Stage;
        hostedEncounterLoadCheckpoint = null;
        hostedPreparationEpoch = checked(hostedPreparationEpoch + 1);
        ClearHostedPresentationFrameOutputs();
        return true;
    }

    private void ValidateHostedEncounterLoadResult(NativeBattleFileIoCompletion completion)
    {
        if (completion.Result < 0 && hostedEncounterLoadCheckpoint is { } checkpoint &&
            NativeModelLoader?.Task is { Phase: 1, Owner: BattleEnemyActorModel owner, File: { } file } &&
            checkpoint.Batch.ActorIds.Contains(owner.Actor.ActorId))
        {
            throw new IOException($"Native model file {file.Name} failed to load for {owner.Actor.ActorId} (result {completion.Result}).");
        }
    }

    internal void RefreshRolledBackHostedEncounter()
    {
        BattlePresentationServices host = services ?? throw new InvalidOperationException("Battle rollback requires its bound logical services.");
        PublishPreparedPresentation(host.CaptureModels(HostedPlannedModelRoster, hostedModelRegistry));
    }
}
