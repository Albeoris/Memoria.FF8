namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool CanChoosePlayerCommand(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (Outcome != BattleOutcome.Active || HasPendingExpandedTerminal || !NativeEntryReady || !actor.IsPlayer ||
            GetGuardianForm(actorId) is { } form && (form.Phase != BattleGuardianFormPhase.Active || form.RemainingTicks == 0))
        {
            return false;
        }
        return !ExpandedTimedLimitOwnsInput && !RequiresNativeCompelledCommand(actor) && actor.IsReady && CanNativeBattleActorAct(actor);
    }

    public IReadOnlyList<BattleCombatant> ReadyPlayerCommandOrder => allies.Where(actor => CanChoosePlayerCommand(actor.Id)).ToArray();

    public void SelectPlayerCommandActor(string actorId)
    {
        if (PendingSlotsDeal is not null)
        {
            return;
        }
        if (!CanChoosePlayerCommand(actorId))
        {
            throw new InvalidOperationException("Player command selection requires an actually registered ready actor.");
        }
        if (GetActor(actorId).NativeLimit is not null)
        {
            RefreshNativeLimit(actorId);
        }
    }
}
