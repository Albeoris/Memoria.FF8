using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303Controller
{
    public static NativeBattleEffect303PointStep ReadMuzzle66C000(NativeBattleEffect303Frame frame, NativeBattleEffectArena sourcePoint8, NativeBattleEffectArena retainedOutput8, NativeBattleEffect303Inputs inputs)
    {
        byte[] point = sourcePoint8.Read(0, 8);
        int bone = BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6));
        uint model = frame.State.ReadDword(0x25075D8);
        frame = inputs.Execute(frame, new NativeBattleEffect303RestorePose508C90(model));
        NativeBattleActionMatrix world = inputs.ReadModelWorld(frame, model).ReadMatrix();
        NativeBattleActionMatrix current = inputs.ReadCurrentBoneMatrix(frame, model, bone).ReadMatrix();
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(world, current);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null));
        NativeBattleGeometryRegisterStep registers = Registers303(frame);
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Vector303(sourcePoint8), unchecked((short)bone)));
        NativeBattleEffectArena result = StoreIr303(retainedOutput8, registers);
        return new(frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, result);
    }

    private static NativeBattleGeometryRegisterStep Registers303(NativeBattleEffect303Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect303 requires its current point geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect303 requires its current point arithmetic owner."));
    private static NativeBattleVector Vector303(NativeBattleEffectArena point) => new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2)));
    private static short? W303(NativeBattleEffectArena point)
    {
        (byte[] data, bool[] known) = point.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(data) : null;
    }
    private static NativeBattleEffectArena StoreIr303(NativeBattleEffectArena point, NativeBattleGeometryRegisterStep registers)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            point = point.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
        }
        return point;
    }
    private static NativeBattleEffect303Frame Publish303(NativeBattleEffect303Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static short Word303(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
    private static int Int303(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt32LittleEndian(view.Read(offset, 4));
    private static uint Dword303(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(view.Read(offset, 4));

}
