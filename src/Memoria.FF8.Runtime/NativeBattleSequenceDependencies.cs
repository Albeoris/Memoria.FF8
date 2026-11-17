using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleSequenceReferenceKind
{
    OriginalSlot,
    FirstMatchingModel,
    AllMatchingModels
}

public readonly record struct NativeBattleSequenceReference(NativeBattleSequenceReferenceKind Kind, byte Operand);

/// <summary>Reads possible source-formation model operands without evaluating a sequence.</summary>
public static class NativeBattleSequenceDependencies
{
    public static ImmutableArray<NativeBattleSequenceReference> Read(NativeBattleGraphicsResource resource)
    {
        resource.Validate();
        int count = BinaryPrimitives.ReadUInt16LittleEndian(resource.SequenceSection.AsSpan());
        if (count > byte.MaxValue)
        {
            throw new InvalidDataException("Native sequence directory exceeds the BYTE sequence selector.");
        }

        HashSet<NativeBattleSequenceReference> dependencies = [];
        HashSet<(int Offset, int End)> visited = [];
        Stack<(int Offset, int End)> pending = new();
        for (int sequence = 1; sequence <= count; sequence++)
        {
            byte index = (byte)sequence;
            if (resource.Contains(index))
            {
                pending.Push((resource.SequenceStart(index), resource.SequenceEnd(index)));
            }
        }

        while (pending.TryPop(out var path))
        {
            if (path.Offset < 2 + count * 2 || path.End > resource.SequenceSection.Length || path.Offset > path.End)
            {
                throw new InvalidDataException("Native sequence dependency cursor exceeds its original program.");
            }
            if (path.Offset == path.End || !visited.Add(path))
            {
                continue;
            }

            int position = path.Offset;
            byte opcode = NativeBattleSequenceEncoding.ReadOpcode(resource.SequenceSection, ref position);
            int next = NativeBattleSequenceEncoding.NextOffset(resource, opcode, path.Offset, position);
            if (next > path.End)
            {
                throw new InvalidDataException("Native sequence operands exceed their original program.");
            }
            if (NativeBattleSequenceEncoding.ModelReference(opcode, position < path.End ? resource.SequenceSection[position] : (byte)0) is { } reference)
            {
                dependencies.Add(reference);
            }
            if (NativeBattleScriptExpressions.IsBranch(opcode))
            {
                int branch = checked(path.Offset + NativeBattleScriptExpressions.ReadRelativeOffset(opcode, resource.SequenceSection.AsSpan()[position..next]));
                if (branch != path.End)
                {
                    int branchEnd = FindProgramEnd(branch, path.End);
                    pending.Push((branch, branchEnd));
                }
                if (NativeBattleScriptExpressions.IsUnconditionalBranch(opcode))
                {
                    continue;
                }
            }
            if (opcode == 0xB4 && (resource.SequenceSection[position + 1] & 8) != 0)
            {
                // A suppressed contact does not consume its optional bone operand.
                pending.Push((next - 1, path.End));
            }
            if (opcode is not (0xA2 or 0xA7 or 0xAC))
            {
                pending.Push((next, path.End));
            }
        }
        return dependencies.OrderBy(reference => reference.Kind).ThenBy(reference => reference.Operand).ToImmutableArray();

        int FindProgramEnd(int offset, int currentEnd)
        {
            if (offset >= currentEnd)
            {
                int end = resource.SequenceSection.Length;
                for (int sequence = 1; sequence <= count; sequence++)
                {
                    int start = resource.SequenceStart((byte)sequence);
                    if (start > offset)
                    {
                        end = Math.Min(end, start);
                    }
                }
                return end;
            }
            return currentEnd;
        }
    }
}

internal static class NativeBattleSequenceEncoding
{
    private static readonly byte[] OperandWidths = BuildOperandWidths();

    public static byte ReadOpcode(ImmutableArray<byte> program, ref int position)
    {
        if ((uint)position >= program.Length)
        {
            throw new InvalidDataException("Native sequence cursor exceeds its original bounded program.");
        }
        byte opcode = program[position++];
        int width = OperandWidths[opcode];
        if (width != byte.MaxValue && position > program.Length - width)
        {
            throw new InvalidDataException("Native sequence operands exceed their original bounded program.");
        }
        return opcode;
    }

    public static int NextOffset(NativeBattleGraphicsResource resource, byte opcode, int origin, int position)
    {
        int width = OperandWidths[opcode];
        if (width != byte.MaxValue)
        {
            return position + width;
        }

        ImmutableArray<byte> source = resource.SequenceSection;
        return opcode switch
        {
            0x97 or 0x98 or 0xB5 or 0xB6 or 0xB8 when position + 1 < source.Length => position + SoundOperandWidth(source[position + 1]),
            0x99 or 0xB1 => NativeBattleAuxiliaryState.SkipSoundParticleProgram(source, position + 1),
            0x9F => NativeBattleAuxiliaryState.SkipSoundParticleProgram(source, position),
            0xAB => NativeBattleRenzokukenRules.ReadBlockEnd(source.AsSpan(), origin),
            0xB0 or 0xB4 => NativeBattleSequenceParticles.ReadOperandEnd(new(source, position)),
            _ => throw new NotSupportedException($"Native sequence opcode{opcode:X2} has no bounded dependency contract.")
        };
    }

    public static int SoundOperandWidth(byte attribute) => (attribute & 2) != 0 ? 3 : 2;

    public static NativeBattleSequenceReference? ModelReference(byte opcode, byte operand)
    {
        return opcode switch
        {
            0x90 when operand >= 16 => new(NativeBattleSequenceReferenceKind.FirstMatchingModel, operand),
            0x90 when operand >= 3 => new(NativeBattleSequenceReferenceKind.OriginalSlot, operand),
            0x92 when operand < NativeBattleModelBoundary.CellCount => new(NativeBattleSequenceReferenceKind.OriginalSlot, operand),
            0x93 when operand >= 16 => new(NativeBattleSequenceReferenceKind.AllMatchingModels, operand),
            0x93 or 0x9E => new(NativeBattleSequenceReferenceKind.OriginalSlot, operand),
            _ => null
        };
    }

    private static byte[] BuildOperandWidths()
    {
        byte[] widths = new byte[256];
        for (int opcode = 0x80; opcode < widths.Length; opcode++)
        {
            widths[opcode] = opcode switch
            {
                >= 0xC0 => NativeBattleScriptExpressions.OperandWidth((byte)opcode),
                0x80 or 0x81 or 0x83 or 0x84 or 0x90 or 0x92 or 0x9A or 0xA0 or 0xA5 or 0xA7 or 0xA8 or 0xAC or 0xAF or 0xB9 or 0xBC or 0xBE or 0xBF => 1,
                0x91 or 0x93 or 0x9B or 0x9E or 0xBD => 2,
                0x96 => 3,
                0xAD => 4,
                0xAE => 5,
                0xA6 => 9,
                0x97 or 0x98 or 0x99 or 0x9F or 0xAB or 0xB0 or 0xB1 or 0xB4 or 0xB5 or 0xB6 or 0xB8 => byte.MaxValue,
                _ => 0
            };
        }
        return widths;
    }
}
