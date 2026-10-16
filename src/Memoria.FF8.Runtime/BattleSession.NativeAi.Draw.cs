namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void DrawNativeAiSpell(BattleCombatant actor, bool reportStock)
    {
        //487DF0 reads the previous queued target WORD B94, rather than the interpreter's local target mask.
        int index = System.Numerics.BitOperations.TrailingZeroCount((uint)actor.NativePreviousTargetMask);
        BattleCombatant source = combatants.FirstOrDefault(candidate => candidate.NativeIndex == index) ?? throw new NotSupportedException("Native AI Draw requires an allocated actor in its previous queued target mask.");
        int occupied = source.NativeMagicInventory.Count(stock => stock.SpellId != 0);
        if (occupied == 0)
        {
            actor.NativeDrawnSpell = 255;
            return;
        }
        //4837E0 deliberately starts at RNG % count, then scans physical slots; it does not choose the nth nonempty entry.
        int slot = NextRandomByte() % occupied;
        while (source.NativeMagicInventory[slot].SpellId == 0)
        {
            slot = (slot + 1) & 31;
        }
        NativeMagicStock selected = source.NativeMagicInventory[slot];
        actor.NativeDrawnSpell = selected.SpellId;
        BattleNativeAction spell = FindInstalledNativeAction(2, selected.SpellId);
        string spellName = spell.Name ?? throw new InvalidDataException("Native AI Draw requires the installed spell name.");
        string separator = NativeBattleGlobalText(11);
        if (separator.Length == 0)
        {
            throw new InvalidDataException("Native battle numeric text bank has no separator byte.");
        }
        string message = reportStock ? source.Name + NativeBattleGlobalText(27) + separator[0] + spellName + separator[0] + NativeBattleGlobalText(117) : NativeBattleGlobalText(19) + separator[0] + "1" + separator[0] + spellName + NativeBattleGlobalText(118);
        int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native Draw message requires its saved message-speed BYTE.")) + 8;
        events.Add(new BattleEvent(Frame, "NativeText", actor.Id, source.Id, duration, message) { NativeMessageMode = 3 });
    }

    private void CastNativeAiDrawnSpell(BattleCombatant actor)
    {
        byte spell = actor.NativeDrawnSpell ?? throw new NotSupportedException("Native AI Draw-cast requires an executed drawn-spell byte.");
        if (spell == 0)
        {
            return;
        }
        BattleNativeAction action = FindInstalledNativeAction(2, spell);
        byte flags = action.TargetFlags ?? throw new InvalidDataException("Native drawn spell lacks its kernel target flags.");
        ushort mask = (flags & 0x30) switch
        {
            0 => (flags & 0x40) != 0 ? (ushort)0x8007 : (ushort)0x80F8,
            0x10 => ResolveNativeAiTargetMask(actor, (flags & 0x40) != 0 ? (byte)201 : (byte)202),
            0x20 => 0x80FF,
            _ => spell
        };
        if ((flags & 1) != 0)
        {
            mask |= 0x4000;
        }
        if ((flags & 2) != 0)
        {
            mask |= 0x2000;
        }
        QueueNativeCommand(actor, 2, spell, 11, mask);
    }

    private string NativeBattleGlobalText(int index)
    {
        if ((uint)index >= definition.NativeGlobalMessages.Length || definition.NativeGlobalMessages[index] is not string text)
        {
            throw new NotSupportedException($"Native battle message requires installed kernel global text {index}.");
        }
        return text;
    }
}
