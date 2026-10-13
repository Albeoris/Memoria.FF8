using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public readonly record struct WorldFieldSelectionInput(int X, int RawZ, ushort StoryWord, int EntityType);
public readonly record struct WorldFieldEntry(int Index, short X, short Y, ushort Triangle, ushort FieldId, byte Direction);
public readonly record struct WorldFieldPredicateEvaluation(int Offset, ushort Opcode, ushort Operand, bool Passed);
public sealed record WorldFieldSelection(int ClauseIndex, int ClauseOffset, int InstructionOffset, WorldFieldEntry Entry, IReadOnlyList<WorldFieldPredicateEvaluation> Predicates);

/// <summary>Original WMSET section 7 selection (545F10/546100/545EA0) and wm2field.tbl packets (4706B0).</summary>
public sealed class WorldFieldEntryReader
{
    private readonly byte[] program;
    private readonly int[] clauses;
    private readonly WorldFieldEntry[] entries;
    private readonly int firstInstruction;

    private WorldFieldEntryReader(byte[] program, int[] clauses, WorldFieldEntry[] entries, int firstInstruction)
    {
        this.program = program;
        this.clauses = clauses;
        this.entries = entries;
        this.firstInstruction = firstInstruction;
    }

    public int ClauseCount => clauses.Length;
    public int ProgramLength => program.Length;
    public IReadOnlyList<WorldFieldEntry> Entries => Array.AsReadOnly(entries);

    public static WorldFieldEntryReader Read(ReadOnlySpan<byte> section7, ReadOnlySpan<byte> wm2field)
    {
        int sectionLength = section7.Length;
        if (section7.Length < 4 || section7.Length % 4 != 0)
        {
            throw new InvalidDataException("Invalid WMSET section 7 instruction alignment.");
        }
        var pointers = new List<int>();
        int position = 0;
        while (position <= section7.Length - 4)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(section7[position..]);
            position += 4;
            if (pointer == 0)
            {
                break;
            }
            if (pointer > int.MaxValue)
            {
                throw new InvalidDataException("Invalid WMSET section 7 clause pointer.");
            }
            pointers.Add((int)pointer);
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(section7[(position - 4)..]) != 0 || pointers.Any(pointer => pointer < position || pointer > sectionLength - 4 || pointer % 4 != 0))
        {
            throw new InvalidDataException("Unterminated or invalid WMSET section 7 clause table.");
        }
        if (wm2field.Length == 0 || wm2field.Length % 24 != 0)
        {
            throw new InvalidDataException("Invalid wm2field.tbl 24-byte record table.");
        }
        var entries = new WorldFieldEntry[wm2field.Length / 24];
        for (int index = 0; index < entries.Length; index++)
        {
            ReadOnlySpan<byte> record = wm2field.Slice(index * 24, 24);
            entries[index] = new WorldFieldEntry(index, BinaryPrimitives.ReadInt16LittleEndian(record), BinaryPrimitives.ReadInt16LittleEndian(record[2..]), BinaryPrimitives.ReadUInt16LittleEndian(record[4..]), BinaryPrimitives.ReadUInt16LittleEndian(record[6..]), record[8]);
        }
        return new WorldFieldEntryReader(section7.ToArray(), pointers.ToArray(), entries, position);
    }

    public WorldFieldSelection? Select(WorldFieldSelectionInput input, byte terrainTextureFlags)
    {
        // Native545EA0 never consults the clauses without the accepted terrain polygon's bit 8.
        if ((terrainTextureFlags & 8) == 0)
        {
            return null;
        }
        if (input.X is < -131072 or >= 131072 || input.RawZ is < -98304 or >= 98304)
        {
            throw new NotSupportedException("World field selection requires centered native X/raw Z coordinates.");
        }
        var trace = new List<WorldFieldPredicateEvaluation>();
        for (int clauseIndex = 0; clauseIndex < clauses.Length; clauseIndex++)
        {
            int position = clauses[clauseIndex];
            int phase = 0;
            int branch = 3;
            var visited = new HashSet<(int Position, int Phase, int Branch)>();
            bool nextClause = false;
            while (!nextClause)
            {
                CheckPosition(position);
                if (!visited.Add((position, phase, branch)))
                {
                    throw new InvalidDataException("WMSET section 7 selection contains an instruction cycle.");
                }
                ushort opcode = Word(position);
                ushort operand = Word(position + 2);
                if (opcode == 0xFF01)
                {
                    phase = 1;
                    position += 4;
                    continue;
                }
                if (opcode == 0xFF04)
                {
                    phase = 2;
                    branch = 3;
                    position += 4;
                    continue;
                }
                if (phase == 0)
                {
                    position += 4;
                    continue;
                }
                if (phase == 1)
                {
                    if (opcode == 0xFF0E)
                    {
                        position = operand;
                    }
                    else if (!Evaluate(input, position, opcode, operand, trace))
                    {
                        nextClause = true;
                    }
                    else
                    {
                        position += 4;
                    }
                    continue;
                }
                switch (opcode)
                {
                    case 0xFF16:
                        // Native545EA0 passes a2=0: this terminator stops selection, rather than advancing the table.
                        return null;
                    case 0xFF05:
                        if (branch is not 2 and not 5)
                        {
                            nextClause = true;
                        }
                        position += 4;
                        break;
                    case 0xFF0A:
                        branch = 1;
                        position += 4;
                        break;
                    case 0xFF0B:
                        if (branch is 1 or 4)
                        {
                            branch = 3;
                        }
                        position += 4;
                        break;
                    case 0xFF0C:
                        if (branch is 2 or 5)
                        {
                            branch = 4;
                        }
                        position += 4;
                        break;
                    case 0xFF0D:
                        if (branch is 2 or 5)
                        {
                            branch = 6;
                        }
                        position += 4;
                        break;
                    default:
                        if (branch is 1 or 4)
                        {
                            if (opcode == 0xFF0E)
                            {
                                position = operand;
                            }
                            else
                            {
                                if (!Evaluate(input, position, opcode, operand, trace))
                                {
                                    branch = branch == 1 ? 2 : 5;
                                }
                                position += 4;
                            }
                        }
                        else if (branch is 3 or 6)
                        {
                            if (opcode == 0xFF0E)
                            {
                                position = operand;
                            }
                            else
                            {
                                WorldFieldEntry entry = ReadSelectedPacket(position);
                                return new WorldFieldSelection(clauseIndex, clauses[clauseIndex], position, entry, trace.ToArray());
                            }
                        }
                        else
                        {
                            // Failed branch predicates and commands are skipped without consulting unknown state.
                            position += 4;
                        }
                        break;
                }
            }
        }
        return null;
    }

    private WorldFieldEntry ReadSelectedPacket(int position)
    {
        int selected = -1;
        var visited = new HashSet<int>();
        while (true)
        {
            CheckPosition(position);
            if (!visited.Add(position))
            {
                throw new InvalidDataException("WMSET section 7 packet commands contain an instruction cycle.");
            }
            ushort opcode = Word(position);
            ushort operand = Word(position + 2);
            if (opcode == 0xFF05)
            {
                if ((uint)selected >= entries.Length)
                {
                    throw new InvalidDataException("WMSET section 7 selected an absent wm2field.tbl record.");
                }
                return entries[selected];
            }
            if (opcode == 0xFF0E)
            {
                position = operand;
            }
            else if (opcode == 0xFF08)
            {
                selected = operand;
                position += 4;
            }
            else
            {
                throw new NotSupportedException($"Reached unsupported world field command 0x{opcode:X4} at section 7 offset {position}.");
            }
        }
    }

    private static bool Evaluate(WorldFieldSelectionInput input, int position, ushort opcode, ushort operand, List<WorldFieldPredicateEvaluation> trace)
    {
        bool passed = opcode switch
        {
            0xFF02 => input.StoryWord >= operand,
            0xFF03 => input.StoryWord < operand,
            0xFF06 => operand == 32 * ((input.RawZ + 294912) % 196608 / 8192) + (input.X + 393216) % 262144 / 8192,
            0xFF07 => operand == 128 * ((input.RawZ + 294912) % 196608 / 2048) + (input.X + 393216) % 262144 / 2048,
            0xFF09 => MatchEntity(input.EntityType, operand),
            0xFF0F => operand > (input.X & 8191),
            0xFF10 => operand > (input.RawZ & 8191),
            0xFF11 => operand < (input.X & 8191),
            0xFF12 => operand < (input.RawZ & 8191),
            _ => throw new NotSupportedException($"Reached unsupported world field predicate 0x{opcode:X4} at section 7 offset {position}.")
        };
        trace.Add(new WorldFieldPredicateEvaluation(position, opcode, operand, passed));
        return passed;
    }

    private static bool MatchEntity(int type, ushort operand)
    {
        // Native546100's normal path has no map-preview override (2040A2C).
        return operand switch
        {
            33 or 48 or 49 or 50 => type == operand,
            128 => (uint)type < 10 || type == 128,
            129 => (uint)type < 2,
            130 => type is >= 8 and <= 9,
            131 => type is >= 16 and <= 22,
            132 => type is >= 32 and <= 40 || type == 132,
            133 => type is >= 34 and <= 40,
            _ => true
        };
    }

    private ushort Word(int position)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan(position, 2));
    }

    private void CheckPosition(int position)
    {
        if (position < firstInstruction || position > program.Length - 4 || position % 4 != 0)
        {
            throw new InvalidDataException("WMSET section 7 instruction lies outside its command stream.");
        }
    }
}
