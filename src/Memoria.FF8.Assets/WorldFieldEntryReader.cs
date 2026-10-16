using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public readonly record struct WorldFieldSelectionInput(int X, int RawZ, ushort StoryWord, int EntityType);
public readonly record struct WorldFieldEntry(int Index, short X, short Y, ushort Triangle, ushort FieldId, byte Direction);
public readonly record struct WorldFieldPredicateEvaluation(int Offset, ushort Opcode, ushort Operand, bool? Passed);
public sealed record WorldFieldSelection(int ClauseIndex, int ClauseOffset, int InstructionOffset, WorldFieldEntry Entry, IReadOnlyList<WorldFieldPredicateEvaluation> Predicates);

/// <summary>Original WMSET section7/36 selection (545F10/546100/545EA0/54D7E0) and wm2field.tbl packets (4706B0).</summary>
public sealed partial class WorldFieldEntryReader
{
    private readonly byte[] program;
    private readonly int[] clauses;
    private readonly WorldFieldEntry[] entries;
    private readonly int firstInstruction;
    private readonly byte[] table;
    private readonly int sectionIndex;

    private WorldFieldEntryReader(byte[] program, int[] clauses, WorldFieldEntry[] entries, int firstInstruction, byte[] table, int sectionIndex)
    {
        this.program = program;
        this.clauses = clauses;
        this.entries = entries;
        this.firstInstruction = firstInstruction;
        this.table = table;
        this.sectionIndex = sectionIndex;
    }

    public int ClauseCount => clauses.Length;
    public int ProgramLength => program.Length;
    public IReadOnlyList<WorldFieldEntry> Entries => Array.AsReadOnly(entries);

    public static WorldFieldEntryReader Read(ReadOnlySpan<byte> section7, ReadOnlySpan<byte> wm2field)
    {
        return Read(section7, wm2field, 7);
    }

    public WorldFieldEntryReader ReadWorldEvents(ReadOnlySpan<byte> section36)
    {
        return Read(section36, table, 36);
    }

    private static WorldFieldEntryReader Read(ReadOnlySpan<byte> section7, ReadOnlySpan<byte> wm2field, int sectionIndex)
    {
        int sectionLength = section7.Length;
        if (section7.Length < 4 || section7.Length % 4 != 0)
        {
            throw new InvalidDataException($"Invalid WMSET section{sectionIndex} instruction alignment.");
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
                throw new InvalidDataException($"Invalid WMSET section{sectionIndex} clause pointer.");
            }
            pointers.Add((int)pointer);
        }
        if (BinaryPrimitives.ReadUInt32LittleEndian(section7[(position - 4)..]) != 0 || pointers.Any(pointer => pointer < position || pointer > sectionLength - 4 || pointer % 4 != 0))
        {
            throw new InvalidDataException($"Unterminated or invalid WMSET section{sectionIndex} clause table.");
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
        return new WorldFieldEntryReader(section7.ToArray(), pointers.ToArray(), entries, position, wm2field.ToArray(), sectionIndex);
    }

    public WorldFieldSelection? Select(WorldFieldSelectionInput input, byte terrainTextureFlags)
    {
        if (sectionIndex != 7)
        {
            throw new InvalidOperationException("Terrain selection requires WMSET section7.");
        }
        // Native545EA0 never consults the clauses without the accepted terrain polygon's bit 8.
        if ((terrainTextureFlags & 8) == 0)
        {
            return null;
        }
        return Select(input, null, null, null, null);
    }

    public WorldFieldSelection? SelectWorldEvent(WorldFieldSelectionInput input, Func<ushort, bool> nearAllocatedShape, NativeWorldEventFlags savedWorldFlags116, Func<ushort, ushort, bool?>? evaluateRuntimePredicate = null, Action<ushort, ushort>? executeRuntimeCommand = null)
    {
        if (sectionIndex != 36)
        {
            throw new InvalidOperationException("Ordinary world events require WMSET section36.");
        }
        ArgumentNullException.ThrowIfNull(nearAllocatedShape);
        ArgumentNullException.ThrowIfNull(savedWorldFlags116);
        savedWorldFlags116.Validate();
        return Select(input, nearAllocatedShape, savedWorldFlags116, evaluateRuntimePredicate, executeRuntimeCommand);
    }

    private WorldFieldSelection? Select(WorldFieldSelectionInput input, Func<ushort, bool>? nearAllocatedShape, NativeWorldEventFlags? savedWorldFlags116, Func<ushort, ushort, bool?>? evaluateRuntimePredicate, Action<ushort, ushort>? executeRuntimeCommand)
    {
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
            WorldFieldPredicateEvaluation? unresolved = null;
            var visited = new HashSet<(int Position, int Phase, int Branch)>();
            bool nextClause = false;
            while (!nextClause)
            {
                CheckPosition(position);
                if (!visited.Add((position, phase, branch)))
                {
                    throw new InvalidDataException($"WMSET section{sectionIndex} selection contains an instruction cycle.");
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
                    if (unresolved is WorldFieldPredicateEvaluation missing)
                    {
                        throw UnknownPredicate(missing);
                    }
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
                    //Pure following predicates can reject both alternatives of an unknown read.
                    //FF29's supported absent-window result is a read-only rejection certificate.
                    if (unresolved is WorldFieldPredicateEvaluation unknown && opcode is not (0xFF02 or 0xFF03 or 0xFF06 or 0xFF07 or 0xFF09 or 0xFF0E or 0xFF0F or 0xFF10 or 0xFF11 or 0xFF12 or 0xFF17 or 0xFF1B or 0xFF25 or 0xFF27 or 0xFF29 or 0xFF2D or 0xFF30 or 0xFF31 or 0xFF32))
                    {
                        throw UnknownPredicate(unknown);
                    }
                    if (opcode == 0xFF0E)
                    {
                        position = operand;
                    }
                    else
                    {
                        bool? passed = Evaluate(input, position, opcode, operand, trace, nearAllocatedShape, savedWorldFlags116, evaluateRuntimePredicate);
                        if (passed == false)
                        {
                            nextClause = true;
                        }
                        else if (passed is null)
                        {
                            unresolved ??= trace[^1];
                        }
                        position += 4;
                    }
                    continue;
                }
                switch (opcode)
                {
                    case 0xFF16:
                        //545EA0 passes a2=0;54D7E0 passes1 and continues the clause table.
                        if (sectionIndex == 7)
                        {
                            return null;
                        }
                        nextClause = true;
                        break;
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
                                bool? passed = Evaluate(input, position, opcode, operand, trace, nearAllocatedShape, savedWorldFlags116, evaluateRuntimePredicate);
                                if (passed is null)
                                {
                                    throw UnknownPredicate(trace[^1]);
                                }
                                if (passed == false)
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
                                WorldFieldEntry? entry = ReadSelectedPacket(position, executeRuntimeCommand);
                                return entry is WorldFieldEntry packet ? new WorldFieldSelection(clauseIndex, clauses[clauseIndex], position, packet, trace.ToArray()) : null;
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

    private WorldFieldEntry? ReadSelectedPacket(int position, Action<ushort, ushort>? executeRuntimeCommand)
    {
        int selected = -1;
        var visited = new HashSet<int>();
        while (true)
        {
            CheckPosition(position);
            if (!visited.Add(position))
            {
                throw new InvalidDataException($"WMSET section{sectionIndex} packet commands contain an instruction cycle.");
            }
            ushort opcode = Word(position);
            ushort operand = Word(position + 2);
            if (opcode == 0xFF05)
            {
                //54D7E0 ends the first selected stream here, including command-only events.
                if (sectionIndex == 36)
                {
                    return null;
                }
                if ((uint)selected >= entries.Length)
                {
                    throw new InvalidDataException($"WMSET section{sectionIndex} selected an absent wm2field.tbl record.");
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
                //54D7E0 returns immediately from its first reached FF08 command.
                if (sectionIndex == 36)
                {
                    if ((uint)selected >= entries.Length)
                    {
                        throw new InvalidDataException("WMSET section36 selected an absent wm2field.tbl record.");
                    }
                    return entries[selected];
                }
                position += 4;
            }
            else if (sectionIndex == 36 && executeRuntimeCommand is not null)
            {
                executeRuntimeCommand(opcode, operand);
                position += 4;
            }
            else
            {
                throw new NotSupportedException($"Reached unsupported world field command 0x{opcode:X4} at section{sectionIndex} offset{position}.");
            }
        }
    }

    private bool? Evaluate(WorldFieldSelectionInput input, int position, ushort opcode, ushort operand, List<WorldFieldPredicateEvaluation> trace, Func<ushort, bool>? nearAllocatedShape, NativeWorldEventFlags? savedWorldFlags116, Func<ushort, ushort, bool?>? evaluateRuntimePredicate)
    {
        bool? passed = opcode switch
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
            0xFF17 when nearAllocatedShape is not null => nearAllocatedShape(operand),
            0xFF27 when savedWorldFlags116 is not null => savedWorldFlags116.IsKnown(operand) ? savedWorldFlags116.Matches(operand) : null,
            0xFF2D or 0xFF30 or 0xFF31 when sectionIndex == 36 => evaluateRuntimePredicate?.Invoke(opcode, operand),
            _ when evaluateRuntimePredicate is not null => evaluateRuntimePredicate(opcode, operand),
            _ => throw new NotSupportedException($"Reached unsupported world field predicate 0x{opcode:X4}/{operand} at section {sectionIndex} offset {position}.")
        };
        trace.Add(new WorldFieldPredicateEvaluation(position, opcode, operand, passed));
        return passed;
    }

    private NotSupportedException UnknownPredicate(WorldFieldPredicateEvaluation predicate)
    {
        return new NotSupportedException($"World event selection depends on unauthenticated predicate0x{predicate.Opcode:X4}/{predicate.Operand} at section{sectionIndex} offset{predicate.Offset}.");
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
            throw new InvalidDataException($"WMSET section{sectionIndex} instruction lies outside its command stream.");
        }
    }
}
