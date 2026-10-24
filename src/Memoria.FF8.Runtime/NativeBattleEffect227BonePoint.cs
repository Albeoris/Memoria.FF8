using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect227BonePointStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Output8);

public static class NativeBattleEffect227BonePoint
{
    public static NativeBattleEffect227BonePointStep Transform693990(NativeBattleEffectMemory memory, NativeBattleActionMatrix world, NativeBattleActionMatrix localBone, NativeBattleEffectArena point8, NativeBattleEffectArena retained8)
    {
        byte[] point = point8.Read(0, 8);
        NativeBattleVector local = new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4)));
        short packedW = BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6));
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, localBone);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect227 requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect227 requires current arithmetic registers."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed, true, true, null) };
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, local, packedW));
        for (int axis = 0; axis < 3; axis++)
        {
            int value = registers.Colors.Ir[axis] ?? throw new NotSupportedException("Effect227 lost its current bone-point IR output.");
            retained8 = retained8.Write(axis * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, retained8);
    }
}
