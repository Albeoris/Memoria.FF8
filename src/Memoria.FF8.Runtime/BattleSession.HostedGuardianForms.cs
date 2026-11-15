using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record HostedGuardianFormVisualOwner(BattlePartyActorModel Actor, string ActorId, long InvocationId, int GuardianForceId);
    internal sealed record HostedGuardianFormFrameFacts(HostedGuardianFormVisualOwner? Form, BattleGuardianFormPhase? Phase, bool HasAction, bool Finishing);
    internal sealed record HostedGuardianFormVisualChange(HostedGuardianFormVisualOwner Owner, bool Entering, HostedGuardianFormFrameFacts Facts);
    internal sealed record PreparedHostedGuardianFormChange(BattleSession Owner, long Epoch, BattleSnapshot Original, BattleSnapshot Replacement, HostedModelRegistry? Registry, BattleSession Staged, ImmutableArray<string> StopSounds);

    private BattleSession(BattleSession source, BattleSnapshot native)
    {
        definition = source.definition;
        combatants = [];
        byId = new(StringComparer.Ordinal);
        handlers = [];
        Combatants = combatants.AsReadOnly();
        random = source.random;
        Mode = source.Mode;
        Difficulty = source.Difficulty;
        Frame = source.Frame;
        hostedBattlePresentation = true;
        hostedModelRegistry = source.hostedModelRegistry;
        nativeEffectResourceResolver = source.nativeEffectResourceResolver;
        nativeActionPackets = source.nativeActionPackets;
        NativeEntryAdvantage = source.NativeEntryAdvantage;
        PublishHostedGuardianFormNativeState(native);
    }

    private BattleSnapshot ReadHostedGuardianFormNativeState()
    {
        return new()
        {
            NativeGraphics = NativeGraphics,
            NativeCamera = NativeCamera,
            NativeStage = NativeStage,
            NativeEffectMemory = NativeEffectMemory,
            NativeFileIo = NativeFileIo,
            NativeFileStaging = NativeFileStaging,
            NativeModelLoader = NativeModelLoader,
            NativeModelCpuResources = NativeModelCpuResources,
            NativeModelTextureCache = NativeModelTextureCache,
            NativeEffectIoResult = NativeEffectIoResult,
            NativeActionPackets = nativeActionPackets
        };
    }

    private void PublishHostedGuardianFormNativeState(BattleSnapshot native)
    {
        NativeGraphics = native.NativeGraphics;
        NativeCamera = native.NativeCamera;
        NativeStage = native.NativeStage;
        NativeEffectMemory = native.NativeEffectMemory;
        NativeFileIo = native.NativeFileIo;
        NativeFileStaging = native.NativeFileStaging;
        NativeModelLoader = native.NativeModelLoader;
        NativeModelCpuResources = native.NativeModelCpuResources;
        NativeModelTextureCache = native.NativeModelTextureCache;
        NativeEffectIoResult = native.NativeEffectIoResult;
    }

    internal void ValidateHostedGuardianFormEntryResources(HostedGuardianFormVisualOwner owner)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true } graphics || !graphics.ActorModels.Contains(owner.Actor) ||
            owner.Actor.Actor.ActorId != owner.ActorId || !HostedPlannedModelRoster.Contains(owner.Actor))
        {
            throw new InvalidDataException("Hosted form admission requires its current party model.");
        }
        PrepareGuardianFormPresentation(owner.Actor, owner.GuardianForceId);
        (ReadOriginalPresentationModelResource(owner.Actor) ?? throw new InvalidDataException("Hosted form admission requires its original human resource.")).Validate();
    }

    internal PreparedHostedGuardianFormChange PrepareHostedGuardianFormChange(HostedGuardianFormVisualChange change)
    {
        if (!hostedBattlePresentation || NativeGraphics is not { EntryReady: true } graphics || hostedFrameServices is not null ||
            !graphics.ActorModels.Contains(change.Owner.Actor) || change.Owner.Actor.Actor.ActorId != change.Owner.ActorId)
        {
            throw new InvalidOperationException("Hosted form preparation requires its current model at a canonical handoff.");
        }
        if (change.Entering)
        {
            if (hostedPresentationInvocation is not null || hostedCompletionInvocation is not null || !HostedControlPrefixRetired || NativeModelLoader?.Task is not null)
            {
                throw new InvalidOperationException("Hosted form entry requires an idle descriptor process.");
            }
            ValidateHostedGuardianFormEntryResources(change.Owner);
        }
        else
        {
            RequireHostedGuardianFormVisualOwner(graphics, change.Owner);
        }
        BattleSnapshot original = ReadHostedGuardianFormNativeState();
        BattleSnapshot native = NativeBattleStorageSnapshots.Fork(original with { NativeActionPackets = null });
        BattleSession staged = new(this, native);
        var stopSounds = ImmutableArray.CreateBuilder<string>();
        if (change.Entering)
        {
            staged.BeginGuardianFormEntry(change.Owner);
        }
        else
        {
            staged.ReturnGuardianFormPresentation(change.Owner, change.Facts, stopSounds.Add);
        }
        staged.RequireNativeGraphics().Validate();
        return new(this, hostedPreparationEpoch, original, staged.ReadHostedGuardianFormNativeState(), hostedModelRegistry, staged, stopSounds.ToImmutable());
    }

    internal void ValidatePreparedHostedGuardianFormChange(PreparedHostedGuardianFormChange prepared)
    {
        BattleSnapshot original = prepared.Original;
        if (!ReferenceEquals(prepared.Owner, this) || prepared.Epoch != hostedPreparationEpoch || hostedFrameServices is not null ||
            !ReferenceEquals(original.NativeGraphics, NativeGraphics) || !ReferenceEquals(original.NativeCamera, NativeCamera) ||
            !ReferenceEquals(original.NativeStage, NativeStage) || !ReferenceEquals(original.NativeEffectMemory, NativeEffectMemory) ||
            !ReferenceEquals(original.NativeFileIo, NativeFileIo) || !ReferenceEquals(original.NativeFileStaging, NativeFileStaging) ||
            !ReferenceEquals(original.NativeModelLoader, NativeModelLoader) || !ReferenceEquals(original.NativeModelCpuResources, NativeModelCpuResources) ||
            !ReferenceEquals(original.NativeModelTextureCache, NativeModelTextureCache) || !ReferenceEquals(original.NativeActionPackets, nativeActionPackets) ||
            !ReferenceEquals(prepared.Registry, hostedModelRegistry) || original.NativeEffectIoResult != NativeEffectIoResult)
        {
            throw new InvalidOperationException("The prepared form change no longer belongs to the current hosted process.");
        }
    }

    internal void CommitHostedGuardianFormChange(PreparedHostedGuardianFormChange prepared)
    {
        PublishHostedGuardianFormNativeState(prepared.Replacement);
    }

    private static NativeBattleGuardianFormPresentation RequireHostedGuardianFormVisualOwner(NativeBattleGraphicsState graphics, HostedGuardianFormVisualOwner owner)
    {
        if (graphics.GuardianFormPresentation is not { } visual || visual.Actor != owner.Actor || visual.ActorId != owner.ActorId || visual.InvocationId != owner.InvocationId)
        {
            throw new InvalidDataException("The hosted form transition does not own the current transformation.");
        }
        return visual;
    }
}
