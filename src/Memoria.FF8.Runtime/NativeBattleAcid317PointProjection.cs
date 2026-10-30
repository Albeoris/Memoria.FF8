using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAcid317PointProjection
{
    public static NativeBattleAcid317Frame Project(NativeBattleAcid317Frame current, uint head, short count, NativeBattlePackedMatrix32 view, NativeBattleCameraProjection projection, IDictionary<uint, System.Numerics.Vector3>? pilotWorld = null)
    {
        if (count < 0 || count > NativeBattleAcid317Profile.PointCount)
        {
            throw new InvalidDataException("Acid projected chain leaves its physical point capacity.");
        }
        NativeBattleGeometryRegisterStep registers = new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Acid requires its current geometry registers."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid requires its current arithmetic registers."));
        short? padding = view.Known[18] && view.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(view.Bytes.AsSpan().Slice(18)) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), true, true, padding) };
        NativeBattleScriptedGraphicsMemory controls = current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Acid requires its projection controls.");
        uint working = current.State.ReadDword(NativeBattleAcid317Profile.WorkingGlobal);
        NativeBattleRibbonBank bank = new(checked(working + NativeBattleAcid317Profile.PointsOffset), checked((int)(working - NativeBattleAcid317Profile.ArenaAddress) + NativeBattleAcid317Profile.PointsOffset), NativeBattleAcid317Profile.PointCount);
        NativeBattleEffectMemory memory = current.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
        return current with { Memory = NativeBattleRibbonProjection.ProjectCurrent(memory, head, bank, projection, controls, captureWorld: pilotWorld is not null ? (address, world) => pilotWorld[address] = world : null) };
    }
}
