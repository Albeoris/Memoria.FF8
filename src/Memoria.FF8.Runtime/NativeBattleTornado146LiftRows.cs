using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleTornado146LiftRows(ImmutableArray<BattleModelOwner> Recipients, ImmutableDictionary<BattleModelOwner, NativeBattleTornado146LiftRow> Rows);
internal sealed record NativeBattleTornado146LiftRow(ImmutableArray<byte> Data, ImmutableArray<bool> Known);

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    internal NativeBattleTornado146LiftRows? TornadoLiftRows { get; init; }
}

internal sealed class NativeBattleTornado146LiftRowWriter
{
    internal const int Length = 52;

    private readonly NativeBattleTornado146Work work;
    private readonly uint? originalAddress;
    private readonly byte[] data;
    private readonly bool[] known;

    internal NativeBattleTornado146LiftRowWriter(NativeBattleTornado146Work work, uint? originalAddress, NativeBattleTornado146LiftRow? saved)
    {
        this.work = work;
        this.originalAddress = originalAddress;
        (data, known) = saved is { } row ? (row.Data.ToArray(), row.Known.ToArray()) : originalAddress is uint address ? work.ReadSnapshot(address, Length) : (new byte[Length], new bool[Length]);
    }

    internal ushort Word(int offset)
    {
        RequireKnown(offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
    }

    internal uint Dword(int offset)
    {
        RequireKnown(offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
    }

    internal short SignedWord(int offset) => unchecked((short)Word(offset));
    internal int SignedDword(int offset) => unchecked((int)Dword(offset));
    internal ushort? OptionalWord(int offset) => known[offset] && known[offset + 1] ? Word(offset) : null;

    internal void WriteWord(int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
        known.AsSpan(offset, 2).Fill(true);
        if (originalAddress is uint address)
        {
            work.WriteWord(address + (uint)offset, value);
        }
    }

    internal void WriteDword(int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, 4), value);
        known.AsSpan(offset, 4).Fill(true);
        if (originalAddress is uint address)
        {
            work.WriteDword(address + (uint)offset, value);
        }
    }

    internal void MarkUnknown(int offset, int count)
    {
        known.AsSpan(offset, count).Fill(false);
        if (originalAddress is uint address)
        {
            work.MarkUnknown(address + (uint)offset, count);
        }
    }

    internal NativeBattleVector Vector(int offset) => new(SignedWord(offset), SignedWord(offset + 2), SignedWord(offset + 4));

    internal void WriteVector(int offset, NativeBattleVector value)
    {
        WriteWord(offset, unchecked((ushort)value.X));
        WriteWord(offset + 2, unchecked((ushort)value.Y));
        WriteWord(offset + 4, unchecked((ushort)value.Z));
    }

    internal NativeBattleTornado146LiftRow Freeze() => new(data.ToImmutableArray(), known.ToImmutableArray());

    private void RequireKnown(int offset, int count)
    {
        for (int index = offset; index < offset + count; index++)
        {
            if (!known[index])
            {
                throw new InvalidDataException("Native Tornado lift read an unobserved model work byte.");
            }
        }
    }
}
