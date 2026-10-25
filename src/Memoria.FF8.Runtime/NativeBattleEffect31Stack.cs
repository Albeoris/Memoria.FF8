using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect31Stack
{
    public const int FirstOffset = -0xFC;
    public const int ObservedLength = 0x6C;
    private const string Owner = "effect31:root508420";

    public static NativeBattleStackBytes Begin()
    {
        return NativeBattleStackBytes.Unknown(Owner, FirstOffset, ObservedLength);
    }

    public static void Validate(NativeBattleStackBytes stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        stack.Validate();
        if (stack.FrameOwner != Owner || stack.FirstOffset != FirstOffset || stack.Bytes.Length != ObservedLength)
        {
            throw new InvalidDataException("Effect31 requires its retained root-relative CPU stack interval.");
        }
    }

    public static NativeBattlePackedMatrix32 ReadMatrix(NativeBattleStackBytes stack, uint stateBody)
    {
        Validate(stack);
        int offset = MatrixOffset(stateBody);
        int source = offset - FirstOffset;
        if (stack.Known.AsSpan().Slice(source, 18).Contains(false))
        {
            throw new NotSupportedException("Effect31 matrix rotation requires its current authenticated CPU stack bytes.");
        }
        byte[] bytes = new byte[32];
        bool[] known = new bool[32];
        stack.Bytes.AsSpan().Slice(source, 20).CopyTo(bytes);
        stack.Known.AsSpan().Slice(source, 20).CopyTo(known);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static NativeBattleStackBytes ObserveTarget(NativeBattleStackBytes stack, uint temporary, uint model, uint boneMatrix)
    {
        Validate(stack);
        RequirePointer(temporary);
        RequirePointer(boneMatrix);
        if (model < 0x1D972C0 || model > 0x1D972C0 + 6 * 0x9C || (model - 0x1D972C0) % 0x9C != 0)
        {
            throw new InvalidDataException("Effect31 target stack writes require the actual physical model row.");
        }
        return stack.StoreUInt32(-0xD0, boneMatrix, 0x50221A, "Target selected bone matrix argument")
            .StoreUInt32(-0xD4, checked(model + 0x40), 0x50221B, "Target world matrix argument")
            .StoreUInt32(-0xD8, temporary, 0x502221, "Target matrix temporary argument")
            .StoreUInt32(-0xFC, checked(model + 0x40), 0x45DEF5, "Target transform saved EDI")
            .StoreUInt32(-0xDC, checked(temporary + 0x30), 0x50222D, "Target point flag destination push")
            .StoreUInt32(-0xE0, checked(temporary + 0x20), 0x50222E, "Target point destination push")
            .StoreUInt32(-0xF0, checked(temporary + 0x20), 0x56C833, "Target point destination argument")
            .StoreUInt32(-0xF4, checked(temporary + 0x30), 0x56C83D, "Target point flag destination argument")
            .StoreUInt32(-0xF8, 0x56C843, 0x56C83E, "Target point flag read return")
            .StoreUInt32(-0xEC, 0x50225A, 0x502255, "Target temporary release return");
    }

    public static NativeBattleStackBytes ObserveBounds(NativeBattleStackBytes stack, uint temporary, int boneCount)
    {
        Validate(stack);
        RequirePointer(temporary);
        if (boneCount is < 1 or > 128)
        {
            throw new InvalidDataException("Effect31 bounds stack observation requires its actual bone count.");
        }
        return boneCount == 1 ? stack : stack.StoreUInt32(-0xA4, checked(temporary + 0x28), 0x8DC913, "Target last-bone flag destination argument");
    }

    public static NativeBattleStackBytes ObserveActor(NativeBattleStackBytes stack, uint taskAddress, NativeBattleModulePoolTask task, uint stateBody, uint instruction, NativeBattlePackedMatrix32 value)
    {
        Validate(stack);
        RequirePointer(taskAddress);
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        if (stateBody == 0x872020 && instruction == 0x8720FD && task.Callback == 0x871F60)
        {
            for (int index = 0; index < 16; index += 4)
            {
                uint word = BinaryPrimitives.ReadUInt32LittleEndian(value.Bytes.AsSpan().Slice(index, 4));
                byte mask = 0;
                for (int item = 0; item < 4; item++)
                {
                    if (value.Known[index + item])
                    {
                        mask |= (byte)(1 << item);
                    }
                }
                stack = stack.StoreRegister32(-0xA0 + index, word, mask, instruction, "Actor saved world matrix DWORD copy");
            }
            return stack;
        }
        if ((stateBody == 0x872020 && instruction == 0x872075 && task.Callback == 0x871F60) || (stateBody == 0x8729F0 && instruction == 0x872A3F && task.Callback == 0x872920) || (stateBody == 0x8721D0 && instruction == 0x87221F && task.Callback == 0x872170) || (stateBody == 0x8725B0 && instruction == 0x8725FF && task.Callback == 0x872170) || (stateBody == 0x872B80 && instruction == 0x872BFB && task.Callback == 0x872B10))
        {
            if (value.Known.Take(18).Any(known => !known))
            {
                throw new NotSupportedException("Effect31 rotation observation requires its actual computed rotation WORDs.");
            }
            for (int index = 0; index < 18; index += 2)
            {
                stack = stack.StoreLiteral(MatrixOffset(stateBody) + index, value.Bytes.AsSpan().Slice(index, 2), instruction, "Actor X rotation result WORD");
            }
            return stack;
        }
        if (stateBody == 0x872990 && instruction == 0x8729D3 && task.Callback == 0x872920)
        {
            return stack.StoreUInt32(-0xA4, 0xF1, 0x8729C8, "Delayed actor selected bone argument")
                .StoreUInt32(-0xA0, 0, 0x8729C6, "Delayed actor bone fraction argument")
                .StoreUInt32(-0x9C, checked(taskAddress + 0x1C), 0x8729C5, "Delayed actor point destination argument")
                .StoreUInt32(-0x98, task.ReadDword(0x74), 0x8729BC, "Delayed actor actual programme argument")
                .StoreUInt32(-0x94, checked(taskAddress + 0x94), 0x8729B1, "Delayed actor workspace argument");
        }
        if ((stateBody == 0x872CB0 && instruction == 0x872DF3 && task.Callback == 0x872B10) || (stateBody == 0x873240 && instruction == 0x873326 && task.Callback == 0x8731D0) || (stateBody == 0x873350 && instruction == 0x87337E && task.Callback == 0x8731D0))
        {
            return stack.StoreUInt32(-0x98, 0x26D2DC0, instruction, "Actor current morph scratch context");
        }
        throw new NotSupportedException("Effect31 actor stack observation requires its selected callback, state and reached instruction.");
    }

    public static NativeBattleStackBytes ObserveDraw(NativeBattleStackBytes stack, uint descriptor, int actualWalkerEbp, uint nodeMatrixAddress)
    {
        Validate(stack);
        RequirePointer(descriptor);
        RequirePointer(nodeMatrixAddress);
        ArgumentOutOfRangeException.ThrowIfNegative(actualWalkerEbp);
        return stack.StoreUInt32(-0xD0, descriptor, 0x86F4CD, "Current sprite descriptor argument")
            .StoreUInt32(-0xD4, 0x86F4DD, 0x86F4D8, "Current sprite draw return")
            .StoreUInt32(-0xD8, nodeMatrixAddress, 0x8DD260, "Current sprite saved EBX matrix pointer")
            .StoreUInt32(-0xDC, (uint)actualWalkerEbp, 0x8DD261, "Current sprite saved actual walker EBP")
            .StoreUInt32(-0xE0, descriptor, 0x8DD262, "Current sprite saved ESI descriptor");
    }

    public static NativeBattleStackBytes InvalidateCustom(NativeBattleStackBytes stack, uint instruction)
    {
        Validate(stack);
        if (instruction == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(instruction));
        }
        return stack.Invalidate(-0xE0, 20, instruction, "Reached custom programme requires its current stack footprint");
    }

    private static int MatrixOffset(uint stateBody)
    {
        return stateBody switch
        {
            0x872020 => -0xFC,
            0x8721D0 or 0x8725B0 or 0x872B80 => -0xA4,
            0x8729F0 => -0xE0,
            _ => throw new NotSupportedException("Effect31 stack consumer requires its selected native state body.")
        };
    }

    private static void RequirePointer(uint address)
    {
        if (address == 0 || (address & 3) != 0)
        {
            throw new InvalidDataException("Effect31 stack observation requires its actual aligned native pointer.");
        }
    }
}
