namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void DispatchNativeStatusTimers()
    {
        //47CCB0 rechecks1D27B00 after action dispatch, even when the earlier4842B0 pass admitted DEB.
        if (nativeBattleClock is { ActionPause: not 0 } || NativeScriptAllocationPaused)
        {
            return;
        }
        ObserveNativeUnknownStackCallback(0x483470, "admitted status clock");
        // The caller already admitted the earlier DEB/UI gates.
        foreach (BattleCombatant actor in combatants.Where(candidate => candidate.IsActive && candidate.NativeIndex is >= 0 and < 7))
        {
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
                        int amount = actor.MaximumHp / 20;
                        if ((NativeSavedStatusBits(actor) & 0x40) != 0)
                        {
                            if (amount >= actor.CurrentHp)
                            {
                                StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 0, 4, 0, 3);
                            }
                            ApplyDiagnosticPeriodicStatus(actor, bit, timer, decrement, unchecked((short)(timer - decrement)), amount, true, "Regen");
                        }
                        else
                        {
                            ApplyDiagnosticPeriodicStatus(actor, bit, timer, decrement, unchecked((short)(timer - decrement)), amount, false, "Regen");
                        }
                    }
                    actor.NativeStatusTimers[bit] = unchecked((short)(timer - decrement));
                    continue;
                }
                uint expiringStatuses = NativeBattleStatusBits(actor);
                actor.NativeStatusTimers[bit] = -1111;
                actor.NativeStatus1 &= ~mask;
                if (bit == 10)
                {
                    //483470 queues4877F0 callback5 -> command246;48FE20 sets the death bit without a resistance roll.
                    StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 246, 43, 0, 3);
                    ApplyDiagnosticPeriodicStatus(actor, bit, timer, 0, -1111, actor.CurrentHp, true, "Doom", expiringStatuses);
                }
                else if (bit == 12)
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
                if (bit != 10)
                {
                    actor.RefreshNativeHpStatusFlags();
                    RefreshNativePresentationStatus(actor);
                    if (NativeGraphics is { } graphics)
                    {
                        byte index = checked((byte)actor.NativeIndex);
                        (ushort saved, uint battle) = NativeContactStatusMirror(actor);
                        graphics = PublishNativeReadyStatus(graphics, new(index, 0, 0, 0, saved, 0, battle));
                        //483470 ->47E250 refreshes the mirrors and queues50A070 through descriptor117.
                        RefreshNativePresentationStatus(actor);
                        NativeGraphics = QueueNativeDescriptor(graphics, 117, NativeParticipantAddress(index), index);
                    }
                }
            }
        }
    }

    private void ApplyDiagnosticPeriodicStatus(BattleCombatant actor, int bit, short timer, int decrement, short timerAfter, int amount, bool damage, string source, uint? battleStatuses = null)
    {
        BattleDiagnosticPeriodicStatus? previous = diagnosticPeriodicStatus;
        if (DiagnosticResolution is not null)
        {
            diagnosticPeriodicStatus = new(0x483470, actor.Id, checked((byte)actor.NativeIndex), bit, timer, decrement, timerAfter, NativeSavedStatusBits(actor), battleStatuses ?? NativeBattleStatusBits(actor), actor.MaximumHp, amount, NativeHostCallbacks ?? Frame);
        }
        try
        {
            if (damage)
            {
                ApplyDamage(null, actor, amount, source);
            }
            else
            {
                ApplyHealing(actor, amount, source);
            }
        }
        finally
        {
            diagnosticPeriodicStatus = previous;
        }
    }

    private void CompleteNativeActionStatuses(BattleCombatant actor)
    {
        //484180 runs after the actor's command, before47DE70 publishes pending counter callbacks.
        if (actor.CurrentHp == 0 || (actor.NativeStatus0 & 3) != 2)
        {
            return;
        }
        int damage = (NextRandomByte() % 5 + 5) * actor.MaximumHp / 100;
        if (damage >= actor.CurrentHp)
        {
            StoreNativeActionMemory(actor, actor, BattleAbility.EnemyAbility, 0, 2, 0, 3);
        }
        ApplyDamage(null, actor, damage, "Poison");
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
