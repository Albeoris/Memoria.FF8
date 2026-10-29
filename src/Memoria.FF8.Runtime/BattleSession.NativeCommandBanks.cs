namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleCommandBanks nativeCommandBanks = NativeBattleCommandBanks.Initialize();
    private NativeBattleCommandLocation? nativeDispatchCommandLocation;
    public NativeBattleCommandBanks NativeCommandBanks => nativeCommandBanks;

    private void EnqueueNativeInputCommand(BattleAttackCommand command)
    {
        BattleCombatant actor = GetActor(command.ActorId);
        byte nativeCommand = command.NativeQueuedCommand ?? command.NativeAction?.Command ?? throw new InvalidDataException("Native4847F0 input requires its command BYTE.");
        byte bank = nativeCommand is 5 or 11 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 254 ? (byte)1 : (byte)2;
        ushort id = checked((ushort)(command.Ability == BattleAbility.GuardianForce ? command.ActionId + 64 : command.NativeRenzokuken is not null || command.NativeDuel is not null ? 0 : command.NativeAction?.ActionId ?? command.ActionId));
        byte flags2 = command.Ability == BattleAbility.Draw ? command.DrawCast ? (byte)9 : (byte)10 : (byte)0;
        byte flags3 = command.Ability == BattleAbility.Draw ? checked((byte)GetActor(command.TargetId).NativeIndex) : (byte)0;
        string selected = command.DrawCast ? command.DrawCastTargetId! : command.TargetId;
        ushort mask = command.NativeTargetMask != 0 ? command.NativeTargetMask : checked((ushort)(1 << GetActor(selected).NativeIndex));
        ushort[] targets = command.NativeSlots is { } slots ? Enumerable.Repeat(mask, slots.Repetitions).ToArray() : command.NativeMagicTargets.IsDefaultOrEmpty ? [mask] : command.NativeMagicTargets.ToArray();
        (NativeBattleCommandBanks state, NativeBattleCommandLocation location) = nativeCommandBanks.Allocate(bank);
        state = state.WriteCommand(location, 0, checked((byte)actor.NativeIndex), nativeCommand, flags2, flags3, id, targets);
        nativeCommandBanks = state.BindCommand(location, 0, command);
    }

    private bool TryTakeNativeBankCommand(out BattleAttackCommand command)
    {
        command = null!;
        if (nativeBattleClock?.ActionPause is > 0)
        {
            return false;
        }
        NativeBattleCommandLocation? location = nativeCommandBanks.FirstEligible(index =>
        {
            BattleCombatant actor = combatants.Single(candidate => candidate.NativeIndex == index);
            return (NativeSavedStatusBits(actor) & 4) != 0 || (NativeBattleStatusBits(actor) & 9) != 0;
        });
        if (location is null)
        {
            return false;
        }
        NativeBattleCommandBank bank = nativeCommandBanks.Banks[location.Bank];
        NativeBattleCommandPacket packet = bank.Packets[2 * location.Slot];
        if (packet.ReadByte(1) == 255)
        {
            BattleCombatant actor = combatants.Single(candidate => candidate.NativeIndex == packet.ReadByte(0));
            command = new(actor.Id, actor.Id, BattleAbility.EnemyAbility, 0) { NativeCallback = new(packet.ReadByte(0), packet.ReadWord(4)) };
        }
        else
        {
            command = bank.LogicalCommands[2 * location.Slot] ?? throw new NotSupportedException("Native485610 packet requires its immutable action binding.");
            if (packet.ReadByte(0) != GetActor(command.ActorId).NativeIndex)
            {
                throw new InvalidDataException("Native485610 binding contradicts its physical actor BYTE.");
            }
        }
        nativeDispatchCommandLocation = location;
        ObserveDiagnosticCounterCallbackTaken(location, packet);
        //485460 clears DF8 once at the selected event, not between its two485610 subcommands.
        BeginNativeReactionBatch();
        return true;
    }

    private void ReleaseNativeDispatchedCommand()
    {
        if (nativeDispatchCommandLocation is { } location)
        {
            nativeCommandBanks = nativeCommandBanks.Release(location);
            nativeDispatchCommandLocation = null;
            diagnosticCounterCurrentCallback = null;
        }
    }

    private bool TryWriteNativeSecondCommand(BattleAttackCommand command)
    {
        if (nativeDispatchCommandLocation is not { } location)
        {
            return false;
        }
        BattleCombatant actor = GetActor(command.ActorId);
        ushort[] targets = command.NativeMagicTargets.IsDefaultOrEmpty ? [command.NativeTargetMask] : command.NativeMagicTargets.ToArray();
        nativeCommandBanks = nativeCommandBanks.WriteCommand(location, 1, checked((byte)actor.NativeIndex), command.NativeQueuedCommand ?? command.NativeAction!.Command, 0, 0, checked((byte)(command.NativeAction?.ActionId ?? command.ActionId)), targets).BindCommand(location, 1, command);
        return true;
    }

    private void DispatchNativeSecondaryBankCommand()
    {
        if (nativeDispatchCommandLocation is not { } location)
        {
            return;
        }
        NativeBattleCommandBank bank = nativeCommandBanks.Banks[location.Bank];
        if (bank.Packets[2 * location.Slot + 1].ReadByte(0) != 255)
        {
            BattleAttackCommand command = bank.LogicalCommands[2 * location.Slot + 1] ?? throw new InvalidDataException("Native485610 second subcommand lacks its immutable binding.");
            if (command.NativeQueuedCommand == 241 && command.NativeLimitCallback == NativeLimitCallback.DuelOpening)
            {
                NativeBattleActionProductionContext? previous = nativeProductionContext;
                nativeProductionContext = new(0, location.Bank, false, 0x47DE70);
                try
                {
                    actions.Enqueue(ProduceNativeQueuedCommand(GetActor(command.ActorId), GetActor(command.TargetId), command));
                }
                finally
                {
                    nativeProductionContext = previous;
                }
            }
            else
            {
                ProduceNativeReflectedBankCommand(command);
            }
        }
    }

    private bool TryTakeQueuedCommand(out BattleAttackCommand command)
    {
        if (actions.TryDequeue(out BattleAttackCommand? queued))
        {
            command = queued;
            return true;
        }
        return TryTakeNativeBankCommand(out command);
    }

    private void RemoveNativeQueuedActorCommands(BattleCombatant actor)
    {
        byte index = checked((byte)actor.NativeIndex);
        foreach (BattleAttackCommand command in nativeCommandBanks.Banks.Skip(1).SelectMany(bank => Enumerable.Range(0, 11).Where(slot => bank.Packets[2 * slot].ReadByte(0) == index).SelectMany(slot => bank.LogicalCommands.Skip(2 * slot).Take(2))).OfType<BattleAttackCommand>())
        {
            RefundCancelledNativeItem(command);
        }
        nativeCommandBanks = nativeCommandBanks.RemoveActor(index);
    }

    private void ClearNativeQueuedCommands()
    {
        foreach (BattleAttackCommand command in nativeCommandBanks.BoundCommands)
        {
            RefundCancelledNativeItem(command);
        }
        for (byte bank = 0; bank < 3; bank++)
        {
            for (byte slot = 0; slot < 11; slot++)
            {
                NativeBattleCommandLink link = nativeCommandBanks.Banks[bank].Links[slot];
                if (link.Previous != 0 || link.Next != 0)
                {
                    nativeCommandBanks = nativeCommandBanks.Release(new(bank, slot));
                }
            }
        }
        nativeDispatchCommandLocation = null;
    }

    private void ValidateNativeCommandBindings(HashSet<string> busy)
    {
        for (int bankIndex = 0; bankIndex < nativeCommandBanks.Banks.Length; bankIndex++)
        {
            NativeBattleCommandBank bank = nativeCommandBanks.Banks[bankIndex];
            for (int index = 0; index < 22; index++)
            {
                NativeBattleCommandPacket physical = bank.Packets[index];
                if (physical.ReadByte(0) == 255)
                {
                    continue;
                }
                BattleAttackCommand command = bank.LogicalCommands[index] ?? throw new InvalidDataException("Saved native command packet lacks its immutable action binding.");
                if (command.NativeCallback is { } callback)
                {
                    ValidateSavedNativeCallback(command);
                    if (physical.ReadByte(1) != 255 || callback.ActorIndex != physical.ReadByte(0) || callback.Stage != physical.ReadWord(4))
                    {
                        throw new InvalidDataException("Saved native callback differs from its physical packet.");
                    }
                    continue;
                }
                if (physical.ReadByte(0) != GetActor(command.ActorId).NativeIndex)
                {
                    throw new InvalidDataException("Saved native command binding contradicts its physical actor BYTE.");
                }
                if (physical.ReadByte(1) != (command.NativeQueuedCommand ?? command.NativeAction?.Command))
                {
                    throw new InvalidDataException("Saved native command differs from its physical command BYTE.");
                }
                if (command.Ability == BattleAbility.GuardianForce)
                {
                    ValidateNativeGuardianInputBinding(command, physical, bankIndex);
                }
                ValidateGuardianFormCommand(command);
                ValidateSavedAction(command.ActorId, command.TargetId, busy, command.Ability, command.ActionId, true, command.NativeAction, command.NativeTargetMask, command.NativeGraphic, command.NativeRenzokuken, command.NativePlayerReaction, allowNativeQueuedState: true, duel: command.NativeDuel, slots: command.NativeSlots);
                ValidateNativeMagicTargets(command.Ability, command.ActionId, command.NativeMagicTargets);
                ValidateNativeDrawPacket(command.Ability, command.ActionId, command.DrawCast, command.DrawCastTargetId, command.NativeDraw, false, command.NativeAction, command.NativeTargetMask);
                ValidateNativeActionInventory(GetActor(command.ActorId), command.Ability, command.NativeAction, command.ReservedNativeItemSlot, command.FreeNativeMagic);
            }
        }
    }
}
