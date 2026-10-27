using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCommonPacketMemory([property: JsonRequired] NativeBattleEffectArena? Bank070Buffer0, [property: JsonRequired] NativeBattleEffectArena? Bank070Buffer1, [property: JsonRequired] NativeBattleEffectArena? Bank094Buffer0, [property: JsonRequired] NativeBattleEffectArena? Bank094Buffer1);

/// <summary>Four unique half-slices of the two native common packet owners; observing a write never proves any other byte zero.</summary>
public sealed partial record NativeBattlePrimitiveProcess : IJsonOnSerializing
{
    void IJsonOnSerializing.OnSerializing()
    {
        if (CommonTransaction is not null)
        {
            throw new InvalidOperationException("Native common packet transactions must publish immutable bytes before serialization.");
        }
    }
    private static readonly ConditionalWeakTable<NativeBattlePrimitiveReference, NativeBattlePrimitiveReference> NormalizedCommonReferences = new();

    [JsonIgnore]
    internal NativeBattleCommonPacketTransaction? CommonTransaction { get; init; }

    public NativeBattleCommonPacketMemory? CommonPacketMemory { get; init; }

    [JsonIgnore]
    public int CommonPacketNominalCapacity
    {
        get
        {
            _ = CommonBank(out int capacity);
            return capacity;
        }
    }

    [JsonIgnore]
    public int CommonPacketWriteCapacity => checked((2 - PacketBuffer) * CommonPacketNominalCapacity);

    public uint NativeRemaining500820()
    {
        int cursor = ByteCursor ?? throw new NotSupportedException("Native remaining packet bytes require the actual current cursor.");
        return unchecked((uint)(CommonPacketNominalCapacity - cursor));
    }

    public static NativeBattlePrimitiveReference NormalizeCommonReference(NativeBattlePrimitiveReference reference)
    {
        int stride = reference.Bank switch { "off_B6D070" => 114688, "off_B6D094" => 147456, _ => 0 };
        if (stride == 0)
        {
            return reference;
        }
        if (reference.Buffer > 1 || reference.ByteOffset < 0)
        {
            throw new InvalidDataException("Native common packet identity requires its actual selected buffer and offset.");
        }
        return reference.Buffer == 0 ? reference : NormalizedCommonReferences.GetValue(reference, static original => original with { Buffer = 0, ByteOffset = checked(original.ByteOffset + (original.Bank == "off_B6D070" ? 114688 : 147456)) });
    }

    public NativeBattlePrimitiveProcess ApplyCommonWrite(int offset, ImmutableArray<byte> data, ImmutableArray<NativeBattleGfPacketByteState> mask)
    {
        return ApplyCommonWrites([new(offset, data, mask)]);
    }

    public NativeBattlePrimitiveProcess MarkCommonUnknown(int offset, int length)
    {
        if (CommonTransaction is { } transaction)
        {
            transaction.MarkUnknown(this, offset, length);
            return this;
        }
        ValidateCommonPacketMemory();
        CommonSliceRange slices = CommonSlices(offset, length);
        NativeBattlePrimitiveProcess result = this;
        int capacity = CommonPacketNominalCapacity;
        foreach (CommonSlice slice in slices)
        {
            NativeBattleEffectArena memory = result.CommonArena(slice.Bank) ?? UnknownCommonArena(capacity);
            result = result.StoreCommonArena(slice.Bank, memory.MarkUnknown(slice.Offset, slice.Count));
        }
        return result;
    }

    public byte[] ReadCommonPacket(int offset, int count)
    {
        if (CommonTransaction is { } transaction)
        {
            return transaction.Read(this, offset, count);
        }
        ValidateCommonPacketMemory();
        CommonSliceRange slices = CommonSlices(offset, count);
        byte[] result = new byte[count];
        foreach (CommonSlice slice in slices)
        {
            NativeBattleEffectArena memory = CommonArena(slice.Bank) ?? throw new NotSupportedException("Native common packet read requires the retained owner of every consumed physical slice.");
            memory.Read(slice.Offset, slice.Count).CopyTo(result, slice.SourceOffset);
        }
        return result;
    }

    public (byte[] Bytes, bool[] Known) ReadCommonPacketSnapshot(int offset, int count)
    {
        if (CommonTransaction is { } transaction)
        {
            return transaction.ReadSnapshot(this, offset, count);
        }
        ValidateCommonPacketMemory();
        CommonSliceRange slices = CommonSlices(offset, count);
        byte[] bytes = new byte[count];
        bool[] known = new bool[count];
        foreach (CommonSlice slice in slices)
        {
            NativeBattleEffectArena memory = CommonArena(slice.Bank) ?? UnknownCommonArena(CommonPacketNominalCapacity);
            (byte[] currentBytes, bool[] currentKnown) = memory.ReadSnapshot(slice.Offset, slice.Count);
            currentBytes.CopyTo(bytes, slice.SourceOffset);
            currentKnown.CopyTo(known, slice.SourceOffset);
        }
        return (bytes, known);
    }

    internal readonly record struct CommonSlice(int Bank, int Offset, int Count, int SourceOffset);

    internal readonly record struct CommonSliceRange(int Bank, int Stride, int Offset, int Count)
    {
        public Enumerator GetEnumerator() => new(this);

        public struct Enumerator(CommonSliceRange range)
        {
            private int cursor;
            public CommonSlice Current { get; private set; }

            public bool MoveNext()
            {
                if (cursor >= range.Count)
                {
                    return false;
                }
                int relative = checked(range.Offset + cursor);
                int local = relative % range.Stride;
                int length = Math.Min(range.Count - cursor, range.Stride - local);
                Current = new(range.Bank + relative / range.Stride, local, length, cursor);
                cursor += length;
                return true;
            }
        }
    }

    internal CommonSliceRange CommonSlices(int offset, int count)
    {
        int bank = CommonBank(out int stride);
        RequireCommonRange(offset, count, CommonPacketWriteCapacity);
        return new(bank, stride, offset, count);
    }

    public void ValidateCommonPacketMemory()
    {
        if (CommonPacketMemory is not { } memory)
        {
            return;
        }
        ValidateCommonArena(memory.Bank070Buffer0, 114688);
        ValidateCommonArena(memory.Bank070Buffer1, 114688);
        ValidateCommonArena(memory.Bank094Buffer0, 147456);
        ValidateCommonArena(memory.Bank094Buffer1, 147456);
    }

    private static void ValidateCommonArena(NativeBattleEffectArena? arena, int capacity)
    {
        if (arena is not null)
        {
            arena.ValidateExplicitCoverage(capacity);
        }
    }

    internal int CommonBank(out int capacity)
    {
        if (PacketBuffer > 1 || PacketBank is not ("off_B6D070" or "off_B6D094"))
        {
            throw new NotSupportedException("Native common packet access requires its published physical bank and buffer.");
        }
        capacity = PacketBank == "off_B6D070" ? 114688 : 147456;
        return (PacketBank == "off_B6D070" ? 0 : 2) + PacketBuffer;
    }

    internal NativeBattleEffectArena? CommonArena(int bank)
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

    internal NativeBattlePrimitiveProcess StoreCommonArena(int bank, NativeBattleEffectArena arena)
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

    internal static NativeBattleEffectArena UnknownCommonArena(int capacity)
    {
        return NativeBattleEffectArena.CreateUnknown(capacity);
    }

    private static void RequireCommonRange(int offset, int count, int capacity)
    {
        if (offset < 0 || count < 0 || offset > capacity - count)
        {
            throw new InvalidDataException("Native common packet access exceeds its physical bank capacity.");
        }
    }
}
