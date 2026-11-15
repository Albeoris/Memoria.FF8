using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshAttackStep(NativeBattleModulePoolTask Task, NativeBattleEffectTaskPool Children, NativeCrtRandomState Random, bool Contact, int Result);

/// <summary>591250/5920E0's finite attack producer over the original raw child bank.</summary>
public static class NativeBattleGilgameshExcalipoorAttack
{
    public static NativeBattleGilgameshAttackStep Advance5920E0(NativeBattleModulePoolTask task, NativeBattleEffectTaskPool children, NativeCrtRandomState random, uint graphicsFlags, ImmutableArray<BattleModelOwner> contactActors, Func<BattleModelOwner, NativeBattleModuleAnchor> readAnchor571400)
    {
        return Advance(NativeBattleGilgameshAttackProfile.ForEffect(330), task, children, random, graphicsFlags, contactActors, readAnchor571400);
    }

    public static NativeBattleGilgameshAttackStep Advance(NativeBattleGilgameshAttackProfile profile, NativeBattleModulePoolTask task, NativeBattleEffectTaskPool children, NativeCrtRandomState random, uint graphicsFlags, ImmutableArray<BattleModelOwner> contactActors, Func<BattleModelOwner, NativeBattleModuleAnchor> readAnchor571400)
    {
        return AdvanceOccurrences(profile, task, children, random, graphicsFlags, contactActors.IsDefault ? default : contactActors.Select(owner => (owner, (BattleContactOccurrence?)null)).ToImmutableArray(), readAnchor571400, null);
    }

    internal static NativeBattleGilgameshAttackStep AdvanceOccurrences(NativeBattleGilgameshAttackProfile profile, NativeBattleModulePoolTask task, NativeBattleEffectTaskPool children, NativeCrtRandomState random, uint graphicsFlags, ImmutableArray<(BattleModelOwner Owner, BattleContactOccurrence? Occurrence)> contacts, Func<BattleModelOwner, NativeBattleModuleAnchor> readAnchor571400, NativeBattleGilgameshAttackStorage? hosted)
    {
        profile.Validate();
        if (task.Callback != profile.Callback || task.PayloadLength != 24 || !NativeBattleGilgameshAttackStorage.ValidChildBank(children) || contacts.IsDefault ||
            (hosted is null ? children.BankAddress != NativeBattleGilgameshAttackStorage.OriginalChildAddress : children.BankAddress != hosted.ChildAddress || children.Slots.Length != hosted.ChildCapacity))
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
            if (hosted is not null && contacts.Any(contact => contact.Occurrence is null))
            {
                throw new InvalidDataException("Excalipoor requires each expanded contact occurrence.");
            }
            for (int index = 0; index < contacts.Length; index++)
            {
                int first = Append(0x5915C0);
                var contact = contacts[index];
                BattleContactOccurrence? occurrence = hosted is null ? null : contact.Occurrence;
                NativeBattleModuleAnchor anchor = readAnchor571400(contact.Owner);
                NativeBattleModulePoolTask positioned = NativeBattleGilgameshParticleRows.WriteGroup(Position(children.Slots[first].WriteWord(12, 0).WriteWord(14, profile.MaterialVariant), anchor), index, NativeBattleGilgameshParticleVariant.Excalipoor, occurrence);
                Publish(first, positioned);

                int second = Append(0x591850);
                Publish(second, NativeBattleGilgameshParticleRows.WriteGroup(CopyPosition(children.Slots[second], positioned).WriteWord(12, 0).WriteWord(14, profile.MaterialVariant), index, NativeBattleGilgameshParticleVariant.ExcalipoorSparks, occurrence));

                int third = Append(0x591B80);
                Publish(third, NativeBattleGilgameshParticleRows.WriteGroup(CopyPosition(children.Slots[third], positioned).WriteWord(12, 0), index, NativeBattleGilgameshParticleVariant.Excalipoor, occurrence));

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
    private static NativeBattleModulePoolTask CopyPosition(NativeBattleModulePoolTask task, NativeBattleModulePoolTask source)
    {
        (byte[] bytes, bool[] known) = source.BorrowPayloadArena(16, 8).ReadSnapshot(0, 8);
        task = task.WriteBytes(16, bytes);
        for (int index = 0; index < known.Length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(16 + index, 1);
            }
        }
        return task;
    }
}
