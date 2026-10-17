using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCommonPacketMemory([property: JsonRequired] NativeBattleEffectArena? Bank070Buffer0, [property: JsonRequired] NativeBattleEffectArena? Bank070Buffer1, [property: JsonRequired] NativeBattleEffectArena? Bank094Buffer0, [property: JsonRequired] NativeBattleEffectArena? Bank094Buffer1);

/// <summary>Four retained native common packet heaps; observing a write never proves any other byte zero.</summary>
public sealed partial record NativeBattlePrimitiveProcess
{
    public NativeBattleCommonPacketMemory? CommonPacketMemory { get; init; }

    public NativeBattlePrimitiveProcess ApplyCommonWrite(int offset, ImmutableArray<byte> data, ImmutableArray<NativeBattleGfPacketByteState> mask)
    {
        int bank = CommonBank(out int capacity);
        if (data.IsDefault || mask.IsDefault)
        {
            throw new InvalidDataException("Native common packet write requires explicit data and byte-state masks.");
        }
        RequireCommonRange(offset, data.Length, capacity);
        ValidateCommonPacketMemory();
        if (data.Length != mask.Length || mask.Any(value => value is not (NativeBattleGfPacketByteState.Untouched or NativeBattleGfPacketByteState.Known or NativeBattleGfPacketByteState.Unknown)))
        {
            throw new InvalidDataException("Native common packet write requires matching data and explicit byte-state masks.");
        }
        if (mask.All(value => value == NativeBattleGfPacketByteState.Untouched))
        {
            return this;
        }
        NativeBattleEffectArena memory = CommonArena(bank) ?? UnknownCommonArena(capacity);
        int cursor = 0;
        while (cursor < mask.Length)
        {
            NativeBattleGfPacketByteState state = mask[cursor];
            int end = cursor + 1;
            while (end < mask.Length && mask[end] == state)
            {
                end++;
            }
            if (state == NativeBattleGfPacketByteState.Known)
            {
                memory = memory.Write(checked(offset + cursor), data.AsSpan().Slice(cursor, end - cursor));
            }
            else if (state == NativeBattleGfPacketByteState.Unknown)
            {
                memory = memory.MarkUnknown(checked(offset + cursor), end - cursor);
            }
            cursor = end;
        }
        return StoreCommonArena(bank, memory);
    }

    public NativeBattlePrimitiveProcess MarkCommonUnknown(int offset, int length)
    {
        int bank = CommonBank(out int capacity);
        RequireCommonRange(offset, length, capacity);
        ValidateCommonPacketMemory();
        if (length == 0)
        {
            return this;
        }
        NativeBattleEffectArena memory = CommonArena(bank) ?? UnknownCommonArena(capacity);
        return StoreCommonArena(bank, memory.MarkUnknown(offset, length));
    }

    public byte[] ReadCommonPacket(int offset, int count)
    {
        int bank = CommonBank(out int capacity);
        RequireCommonRange(offset, count, capacity);
        ValidateCommonPacketMemory();
        NativeBattleEffectArena memory = CommonArena(bank) ?? throw new NotSupportedException("Native common packet read requires the retained owner of this physical bank and buffer.");
        return memory.Read(offset, count);
    }

    public void ValidateCommonPacketMemory()
    {
        if (CommonPacketMemory is not { } memory)
        {
            return;
        }
        NativeBattleEffectArena?[] banks = [memory.Bank070Buffer0, memory.Bank070Buffer1, memory.Bank094Buffer0, memory.Bank094Buffer1];
        for (int index = 0; index < banks.Length; index++)
        {
            if (banks[index] is not { } arena)
            {
                continue;
            }
            arena.Validate();
            int capacity = index < 2 ? 114688 : 147456;
            if (arena.Regions.Any(region => region.Offset > capacity - region.Data.Length) || arena.UnknownRegions.Any(region => region.Offset > capacity - region.Length))
            {
                throw new InvalidDataException("Native common packet memory exceeds its selected physical heap capacity.");
            }
            IEnumerable<(int Offset, int Length)> spans = arena.Regions.Select(region => (region.Offset, region.Data.Length)).Concat(arena.UnknownRegions.Select(region => (region.Offset, region.Length))).OrderBy(region => region.Offset);
            int covered = 0;
            foreach ((int offset, int length) in spans)
            {
                if (offset > covered)
                {
                    throw new InvalidDataException("Native common packet memory has unclassified bytes; common heaps have no implicit cleared holes.");
                }
                covered = Math.Max(covered, checked(offset + length));
            }
            if (covered != capacity)
            {
                throw new InvalidDataException("Native common packet memory must classify its entire physical capacity as explicit data or unknown bytes.");
            }
        }
    }

    private int CommonBank(out int capacity)
    {
        if (PacketBuffer > 1 || PacketBank is not ("off_B6D070" or "off_B6D094"))
        {
            throw new NotSupportedException("Native common packet access requires its published physical bank and buffer.");
        }
        capacity = PacketBank == "off_B6D070" ? 114688 : 147456;
        return (PacketBank == "off_B6D070" ? 0 : 2) + PacketBuffer;
    }

    private NativeBattleEffectArena? CommonArena(int bank)
    {
        return bank switch
        {
            0 => CommonPacketMemory?.Bank070Buffer0,
            1 => CommonPacketMemory?.Bank070Buffer1,
            2 => CommonPacketMemory?.Bank094Buffer0,
            3 => CommonPacketMemory?.Bank094Buffer1,
            _ => throw new InvalidDataException("Native common packet heap index exceeds four physical banks.")
        };
    }

    private NativeBattlePrimitiveProcess StoreCommonArena(int bank, NativeBattleEffectArena arena)
    {
        NativeBattleCommonPacketMemory memory = CommonPacketMemory ?? new(null, null, null, null);
        memory = bank switch
        {
            0 => memory with { Bank070Buffer0 = arena },
            1 => memory with { Bank070Buffer1 = arena },
            2 => memory with { Bank094Buffer0 = arena },
            3 => memory with { Bank094Buffer1 = arena },
            _ => throw new InvalidDataException("Native common packet heap index exceeds four physical banks.")
        };
        return this with { CommonPacketMemory = memory };
    }

    private static NativeBattleEffectArena UnknownCommonArena(int capacity)
    {
        return new([]) { UnknownRegions = [new(0, capacity)] };
    }

    private static void RequireCommonRange(int offset, int count, int capacity)
    {
        if (offset < 0 || count < 0 || offset > capacity - count)
        {
            throw new InvalidDataException("Native common packet access exceeds its physical bank capacity.");
        }
    }
}
