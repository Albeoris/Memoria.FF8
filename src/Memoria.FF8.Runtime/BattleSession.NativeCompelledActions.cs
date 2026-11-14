namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record NativeCompelledCommand(byte Command, ushort ActionId, ushort TargetMask, bool FreeMagic)
    {
        public BattleGuardianFormActionBinding? GuardianFormBinding { get; init; }
    }

    private static bool RequiresNativeCompelledCommand(BattleCombatant actor)
    {
        return actor.IsPlayer && ((NativeSavedStatusBits(actor) & 0x20) != 0 || (NativeBattleStatusBits(actor) & 0x4000) != 0 || HasNativeAngelWing(actor));
    }

    private static bool HasNativeAngelWing(BattleCombatant actor)
    {
        return (NativeBattleStatusBits(actor) & 0x2000000) != 0;
    }

    private void DispatchNativeCompelledActors()
    {
        if (UsesExpandedRoster)
        {
            if (!ExpandedTimedLimitOwnsInput)
            {
                DispatchExpandedAngelWingActors();
                foreach (BattleCombatant actor in allies.Where(actor => actor.IsReady && CanNativeBattleActorAct(actor) && RequiresNativeCompelledCommand(actor) && !HasNativeAngelWing(actor)))
                {
                    BattleAttackCommand? command = PrepareExpandedCompelledCommand(actor);
                    if (command is not null)
                    {
                        actions.Enqueue(command);
                    }
                }
            }
            return;
        }
        if (HasNativeUiServiceClock)
        {
            //483EB0 already queued the original command at full-gauge registration, preserving its gauge until completion.
            return;
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsReady && RequiresNativeCompelledCommand(actor)))
        {
            BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.IsActive && candidate.EncodeNativeSlot() is >= 3 and < 7) ?? actor;
            EnqueueAttack(actor, target);
        }
    }

    private BattleAttackCommand PrepareNativeQueuedCommand(BattleCombatant actor, BattleAttackCommand original)
    {
        original = RevalidateSmartAttack(actor, original);
        if (original.NativePlayerReaction is not null)
        {
            return original;
        }
        if (RequiresNativeCompelledCommand(actor))
        {
            ReleaseMagicReservation(original.MagicReservationId);
        }
        NativeCompelledCommand? prepared = PrepareNativeCompelledCommand(actor);
        if (prepared is null)
        {
            return original;
        }
        ReleaseMagicReservation(original.MagicReservationId);
        int? reserved = prepared.Command == 4 ? ConsumeNativeItem(prepared.ActionId) : null;
        if (original.ReservedNativeItemSlot is int previousSlot)
        {
            RefundNativeReservedItem(previousSlot, original.NativeAction?.ActionId ?? original.ActionId);
        }
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.EncodeNativeSlot() is >= 0 and < 7 && (prepared.TargetMask & (1 << candidate.EncodeNativeSlot())) != 0) ?? actor;
        BattleNativeAction? native = prepared.Command == 1 ? actor.NativeAttack : FindNativeCommandAction(actor, prepared.Command, prepared.ActionId);
        BattleAbility ability = prepared.GuardianFormBinding?.Source == BattleGuardianFormActionSource.Magic ? BattleAbility.Magic : prepared.Command is 1 or 12 ? BattleAbility.Attack : BattleAbility.EnemyAbility;
        return new BattleAttackCommand(actor.Id, target.Id, ability, prepared.ActionId)
        {
            GuardianFormAction = prepared.GuardianFormBinding?.Action,
            NativeSequence = prepared.GuardianFormBinding?.Sequence,
            NativeLimitCallback = original.NativeLimitCallback,
            NativeAction = native,
            NativeQueuedCommand = prepared.Command,
            NativeTargetMask = prepared.TargetMask,
            FreeNativeMagic = prepared.FreeMagic,
            ReservedNativeItemSlot = reserved
        };
    }

    private NativeCompelledCommand? PrepareNativeCompelledCommand(BattleCombatant actor)
    {
        if (!actor.IsPlayer)
        {
            return null;
        }
        uint battleStatus = NativeBattleStatusBits(actor);
        if (GetGuardianForm(actor.Id) is { Phase: BattleGuardianFormPhase.Active, RemainingTicks: > 0 } form && RequiresNativeCompelledCommand(actor))
        {
            BattleGuardianFormDefinition profile = BattleGuardianFormProfiles.Find(form.GuardianForceId)!;
            BattleGuardianFormActionBinding binding = (battleStatus & 0x2000000) != 0 ? profile.Actions.First(action => action.Source == BattleGuardianFormActionSource.Magic) : profile.Actions.First(action => action.Source == BattleGuardianFormActionSource.Physical);
            BattleNativeAction native = GetGuardianFormActionDefinition(actor.Id, binding.Action).NativeAction!;
            ushort mask = binding.Source == BattleGuardianFormActionSource.Physical ? ResolveNativeAiTargetMask(actor, (battleStatus & 0x4000) != 0 ? (byte)201 : (byte)202) : NativeCompelledTargetMask(actor, native.TargetFlags ?? throw new InvalidDataException("GF compelled spell lacks its installed targeting flags."), true);
            return new(native.Command, native.ActionId, mask, binding.Cost == BattleGuardianFormActionCost.Innate) { GuardianFormBinding = binding };
        }
        if (HasNativeAngelWing(actor))
        {
            return SelectNativeAngelWingSpell(actor) is byte spell
                ? new(2, spell, NativeCompelledTargetMask(actor, RequiredNativeSpellFlags(spell), true), true)
                : new(1, 0, ResolveNativeAiTargetMask(actor, 202), false);
        }
        if ((NativeSavedStatusBits(actor) & 0x20) != 0)
        {
            return new(1, 0, ResolveNativeAiTargetMask(actor, (battleStatus & 0x4000) != 0 ? (byte)201 : (byte)202), false);
        }
        if ((battleStatus & 0x4000) == 0)
        {
            return null;
        }
        var selection = SelectNativeConfuseCommand(actor);
        ushort targetMask = selection.TargetFlags is byte flags
            ? NativeCompelledTargetMask(actor, flags, selection.OppositeSide)
            : ResolveNativeAiTargetMask(actor, selection.OppositeSide ? (byte)202 : (byte)201);
        return new(selection.Command, selection.ActionId, targetMask, false);
    }

    private (byte Command, ushort ActionId, byte? TargetFlags, bool OppositeSide) SelectNativeConfuseCommand(BattleCombatant actor)
    {
        if (actor.NativeCommandSlots.Length != 4)
        {
            throw new NotSupportedException("Native Confuse requires all four original physical command slots.");
        }
        byte rules = definition.NativeFieldBattleRules ?? throw new NotSupportedException("Native Confuse requires the current field battle prohibition byte.");
        int commandSlot = NextRandomByte() & 3;
        for (int visited = 0; visited < 4; visited++)
        {
            int side = NextRandomByte() % 3;
            byte command = actor.NativeCommandSlots[commandSlot];
            switch (command)
            {
                case 1:
                case 12:
                    return (command, 0, null, side == 0);
                case 2:
                    if ((rules & 2) == 0)
                    {
                        NativeCharacterVitals? stockOwner = actor.MagicOwner?.Invoke();
                        bool logicalStock = stockOwner is not null && actor.MagicRecordId is int record && (stockOwner.MagicSystem.SharedMagicStock || stockOwner.MagicSystem.InfiniteMagic || stockOwner.MagicSystem.UnlimitedMagic || record == 0 && stockOwner.MagicSystem.RecordZeroExtension.Length != 0);
                        NativeMagicStock[] candidates = logicalStock ? actor.MagicStocks.Where(stock => MagicCastingService.Quote(stockOwner!, actor.MagicRecordId!.Value, stock.Id, MagicCastContext.Battle).Allowed).Select(stock => new NativeMagicStock((byte)stock.Id, (byte)stock.Count)).ToArray() : actor.NativeMagicInventory;
                        int occupied = candidates.Count(stock => stock.SpellId != 0);
                        if (occupied != 0)
                        {
                            int slot = NextRandomByte() % occupied;
                            while (candidates[slot].SpellId == 0)
                            {
                                slot = (slot + 1) % candidates.Length;
                            }
                            byte spell = candidates[slot].SpellId;
                            return (2, spell, RequiredNativeSpellFlags(spell), side == 0);
                        }
                    }
                    break;
                case 4:
                    if ((rules & 1) == 0)
                    {
                        int item = SelectNativeConfuseItem();
                        if (item != 255)
                        {
                            BattleNativeAction action = FindInstalledNativeAction(4, (ushort)item);
                            byte flags = action.TargetFlags ?? throw new InvalidDataException("Native Confuse item lacks original target flags.");
                            return (4, (ushort)item, flags, side == 0);
                        }
                    }
                    break;
            }
            commandSlot = (commandSlot + 1) & 3;
        }
        throw new NotSupportedException("Native Confuse command rejection has no supported available physical command and would not terminate.");
    }

    private static bool RequiresExpandedCompelledReplacement(BattleCombatant actor, BattleAttackCommand command)
    {
        return RequiresNativeCompelledCommand(actor) && !HasNativeAngelWing(actor) && command.NativePlayerReaction is null &&
            command.NativeLimitCallback is null && command.AiRecipients?.TargetPolicy != BattleAiTargetPolicy.Compelled;
    }

    private BattleAttackCommand? PrepareExpandedCompelledCommand(BattleCombatant actor, BattleAttackCommand? previous = null)
    {
        if (previous is not null)
        {
            // The native invocation discards the buffered input and releases its reservation before replacement.
            RefundCancelledNativeItem(previous);
        }
        bool confused = (NativeBattleStatusBits(actor) & 0x4000) != 0;
        var selection = (NativeSavedStatusBits(actor) & 0x20) != 0
            ? (Command: (byte)1, ActionId: (ushort)0, TargetFlags: (byte?)null, OppositeSide: !confused)
            : SelectNativeConfuseCommand(actor);
        BattleAiRecipients recipients = SelectExpandedCompelledRecipients(selection.TargetFlags, selection.OppositeSide);
        if (recipients.ActorIds.IsEmpty)
        {
            actor.Atb = 0;
            actor.IsBusy = false;
            return null;
        }

        string targetId = recipients.ActorIds[0];
        BattleAbility ability = selection.Command switch
        {
            2 => BattleAbility.Magic,
            4 => BattleAbility.Item,
            _ => BattleAbility.Attack
        };
        BattleNativeAction? native = selection.Command == 1 && actor.NativeAttack is null ? null : FindNativeCommandAction(actor, selection.Command, selection.ActionId);
        if (ability is BattleAbility.Magic or BattleAbility.Item)
        {
            RequireExpandedAction(FindAction(ability, selection.ActionId));
        }
        if (selection.Command == 12 && GetActor(targetId) is { IsPlayer: false, NativeMug: null })
        {
            throw new NotSupportedException("Expanded Confuse Mug requires the recipient's installed steal table.");
        }
        byte? sequence = native is null ? null : NativeBattleActionGraphics.ResolvePlayerSequence(selection.Command, 0, selection.ActionId, definition.NativeCommandSequences);
        if (native is not null)
        {
            ValidateNativeActionContract(native);
        }
        BattleAttackCommand command = new(actor.Id, targetId, ability, selection.ActionId) { NativeQueuedCommand = selection.Command, NativeAction = native, NativeSequence = sequence, AiRecipients = recipients };
        command = EnsureMagicReservation(actor, command);
        if (ability == BattleAbility.Item)
        {
            command = command with { ReservedNativeItemSlot = ConsumeNativeItem(selection.ActionId) };
        }
        OnNativePlayerCommandSubmitted(actor, selection.Command);
        actor.Atb = 0;
        actor.IsBusy = true;
        nativeLimitLevels.Remove(actor.Id);
        Log("Queued", actor.Id, targetId, 0, $"{actor.Name}: {native?.Name ?? ActionName(ability, selection.ActionId)} queued.");
        return command;
    }

    private BattleAiRecipients SelectExpandedCompelledRecipients(byte? targetFlags, bool oppositeSide)
    {
        byte flags = targetFlags.GetValueOrDefault();
        bool selectsBothSides = (flags & 0x30) == 0x20;
        bool randomizes = (flags & 2) != 0;
        bool targetsAllies = targetFlags is null ? !oppositeSide : ((flags & 0x40) != 0) != oppositeSide;
        BattleSide side = targetsAllies ? BattleSide.Allies : BattleSide.Enemies;
        if (selectsBothSides && randomizes)
        {
            side = BattleSide.Enemies;
        }
        IEnumerable<BattleCombatant> roster = selectsBothSides && !randomizes ? combatants : Roster(side);
        BattleAiRecipients selected = targetFlags is null || (flags & 0x30) == 0x10
            ? PickExpandedAiTarget(roster.Where(IsExpandedAiTarget))
            : new([.. roster.Where(candidate => IsExpandedCompelledTarget(candidate, flags)).Select(candidate => candidate.Id)]);
        return selected with { TargetPolicy = BattleAiTargetPolicy.Compelled, RandomSide = randomizes ? side : null };
    }

    private static bool IsExpandedCompelledTarget(BattleCombatant actor, byte flags)
    {
        return actor.IsActive && !actor.Untargetable && !actor.IsEjected && (actor.IsAlive || (flags & 1) != 0 && actor.IsPlayer);
    }

    private byte? SelectNativeAngelWingSpell(BattleCombatant actor)
    {
        //483D60 scans the stocked spells from RNG & 31. Logical stock calls use their own bounded bank.
        (NativeMagicStock[] inventory, bool logical, NativeCharacterVitals? owner) = NativeAngelWingInventory(actor);
        bool Eligible(NativeMagicStock stock) => IsNativeAngelWingSpell(actor, stock.SpellId, owner);
        if (!inventory.Any(Eligible))
        {
            return null;
        }
        int slot = logical ? NextRandomByte() % inventory.Length : NextRandomByte() & 31;
        while (!Eligible(inventory[slot]))
        {
            slot = (slot + 1) % inventory.Length;
        }
        return inventory[slot].SpellId;
    }

    private static (NativeMagicStock[] Inventory, bool Logical, NativeCharacterVitals? Owner) NativeAngelWingInventory(BattleCombatant actor)
    {
        NativeCharacterVitals? owner = actor.MagicOwner?.Invoke();
        bool logical = owner is not null && (owner.MagicSystem.SharedMagicStock || owner.MagicSystem.RecordZeroExtension.Length != 0 && actor.MagicRecordId == 0);
        return (logical ? actor.MagicStocks.Select(stock => new NativeMagicStock((byte)stock.Id, (byte)stock.Count)).ToArray() : actor.NativeMagicInventory, logical, owner);
    }

    private bool IsNativeAngelWingSpell(BattleCombatant actor, byte spell, NativeCharacterVitals? owner)
    {
        return spell != 0 && (RequiredNativeSpellFlags(spell) & 0x40) != 0 && (owner is null || actor.MagicRecordId is not int row || MagicJunctionPolicy.CanAccess(owner, row, spell));
    }

    private int SelectNativeConfuseItem()
    {
        bool Eligible(NativeItemStock stock)
        {
            if (stock.ItemId == 0)
            {
                return false;
            }
            BattleNativeAction action = FindInstalledNativeAction(4, stock.ItemId);
            if (action.RawRecord.Length != 24)
            {
                throw new NotSupportedException("Native Confuse item eligibility requires the original24-byte kernel record.");
            }
            return (action.RawRecord[21] & 1) != 0;
        }
        int count = nativeItems.Count(Eligible);
        if (count == 0)
        {
            return 255;
        }
        int index = NextRandomByte() % count;
        for (int scanned = 0; scanned <= 32; scanned++)
        {
            if (Eligible(nativeItems[index]))
            {
                return nativeItems[index].ItemId;
            }
            index = (index + 1) & 31;
        }
        throw new NotSupportedException("Native Confuse item rejection cannot reach an eligible physical inventory slot.");
    }

    private byte RequiredNativeSpellFlags(int spell)
    {
        return FindInstalledNativeAction(2, (ushort)spell).TargetFlags ?? throw new InvalidDataException("Native compelled spell lacks original target flags.");
    }

    private ushort NativeCompelledTargetMask(BattleCombatant actor, byte flags, bool invertSide)
    {
        bool party = ((flags & 0x40) != 0) != invertSide;
        ushort mask = (flags & 0x30) switch
        {
            0 => party ? (ushort)0x8007 : (ushort)0x80F8,
            0x10 => ResolveNativeAiTargetMask(actor, party ? (byte)201 : (byte)202),
            0x20 => 0x80FF,
            _ => throw new NotSupportedException("Native compelled action target flags depend on an uninitialized native temporary.")
        };
        if ((flags & 1) != 0)
        {
            mask |= 0x4000;
        }
        if ((flags & 2) != 0)
        {
            mask |= 0x2000;
        }
        return mask;
    }
}
