using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshMasamuneAnchor(uint Address, BattleModelOwner Actor) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshMasamuneProjectFlare(uint Address) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshMasamuneAttackStep(NativeBattleGilgameshFrame Frame, bool Contact, int Result);

public static class NativeBattleGilgameshMasamuneAttack
{
    public static NativeBattleGilgameshMasamuneAttackStep Advance(NativeBattleGilgameshFrame frame, uint parentAddress, uint graphicsFlags, ImmutableArray<BattleModelOwner> contactActors, Func<NativeBattleGilgameshFrame, NativeBattleGilgameshOperation, NativeBattleGilgameshFrame> execute)
    {
        return AdvanceOccurrences(frame, parentAddress, graphicsFlags, contactActors.IsDefault ? default : contactActors.Select(owner => (owner, (BattleContactOccurrence?)null)).ToImmutableArray(), execute);
    }

    internal static NativeBattleGilgameshMasamuneAttackStep AdvanceOccurrences(NativeBattleGilgameshFrame frame, uint parentAddress, uint graphicsFlags, ImmutableArray<(BattleModelOwner Owner, BattleContactOccurrence? Occurrence)> contacts, Func<NativeBattleGilgameshFrame, NativeBattleGilgameshOperation, NativeBattleGilgameshFrame> execute)
    {
        if (frame.Effect.Binding.Effect != 328 || frame.Effect.Binding.Entry != 0x58D930 || frame.Effect.Pools.IsDefault || frame.Effect.Pools.Length != 3)
        {
            throw new InvalidDataException("Masamune requires its original selected entry and raw task banks.");
        }
        NativeBattleGilgameshFrame current = frame;
        NativeBattleEffectTaskPool children = current.Effect.Pools[1];
        if (!NativeBattleGilgameshAttackStorage.ValidChildBank(frame.Effect, children))
        {
            throw new InvalidDataException("Masamune requires its original hundred raw child tasks.");
        }
        children.Validate();
        NativeBattleModulePoolTask parent = Read(parentAddress);
        if (parent.Callback != 0x590510 || parent.PayloadLength != 24)
        {
            throw new InvalidDataException("Masamune requires its actual attack callback task.");
        }
        if ((graphicsFlags & 0x201) != 0)
        {
            return new(frame, false, 0);
        }
        ushort counter = parent.ReadWord(0x0C);
        if (counter is 0 or 16)
        {
            if (contacts.IsDefault)
            {
                throw new NotSupportedException("Masamune requires its actual first-header contact actors.");
            }
            bool expanded = frame.Effect.GilgameshAttackStorage is not null;
            if (expanded && contacts.Any(contact => contact.Occurrence is null))
            {
                throw new InvalidDataException("Masamune requires each expanded contact occurrence.");
            }
            for (int index = 0; index < contacts.Length; index++)
            {
                (BattleModelOwner actor, BattleContactOccurrence? occurrence) = contacts[index];
                if (counter == 0)
                {
                    uint flare = Append(0x590790);
                    Execute(new NativeBattleGilgameshMasamuneAnchor(flare, actor));
                    Execute(new NativeBattleGilgameshMasamuneProjectFlare(flare));
                    Write(flare, Read(flare).WriteWord(0x18, 0xA00).WriteWord(0x1C, 0x1C0).WriteWord(0x1E, 0x1C0).WriteWord(0x20, 0x300));
                    continue;
                }
                uint first = Append(0x5908D0);
                Execute(new NativeBattleGilgameshMasamuneAnchor(first, actor));
                Write(first, Read(first).WriteWord(0x18, 0x200));
                NativeCrtRandomState random = current.Random.Draw(out ushort draw);
                current = current with { Random = random };
                ushort size = (ushort)((draw % 2560 + 2560) / 3);
                Write(first, Read(first).WriteWord(0x1C, size).WriteWord(0x1E, size));

                uint second = Append(0x590A10);
                ushort larger = unchecked((ushort)(Read(first).ReadWord(0x1E) + 1536));
                Write(second, CopyAnchor(Read(second), Read(first)).WriteWord(0x18, 0x200).WriteWord(0x1C, larger).WriteWord(0x1E, larger));

                uint particles = Append(0x590B60);
                Write(particles, NativeBattleGilgameshParticleRows.WriteGroup(CopyAnchor(Read(particles), Read(first)), index, NativeBattleGilgameshParticleVariant.Masamune, expanded ? occurrence : null));

                uint ring = Append(0x590F10);
                Write(ring, NativeBattleGilgameshParticleRows.WriteGroup(CopyAnchor(Read(ring), Read(first)).WriteWord(0x0E, 3), index, NativeBattleGilgameshParticleVariant.Masamune, expanded ? occurrence : null));
            }
        }
        parent = Read(parentAddress);
        ushort next = unchecked((ushort)(parent.ReadWord(0x0C) + 1));
        Write(parentAddress, parent.WriteWord(0x0C, next));
        return new(current, counter == 17, unchecked((short)next) >= 50 ? 2 : 0);

        uint Append(uint callback)
        {
            NativeBattleEffectTaskPool pool = current.Effect.Pools[1];
            int slot;
            (pool, slot) = pool.Append(callback);
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(1, pool) } };
            uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
            Write(address, Read(address).WriteWord(0x0C, 0));
            return address;
        }

        void Execute(NativeBattleGilgameshOperation operation)
        {
            current = execute(current, operation);
        }

        (int Pool, int Slot) Locate(uint address)
        {
            for (int index = 0; index < current.Effect.Pools.Length; index++)
            {
                NativeBattleEffectTaskPool pool = current.Effect.Pools[index];
                uint offset = unchecked(address - pool.BankAddress);
                if (offset < pool.Slots.Length * pool.Stride && offset % pool.Stride == 0)
                {
                    return (index, (int)(offset / pool.Stride));
                }
            }
            throw new InvalidDataException("Masamune callback address leaves its physical raw task banks.");
        }

        NativeBattleModulePoolTask Read(uint address)
        {
            (int pool, int slot) = Locate(address);
            return current.Effect.Pools[pool].Slots[slot];
        }

        void Write(uint address, NativeBattleModulePoolTask value)
        {
            (int index, int slot) = Locate(address);
            NativeBattleEffectTaskPool pool = current.Effect.Pools[index];
            current = current with { Effect = current.Effect with { Pools = current.Effect.Pools.SetItem(index, pool.WriteSlot(slot, value.UpdateHeader(value.Flags, value.State, pool.Slots[slot].Next, value.Callback))) } };
        }
    }

    private static NativeBattleModulePoolTask CopyAnchor(NativeBattleModulePoolTask destination, NativeBattleModulePoolTask source)
    {
        NativeBattleModulePoolTask copied = destination.WriteBytes(0x10, source.ReadRawPayloadSpan().Slice(0x10 - 12, 8));
        if (source.ReadUnknownPayloadRanges() is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(0x10, range.Offset);
                int end = Math.Min(0x18, checked(range.Offset + range.Length));
                if (first < end)
                {
                    copied = copied.MarkPayloadUnknown(first, end - first);
                }
            }
        }
        return copied;
    }
}
