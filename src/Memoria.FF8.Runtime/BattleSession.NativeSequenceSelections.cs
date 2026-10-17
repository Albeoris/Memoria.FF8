namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState RebindNativeSequenceGeometry92(NativeBattleGraphicsState graphics, int currentIndex, byte selector)
    {
        (byte actorIndex, ushort mask) = NativeBattleSequenceSelections.GeometryArguments92(currentIndex, selector, graphics.ActionContext);
        if ((mask & 127) == 0)
        {
            throw new InvalidDataException("Native92 target mask has no physical recipient; original5020A0 would dereference its null bounds pointer.");
        }
        BattleCombatant actor = GraphicsActor(graphics, actorIndex);
        return PrepareNativeActionGeometry(graphics, actor, new(actor.Id, actor.Id) { NativeTargetMask = mask });
    }

    private NativeBattleGraphicsState ApplyNativeCurrentSequenceContact(NativeBattleGraphicsState graphics)
    {
        NativeBattleContactPacket contact = nativeActionPackets.ReadActiveContact();
        var pending = graphics.PendingContacts;
        return ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [contact] }) with { PendingContacts = pending };
    }

    private NativeBattleGraphicsState SelectNativeSequenceModels(NativeBattleGraphicsState graphics, int currentIndex, byte opcode, byte selector, byte sequence)
    {
        if (opcode == 0x93)
        {
            foreach (int index in NativeBattleSequenceSelections.SelectModels93(graphics.Models, currentIndex, selector))
            {
                graphics = SelectNativeHeaderSequence(graphics, checked((byte)index), sequence);
            }
            return graphics;
        }
        if (opcode == 0xBF)
        {
            NativeBattleContactHalf contact = nativeActionPackets.ReadActiveContact().Primary;
            if (contact.Sequence == 0)
            {
                return graphics;
            }
            if ((contact.HitFlags3 & 8) != 0)
            {
                return ApplyNativeSequenceCover5065B0(graphics, contact);
            }
        }
        int target = (graphics.ActionContext ?? throw new NotSupportedException("Native target sequence requires its50D300 current target owner.")).TargetIndex ?? throw new NotSupportedException("Native target sequence requires its retained physical target identity.");
        if (opcode == 0xBF && !NativeBattleSequenceSelections.CanSelectTargetBF(graphics.Models[target]))
        {
            return graphics;
        }
        return SelectNativeHeaderSequence(graphics, checked((byte)target), sequence);
    }
}
