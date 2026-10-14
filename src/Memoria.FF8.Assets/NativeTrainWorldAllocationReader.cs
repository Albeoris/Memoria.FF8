using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeTrainWorldAllocationReader
{
    public static NativeTrainWorldAllocation Read(ReadOnlySpan<byte> program, ReadOnlySpan<byte> coordinates, ushort story, byte eventFlags103)
    {
        if (program.Length < 12 || program.Length % 4 != 0)
        {
            throw new InvalidDataException("Native world allocation program has no complete pointer table or instruction words.");
        }
        int tableBytes = ReadPointer(program, 0);
        if (tableBytes < 8 || tableBytes % 4 != 0 || tableBytes > program.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(program[(tableBytes - 4)..]) != 0)
        {
            throw new InvalidDataException("Native world allocation pointer table overlaps its clause bytes.");
        }
        int count = tableBytes / 4 - 1;
        var pointers = new int[count + 1];
        for (int index = 0; index < count; index++)
        {
            int pointer = ReadPointer(program, index * 4);
            if (pointer < tableBytes || pointer % 4 != 0 || pointer >= program.Length || index > 0 && pointer <= pointers[index - 1])
            {
                throw new InvalidDataException("Native world allocation clause pointer is overlapping, unordered or truncated.");
            }
            pointers[index] = pointer;
        }
        pointers[count] = program.Length;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> clause = program.Slice(pointers[index], pointers[index + 1] - pointers[index]);
            if (clause.Length < 24 || !HasHeader(clause))
            {
                throw new NotSupportedException("Native world allocation selection reached an unrepresented conditional or branch program.");
            }
            ushort minimum = ReadWord(clause, 6);
            ushort maximum = ReadWord(clause, 10);
            if (minimum >= maximum)
            {
                throw new InvalidDataException("Native world allocation story range is empty or reversed.");
            }
            if (story < minimum || story >= maximum)
            {
                continue;
            }
            return ReadSelected(program, coordinates, clause, index, minimum, maximum, story, eventFlags103);
        }
        throw new NotSupportedException("Original world allocation has no represented selected simple-range clause.");
    }

    private static NativeTrainWorldAllocation ReadSelected(ReadOnlySpan<byte> program, ReadOnlySpan<byte> coordinates, ReadOnlySpan<byte> clause, int clauseIndex, ushort minimum, ushort maximum, ushort story, byte eventFlags103)
    {
        if (ReadWord(clause, clause.Length - 4) != 0xFF05 || ReadWord(clause, clause.Length - 2) != 0)
        {
            throw new NotSupportedException("Native world allocation selected clause has an unrepresented terminal instruction.");
        }
        var objects = ImmutableArray.CreateBuilder<NativeTrainWorldObject>();
        var words = ImmutableArray.CreateBuilder<ushort>(clause.Length / 2);
        for (int offset = 0; offset < clause.Length; offset += 2)
        {
            words.Add(ReadWord(clause, offset));
        }
        int controlled = -1;
        for (int offset = 16; offset < clause.Length - 4; offset += 4)
        {
            if (ReadWord(clause, offset) != 0xFF13)
            {
                throw new NotSupportedException("Native world allocation selected an unrepresented record kind or bytecode branch.");
            }
            ushort operand = ReadWord(clause, offset + 2);
            byte shape = unchecked((byte)operand);
            byte coordinate = unchecked((byte)(operand >> 8));
            int coordinateOffset = coordinate * 16;
            if (coordinate >= 128)
            {
                throw new NotSupportedException("Native world allocation null-coordinate records require retained physical allocation memory.");
            }
            if (coordinateOffset > coordinates.Length - 16)
            {
                throw new InvalidDataException("Native world allocation coordinate leaves original section10.");
            }
            ReadOnlySpan<byte> raw = coordinates.Slice(coordinateOffset, 16);
            int x = BinaryPrimitives.ReadInt32LittleEndian(raw);
            int y = BinaryPrimitives.ReadInt32LittleEndian(raw[4..]);
            int z = BinaryPrimitives.ReadInt32LittleEndian(raw[8..]);
            int orientation = BinaryPrimitives.ReadInt32LittleEndian(raw[12..]);
            if (y is < short.MinValue or > short.MaxValue || z == int.MinValue)
            {
                throw new NotSupportedException("Native world allocation requires a coordinate outside the represented saved-pose widths.");
            }
            var item = new NativeTrainWorldObject(shape, coordinate, new NativeSavedWorldPose(x, -z, (short)y, unchecked((ushort)orientation)), operand, ImmutableArray.CreateRange(raw.ToArray()));
            item.Validate();
            if (controlled < 0 && (item.EntityType is >= 0 and <= 9 or 128))
            {
                controlled = objects.Count;
            }
            objects.Add(item);
        }
        if (objects.Count is < 1 or > 64 || controlled < 0)
        {
            throw new NotSupportedException("Native train allocation has no represented normal-foot record or exceeds the native object table.");
        }
        var allocation = new NativeTrainWorldAllocation(1, NativeTrainWorldAllocation.AllocationRule, Hash(program), Hash(coordinates), Hash(clause), clauseIndex, minimum, maximum, story, eventFlags103, words.MoveToImmutable(), objects.ToImmutable(), controlled);
        allocation.Validate();
        return allocation;
    }

    private static bool HasHeader(ReadOnlySpan<byte> clause)
    {
        return ReadWord(clause, 0) == 0xFF01 && ReadWord(clause, 2) == 0 && ReadWord(clause, 4) == 0xFF02 && ReadWord(clause, 8) == 0xFF03 && ReadWord(clause, 12) == 0xFF04 && ReadWord(clause, 14) == 0;
    }

    private static ushort ReadWord(ReadOnlySpan<byte> source, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(source[offset..]);
    }

    private static int ReadPointer(ReadOnlySpan<byte> source, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
    }

    private static string Hash(ReadOnlySpan<byte> source)
    {
        return Convert.ToHexString(SHA256.HashData(source));
    }
}

public sealed partial class WorldAssets
{
    private readonly Dictionary<(ushort Story, byte EventFlags103), NativeTrainWorldAllocation> trainAllocations = [];

    public NativeTrainWorldAllocation ReadTrainWorldAllocation(ushort story, byte eventFlags103)
    {
        var key = (story, eventFlags103);
        if (!trainAllocations.TryGetValue(key, out NativeTrainWorldAllocation? allocation))
        {
            allocation = NativeTrainWorldAllocationReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), wmset.AsSpan(sections[10], sections[11] - sections[10]), story, eventFlags103);
            trainAllocations.Add(key, allocation);
        }
        return allocation;
    }
}
