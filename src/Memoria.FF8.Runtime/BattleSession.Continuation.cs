using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record DiagnosticTransportContinuation(bool HoldEnemyCommands, bool InstantGuardianForceCharge, string? ReadinessActorId, bool EffectPreview, long? PreviewToken);

    internal sealed record BattleLogicalContinuation(ExpandedHostedEvent? Event, ExpandedOperationCursor Operations, long? ActionToken,
        ImmutableArray<int> Contacts, int RoutedContacts, BattlePendingEnemyDraw? EnemyDraw, HostedBattleTerminal? Terminal, bool TerminalEntered,
        ImmutableDictionary<string, NativeBattlePartyPresentedVital>? PartyVitals,
        ImmutableDictionary<string, NativeBattleStatusMirror>? PartyReadyStatuses, BattleSnapshot? AllocationRollback,
        ImmutableArray<BattlePresentationProcess.HostedModelLoadAdmission> EncounterLoadAdmissions)
    {
        internal DiagnosticTransportContinuation? DiagnosticTransport { get; init; }
    }

    public BattleSnapshot CaptureForContinuation()
    {
        RequireExpandedPresentationDispatchIdle();
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Battle continuation requires its bound presentation.");
        ExpandedInvocationProgress? progress = expandedInvocationProgress;
        BattleLogicalContinuation logical = new(expandedHostedEvent, expandedOperationCursor, expandedHostedActionToken,
            progress?.Occurrences.ToImmutableArray() ?? [], progress?.PresentedRoutedContacts ?? 0, progress?.EnemyDraw,
            expandedHostedTerminal, expandedTerminalEntered, expandedPartyPresentation, expandedPartyReadyStatusMirrors, encounterAllocationRollback, hostedEncounterLoadAdmissions)
        {
            DiagnosticTransport = diagnosticHoldEnemyCommands || diagnosticInstantGuardianForceCharge || diagnosticReadinessActor is not null || diagnosticEffectPreview || diagnosticPreviewToken is not null
                ? new(diagnosticHoldEnemyCommands, diagnosticInstantGuardianForceCharge, diagnosticReadinessActor?.Id, diagnosticEffectPreview, diagnosticPreviewToken)
                : null
        };
        BattlePresentationSnapshot presentation = process.CaptureSnapshot();
        ValidateEncounterAllocationContinuation(encounterAllocation, logical, presentation);
        return NativeBattleStorageSnapshots.CapturePrivate(CaptureState() with { PresentationContinuation = presentation, LogicalContinuation = logical });
    }

    public BattleSession ForkForContinuation()
    {
        RequireExpandedPresentationDispatchIdle();
        if (Presentation is null)
        {
            return Restore(Capture());
        }
        BattleSnapshot snapshot = CaptureForContinuation();
        BattleSession clone = Restore(snapshot);
        clone.nativeEffectResourceResolver = nativeEffectResourceResolver;
        clone.AttachRestoredPresentation(snapshot.PresentationContinuation!.Resources, snapshot, preparePresentationModelBinding);
        return clone;
    }

    public void AttachRestoredPresentation(BattlePresentationResources resources, BattleSnapshot continuation, Func<BattleSession, BattleModelOwner, BattleRosterSlot, NativeBattleGraphicsBinding>? modelBindingProvider = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(continuation);
        RequireExpandedPresentationDispatchIdle();
        RequirePresentationClock();
        if (Presentation is not null || Frame != continuation.Frame || Mode != continuation.Mode ||
            Outcome != continuation.Outcome || NativeHostCallbacks != continuation.NativeHostCallbacks ||
            continuation.PresentationContinuation is not { } presentation || continuation.LogicalContinuation is not { } logical)
        {
            throw new InvalidDataException("Battle continuation requires its restored logical state and matching presentation callback boundary.");
        }
        ValidateEncounterAllocationContinuation(encounterAllocation, logical, presentation);
        DiagnosticTransportContinuation? diagnosticTransport = logical.DiagnosticTransport;
        BattleCombatant? readinessActor = null;
        if (diagnosticTransport is not null && ((!diagnosticTransport.EffectPreview && diagnosticTransport.PreviewToken is not null) || diagnosticTransport.PreviewToken is <= 0 || diagnosticTransport.ReadinessActorId is { } actorId && !byId.TryGetValue(actorId, out readinessActor)))
        {
            throw new InvalidDataException("Private diagnostic continuation changed its declared preview or scheduling owner.");
        }
        BattlePresentationProcess process = BattlePresentationProcess.Restore(resources, presentation, CreateBattlePresentationServices(), nativeEffectResourceResolver);
        if (process.EntryReady && process.Graphics.UiServices is null)
        {
            throw new InvalidDataException("An entered battle continuation requires its current native UI clock services.");
        }
        expandedHostedEvent = logical.Event;
        diagnosticHoldEnemyCommands = diagnosticTransport?.HoldEnemyCommands ?? false;
        diagnosticInstantGuardianForceCharge = diagnosticTransport?.InstantGuardianForceCharge ?? false;
        diagnosticReadinessActor = readinessActor;
        diagnosticEffectPreview = diagnosticTransport?.EffectPreview ?? false;
        diagnosticPreviewToken = diagnosticTransport?.PreviewToken;
        expandedOperationCursor = logical.Operations;
        expandedHostedActionToken = logical.ActionToken;
        expandedHostedTerminal = logical.Terminal;
        expandedTerminalEntered = logical.TerminalEntered;
        expandedPartyPresentation = logical.PartyVitals;
        expandedPartyReadyStatusMirrors = logical.PartyReadyStatuses;
        encounterAllocationRollback = logical.AllocationRollback;
        hostedEncounterLoadAdmissions = logical.EncounterLoadAdmissions;
        if (CurrentExpandedHostedInvocation is { } invocation)
        {
            BattleModelRoster roster = process.PlannedModelRoster;
            var progress = new ExpandedInvocationProgress(invocation, roster, PrepareExpandedHostedContactBridge(invocation, roster))
            {
                EnemyDraw = logical.EnemyDraw,
                PresentedRoutedContacts = logical.RoutedContacts
            };
            progress.Occurrences.UnionWith(logical.Contacts);
            expandedInvocationProgress = progress;
        }
        preparePresentationModelBinding = modelBindingProvider;
        expandedPresentationProcess = process;
        process.RefreshPreparedPresentationAtBoundary();
    }
}
