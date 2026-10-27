using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshZantetsukenContactRow(byte Actor, byte Flags3);
public sealed record NativeBattleGilgameshZantetsukenAnchor(uint Address, byte Actor) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenContact(int Ordinal) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenActorFlags(byte Actor, byte OrMask) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenFadeActor(byte Actor, int Cue) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenBeginDrawActor(uint Address) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenDrawActor(uint Address) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenStageVisibility(bool Visible) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenDrawStage(uint Address) : NativeBattleGilgameshOperation;
public sealed record NativeBattleGilgameshZantetsukenInputs(uint GraphicsFlags, ImmutableArray<NativeBattleGilgameshZantetsukenContactRow> Contacts, Func<byte, short> ReadModelWord26, Func<byte, NativeBattlePackedMatrix32> ReadWorld32, Func<NativeBattleGilgameshFrame, NativeBattleGilgameshOperation, NativeBattleGilgameshFrame> Execute);
public sealed record NativeBattleGilgameshZantetsukenStep(NativeBattleGilgameshFrame Frame, int Result);

public static class NativeBattleGilgameshZantetsukenAttack
{
    public static NativeBattleGilgameshZantetsukenStep Advance(NativeBattleGilgameshFrame frame, uint address, NativeBattleGilgameshZantetsukenInputs inputs)
    {
        NativeBattleZantetsukenTasks.Require(frame, address, 0x58DED0);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        NativeBattleGilgameshFrame current = frame;
        ushort counter = NativeBattleZantetsukenTasks.Read(current, address).ReadWord(12);
        if (counter == 0)
        {
            uint child;
            (current, child) = NativeBattleZantetsukenTasks.Append(current, 0x58E120);
            current = NativeBattleZantetsukenTasks.Write(current, child, NativeBattleZantetsukenTasks.Read(current, child).WriteWord(0x10, unchecked((ushort)-170)).WriteWord(0x12, unchecked((ushort)-30)).WriteWord(0x18, 0x860).WriteWord(0x1C, 0x1C0).WriteWord(0x1E, 0x1C0).WriteWord(0x20, 0x600));
        }
        if (counter == 16)
        {
            if (inputs.Contacts.IsDefault || inputs.Contacts.Length > byte.MaxValue)
            {
                throw new NotSupportedException("Zantetsuken requires its actual first-header contact rows.");
            }
            int eligible = 0;
            for (int ordinal = 0; ordinal < inputs.Contacts.Length; ordinal++)
            {
                NativeBattleGilgameshZantetsukenContactRow contact = inputs.Contacts[ordinal];
                if ((contact.Flags3 & 4) != 0)
                {
                    current = inputs.Execute(current, new NativeBattleGilgameshZantetsukenContact(ordinal));
                    continue;
                }
                if (contact.Actor >= 7)
                {
                    throw new InvalidDataException("Zantetsuken contact leaves its seven physical actors.");
                }
                uint first;
                (current, first) = NativeBattleZantetsukenTasks.Append(current, 0x58E3E0);
                current = NativeBattleZantetsukenTasks.Write(current, first, NativeBattleZantetsukenTasks.Read(current, first).WriteWord(14, contact.Actor));
                current = inputs.Execute(current, new NativeBattleGilgameshZantetsukenAnchor(first, contact.Actor));
                int angle = Draw() % 1280 + 2816;
                if ((angle & 1) != 0)
                {
                    angle = -angle;
                }
                current = NativeBattleZantetsukenTasks.Write(current, first, NativeBattleZantetsukenTasks.Read(current, first).WriteWord(0x18, unchecked((ushort)angle)));
                short velocity = unchecked((short)(-150 - Draw() % 300));
                current = NativeBattleZantetsukenTasks.Write(current, first, NativeBattleZantetsukenTasks.Read(current, first).WriteWord(0x20, unchecked((ushort)velocity)).WriteWord(0x22, unchecked((ushort)velocity)));
                uint second;
                (current, second) = NativeBattleZantetsukenTasks.Append(current, 0x58E570);
                NativeBattleModulePoolTask copy = NativeBattleZantetsukenTasks.CopyAnchor(NativeBattleZantetsukenTasks.Read(current, second), NativeBattleZantetsukenTasks.Read(current, first)).WriteWord(14, contact.Actor);
                current = NativeBattleZantetsukenTasks.Write(current, second, copy);
                short height = inputs.ReadModelWord26(contact.Actor);
                int radius = unchecked((Draw() % 256 + 128) * height) >> 12;
                int denominator = unchecked(radius * 2);
                foreach (int offset in new[] { 0x10, 0x12, 0x14 })
                {
                    int displacement = unchecked(Draw() % denominator - radius);
                    NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(current, second);
                    current = NativeBattleZantetsukenTasks.Write(current, second, task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + displacement))));
                }
                NativeBattleModulePoolTask secondTask = NativeBattleZantetsukenTasks.Read(current, second).WriteWord(0x18, 128).WriteWord(0x1A, (ushort)ordinal).WriteWord(0x16, (ushort)ordinal);
                current = NativeBattleZantetsukenTasks.Write(current, second, secondTask);
                current = NativeBattleZantetsukenTasks.Write(current, first, NativeBattleZantetsukenTasks.Read(current, first).WriteWord(0x16, (ushort)ordinal));
                eligible++;
            }
            if (eligible == 0)
            {
                uint child;
                (current, child) = NativeBattleZantetsukenTasks.Append(current, 0x58F940);
                current = NativeBattleZantetsukenTasks.Write(current, child, NativeBattleZantetsukenTasks.Read(current, child).WriteWord(14, 20).WriteWord(0x20, 0).WriteWord(0x22, 600));
            }
        }
        NativeBattleModulePoolTask parent = NativeBattleZantetsukenTasks.Read(current, address);
        ushort next = unchecked((ushort)(parent.ReadWord(12) + 1));
        current = NativeBattleZantetsukenTasks.Write(current, address, parent.WriteWord(12, next));
        return new(current, unchecked((short)next) >= 50 ? 2 : 0);

        ushort Draw()
        {
            NativeCrtRandomState random = current.Random.Draw(out ushort value);
            current = current with { Random = random };
            return value;
        }
    }
}

internal static class NativeBattleZantetsukenTasks
{
    internal static void Require(NativeBattleGilgameshFrame frame, uint address, uint callback)
    {
        if (frame.Effect.Binding.Effect != 327 || frame.Effect.Binding.Entry != 0x58D760 || frame.Effect.Pools.IsDefault || frame.Effect.Pools.Length != 3 || frame.Effect.Pools[1].DescriptorAddress != 0x21FFD58 || frame.Effect.Pools[1].BankAddress != 0x21FFD68 || frame.Effect.Pools[1].Stride != 36 || frame.Effect.Pools[1].Slots.Length != 100)
        {
            throw new InvalidDataException("Zantetsuken requires its selected entry and original child task bank.");
        }
        frame.Effect.Pools[1].Validate();
        if (Read(frame, address).Callback != callback)
        {
            throw new InvalidDataException("Zantetsuken requires its actual selected callback.");
        }
    }

    internal static NativeBattleModulePoolTask Read(NativeBattleGilgameshFrame frame, uint address)
    {
        (int pool, int slot) = Locate(frame, address);
        return frame.Effect.Pools[pool].Slots[slot];
    }

    internal static NativeBattleGilgameshFrame Write(NativeBattleGilgameshFrame frame, uint address, NativeBattleModulePoolTask task)
    {
        (int index, int slot) = Locate(frame, address);
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback))) } };
    }

    internal static (NativeBattleGilgameshFrame Frame, uint Address) Append(NativeBattleGilgameshFrame frame, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        int slot;
        (pool, slot) = pool.Append(callback);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        uint address = checked(pool.BankAddress + (uint)(slot * pool.Stride));
        return (Write(frame, address, Read(frame, address).WriteWord(12, 0)), address);
    }

    internal static NativeBattleModulePoolTask CopyAnchor(NativeBattleModulePoolTask destination, NativeBattleModulePoolTask source)
    {
        NativeBattleModulePoolTask result = destination.WriteBytes(0x10, source.ReadRawPayloadSpan().Slice(4, 8));
        if (source.UnknownPayloadRanges is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion range in ranges)
            {
                int first = Math.Max(0x10, range.Offset);
                int end = Math.Min(0x18, range.Offset + range.Length);
                if (first < end)
                {
                    result = result.MarkPayloadUnknown(first, end - first);
                }
            }
        }
        return result;
    }

    private static (int Pool, int Slot) Locate(NativeBattleGilgameshFrame frame, uint address)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return (index, checked((int)(offset / pool.Stride)));
            }
        }
        throw new InvalidDataException("Zantetsuken task address leaves its original physical pools.");
    }
}
