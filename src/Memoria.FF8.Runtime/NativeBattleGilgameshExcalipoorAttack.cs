using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshAttackStep(NativeBattleModulePoolTask Task, NativeBattleEffectTaskPool Children, NativeCrtRandomState Random, bool Contact, int Result);

/// <summary>591250/5920E0's finite attack producer over the original raw child bank.</summary>
public static class NativeBattleGilgameshExcalipoorAttack
{
    public static NativeBattleGilgameshAttackStep Advance5920E0(NativeBattleModulePoolTask task, NativeBattleEffectTaskPool children, NativeCrtRandomState random, uint graphicsFlags, ImmutableArray<byte> contactActors, Func<byte, NativeBattleModuleAnchor> readAnchor571400)
    {
        return Advance(NativeBattleGilgameshAttackProfile.ForEffect(330), task, children, random, graphicsFlags, contactActors, readAnchor571400);
    }

    public static NativeBattleGilgameshAttackStep Advance(NativeBattleGilgameshAttackProfile profile, NativeBattleModulePoolTask task, NativeBattleEffectTaskPool children, NativeCrtRandomState random, uint graphicsFlags, ImmutableArray<byte> contactActors, Func<byte, NativeBattleModuleAnchor> readAnchor571400)
    {
        profile.Validate();
        if (task.Callback != profile.Callback || task.PayloadLength != 24 || children.DescriptorAddress != 0x21FFD58 || children.BankAddress != 0x21FFD68 || children.Stride != 36 || children.Slots.Length != 100 || contactActors.IsDefault || contactActors.Length > byte.MaxValue)
        {
            throw new InvalidDataException("Native Gilgamesh attack requires its actual raw task, child bank and physical header contacts.");
        }
        children.Validate();
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(task, children, random, false, 0);
        }
        ushort counter = task.ReadWord(12);
        if (counter == 0)
        {
            int slot = Append(0x591470);
            Publish(slot, children.Slots[slot].WriteWord(12, 0).WriteWord(14, profile.MaterialVariant).WriteWord(16, 0xFF56).WriteWord(18, 0xFF9C).WriteWord(24, 0x968).WriteWord(30, 0x555).WriteWord(28, 0x555).WriteWord(32, 0x1000));
        }
        if (counter == 18)
        {
            for (int index = 0; index < contactActors.Length; index++)
            {
                int first = Append(0x5915C0);
                NativeBattleModuleAnchor anchor = readAnchor571400(contactActors[index]);
                ushort mask = unchecked((ushort)(1u << index));
                NativeBattleModulePoolTask positioned = Position(children.Slots[first].WriteWord(12, 0).WriteWord(14, profile.MaterialVariant), anchor).WriteWord(22, mask);
                Publish(first, positioned);

                int second = Append(0x591850);
                Publish(second, CopyPosition(children.Slots[second], positioned).WriteWord(12, 0).WriteWord(14, profile.MaterialVariant).WriteWord(22, unchecked((ushort)(0x100u << index))));

                int third = Append(0x591B80);
                Publish(third, CopyPosition(children.Slots[third], positioned).WriteWord(12, 0).WriteWord(22, mask));

                int fourth = Append(0x591F50);
                NativeBattleModulePoolTask flare = CopyPosition(children.Slots[fourth], positioned).WriteWord(12, 0).WriteWord(14, 1).WriteWord(24, 0x800);
                random = random.Draw(out ushort firstRandom);
                ushort x = (ushort)((firstRandom % 512 + 768) / 3);
                flare = flare.WriteWord(30, x).WriteWord(28, x);
                random = random.Draw(out ushort secondRandom);
                ushort y = (ushort)((secondRandom % 768 + 768) / 3);
                Publish(fourth, flare.WriteWord(34, y).WriteWord(32, y));
            }
        }
        task = task.WriteWord(12, unchecked((ushort)(counter + 1)));
        return new(task, children, random, counter == 19, unchecked((short)task.ReadWord(12)) >= 50 ? 2 : 0);

        int Append(uint callback)
        {
            int slot;
            (children, slot) = children.Append(callback);
            return slot;
        }

        void Publish(int slot, NativeBattleModulePoolTask value)
        {
            children = children.WriteSlot(slot, value);
        }
    }

    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleModuleAnchor anchor) => task.WriteWord(16, unchecked((ushort)anchor.X)).WriteWord(18, unchecked((ushort)anchor.Y)).WriteWord(20, unchecked((ushort)anchor.Z)).WriteWord(22, unchecked((ushort)anchor.W));
    private static NativeBattleModulePoolTask CopyPosition(NativeBattleModulePoolTask task, NativeBattleModulePoolTask source) => task.WriteDword(16, source.ReadDword(16)).WriteDword(20, source.ReadDword(20));
}
