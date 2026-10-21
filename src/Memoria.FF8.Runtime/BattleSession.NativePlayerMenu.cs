namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public bool UsesNativeReadyMenu => NativeGraphics is not null && nativeBattleClock is not null;

    public bool CanChoosePlayerCommand(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        if (Outcome != BattleOutcome.Active || !NativeEntryReady || !actor.IsPlayer || GetGuardianForm(actorId) is { } form && (form.Phase != BattleGuardianFormPhase.Active || form.RemainingTicks == 0))
        {
            return false;
        }
        if (!UsesNativeReadyMenu)
        {
            return actor.IsReady && CanNativeBattleActorAct(actor);
        }
        NativeBattleUiServices ui = NativeGraphics!.UiServices ?? throw new InvalidDataException("Native command admission lost its initialized UI owner.");
        return !actor.IsBusy && actor.NativeIndex is >= 0 and < 3 && (ui.ReadyMask & (1u << actor.NativeIndex)) != 0;
    }

    public IReadOnlyList<BattleCombatant> ReadyPlayerCommandOrder
    {
        get
        {
            if (!UsesNativeReadyMenu)
            {
                return combatants.Where(actor => actor.IsPlayer && CanChoosePlayerCommand(actor.Id)).ToArray();
            }
            NativeBattleUiCommands? commands = NativeGraphics!.UiServices?.Commands;
            List<BattleCombatant> result = [];
            for (int? next = commands?.ReadyHead; next is int index; next = commands!.ReadyNodes[index].Next)
            {
                BattleCombatant? actor = combatants.SingleOrDefault(candidate => candidate.NativeIndex == index && candidate.IsPlayer);
                if (actor is not null && CanChoosePlayerCommand(actor.Id))
                {
                    result.Add(actor);
                }
            }
            return result;
        }
    }

    public string? SelectedPlayerCommandActorId
    {
        get
        {
            if (!UsesNativeReadyMenu)
            {
                return null;
            }
            sbyte? selected = NativeGraphics!.UiServices?.Commands.SelectedActor;
            return selected is >= 0 ? combatants.SingleOrDefault(actor => actor.IsPlayer && actor.NativeIndex == selected && CanChoosePlayerCommand(actor.Id))?.Id : null;
        }
    }

    public void SelectPlayerCommandActor(string actorId)
    {
        if (!CanChoosePlayerCommand(actorId))
        {
            throw new InvalidOperationException("Player command selection requires an actually registered ready actor.");
        }
        if (GetActor(actorId).NativeLimit is not null)
        {
            RefreshNativeLimit(actorId);
        }
        if (UsesNativeReadyMenu)
        {
            NativeBattleGraphicsState graphics = RequireNativeGraphics();
            NativeBattleUiServices ui = graphics.UiServices!;
            //4BB9E0 state19 writes byte1D76844 from the ordered4AD370 ready list;957 remains the command menu.
            NativeGraphics = graphics with { UiServices = ui with { Commands = ui.Commands with { SelectedActor = checked((sbyte)GetActor(actorId).NativeIndex) } } };
        }
    }
}
