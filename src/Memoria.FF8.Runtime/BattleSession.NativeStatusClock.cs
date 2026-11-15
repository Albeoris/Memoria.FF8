namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool HasExpandedDoomPresentation => expandedPresentationProcess is not null || expandedTurnClockObservation is not null;

    private bool HasPendingExpandedDoom => HasExpandedDoomPresentation && combatants.Any(IsPendingDoomExpiration);

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
        ObserveNativeUnknownStackCallback(0x483470, "admitted status clock");
        // The caller already admitted the earlier DEB/UI gates.
        foreach (BattleCombatant actor in combatants.Where(candidate => candidate.IsActive && (UsesExpandedRoster || HasNativeRosterSlot(candidate))))
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
                        //483470 requests callback6; its later0/4 command owns HP and presentation contacts.
                        if (UsesExpandedRoster)
                        {
                            BattleNativeAction regen = NativePostActionEffect(4);
                            BattleActionDefinition action = new(BattleAbility.EnemyAbility, 4, regen.Name!, BattleActionEffect.Native, 0, true) { NativeAction = regen };
                            ResolveInstalledNativeHitWithContact(actor, actor, new(actor.Id, actor.Id, BattleActionPhase.Recovery, 1, SelectBattleTarget(actor), action.Ability, action.Id) { NativeAction = regen }, action, regen);
                        }
                        else
                        {
                            QueueNativeCallback(actor, 6);
                        }
                    }
                    actor.NativeStatusTimers[bit] = unchecked((short)(timer - decrement));
                    continue;
                }
                if (bit == 10 && HasExpandedDoomPresentation)
                {
                    // The action lane admits finished countdowns in roster order before its next command.
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
                    if (NativeGraphics is { } graphics && actor.NativeActorIndex is int physicalIndex)
                    {
                        byte index = checked((byte)physicalIndex);
                        BattleModelOwner owner = CreatePresentationModelOwner(actor);
                        (ushort saved, uint battle) = NativeContactStatusMirror(actor);
                        graphics = PublishNativeReadyStatus(graphics, new(owner, 0, 0, 0, saved, 0, battle));
                        //483470 ->47E250 refreshes the mirrors and queues50A070 through descriptor117.
                        RefreshNativePresentationStatus(actor);
                        NativeGraphics = QueueNativeModelDescriptor(graphics, 117, NativeParticipantAddress(index), owner);
                    }
                }
            }
        }
    }

    private bool TryDispatchPendingExpandedDoom()
    {
        if (expandedPresentationProcess is null || nativeBattleClock is { ActionPause: not 0 } || NativeScriptAllocationPaused)
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
        BattleActionPresentation command = new(actor.Id, actor.Id, BattleActionPhase.Windup, 1, SelectBattleTarget(actor), BattleAbility.EnemyAbility, action.ActionId)
        {
            NativeAction = action,
            NativeQueuedCommand = action.Command,
            NativeSequence = 0
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

    private void ApplyDiagnosticPeriodicStatus(BattleCombatant actor, int bit, short timer, int decrement, short timerAfter, int amount, bool damage, string source, uint? battleStatuses = null)
    {
        BattleDiagnosticPeriodicStatus? previous = diagnosticPeriodicStatus;
        if (DiagnosticResolution is not null && actor.NativeActorIndex is int physicalIndex)
        {
            diagnosticPeriodicStatus = new(0x483470, actor.Id, checked((byte)physicalIndex), bit, timer, decrement, timerAfter, NativeSavedStatusBits(actor), battleStatuses ?? NativeBattleStatusBits(actor), actor.MaximumHp, amount, NativeHostCallbacks ?? Frame);
        }
        try
        {
            if (damage)
            {
                ApplyDamage(null, actor, amount, source, cause: bit == 10 ? EncounterHpCause.DeathStatus : EncounterHpCause.Damage);
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
