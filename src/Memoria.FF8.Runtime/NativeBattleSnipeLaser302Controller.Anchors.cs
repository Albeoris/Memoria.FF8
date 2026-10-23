using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleSnipeLaser302Controller
{
    public static NativeBattleSnipeLaser302PointStep ReadMuzzle66D880(NativeBattleSnipeLaser302Frame frame, NativeBattleEffectArena sourcePoint8, NativeBattleEffectArena retainedOutput8, NativeBattleSnipeLaser302Inputs inputs)
    {
        byte[] point = sourcePoint8.Read(0, 8);
        int bone = BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6));
        uint model = frame.State.ReadDword(0x2507660);
        NativeBattleActionMatrix world = inputs.ReadModelWorld(frame, model).ReadMatrix();
        NativeBattleActionMatrix current = inputs.ReadCurrentBoneMatrix(frame, model, bone).ReadMatrix();
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, current);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        NativeBattleGeometryRegisterStep registers = PointRegisters302(frame);
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, PointVector302(sourcePoint8), unchecked((short)bone)));
        NativeBattleEffectArena result = StoreIr302(retainedOutput8, registers);
        return new(frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, result);
    }

    public static NativeBattleSnipeLaser302PointStep TransformPoint66D810(NativeBattleSnipeLaser302Frame frame, NativeBattlePackedMatrix32 basisR, NativeBattleEffectArena sourcePoint8, NativeBattleEffectArena retainedPoint8, NativeBattleSnipeLaser302Inputs inputs)
    {
        frame = LoadPointRotation302(frame, basisR);
        NativeBattleGeometryRegisterStep registers = PointRegisters302(frame);
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, PointVector302(retainedPoint8), PointW302(retainedPoint8)), false);
        NativeBattleEffectArena point = StoreIr302(retainedPoint8, registers);
        for (int axis = 0; axis < 3; axis++)
        {
            ushort a = BinaryPrimitives.ReadUInt16LittleEndian(point.Read(axis * 2, 2));
            ushort b = BinaryPrimitives.ReadUInt16LittleEndian(sourcePoint8.Read(axis * 2, 2));
            point = point.Write(axis * 2, BitConverter.GetBytes(unchecked((ushort)(a + b))));
        }
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        frame = LoadPointRotation302(frame, inputs.ReadView(frame));
        registers = PointRegisters302(frame);
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, PointVector302(point), PointW302(point)), false);
        return new(frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, point);
    }

    private static NativeBattleSnipeLaser302Frame LoadPointRotation302(NativeBattleSnipeLaser302Frame frame, NativeBattlePackedMatrix32 packed)
    {
        packed.Validate();
        NativeBattleGeometryRegisters geometry = frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Snipe Laser requires its current rotation owner.");
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        return frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
    }

    private static NativeBattleGeometryRegisterStep PointRegisters302(NativeBattleSnipeLaser302Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Snipe Laser requires its current point geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Snipe Laser requires its current point arithmetic owner."));
    private static NativeBattleVector PointVector302(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
    private static short? PointW302(NativeBattleEffectArena point)
    {
        (byte[] data, bool[] known) = point.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
    }
    private static NativeBattleEffectArena StoreIr302(NativeBattleEffectArena point, NativeBattleGeometryRegisterStep registers)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            point = point.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
        }
        return point;
    }
}
