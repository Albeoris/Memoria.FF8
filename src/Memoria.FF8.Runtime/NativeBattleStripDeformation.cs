using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleStripDeformation
{
    public static NativeBattleEffectMemory Transform(NativeBattleEffectMemory memory, ImmutableArray<byte> source, int phase, ImmutableArray<short> trigTable, int destinationOffset)
    {
        if (source.IsDefault || source.Length < 8 + 165 * 8 || trigTable.IsDefault || trigTable.Length != 8192 || destinationOffset < 0 || destinationOffset > NativeBattleEffectArena.Length - 165 * 8)
        {
            throw new InvalidDataException("Strip deformation requires its complete vertex source, integer waveform and physical output span.");
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Bad Breath requires its current working arena.");
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        for (int index = 0; index < 165; index++)
        {
            ReadOnlySpan<byte> row = source.AsSpan().Slice(8 + index * 8, 8);
            NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
            short w = BinaryPrimitives.ReadInt16LittleEndian(row[6..]);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, vertex, w));
            int amplitude = trigTable[w & 0xFFE] >> 4;
            int angle = unchecked(phase + 2 * w) & 4095;
            short x = unchecked((short)((registers.Colors.Ir[0] ?? throw new NotSupportedException("Bad Breath MVMVA has no IR1.")) + (trigTable[angle * 2 + 1] * amplitude >> 12)));
            short y = unchecked((short)((registers.Colors.Ir[1] ?? throw new NotSupportedException("Bad Breath MVMVA has no IR2.")) - (trigTable[angle * 2] * amplitude >> 12)));
            short z = unchecked((short)(registers.Colors.Ir[2] ?? throw new NotSupportedException("Bad Breath MVMVA has no IR3.")));
            byte[] xyz = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(xyz, x);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(2), y);
            BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(4), z);
            arena = arena.Write(destinationOffset + index * 8, xyz);
        }
        return memory with { Arena = arena, GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    }
}
