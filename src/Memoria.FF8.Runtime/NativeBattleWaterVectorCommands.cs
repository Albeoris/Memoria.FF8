using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum NativeBattleWaterVectorPostAction
{
    None,
    /// <summary>The caller executes the selected update first, then copies position DWORDs into the cached WORDs.</summary>
    RefreshPosition,
    RefreshAcceleration
}

public sealed record NativeBattleWaterVectorResult(ImmutableArray<byte> Slot, int NextOffset, NativeCrtRandomState Random, NativeBattleWaterVectorPostAction PostAction);
public sealed record NativeBattleWaterVectorRandom(int Value, NativeCrtRandomState Random);

/// <summary>The finite vector command leaf over an explicit current slot and operand stream.</summary>
public static class NativeBattleWaterVectorCommands
{
    private static readonly byte[] Types = [0x00, 0x01, 0x02, 0x10, 0x11, 0x12, 0x20, 0x03, 0x30, 0x31, 0x32, 0x33, 0x40, 0x50];

    public static NativeBattleWaterVectorResult ExecuteAA1810(ImmutableArray<byte> slot, ImmutableArray<byte> program, int instructionOffset, NativeCrtRandomState random)
    {
        if (slot.IsDefault || slot.Length != 0x100 || program.IsDefault || instructionOffset < 0 || instructionOffset > program.Length - 2)
        {
            throw new InvalidDataException("Native vector commands require the retained 256-byte slot and a bounded instruction stream.");
        }
        ushort instruction = BinaryPrimitives.ReadUInt16LittleEndian(program.AsSpan().Slice(instructionOffset));
        int opcode = instruction & 0x1FF;
        if (opcode < 10 || opcode > 23)
        {
            throw new NotSupportedException("Native vector command leaf admits only its fourteen original operations.");
        }
        byte type = Types[opcode - 10];
        int operation = type >> 4;
        (int destination, int shift, int stride, NativeBattleWaterVectorPostAction action) = (type & 15) switch
        {
            0 => (0x50, 16, 4, NativeBattleWaterVectorPostAction.RefreshPosition),
            1 => (0x68, 8, 4, NativeBattleWaterVectorPostAction.None),
            2 => (0x80, 4, 2, NativeBattleWaterVectorPostAction.RefreshAcceleration),
            3 => (0x68, 16, 4, NativeBattleWaterVectorPostAction.None),
            _ => throw new InvalidDataException("Native vector command layout is unsupported.")
        };
        int cursor = instructionOffset + 2;
        byte[] bytes = slot.ToArray();
        int shared = 0;
        if (operation is 1 or 4)
        {
            shared = Operand();
            if (operation == 4)
            {
                shared = Random((short)shared);
            }
        }
        for (int lane = 0; lane < 6; lane++)
        {
            if ((instruction & (0x8000 >> lane)) == 0)
            {
                continue;
            }
            int value;
            switch (operation)
            {
                case 0:
                case 2:
                    value = Operand();
                    if (value == 0x7654)
                    {
                        continue;
                    }
                    break;
                case 1:
                case 4:
                    value = shared;
                    break;
                case 3:
                    short operand = Operand();
                    if (operand == 0)
                    {
                        continue;
                    }
                    value = Random(operand);
                    break;
                case 5:
                    short first = Operand();
                    short second = Operand();
                    _ = Random(first);
                    int secondReturn = Random(second);
                    value = unchecked((second < 0 ? -first : first) + secondReturn);
                    break;
                default:
                    throw new NotSupportedException("Native vector command arithmetic is unsupported.");
            }
            int offset = destination + lane * stride;
            if (stride == 4)
            {
                uint shifted = unchecked((uint)(value << shift));
                uint output = operation is 0 or 1 ? shifted : unchecked(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)) + shifted);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), output);
            }
            else
            {
                int shifted = operation is 0 or 1 or 4 ? value >> shift : unchecked(value << shift);
                ushort output = operation is 0 or 1 ? unchecked((ushort)shifted) : unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset)) + shifted));
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), output);
            }
        }
        return new(bytes.ToImmutableArray(), cursor, random, action);

        short Operand()
        {
            if (cursor > program.Length - 2)
            {
                throw new InvalidDataException("Native vector command operand leaves its supplied program bytes.");
            }
            short value = BinaryPrimitives.ReadInt16LittleEndian(program.AsSpan().Slice(cursor));
            cursor += 2;
            return value;
        }

        int Random(short operand)
        {
            NativeBattleWaterVectorRandom draw = RandomB65740(operand, random);
            random = draw.Random;
            return draw.Value;
        }
    }

    public static NativeBattleWaterVectorRandom RandomB65740(short operand, NativeCrtRandomState random)
    {
        random = random.Draw(out ushort value);
        int product = unchecked(value * operand);
        int magnitude = (int)((uint)product >> 15);
        return new((value & 1) != 0 ? unchecked(-magnitude) : magnitude, random);
    }
}
