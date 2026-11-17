using System.Collections.Immutable;
using static Memoria.FF8.Runtime.BattlePresentationProcess;

namespace Memoria.FF8.Runtime;

internal sealed record BattlePresentationNativeState
{
    internal NativeBattleGraphicsState? NativeGraphics { get; init; }
    internal NativeBattleCameraState? NativeCamera { get; init; }
    internal NativeBattleStageState? NativeStage { get; init; }
    internal NativeBattleEffectMemory? NativeEffectMemory { get; init; }
    internal NativeBattleFileIoState? NativeFileIo { get; init; }
    internal NativeBattleFileStaging? NativeFileStaging { get; init; }
    internal NativeBattleModelLoader? NativeModelLoader { get; init; }
    internal NativeBattleModelCpuResources? NativeModelCpuResources { get; init; }
    internal NativeBattleModelTextureCache? NativeModelTextureCache { get; init; }
    internal int? NativeEffectIoResult { get; init; }
    internal NativeBattleActionPacketsState? NativeActionPackets { get; init; }
}

internal sealed record BattlePresentationSnapshot(BattlePresentationResources Resources, BattlePresentationEntry? Entry, BattlePresentationNativeState Native, BattlePresentationSharedState Shared, ImmutableArray<HostedEntryDescriptor> EntryQueue, NativeBattleHostedModelPackets.Snapshot? ModelPackets)
{
    internal BattleExternalEffectContinuation? ExternalEffect { get; init; }
    internal HostedPresentationInvocation? Invocation { get; init; }
    internal HostedCompletionInvocation? Completion { get; init; }
    internal ImmutableArray<HostedControlPacket> ControlPackets { get; init; }
    internal HostedModelRegistry? ModelRegistry { get; init; }
    internal HostedEncounterLoadCheckpoint? LoadCheckpoint { get; init; }
    internal long PreparationEpoch { get; init; }
    internal long LastActionIssued { get; init; }
    internal long LastActionCompleted { get; init; }
    internal long LastCompletionIssued { get; init; }
    internal long LastCompletionAcknowledged { get; init; }
    internal HostedGuardianBoostRequest? PendingGuardianBoost { get; init; }
    internal long LastGuardianBoostAcknowledged { get; init; }
    internal HostedPresentationInvocation? GuardianBoostOwner { get; init; }
    internal NativeBattleGuardianBoost? GuardianBoost { get; init; }
    internal ushort GuardianBoostMappedButtons { get; init; }
    internal NativeBattleDuelSelectionReached? DuelSelection { get; init; }
    internal NativeBattleRenzokukenFinisherReached? RenzokukenFinisher { get; init; }
    internal NativeBattleRenzokukenWidgetStepped? RenzokukenPendingStep { get; init; }
    internal NativeBattleRenzokukenWidgetStepped? RenzokukenPublishedStep { get; init; }
    internal HostedBattleTerminal? Terminal { get; init; }
    internal bool TerminalEntered { get; init; }
    internal bool TerminalFadePublished { get; init; }
    internal bool TerminalFinished { get; init; }
    internal long TerminalSequence { get; init; }
    internal required NativeBattleScriptTasks TerminalScriptTasks { get; init; }
    internal ImmutableArray<EncounterArrivalState> Arrivals { get; init; }
    internal NativeBattleGraphicsResource? Source { get; init; }
    internal BattleModelOwner? SourceOwner { get; init; }
    internal byte? SourceModel { get; init; }
    internal bool? SourceIsParty { get; init; }
    internal int? PartyActorCount { get; init; }
    internal NativeExamTimerState? Timer { get; init; }
    internal long PreparedVersion { get; init; }
    internal NativeBattleCameraPose? PilotCamera { get; init; }
    internal NativeBattleCameraProjection? PilotProjection { get; init; }
    internal required NativeBattleEffectPresentationStep LastStep { get; init; }
    internal long? LastStepAction { get; init; }
    internal long DiagnosticGeneration { get; init; }
    internal ImmutableDictionary<int, long>? DiagnosticGenerations { get; init; }
}

public sealed partial class BattlePresentationProcess
{
    internal BattlePresentationSnapshot CaptureSnapshot()
    {
        RequireIdle();
        if (hostedFrameServices is not null || nativePreRootStack is not null ||
            HasBattleEntry && (hostedEntryRoster is null || hostedEntryAliases is null || hostedEntryDescriptors is null || hostedModelPackets is null))
        {
            throw new InvalidOperationException("Battle presentation continuation requires its entered callback boundary.");
        }
        BattlePresentationEntry? entry = HasBattleEntry ? new(hostedEntryRoster!, hostedEntryAliases!, hostedEntryFacts, NativeEntryAdvantage, ResolvedNativeFlags) : null;
        BattlePresentationSnapshot snapshot = new(resources, entry, ReadNativeState(), ObserveSharedState(), hostedEntryDescriptors?.ToImmutableArray() ?? [], hostedModelPackets?.Capture())
        {
            ExternalEffect = entry is null ? CaptureExternalEffectContinuation() : null,
            Invocation = hostedPresentationInvocation,
            Completion = hostedCompletionInvocation,
            ControlPackets = hostedControlPackets,
            ModelRegistry = hostedModelRegistry,
            LoadCheckpoint = hostedEncounterLoadCheckpoint,
            PreparationEpoch = hostedPreparationEpoch,
            LastActionIssued = lastHostedActionIssued,
            LastActionCompleted = lastHostedActionCompleted,
            LastCompletionIssued = lastHostedCompletionIssued,
            LastCompletionAcknowledged = lastHostedCompletionAcknowledged,
            PendingGuardianBoost = pendingHostedGuardianBoost,
            LastGuardianBoostAcknowledged = lastHostedGuardianBoostAcknowledged,
            GuardianBoostOwner = hostedGuardianBoostWidgetOwner,
            GuardianBoost = nativeGuardianBoost,
            GuardianBoostMappedButtons = hostedGuardianBoostMappedButtons,
            DuelSelection = pendingHostedDuelSelection,
            RenzokukenFinisher = pendingHostedRenzokukenFinisher,
            RenzokukenPendingStep = hostedRenzokukenPendingStep,
            RenzokukenPublishedStep = hostedRenzokukenPublishedStep,
            Terminal = hostedTerminal,
            TerminalEntered = hostedTerminalEntered,
            TerminalFadePublished = hostedTerminalFadePublished,
            TerminalFinished = hostedTerminalFinished,
            TerminalSequence = terminalSequence,
            TerminalScriptTasks = terminalScriptTasks,
            Arrivals = encounterArrivals,
            Source = presentationSourceResource,
            SourceOwner = presentationSourceOwner,
            SourceModel = presentationSourceModelId,
            SourceIsParty = presentationSourceIsParty,
            PartyActorCount = presentationPartyActorCount,
            Timer = NativeExamTimer,
            PreparedVersion = preparedPresentationVersion,
            PilotCamera = nativePilotSourceCamera,
            PilotProjection = nativePilotSourceProjection,
            LastStep = LastStep,
            LastStepAction = LastStepActionToken,
            DiagnosticGeneration = diagnosticHeaderGeneration,
            DiagnosticGenerations = diagnosticHeaderGenerations?.ToImmutableDictionary()
        };
        return NativeBattleStorageSnapshots.CapturePrivate(snapshot);
    }

    internal static BattlePresentationProcess Restore(BattlePresentationResources resources, BattlePresentationSnapshot snapshot, BattlePresentationServices? services, Func<ushort, NativeBattleEffectBinding>? resolveEffect, BattleExternalEffectHostBindings? externalHost = null)
    {
        _ = resources.RequirePrimitiveExecution();
        if (snapshot.Native.NativeGraphics?.PrimitiveFrame is null || snapshot.Native.NativeEffectMemory?.PrimitiveProcess is null)
        {
            throw new InvalidDataException("Presentation continuation requires its initialized primitive frame and process.");
        }
        // A future rollback must acquire its own storage from the captured pre-load state.
        BattlePresentationSnapshot native = NativeBattleStorageSnapshots.Restore(snapshot with { LoadCheckpoint = null }) with { LoadCheckpoint = snapshot.LoadCheckpoint };
        return new(resources, native, services, resolveEffect, externalHost);
    }

    private BattlePresentationProcess(BattlePresentationResources resources, BattlePresentationSnapshot snapshot, BattlePresentationServices? services, Func<ushort, NativeBattleEffectBinding>? resolveEffect, BattleExternalEffectHostBindings? externalHost)
    {
        this.resources = resources;
        this.services = services;
        if (snapshot.Entry is { } entry && snapshot.ModelPackets is { } models && services is not null && snapshot.ExternalEffect is null && externalHost is null)
        {
            hostedEntryFacts = entry.Actors;
            hostedEntryRoster = entry.Roster;
            hostedEntryAliases = entry.Aliases;
            hostedEntryDescriptors = new(snapshot.EntryQueue);
            hostedModelPackets = NativeBattleHostedModelPackets.Restore(models);
            NativeEntryAdvantage = entry.Advantage;
            ResolvedNativeFlags = entry.Flags;
        }
        else if (snapshot.Entry is not null || snapshot.ModelPackets is not null || services is not null || snapshot.ExternalEffect is null || externalHost is null)
        {
            throw new InvalidDataException("Presentation continuation has mismatched scene and host bindings.");
        }
        nativeEffectResourceResolver = resolveEffect;
        presentationCameraMode = NativeBattleEffectCameraMode.NativeCinematic;
        presentationRibbonProjectionPolicy = NativeBattleRibbonProjectionPolicy.IndependentGeometricRuns;
        presentationForeignActors = [];
        presentationNativeTargets = [];
        PublishNativeState(snapshot.Native);
        nativeActionPackets = snapshot.Native.NativeActionPackets ?? throw new InvalidDataException("Battle presentation continuation has no action packet state.");
        nativeCrtRandom = snapshot.Shared.CrtRandom;
        nativeBattleUiRandom = snapshot.Shared.UiRandom;
        nativeGlobalRandom = snapshot.Shared.GlobalRandom;
        ImportSharedState(snapshot.Shared);
        hostedPresentationInvocation = snapshot.Invocation;
        hostedCompletionInvocation = snapshot.Completion;
        hostedControlPackets = snapshot.ControlPackets;
        hostedModelRegistry = snapshot.ModelRegistry;
        hostedEncounterLoadCheckpoint = snapshot.LoadCheckpoint;
        hostedPreparationEpoch = snapshot.PreparationEpoch;
        lastHostedActionIssued = snapshot.LastActionIssued;
        lastHostedActionCompleted = snapshot.LastActionCompleted;
        lastHostedCompletionIssued = snapshot.LastCompletionIssued;
        lastHostedCompletionAcknowledged = snapshot.LastCompletionAcknowledged;
        pendingHostedGuardianBoost = snapshot.PendingGuardianBoost;
        lastHostedGuardianBoostAcknowledged = snapshot.LastGuardianBoostAcknowledged;
        hostedGuardianBoostWidgetOwner = snapshot.GuardianBoostOwner;
        nativeGuardianBoost = snapshot.GuardianBoost;
        hostedGuardianBoostMappedButtons = snapshot.GuardianBoostMappedButtons;
        pendingHostedDuelSelection = snapshot.DuelSelection;
        pendingHostedRenzokukenFinisher = snapshot.RenzokukenFinisher;
        hostedRenzokukenPendingStep = snapshot.RenzokukenPendingStep;
        hostedRenzokukenPublishedStep = snapshot.RenzokukenPublishedStep;
        hostedTerminal = snapshot.Terminal;
        hostedTerminalEntered = snapshot.TerminalEntered;
        hostedTerminalFadePublished = snapshot.TerminalFadePublished;
        hostedTerminalFinished = snapshot.TerminalFinished;
        terminalSequence = snapshot.TerminalSequence;
        terminalScriptTasks = snapshot.TerminalScriptTasks;
        encounterArrivals = snapshot.Arrivals;
        presentationSourceResource = snapshot.Source;
        presentationSourceOwner = snapshot.SourceOwner;
        presentationSourceModelId = snapshot.SourceModel;
        presentationSourceIsParty = snapshot.SourceIsParty;
        presentationPartyActorCount = snapshot.PartyActorCount;
        NativeExamTimer = snapshot.Timer;
        preparedPresentationVersion = snapshot.PreparedVersion;
        nativePilotSourceCamera = snapshot.PilotCamera;
        nativePilotSourceProjection = snapshot.PilotProjection;
        LastStep = snapshot.LastStep;
        LastStepActionToken = snapshot.LastStepAction;
        diagnosticHeaderGeneration = snapshot.DiagnosticGeneration;
        diagnosticHeaderGenerations = snapshot.DiagnosticGenerations?.ToDictionary();
        if (snapshot.ExternalEffect is { } external)
        {
            Graphics.Validate(owner => external.Actors.TryGetValue(owner, out BattleExternalEffectActorState? actor) && actor.NativeResource is null);
        }
        else
        {
            Graphics.Validate();
        }
        Memory.ValidateReturnedTaskOwner(Graphics.OriginalEffect);
        if (snapshot.ExternalEffect is { } externalEffect)
        {
            RestoreExternalEffectContinuation(externalEffect, externalHost!);
        }
        else
        {
            GunbladeMode = services!.ReadGunbladeMode();
        }
    }

    internal void RefreshPreparedPresentationAtBoundary()
    {
        if (!advancing && services is { } host && NativeGraphics?.PrimitiveFrame is not null)
        {
            PublishPreparedPresentation(host.CaptureModels(HostedPlannedModelRoster, hostedModelRegistry));
        }
    }

    private BattlePresentationNativeState ReadNativeState()
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
            NativeActionPackets = nativeActionPackets,
        };
    }

    private void PublishNativeState(BattlePresentationNativeState native)
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
}
