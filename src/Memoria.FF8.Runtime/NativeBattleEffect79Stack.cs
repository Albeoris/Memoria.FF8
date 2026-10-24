using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect79Stack
{
    public const int MatrixOffset = -0x50;
    public const int ObservedLength = 20;

    public static NativeBattleStackBytes Begin(long callback)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callback);
        return NativeBattleStackBytes.Unknown($"effect79:pool508420:{callback}", MatrixOffset, ObservedLength);
    }

    public static NativeBattleStackBytes ObserveTargetBounds(NativeBattleStackBytes stack, NativeBattleResourcePointer skeleton, int boneCount)
    {
        Require(stack);
        ArgumentNullException.ThrowIfNull(skeleton);
        if (!skeleton.HasValidOwner() || boneCount is < 1 or > 128 || skeleton.Length is int length && length < 16 + boneCount * 48)
        {
            throw new InvalidDataException("Effect79 stack observation requires its current complete skeleton owner.");
        }
        if (boneCount == 1)
        {
            return stack.Invalidate(MatrixOffset, 4, 0x8DC870, "One-bone bounds bypasses the observed GTE frame");
        }
        uint basis = skeleton.Cpu is { } cpu ? cpu.PhysicalAddress : skeleton.StagingAddress ?? throw new NotSupportedException("Effect79 stack observation requires its resolved current physical skeleton pointer.");
        uint lastRotation = checked(basis + 32u + (uint)((boneCount - 1) * 48));
        return stack.StoreUInt32(MatrixOffset, lastRotation, 0x460865, "Main target bounds last GTE matrix saved EDI");
    }

    public static NativeBattleStackBytes ObserveBody(NativeBattleStackBytes stack, NativeBattleEffectTaskPool particlePool, uint taskAddress, NativeBattleModulePoolTask task, bool drewBody)
    {
        Require(stack);
        if (particlePool.DescriptorAddress != 0x25E5100 || task.Callback != 0x7BCF10 || particlePool.Head is not int head || taskAddress != checked(particlePool.BankAddress + (uint)(head * particlePool.Stride)))
        {
            return Invalidate(stack, 0x7BCF10, "Body callback does not have the observed first surviving particle slot");
        }
        uint caster = checked(0x1D972C0u + (uint)(156 * task.ReadByte(0x2C)));
        if (task.ReadByte(0x2C) >= 7)
        {
            throw new InvalidDataException("Effect79 stack observation requires its current physical caster row.");
        }
        if (drewBody)
        {
            return stack.StoreUInt32(MatrixOffset + 16, 0, 0x7BD020, "Body draw saved EBX survivor count")
                .StoreUInt32(MatrixOffset + 12, particlePool.DescriptorAddress, 0x7BD025, "Body draw saved EBP descriptor")
                .StoreUInt32(MatrixOffset + 8, caster, 0x7BD026, "Body draw saved caller ESI model")
                .StoreUInt32(MatrixOffset + 4, taskAddress, 0x7BD02E, "Body draw saved caller EDI task")
                .StoreUInt32(MatrixOffset, checked(caster + 0x60), 0x7BD0FC, "Body draw final current pose restoration argument");
        }
        if (task.ReadByte(0x29) == 1 && unchecked((short)task.ReadWord(0x24)) > 0 && (task.ReadByte(0x26) & 8) == 0)
        {
            return stack.StoreUInt32(MatrixOffset + 16, caster, 0x7BD900, "Waiting body saved caller ESI model")
                .StoreUInt32(MatrixOffset + 12, taskAddress, 0x7BD907, "Waiting body saved caller EDI task")
                .StoreUInt32(MatrixOffset + 8, 2, 0x7BD90B, "Waiting body acknowledgement selector")
                .StoreUInt32(MatrixOffset + 4, 0x7BD921, 0x7BD91C, "Waiting body acknowledgement return address");
        }
        return Invalidate(stack, 0x7BCF10, "Other body phase requires its reached stack writers");
    }

    public static NativeBattleStackBytes ObserveNode(NativeBattleStackBytes stack, uint instruction, uint? firstDword)
    {
        Require(stack);
        return firstDword is uint value ? stack.StoreUInt32(MatrixOffset, value, instruction, "Current node geometry callback return") : stack.Invalidate(MatrixOffset, 4, instruction, "Reached node call requires its stack footprint");
    }

    public static NativeBattleStackBytes Invalidate(NativeBattleStackBytes stack, uint callback, string phase)
    {
        Require(stack);
        return stack.Invalidate(MatrixOffset, ObservedLength, callback, phase);
    }

    public static ImmutableArray<short> ReadRotation(NativeBattleStackBytes stack)
    {
        Require(stack);
        return Enumerable.Range(0, 9).Select(index => unchecked((short)stack.ReadUInt16(MatrixOffset + index * 2))).ToImmutableArray();
    }

    private static void Require(NativeBattleStackBytes stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        stack.Validate();
        if (stack.FirstOffset != MatrixOffset || stack.Bytes.Length != ObservedLength)
        {
            throw new InvalidDataException("Effect79 stack observation requires its exact current callback-relative matrix interval.");
        }
    }
}
