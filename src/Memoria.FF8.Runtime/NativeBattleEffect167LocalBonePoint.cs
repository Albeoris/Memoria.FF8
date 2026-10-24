using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect167LocalBonePointStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleEffectArena Output8, int Result);

public static class NativeBattleEffect167LocalBonePoint
{
    public static NativeBattleEffect167LocalBonePointStep Query(NativeBattleEffectMemory memory, NativeBattleFileStaging staging, NativeBattleGraphicsModel model, NativeBattlePoseSource source, int selector, int fraction, NativeBattleEffectArena retained8)
    {
        if ((model.Flags0 & 2) == 0)
        {
            NativeBattleVector position = (model.Transform ?? throw new NotSupportedException("Effect167 local bone point requires its disabled model position.")).Position28;
            return new(memory, staging, retained8.Write(0, BitConverter.GetBytes(position.X)).Write(2, BitConverter.GetBytes(position.Y)).Write(4, BitConverter.GetBytes(position.Z)), -1);
        }
        int bone = selector;
        if (selector >= 0xF0)
        {
            int alias = selector - 0xEC;
            if ((uint)alias >= 16)
            {
                throw new InvalidDataException("Effect167 local bone selector leaves its current skeleton aliases.");
            }
            bone = alias switch
            {
                4 => model.SequenceState?.Geometry4 ?? source.Skeleton[alias],
                5 => model.SequenceState?.Geometry5 ?? source.Skeleton[alias],
                _ => source.Skeleton[alias]
            };
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
        if ((uint)bone >= (uint)bones.Length)
        {
            throw new InvalidDataException("Effect167 local bone selector leaves its current matrix bank.");
        }
        short parent = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(16 + bone * 48, 2));
        short length = BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(18 + bone * 48, 2));
        short distance = parent < 0 ? (short)0 : unchecked((short)(unchecked(length * fraction) >> 12));
        NativeBattleActionMatrix matrix = bones[bone];
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 52);
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? throw new NotSupportedException("Effect167 local bone point requires its current GTE matrix registers."), matrix, true, true, null), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect167 local bone point requires its current GTE arithmetic."));
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(0, 0, distance), null);
        registers = NativeBattleGeometry.Transform(registers);
        staging = allocation.Staging;
        NativeBattleEffectArena output = retained8;
        for (int axis = 0; axis < 3; axis++)
        {
            int mac = registers.Colors.Mac[axis] ?? throw new InvalidDataException("Effect167 local bone point did not publish full MAC output.");
            staging = staging.Write(allocation.Address + 32u + (uint)(axis * 4), BitConverter.GetBytes(mac));
            output = output.Write(axis * 2, BitConverter.GetBytes(unchecked((short)mac)));
        }
        staging = staging.Write(allocation.Address + 48, BitConverter.GetBytes(registers.Colors.Flags ?? throw new InvalidDataException("Effect167 local bone point did not publish its GTE flags.")));
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 52);
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, staging, output, 0);
    }
}
