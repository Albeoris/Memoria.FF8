namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record NativeCompelledCommand(byte Command, ushort ActionId, ushort TargetMask, bool FreeMagic);

    private static bool RequiresNativeCompelledCommand(BattleCombatant actor)
    {
        return actor.IsPlayer && ((NativeSavedStatusBits(actor) & 0x20) != 0 || (NativeBattleStatusBits(actor) & 0x2004000) != 0);
    }

    private void DispatchNativeCompelledActors()
    {
        if (HasNativeUiServiceClock)
        {
            //483EB0 already queued the original command at full-gauge registration, preserving its gauge until completion.
            return;
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsReady && RequiresNativeCompelledCommand(actor)))
        {
            BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.IsActive && candidate.NativeIndex is >= 3 and < 7) ?? actor;
            EnqueueAttack(actor, target);
        }
    }

    private BattleAttackCommand PrepareNativeQueuedCommand(BattleCombatant actor, BattleAttackCommand original)
    {
        if (original.NativePlayerReaction is not null)
        {
            return original;
        }
        NativeCompelledCommand? prepared = PrepareNativeCompelledCommand(actor);
        if (prepared is null)
        {
            return original;
        }
        int? reserved = prepared.Command == 4 ? ConsumeNativeItem(prepared.ActionId) : null;
        if (original.ReservedNativeItemSlot is int previousSlot)
        {
            RefundNativeReservedItem(previousSlot, original.NativeAction?.ActionId ?? original.ActionId);
        }
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 7 && (prepared.TargetMask & (1 << candidate.NativeIndex)) != 0) ?? actor;
        BattleNativeAction? native = prepared.Command == 1 ? actor.NativeAttack : FindNativeCommandAction(actor, prepared.Command, prepared.ActionId);
        BattleAbility ability = prepared.Command is 1 or 12 ? BattleAbility.Attack : BattleAbility.EnemyAbility;
        return new BattleAttackCommand(actor.Id, target.Id, ability, prepared.ActionId)
        {
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
        if ((battleStatus & 0x2000000) != 0)
        {
            //483D60 scans physical slots from RNG&31; Angel Wing ignores command availability and does not consume magic.
            NativeMagicStock[] eligible = actor.NativeMagicInventory.Where(stock => stock.SpellId != 0 && (RequiredNativeSpellFlags(stock.SpellId) & 0x40) != 0).ToArray();
            if (eligible.Length == 0)
            {
                return new(1, 0, ResolveNativeAiTargetMask(actor, 202), false);
            }
            int slot = NextRandomByte() & 31;
            while (actor.NativeMagicInventory[slot].SpellId == 0 || (RequiredNativeSpellFlags(actor.NativeMagicInventory[slot].SpellId) & 0x40) == 0)
            {
                slot = (slot + 1) & 31;
            }
            byte spell = actor.NativeMagicInventory[slot].SpellId;
            return new(2, spell, NativeCompelledTargetMask(actor, RequiredNativeSpellFlags(spell), true), true);
        }
        if ((NativeSavedStatusBits(actor) & 0x20) != 0)
        {
            return new(1, 0, ResolveNativeAiTargetMask(actor, (battleStatus & 0x4000) != 0 ? (byte)201 : (byte)202), false);
        }
        if ((battleStatus & 0x4000) == 0)
        {
            return null;
        }
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
                    return new(command, 0, ResolveNativeAiTargetMask(actor, side != 0 ? (byte)201 : (byte)202), false);
                case 2:
                    if ((rules & 2) == 0 && actor.NativeMagicInventory.Any(stock => stock.SpellId != 0))
                    {
                        int occupied = actor.NativeMagicInventory.Count(stock => stock.SpellId != 0);
                        int slot = NextRandomByte() % occupied;
                        while (actor.NativeMagicInventory[slot].SpellId == 0)
                        {
                            slot = (slot + 1) & 31;
                        }
                        byte spell = actor.NativeMagicInventory[slot].SpellId;
                        return new(2, spell, NativeCompelledTargetMask(actor, RequiredNativeSpellFlags(spell), side == 0), false);
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
                            return new(4, (ushort)item, NativeCompelledTargetMask(actor, flags, side == 0), false);
                        }
                    }
                    break;
            }
            commandSlot = (commandSlot + 1) & 3;
        }
        throw new NotSupportedException("Native Confuse command rejection has no supported available physical command and would not terminate.");
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
