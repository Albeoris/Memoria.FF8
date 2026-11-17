namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleGraphicsState RebindNativeSequenceGeometry92(NativeBattleGraphicsState graphics, BattleModelOwner current, byte selector)
    {
        (BattleModelOwner source, BattleModelTargets targets, bool sourceHasNonActorSelectors) = NativeBattleSequenceSelections.GeometryArguments92(graphics.ActorModels, current, selector, graphics.ActionContext, graphics.NativeBindings);
        if (targets.IsEmpty)
        {
            throw new InvalidDataException("Native92 requires a bound target selection.");
        }
        return PrepareNativeActionGeometry(graphics, source, targets, targets, sourceHasNonActorSelectors);
    }

    private NativeBattleGraphicsState ApplyNativeCurrentSequenceContact(NativeBattleGraphicsState graphics)
    {
        NativeBattleContactPacket contact = nativeActionPackets.ReadActiveContact();
        var pending = graphics.PendingContacts;
        return ConsumeNativeGraphicsContacts(graphics with { PendingContacts = [contact] }) with { PendingContacts = pending };
    }

    private NativeBattleGraphicsState SelectNativeSequenceModels(NativeBattleGraphicsState graphics, BattleModelOwner current, byte opcode, byte selector, byte sequence)
    {
        if (opcode == 0x93)
        {
            foreach (BattleModelOwner owner in NativeBattleSequenceSelections.SelectModels93(graphics.ActorModels, current, selector, graphics.NativeBindings).Enumerate())
            {
                graphics = SelectNativeHeaderSequence(graphics, owner, sequence);
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
            if ((contact.HitFlags & 8) != 0)
            {
                return ApplyNativeSequenceCover5065B0(graphics, contact);
            }
        }
        BattleModelOwner target = (graphics.ActionContext ?? throw new NotSupportedException("Native target sequence requires its50D300 current target owner.")).ResolveTarget(graphics.ActorModels, graphics.NativeBindings) ?? throw new NotSupportedException("Native target sequence requires its retained physical target identity.");
        if (opcode == 0xBF && !NativeBattleSequenceSelections.CanSelectTargetBF(graphics.ActorModels, target))
        {
            return graphics;
        }
        return SelectNativeHeaderSequence(graphics, target, sequence);
    }
}
