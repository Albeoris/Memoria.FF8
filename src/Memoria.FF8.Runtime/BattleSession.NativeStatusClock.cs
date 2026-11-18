namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool HasPendingExpandedDoom => combatants.Any(IsPendingDoomExpiration);

    private static bool IsPendingDoomExpiration(BattleCombatant actor)
    {
        return actor.IsActive && (NativeSavedStatusBits(actor) & 5) == 0 && actor.NativeStatusTimers[10] is <= 0 and not -1111;
    }

    private void DispatchNativeStatusTimers()
    {
        //47CCB0 rechecks1D27B00 after action dispatch, even when the earlier4842B0 pass admitted DEB.
        if (nativeBattleClock is { ActionPause: not 0 } || NativeScriptAllocationPaused)
        {
            return;
        }
        ObserveMegaSpark279StatusClockStack();
        // The caller already admitted the earlier DEB/UI gates.
        const int originalCount = NativeBattleSlots.Count;
        const int addedEnemyStart = originalCount + ExpandedBattleFormation.MaximumAllies;
        Span<int> occupied = stackalloc int[addedEnemyStart + ExpandedBattleFormation.MaximumEnemies];
        occupied.Clear();
        for (int row = 0; row < combatants.Count; row++)
        {
            BattleCombatant actor = combatants[row];
            if (!actor.IsActive || actor.Slot is not { } slot)
            {
                continue;
            }
            int position = actor.NativeActorIndex ?? (slot.Side == BattleSide.Allies ? originalCount : addedEnemyStart) + slot.Position;
            occupied[position] = row + 1;
        }
        foreach (int row in occupied)
        {
            if (row == 0)
            {
                continue;
            }
            BattleCombatant actor = combatants[row - 1];
            if ((NativeSavedStatusBits(actor) & 5) != 0)
            {
                continue;
            }
            for (int bit = 0; bit < actor.NativeStatusTimers.Length; bit++)
            {
                short timer = actor.NativeStatusTimers[bit];
                if (timer == -1111)
                {
                    continue;
                }
                uint mask = 1u << bit;
                if (timer > 0)
                {
                    uint statuses = NativeBattleStatusBits(actor);
                    int decrement = (statuses & 2) != 0 ? 3 : 2;
                    if ((statuses & 4) != 0)
                    {
                        decrement = 1;
                    }
                    if ((statuses & 8) != 0 ? bit != 3 : (statuses & 1) != 0 && bit != 0)
                    {
                        decrement = 0;
                    }
                    if (bit == 4 && decrement != 0 && timer / decrement % (60 / decrement) == 0)
                    {
                        //483470 enqueues callback6 before decrementing its signed timer.
                        QueueExpandedAiCallback(actor, 6);
                    }
                    actor.NativeStatusTimers[bit] = unchecked((short)(timer - decrement));
                    continue;
                }
                if (bit == 10)
                {
                    // The action lane admits finished countdowns in roster order before its next command.
                    continue;
                }
                actor.NativeStatusTimers[bit] = -1111;
                actor.NativeStatus1 &= ~mask;
                if (bit == 12)
                {
                    actor.NativeStatus0 |= 4;
                    RemoveNativeStatuses(actor, 0x7A, 0x30E77FF);
                    actor.Atb = 0;
                    Log("Status", actor.Id, actor.Id, 2, $"{actor.Name} is petrified.");
                }
                else
                {
                    Log("NativeStatusExpired", actor.Id, actor.Id, bit + 16, $"Native status {bit + 16} expired.");
                }
                actor.RefreshNativeHpStatusFlags();
                RefreshNativePresentationStatus(actor);
            }
        }
    }

    private bool TryDispatchPendingExpandedDoom()
    {
        if (nativeBattleClock is { ActionPause: not 0 } || NativeScriptAllocationPaused)
        {
            return false;
        }
        BattleCombatant? actor = combatants.FirstOrDefault(IsPendingDoomExpiration);
        if (actor is null)
        {
            return false;
        }

        // 483470 -> 4877F0 callback 5 -> 48D200 command 246 / action 43 / effect 9.
        BattleNativeAction action = new(246, 43, -1, -1) { Name = "Doom", MagicId = 9, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleActionPresentation command = new(actor.Id, actor.Id, BattleActionPhase.Windup, SelectBattleTarget(actor), BattleAbility.EnemyAbility, action.ActionId)
        {
            NativeAction = action,
            NativeQueuedCommand = action.Command,
            NativeSequence = 0,
            DiagnosticDoomExpiration = CaptureDiagnosticDoomExpiration(actor)
        };
        PrepareAndPublishExpandedAction(command);
        return true;
    }

    private void PrepareDoomExpiration(BattleCombatant actor)
    {
        if (!IsPendingDoomExpiration(actor))
        {
            throw new InvalidDataException("The Doom command no longer owns an expired status timer.");
        }
        actor.NativeStatusTimers[10] = -1111;
        actor.NativeStatus1 &= ~0x400u;
        StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 246, 43, 0, 3);
        actor.IsBusy = true;
        BeginNativeBattleActionPause();
    }

    private static bool IsNativeBattleCapable(BattleCombatant actor)
    {
        return actor.IsActive && (NativeSavedStatusBits(actor) & 5) == 0 && (NativeBattleStatusBits(actor) & 0x10000) == 0;
    }

    internal static bool CanNativeBattleActorAct(BattleCombatant actor)
    {
        return IsNativeBattleCapable(actor) && !actor.Suspended && (NativeBattleStatusBits(actor) & 9) == 0;
    }
}
