using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleBreath129Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBreath129Deformation
{
    public static NativeBattleBreath129Frame Transform(NativeBattleBreath129Frame frame, uint source, int phase, NativeBattleBreath129Resources resources)
    {
        int count = checked((int)frame.State.ReadDword(source + 4));
        if (source != MeshAddress || count != VertexCount)
        {
            throw new InvalidDataException("Breath deformation requires its actual selected176-vertex source.");
        }
        uint working = frame.State.ReadDword(WorkingGlobal);
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Breath deformation requires its current working arena.");
        int output = checked((int)(working - NativeBattleEffectNodeMemory.ArenaAddress)) + SelectedVerticesOffset;
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Breath deformation requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Breath deformation requires its current color registers."));
        for (int index = 0; index < count; index++)
        {
            uint vertex = checked(source + 8 + (uint)(index * 8));
            short x = unchecked((short)frame.State.ReadWord(vertex));
            short y = unchecked((short)frame.State.ReadWord(vertex + 2));
            short z = unchecked((short)frame.State.ReadWord(vertex + 4));
            short w = unchecked((short)frame.State.ReadWord(vertex + 6));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, new(x, y, z), w));
            int radius = BinaryPrimitives.ReadInt16LittleEndian(resources.Read(checked(TrigonometricTableAddress + (uint)((w & 0xFFE) * 2)), 2).AsSpan()) >> 4;
            int selector = (w + phase) & 0xFFF;
            int sine = resources.TrigonometricWords8192[selector * 2];
            int cosine = resources.TrigonometricWords8192[selector * 2 + 1];
            int destination = output + index * 8;
            short selectedX = unchecked((short)(Require(registers.Colors.Ir[0]) + (unchecked(cosine * radius) >> 12)));
            short selectedY = unchecked((short)(Require(registers.Colors.Ir[1]) - (unchecked(sine * radius) >> 12)));
            short selectedZ = unchecked((short)Require(registers.Colors.Ir[2]));
            arena = arena.Write(destination, BitConverter.GetBytes(selectedX)).Write(destination + 2, BitConverter.GetBytes(selectedY)).Write(destination + 4, BitConverter.GetBytes(selectedZ));
        }
        return frame with { Memory = frame.Memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    }

    private static int Require(int? value) => value ?? throw new NotSupportedException("Breath deformation requires its actual transformed IR lane.");
}
