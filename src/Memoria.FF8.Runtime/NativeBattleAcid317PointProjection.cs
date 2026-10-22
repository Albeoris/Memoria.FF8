using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAcid317PointProjection
{
    public static NativeBattleAcid317Frame Project(NativeBattleAcid317Frame current, uint head, short count, NativeBattlePackedMatrix32 view, NativeBattleCameraProjection projection)
    {
        if (count < 0 || count > NativeBattleAcid317Profile.PointCount)
        {
            throw new InvalidDataException("Acid projected chain leaves its physical point capacity.");
        }
        NativeBattleGeometryRegisterStep registers = new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Acid requires its current geometry registers."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid requires its current arithmetic registers."));
        short? padding = view.Known[18] && view.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(view.Bytes.AsSpan().Slice(18)) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), true, true, padding) };
        NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Acid requires its projection controls.");
        NativeBattleEffectArena arena = current.Memory.Arena ?? throw new NotSupportedException("Acid requires its actual point arena.");
        uint address = head;
        for (int index = 0; address != 0; index++)
        {
            if (index >= NativeBattleAcid317Profile.PointCount)
            {
                throw new InvalidDataException("Acid projected chain exceeds its physical point bank.");
            }
            uint start = current.State.ReadDword(NativeBattleAcid317Profile.WorkingGlobal) + NativeBattleAcid317Profile.PointsOffset;
            uint offset = unchecked(address - start);
            if (offset >= NativeBattleAcid317Profile.PointCount * NativeBattleAcid317Profile.PointStride || offset % NativeBattleAcid317Profile.PointStride != 0)
            {
                throw new InvalidDataException("Acid projected point leaves its physical linked bank.");
            }
            int at = NativeBattleWaterExecutionFields.ArenaOffset(address, 40);
            byte[] point = arena.Read(at, 8);
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4))), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6)));
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            arena = arena.Write(at + 0x10, BitConverter.GetBytes(registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Acid requires its projected screen."))).Write(at + 0x14, BitConverter.GetBytes((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Acid requires its projected depth.")) >> 2));
            address = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at + 0x24, 4));
        }
        return current with { Memory = current.Memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    }

}
