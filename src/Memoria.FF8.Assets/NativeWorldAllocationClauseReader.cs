using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Assets;

/// <summary>The executed simple-range section9 clause, without trailing resource alignment padding or materialized vehicle state.</summary>
public sealed record NativeWorldAllocationClause(int Index, int Offset, int Length, ushort MinimumStory, ushort MaximumStory, ImmutableArray<ushort> Words)
{
    public bool ContainsShape(byte shape)
    {
        for (int index = 8; index < Words.Length - 2; index += 2)
        {
            if (Words[index] == 0xFF13 && unchecked((byte)Words[index + 1]) == shape)
            {
                return true;
            }
        }
        return false;
    }
}

public static class NativeWorldAllocationClauseReader
{
    public static NativeWorldAllocationClause Read(ReadOnlySpan<byte> program, ushort story)
    {
        if (program.Length < 12 || program.Length % 4 != 0)
        {
            throw new InvalidDataException("Native world allocation program has no complete pointer table or instruction words.");
        }
        int tableBytes = BinaryPrimitives.ReadInt32LittleEndian(program);
        if (tableBytes < 8 || tableBytes % 4 != 0 || tableBytes > program.Length - 4 || BinaryPrimitives.ReadInt32LittleEndian(program[(tableBytes - 4)..]) != 0)
        {
            throw new InvalidDataException("Native world allocation pointer table overlaps its clause bytes.");
        }
        int count = tableBytes / 4 - 1;
        var pointers = new int[count + 1];
        for (int index = 0; index < count; index++)
        {
            int pointer = BinaryPrimitives.ReadInt32LittleEndian(program[(index * 4)..]);
            if (pointer < tableBytes || pointer % 4 != 0 || pointer >= program.Length || index > 0 && pointer <= pointers[index - 1])
            {
                throw new InvalidDataException("Native world allocation clause pointer is overlapping, unordered or truncated.");
            }
            pointers[index] = pointer;
        }
        pointers[count] = program.Length;
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> clause = program[pointers[index]..pointers[index + 1]];
            if (clause.Length < 24 || Word(clause, 0) != 0xFF01 || Word(clause, 2) != 0 || Word(clause, 4) != 0xFF02 || Word(clause, 8) != 0xFF03 || Word(clause, 12) != 0xFF04 || Word(clause, 14) != 0)
            {
                throw new NotSupportedException("Native world allocation selection reached an unrepresented conditional or branch program.");
            }
            ushort minimum = Word(clause, 6);
            ushort maximum = Word(clause, 10);
            if (minimum >= maximum)
            {
                throw new InvalidDataException("Native world allocation story range is empty or reversed.");
            }
            if (story < minimum || story >= maximum)
            {
                continue;
            }
            for (int offset = 16; offset < clause.Length; offset += 4)
            {
                ushort opcode = Word(clause, offset);
                if (opcode == 0xFF05)
                {
                    if (Word(clause, offset + 2) != 0)
                    {
                        throw new NotSupportedException("Native world allocation terminal instruction has an unrepresented operand.");
                    }
                    int length = offset + 4;
                    ReadOnlySpan<byte> padding = clause[length..];
                    //544860 stops at FF05. The final original section9 clause has one zero DWORD of resource alignment.
                    if (padding.Length != 0 && (index != count - 1 || padding.Length != 4 || padding.IndexOfAnyExcept((byte)0) >= 0))
                    {
                        throw new NotSupportedException("Native world allocation has unrepresented bytes after its terminal instruction.");
                    }
                    var words = ImmutableArray.CreateBuilder<ushort>(length / 2);
                    for (int word = 0; word < length; word += 2)
                    {
                        words.Add(Word(clause, word));
                    }
                    return new(index, pointers[index], length, minimum, maximum, words.MoveToImmutable());
                }
                if (opcode != 0xFF13)
                {
                    throw new NotSupportedException("Native world allocation selected an unrepresented record kind or bytecode branch.");
                }
            }
            throw new NotSupportedException("Native world allocation selected clause has no terminal instruction.");
        }
        throw new NotSupportedException("Original world allocation has no represented selected simple-range clause.");
    }

    private static ushort Word(ReadOnlySpan<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    }
}

public sealed partial class WorldAssets
{
    public NativeWorldAllocationClause ReadWorldAllocationClause(ushort story)
    {
        return NativeWorldAllocationClauseReader.Read(wmset.AsSpan(sections[9], sections[10] - sections[9]), story);
    }
}
