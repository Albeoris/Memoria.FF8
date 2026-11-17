namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleOutcome? DebugOutcome { get; private set; }

    public bool CanAdmitDebugOutcome => Outcome == BattleOutcome.Active && DebugOutcome is null && !dispatchingExpandedPresentation &&
        DiagnosticTerminalHold is null && !HasPendingExpandedTerminal && !HasPendingTurn && !HasPendingEncounterRetirements &&
        !HasNativePendingReactions && nativeActionCommitDepth == 0 && pendingSlotsDeal is null && !ExpandedTimedLimitOwnsInput &&
        !HasExpandedHostedGuardianFormEntry && !nativeEscape.Held && !nativeEscape.PresentationHeld &&
        Presentation is { CanPrepareDebugTerminal: true };

    private void AdmitDebugOutcome(BattleOutcome requested)
    {
        if (!CanAdmitDebugOutcome)
        {
            throw new InvalidOperationException("A debug outcome requires an entered battle with drained action, presentation and input owners.");
        }
        BattlePresentationProcess process = Presentation!;
        BattlePresentationProcess.PreparedTerminal prepared = process.PrepareTerminal(requested, EscapeFlags);
        process.ValidateTerminal(prepared);
        bool defeat = requested == BattleOutcome.Defeat;
        foreach (BattleCombatant actor in defeat ? allies : enemies)
        {
            actor.CurrentHp = 0;
            actor.NativeStatus0 |= 1;
            actor.Atb = 0;
        }
        DebugOutcome = requested;
        PublishExpandedHostedTerminal(process, prepared);
        Log(defeat ? "debug-defeat" : "debug-victory", null, null, 0, defeat ? "Debug shortcut: all party members defeated." : "Debug shortcut: all enemies defeated.");
    }

    private void RestoreDebugOutcome(BattleSnapshot snapshot, HostedBattleTerminal? preparedTerminal)
    {
        if (snapshot.DebugOutcome is not null and not (BattleOutcome.Victory or BattleOutcome.Defeat))
        {
            throw new InvalidDataException("An explicit debug outcome must select Victory or Defeat.");
        }
        if (snapshot.DebugOutcome is BattleOutcome requested &&
            (snapshot.Outcome != requested && !(snapshot.Outcome == BattleOutcome.Active && preparedTerminal?.Outcome == requested) ||
            combatants.Any(actor => (requested == BattleOutcome.Defeat ? actor.IsPlayer : !actor.IsPlayer) && actor.CurrentHp != 0)))
        {
            throw new InvalidDataException("The saved debug outcome disagrees with its terminal HP and outcome owners.");
        }
        DebugOutcome = snapshot.DebugOutcome;
    }
}
