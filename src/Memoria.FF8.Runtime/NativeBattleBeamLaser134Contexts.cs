using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBeamLaser134PointMemoryStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Context40, NativeBattleVector UpdatedLocal, short? LocalW);

public static class NativeBattleBeamLaser134Contexts
{
    public static NativeBattleBeamLaser134PointMemoryStep RotatePoint(NativeBattleEffectMemory memory, NativeBattlePackedMatrix32 sourceRotation, NativeBattleVector local, short? localW, NativeBattleVector anchor, NativeBattlePackedMatrix32 camera, uint morphAddress)
    {
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = Transform(registers, sourceRotation, local, localW);
        NativeBattleVector updated = new(unchecked((short)(registers.Colors.Ir[0]!.Value + anchor.X)), unchecked((short)(registers.Colors.Ir[1]!.Value + anchor.Y)), unchecked((short)(registers.Colors.Ir[2]!.Value + anchor.Z)));
        registers = Transform(registers, camera, updated, localW);
        NativeBattleEffectArena context = Empty(morphAddress).Write(0, camera.Bytes.AsSpan(0, 18));
        for (int axis = 0; axis < 3; axis++)
        {
            int translation = unchecked(registers.Colors.Mac[axis]!.Value + ReadTranslation(camera, axis));
            context = context.Write(20 + axis * 4, BitConverter.GetBytes(translation));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, context, updated, localW);
    }

    public static NativeBattleEffectArena Child(NativeBattlePackedMatrix32 camera, NativeBattlePackedMatrix32 savedTaskMatrix, uint morphAddress)
    {
        camera.Validate();
        savedTaskMatrix.Validate();
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), savedTaskMatrix.ReadMatrix());
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(composed, null);
        return Empty(morphAddress).Write(0, packed.Bytes.AsSpan(0, 18)).Write(20, packed.Bytes.AsSpan(20, 12));
    }

    private static NativeBattleEffectArena Empty(uint morphAddress) => NativeBattleEffectArena.Clear().MarkUnknown(0, 40).Write(32, BitConverter.GetBytes(0)).Write(36, BitConverter.GetBytes(morphAddress));

    private static NativeBattleGeometryRegisterStep Transform(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 packed, NativeBattleVector point, short? w)
    {
        packed.Validate();
        registers = registers with { Geometry = registers.Geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() } };
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
        return NativeBattleGeometry.Transform(registers, false);
    }

    private static int ReadTranslation(NativeBattlePackedMatrix32 packed, int axis)
    {
        int offset = 20 + axis * 4;
        if (packed.Known.AsSpan(offset, 4).Contains(false))
        {
            throw new NotSupportedException("Beam Laser requires its actual camera translation DWORDs.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(packed.Bytes.AsSpan(offset, 4));
    }
}
