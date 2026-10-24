using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect285BonePointResult(NativeBattleEffectMemory Memory, NativeBattleEffectArena Output8);

public static class NativeBattleEffect285BonePoint
{
    public static NativeBattleEffect285BonePointResult Transform(NativeBattleEffectMemory memory, NativeBattleActionMatrix world, NativeBattleActionMatrix bone, NativeBattleEffectArena local8, NativeBattleEffectArena retained8)
    {
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, bone);
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? throw new NotSupportedException("The current bone point requires its ordinary geometry registers."), composed, true, true, null);
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? throw new NotSupportedException("The current bone point requires its arithmetic registers.");
        byte[] local = local8.Read(0, 8);
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(local), BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(4)));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(new(geometry, colors), 0, point, BinaryPrimitives.ReadInt16LittleEndian(local.AsSpan(6)));
        registers = NativeBattleGeometry.Transform(registers);
        for (int axis = 0; axis < 3; axis++)
        {
            int value = registers.Colors.Ir[axis] ?? throw new NotSupportedException("The current bone point requires its transformed IR lanes.");
            retained8 = retained8.Write(axis * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, retained8);
    }
}
