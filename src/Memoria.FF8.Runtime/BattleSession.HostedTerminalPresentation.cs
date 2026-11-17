namespace Memoria.FF8.Runtime;

internal sealed record HostedBattleTerminal(long Token, BattleOutcome Outcome);
internal sealed record NativeBattleTerminalEntered(HostedBattleTerminal Terminal) : NativeBattlePresentationReceipt;
internal sealed record NativeBattleTerminalFinished(HostedBattleTerminal Terminal) : NativeBattlePresentationReceipt;

public sealed partial class BattleSession
{
    private abstract record HostedCandidateProof;
    private sealed record HostedTerminalCandidateProof(HostedBattleTerminal Terminal) : HostedCandidateProof;
    private sealed record HostedEventCompletionCandidateProof(BattleSnapshot Snapshot, ExpandedHostedEvent Event, NativeBattlePresentationCompletionRequested Receipt) : HostedCandidateProof;

    private HostedBattleTerminal? expandedHostedTerminal;
    private bool expandedTerminalEntered;
    private bool deferExpandedHostedTerminal;
    private BattleOutcome? deferredExpandedTerminalOutcome;
    private bool HasPendingExpandedTerminal => expandedHostedTerminal is not null;
    private bool OwnsExpandedTerminal => expandedPresentationProcess is not null || deferExpandedHostedTerminal;

    private void AdmitExpandedHostedTerminal(BattleOutcome outcome)
    {
        if (deferExpandedHostedTerminal)
        {
            deferredExpandedTerminalOutcome = outcome;
            StopTurnAdvance();
            return;
        }
        BattlePresentationProcess process = expandedPresentationProcess ?? throw new InvalidOperationException("Expanded terminal admission requires its attached presentation owner.");
        BattlePresentationProcess.PreparedTerminal preparation = process.PrepareTerminal(outcome, EscapeFlags);
        process.ValidateTerminal(preparation);
        PublishExpandedHostedTerminal(process, preparation);
    }

    private void PublishExpandedHostedTerminal(BattlePresentationProcess process, BattlePresentationProcess.PreparedTerminal preparation)
    {
        process.CommitTerminal(preparation);
        expandedHostedTerminal = preparation.Terminal;
        StopTurnAdvance();
    }

    private void AcknowledgeHostedTerminalEntry(NativeBattleTerminalEntered receipt)
    {
        if (!ReferenceEquals(expandedHostedTerminal, receipt.Terminal) || expandedTerminalEntered || Outcome != BattleOutcome.Active)
        {
            throw new InvalidDataException("Hosted terminal entry does not match the pending canonical battle.");
        }
        foreach (BattleCombatant actor in allies)
        {
            actor.NativeCombatantFlags &= 0x7FFFFFFFu;
        }
        if (nativeBattleClock is { } clock)
        {
            nativeBattleClock = clock with { Active = 0, AtbAdmitted = 0 };
        }
        expandedTerminalEntered = true;
    }

    private void AcknowledgeHostedTerminalFinish(NativeBattleTerminalFinished receipt)
    {
        if (!ReferenceEquals(expandedHostedTerminal, receipt.Terminal) || !expandedTerminalEntered || Outcome != BattleOutcome.Active ||
            DebugOutcome is { } requested && receipt.Terminal.Outcome != requested)
        {
            throw new InvalidDataException("Hosted terminal completion does not match the pending canonical battle.");
        }
        if (receipt.Terminal.Outcome == BattleOutcome.Escaped)
        {
            nativeEscape = nativeEscape with { Completed = true };
        }
        FinalizeBattleOutcome(receipt.Terminal.Outcome);
        expandedHostedTerminal = null;
    }
}
