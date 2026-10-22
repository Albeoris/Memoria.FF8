using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRibbonProjection
{
    public static NativeBattleEffectMemory ProjectCurrent(NativeBattleEffectMemory memory, uint head, NativeBattleRibbonBank bank, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls)
    {
        if (bank.PointCount <= 0 || bank.ArenaOffset < 0 || bank.ArenaOffset > NativeBattleEffectArena.Length - checked(bank.PointCount * 40))
        {
            throw new InvalidDataException("Ribbon projection requires its bounded physical point bank.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Ribbon projection requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Ribbon projection requires its current arithmetic registers."));
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Ribbon requires its actual point arena.");
        uint address = head;
        for (int index = 0; address != 0; index++)
        {
            if (index >= bank.PointCount)
            {
                throw new InvalidDataException("Ribbon projected chain exceeds its physical point bank.");
            }
            uint offset = unchecked(address - bank.Address);
            if (offset >= bank.PointCount * 40 || offset % 40 != 0)
            {
                throw new InvalidDataException("Ribbon projected point leaves its physical linked bank.");
            }
            int at = checked(bank.ArenaOffset + (int)offset);
            byte[] point = arena.Read(at, 8);
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(4))), BinaryPrimitives.ReadInt16LittleEndian(point.AsSpan(6)));
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            arena = arena.Write(at + 0x10, BitConverter.GetBytes(registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Ribbon requires its projected screen."))).Write(at + 0x14, BitConverter.GetBytes((registers.Geometry.Sz[3] ?? throw new NotSupportedException("Ribbon requires its projected depth.")) >> 2));
            address = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(at + 0x24, 4));
        }
        return memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    }
}
