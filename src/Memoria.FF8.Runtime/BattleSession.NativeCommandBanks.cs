namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleCommandBanks nativeCommandBanks = NativeBattleCommandBanks.Initialize();
    private NativeBattleCommandLocation? nativeDispatchCommandLocation;
    public NativeBattleCommandBanks NativeCommandBanks => nativeCommandBanks;

    private void EnqueueNativeInputCommand(BattleAttackCommand command)
    {
        nativeCommandBanks = PrepareNativeInputCommand(command);
    }

    private NativeBattleCommandBanks PrepareNativeInputCommand(BattleAttackCommand command)
    {
        BattleCombatant actor = GetActor(command.ActorId);
        byte nativeCommand = command.NativeQueuedCommand ?? command.NativeAction?.Command ?? throw new InvalidDataException("Native4847F0 input requires its command BYTE.");
        byte bank = nativeCommand is 5 or 11 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 254 ? (byte)1 : (byte)2;
        ushort id = checked((ushort)(command.Ability == BattleAbility.GuardianForce ? command.ActionId + 64 : command.NativeRenzokuken is not null || command.NativeDuel is not null ? 0 : command.NativeAction?.ActionId ?? command.ActionId));
        byte flags2 = command.Ability == BattleAbility.Draw ? command.DrawCast ? (byte)9 : (byte)10 : (byte)0;
        byte flags3 = command.Ability == BattleAbility.Draw ? checked((byte)GetActor(command.TargetId).RequireNativeActorIndex()) : (byte)0;
        ushort?[] targets = ProjectNativeCommandTargets(command);
        (NativeBattleCommandBanks state, NativeBattleCommandLocation location) = nativeCommandBanks.Allocate(bank);
        state = state.WriteProjectedCommand(location, 0, checked((byte)actor.RequireNativeActorIndex()), nativeCommand, flags2, flags3, id, targets);
        return state.BindCommand(location, 0, command);
    }

    private ushort?[] ProjectNativeCommandTargets(BattleAttackCommand command)
    {
        ushort? Project(BattleTargetRequest request)
        {
            return NativeBattleTargetRequestAdapter.ProjectOriginalWord(request, ReadOriginalTargetSlot);
        }

        if (!command.SecondaryReflectionCasts.IsEmpty)
        {
            return command.SecondaryReflectionCasts.Select(cast => Project(cast.Command.TargetRequest)).ToArray();
        }

        return command.NativeSlots is { } slots
            ? Enumerable.Repeat(Project(command.TargetRequest), slots.Repetitions).ToArray()
            : command.MagicCasts.IsDefaultOrEmpty ? [Project(command.TargetRequest)] : command.MagicCasts.Select(cast => Project(cast.Targets)).ToArray();
    }



    private void ReleaseNativeDispatchedCommand()
    {
        if (nativeDispatchCommandLocation is { } location)
        {
            NativeBattleCommandPacket? diagnosticOriginal = diagnosticAftercare is null ? null : nativeCommandBanks.Banks[location.Bank].Packets[2 * location.Slot];
            nativeCommandBanks = nativeCommandBanks.Release(location);
            ObserveDiagnosticInvocationAftercareBankReleased(location, diagnosticOriginal);
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
        ushort?[] targets = ProjectNativeCommandTargets(command);
        nativeCommandBanks = nativeCommandBanks.WriteProjectedCommand(location, 1, checked((byte)actor.RequireNativeActorIndex()), command.NativeQueuedCommand ?? command.NativeAction!.Command, 0, 0, checked((byte)(command.NativeAction?.ActionId ?? command.ActionId)), targets).BindCommand(location, 1, command);
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
                    FlushEncounterCounterCommands();
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
        if (nativeShotProcesses.Count != 0)
        {
            (string actorId, BattleNativeShotState shot) = nativeShotProcesses.Single();
            if (shot.Phase != BattleNativeShotPhase.Opening)
            {
                // The native Shot UI directly submits 237/238 to the event that owns the loaded effect.
                return TryTakeHostedShotCommand(actorId, out command);
            }
        }
        while (actions.TryPeek(out BattleAttackCommand? queued))
        {
            if (queued.ExpandedAiCallback is not null || queued.RosterOperation is not null || queued.NativeDuel is not null || queued.NativeRenzokuken is not null)
            {
                command = queued;
                return true;
            }
            BattleAttackCommand? prepared = queued;
            if (RequiresExpandedCompelledReplacement(GetActor(queued.ActorId), queued))
            {
                // Keep the source command in the FIFO until replacement preparation commits.
                prepared = PrepareExpandedCompelledCommand(GetActor(queued.ActorId), queued);
            }
            actions.Dequeue();
            if (prepared is not null)
            {
                command = prepared;
                return true;
            }
        }
        command = null!;
        return false;
    }

    private void RemoveNativeQueuedActorCommands(BattleCombatant actor)
    {
        if (actor.NativeActorIndex is not int actorIndex)
        {
            return;
        }
        byte index = checked((byte)actorIndex);
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
                ValidateNativeEventOwner(command.ActorId, command.NativeHeader, command.NativeEventOwner, command.NativeAiCallbackStage, command.NativeAction);
                if (command.NativeCallback is { } callback)
                {
                    ValidateSavedNativeCallback(command);
                    if (physical.ReadByte(1) != 255 || callback.ActorIndex != physical.ReadByte(0) || callback.Stage != physical.ReadWord(4))
                    {
                        throw new InvalidDataException("Saved native callback differs from its physical packet.");
                    }
                    if (callback.Stage == 1 && bankIndex != 0)
                    {
                        busy.Add(command.ActorId);
                    }
                    continue;
                }
                if (physical.ReadByte(0) != GetActor(command.ActorId).NativeActorIndex)
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
                ValidateSavedAction(command.ActorId, command.TargetId, busy, command.Ability, command.ActionId, true, command.NativeAction, command.TargetRequest, command.NativeGraphic, command.NativeRenzokuken, command.NativePlayerReaction, allowNativeQueuedState: true, duel: command.NativeDuel, slots: command.NativeSlots, nativeQueuedCommand: command.NativeQueuedCommand);
                ValidateNativeMagicTargets(command.Ability, command.ActionId, command.MagicCasts, command.NativeMagicCastMode);
                ValidateNativeDrawPacket(command.Ability, command.ActionId, command.DrawCast, command.DrawCastTargetId, command.NativeDraw, false, command.NativeAction, command.TargetRequest);
                ValidateNativeActionInventory(GetActor(command.ActorId), command.Ability, command.NativeAction, command.ReservedNativeItemSlot, command.FreeNativeMagic);
            }
        }
    }
}
