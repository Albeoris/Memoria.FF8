using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleSequenceSet
{
    /// <summary>Resolve only control programs whose reads are owned by explicit local writes. Pose/effect predicates require the graphical VM.</summary>
    public NativeBattleSequenceTrack? ReadLocalControlTrack(byte sequence)
    {
        if (!Contains(sequence))
        {
            return null;
        }
        int start = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(sequence * 2));
        int end = bytes.Length;
        for (int index = 1; index <= BinaryPrimitives.ReadUInt16LittleEndian(bytes); index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(index * 2));
            if (offset > start)
            {
                end = Math.Min(end, offset);
            }
        }
        int?[] variables = new int?[128];
        List<NativeBattleSequenceMessage> messages = [];
        int position = start;
        int frame = 0;
        int accumulator = 0;
        int? completed = null;
        for (int instructions = 0; instructions < 100_000; instructions++)
        {
            if (position < start || position >= end)
            {
                throw new InvalidDataException("Original local battle sequence branches beyond its bounded program.");
            }
            int origin = position;
            byte opcode = bytes[position++];
            if (opcode >= 0xC0)
            {
                if (opcode < 0xE4)
                {
                    int operand;
                    int kind = opcode & 3;
                    if (kind == 0)
                    {
                        operand = unchecked((short)(ReadByte() | ReadByte() << 8));
                    }
                    else
                    {
                        int value = ReadByte();
                        if (kind == 3)
                        {
                            if (value >= 128 || value is >= 8 and < 120 || variables[value] is not int local)
                            {
                                return null;
                            }
                            operand = local;
                        }
                        else
                        {
                            operand = kind == 1 ? unchecked((sbyte)value) : value;
                        }
                    }
                    if ((opcode & 0xFC) is 0xD0 or 0xE0 && operand == 0)
                    {
                        throw new InvalidDataException("Original local battle sequence divides by zero.");
                    }
                    accumulator = (opcode & 0xFC) switch
                    {
                        0xC0 => operand,
                        0xC4 => unchecked(accumulator + operand),
                        0xC8 => unchecked(accumulator - operand),
                        0xCC => unchecked(accumulator * operand),
                        0xD0 => accumulator / operand,
                        0xD4 => accumulator & operand,
                        0xD8 => accumulator | operand,
                        0xDC => accumulator ^ operand,
                        0xE0 => accumulator % operand,
                        _ => throw new InvalidDataException("Invalid original battle expression operation.")
                    };
                }
                else if (opcode == 0xE4)
                {
                    accumulator = 0;
                }
                else if (opcode == 0xE5)
                {
                    int variable = ReadByte();
                    if (variable is >= 8 and < 120 || variable >= 128)
                    {
                        return null;
                    }
                    //5048E0 stores local/scratch values as signedWORD;50DB40's accumulator is signedDWORD.
                    variables[variable] = unchecked((short)accumulator);
                }
                else if (opcode <= 0xF3)
                {
                    bool take = ((opcode - 0xE6) % 7) switch
                    {
                        0 => true,
                        1 => accumulator > 0,
                        2 => accumulator >= 0,
                        3 => accumulator == 0,
                        4 => accumulator != 0,
                        5 => accumulator <= 0,
                        6 => accumulator < 0,
                        _ => false
                    };
                    int relative = opcode < 0xED ? unchecked((sbyte)ReadByte()) : unchecked((short)(ReadByte() | ReadByte() << 8));
                    if (take)
                    {
                        position = origin + relative;
                    }
                }
                else
                {
                    return null;
                }
                continue;
            }
            switch (opcode)
            {
                case 0x91:
                    messages.Add(new(frame, ReadByte(), ReadByte()));
                    break;
                case 0xA0:
                    //Setting the same pose does not yield; onlyA1 yields this local/control callback.
                    _ = ReadByte();
                    break;
                case 0xA1:
                    frame++;
                    accumulator = 0;
                    break;
                case 0xAA:
                    completed = frame;
                    break;
                case 0xA2:
                case 0xA3:
                    return completed is int completion ? new(sequence, bytes[start..end].ToImmutableArray(), messages.ToImmutableArray(), completion) : null;
                case 0xBB:
                    //50A750 only displays an already-owned command title, without a branch or callback wait.
                    break;
                default:
                    return null;
            }
        }
        //A local loop without a proved completion is not a finite action track.
        return null;

        byte ReadByte()
        {
            if (position >= end)
            {
                throw new InvalidDataException("Original local battle sequence has a truncated operand.");
            }
            return bytes[position++];
        }
    }
}
