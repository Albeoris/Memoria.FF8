using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    internal sealed record HostedGuardianFormVisualOwner(BattlePartyActorModel Actor, string ActorId, long InvocationId, int GuardianForceId);
    internal sealed record HostedGuardianFormFrameFacts(HostedGuardianFormVisualOwner? Form, BattleGuardianFormPhase? Phase, bool HasAction, bool Finishing);
    internal sealed record HostedGuardianFormVisualChange(HostedGuardianFormVisualOwner Owner, bool Entering, HostedGuardianFormFrameFacts Facts);
    internal sealed record PreparedHostedGuardianFormChange(BattlePresentationProcess Owner, long Epoch, BattlePresentationNativeState Original, BattlePresentationNativeState Replacement, HostedModelRegistry? Registry, BattlePresentationProcess Staged, ImmutableArray<string> StopSounds);

    private BattlePresentationProcess(BattlePresentationProcess source, BattlePresentationNativeState native)
    {
        resources = source.resources;
        hostedEntryRoster = source.hostedEntryRoster;
        services = source.services;
        ResolvedNativeFlags = source.ResolvedNativeFlags;
        hostedModelRegistry = source.hostedModelRegistry;
        nativeEffectResourceResolver = source.nativeEffectResourceResolver;
        nativeActionPackets = source.nativeActionPackets;
        NativeEntryAdvantage = source.NativeEntryAdvantage;
        PublishNativeState(native);
    }

    internal void ValidateHostedGuardianFormEntryResources(HostedGuardianFormVisualOwner owner)
    {
        if (!HasBattleEntry || NativeGraphics is not { EntryReady: true } graphics || !graphics.ActorModels.Contains(owner.Actor) ||
            owner.Actor.Actor.ActorId != owner.ActorId || !HostedPlannedModelRoster.Contains(owner.Actor))
        {
            throw new InvalidDataException("Hosted form admission requires its current party model.");
        }
        PrepareGuardianFormPresentation(owner.Actor, owner.GuardianForceId);
        (ReadOriginalPresentationModelResource(owner.Actor) ?? throw new InvalidDataException("Hosted form admission requires its original human resource.")).Validate();
    }

    internal PreparedHostedGuardianFormChange PrepareHostedGuardianFormChange(HostedGuardianFormVisualChange change)
    {
        if (!HasBattleEntry || NativeGraphics is not { EntryReady: true } graphics || hostedFrameServices is not null ||
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
        BattlePresentationNativeState original = ReadNativeState();
        BattlePresentationNativeState native = NativeBattleStorageSnapshots.Fork(original with { NativeActionPackets = null });
        BattlePresentationProcess staged = new(this, native);
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
        return new(this, hostedPreparationEpoch, original, staged.ReadNativeState(), hostedModelRegistry, staged, stopSounds.ToImmutable());
    }

    internal void ValidatePreparedHostedGuardianFormChange(PreparedHostedGuardianFormChange prepared)
    {
        BattlePresentationNativeState original = prepared.Original;
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
        PublishNativeState(prepared.Replacement);
    }

    internal static NativeBattleGuardianFormPresentation RequireHostedGuardianFormVisualOwner(NativeBattleGraphicsState graphics, HostedGuardianFormVisualOwner owner)
    {
        if (graphics.GuardianFormPresentation is not { } visual || visual.Actor != owner.Actor || visual.ActorId != owner.ActorId || visual.InvocationId != owner.InvocationId)
        {
            throw new InvalidDataException("The hosted form transition does not own the current transformation.");
        }
        return visual;
    }
}
