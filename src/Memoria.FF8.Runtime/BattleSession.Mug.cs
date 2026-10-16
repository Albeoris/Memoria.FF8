namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void ResolveNativeMug(BattleCombatant actor, BattleCombatant recipient)
    {
        if ((recipient.NativeCombatantFlags & 0x800) != 0)
        {
            QueueNativeMugMessage(actor, recipient, NativeBattleGlobalText(18));
            return;
        }
        NativeBattleMugDefinition mug = recipient.NativeMug ?? throw new NotSupportedException("Native Mug requires the recipient's original monster steal rate and all twelve level-band item entries.");
        mug.Validate();
        if (mug.Rate == 0)
        {
            QueueNativeMugMessage(actor, recipient, NativeBattleGlobalText(18));
            return;
        }
        if (NextRandomByte() > mug.Rate + actor.Speed / 2)
        {
            QueueNativeMugMessage(actor, recipient, NativeBattleGlobalText(17));
            return;
        }
        //4867C0 shares486650's four-slot probability thresholds, including unreachable rare threshold261.
        BattleStock stolen = mug.Item(recipient.Level, BattleEnemyRewardDefinition.SelectDropSlot((byte)NextRandomByte(), definition.RareItem));
        recipient.NativeCombatantFlags |= 0x800;
        AddNativeBattleItem(stolen.Id, stolen.Count);
        string name = stolen.Id == 0 ? "" : FindInstalledNativeAction(4, checked((ushort)stolen.Id)).Name ?? throw new InvalidDataException("Native Mug requires the installed item name.");
        string digits = NativeBattleGlobalText(11);
        if (digits.Length < 11)
        {
            throw new InvalidDataException("Native battle numeric text must contain its separator and ten digits.");
        }
        string quantity = string.Concat(stolen.Count.ToString(System.Globalization.CultureInfo.InvariantCulture).Select(digit => digits[digit - '0' + 1]));
        string message = NativeBattleGlobalText(19) + digits[0] + quantity + digits[0] + NativeBattleGlobalText(71) + name + (stolen.Count == 1 ? "" : NativeBattleGlobalText(119)) + NativeBattleGlobalText(115);
        QueueNativeMugMessage(actor, recipient, message);
    }

    private void QueueNativeMugMessage(BattleCombatant actor, BattleCombatant recipient, string message)
    {
        //48E5F0/48E600 ->485420/485610 ->47E220 uses message-speed duration and mode3.
        int duration = 8 * (definition.NativeMessageSpeed ?? throw new NotSupportedException("Native Mug message requires its saved message-speed BYTE.")) + 8;
        QueueNativeDescriptorMessage(new BattleEvent(Frame, "NativeText", actor.Id, recipient.Id, duration, message) { NativeMessageMode = 3 });
    }
}
