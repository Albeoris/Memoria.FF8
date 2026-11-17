using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    internal sealed record BattleLogicalContinuation(ExpandedHostedEvent? Event, ExpandedOperationCursor Operations, long? ActionToken,
        ImmutableArray<int> Contacts, int RoutedContacts, BattlePendingEnemyDraw? EnemyDraw, HostedBattleTerminal? Terminal, bool TerminalEntered,
        ImmutableDictionary<string, NativeBattlePartyPresentedVital>? PartyVitals, BattleSnapshot? AllocationRollback);

    public BattleSnapshot CaptureForContinuation()
    {
        RequireExpandedPresentationDispatchIdle();
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Battle continuation requires its bound presentation.");
        ExpandedInvocationProgress? progress = expandedInvocationProgress;
        BattleLogicalContinuation logical = new(expandedHostedEvent, expandedOperationCursor, expandedHostedActionToken,
            progress?.Occurrences.ToImmutableArray() ?? [], progress?.PresentedRoutedContacts ?? 0, progress?.EnemyDraw,
            expandedHostedTerminal, expandedTerminalEntered, expandedPartyPresentation, encounterAllocationRollback);
        BattlePresentationSnapshot presentation = process.CaptureSnapshot();
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
        clone.AttachRestoredPresentation(snapshot.PresentationContinuation!.Resources, snapshot, preparePresentationModelBinding);
        return clone;
    }

    public void AttachRestoredPresentation(BattlePresentationResources resources, BattleSnapshot continuation, Func<BattleSession, BattleModelOwner, BattleRosterSlot, NativeBattleGraphicsBinding>? modelBindingProvider = null)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(continuation);
        RequireExpandedPresentationDispatchIdle();
        if (Presentation is not null || Frame != continuation.Frame || Mode != continuation.Mode ||
            Outcome != continuation.Outcome || NativeHostCallbacks != continuation.NativeHostCallbacks ||
            continuation.PresentationContinuation is not { } presentation || continuation.LogicalContinuation is not { } logical)
        {
            throw new InvalidDataException("Battle continuation requires its restored logical state and matching presentation callback boundary.");
        }
        BattlePresentationProcess process = BattlePresentationProcess.Restore(resources, presentation, CreateBattlePresentationServices(), nativeEffectResourceResolver);
        expandedHostedEvent = logical.Event;
        expandedOperationCursor = logical.Operations;
        expandedHostedActionToken = logical.ActionToken;
        expandedHostedTerminal = logical.Terminal;
        expandedTerminalEntered = logical.TerminalEntered;
        expandedPartyPresentation = logical.PartyVitals;
        encounterAllocationRollback = logical.AllocationRollback;
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
