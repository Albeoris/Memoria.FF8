using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect65CasterPointStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Output);

public static class NativeBattleEffect65CasterPoint
{
    public static NativeBattleEffect65CasterPointStep Transform(NativeBattleEffectMemory memory, NativeBattleActionMatrix world, NativeBattleActionMatrix bone, NativeBattleEffectArena source8, NativeBattleEffectArena output, bool wide)
    {
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, bone);
        NativeBattleGeometryRegisters geometry = memory.GeometryRegisters ?? throw new NotSupportedException("Effect65 caster points require their current geometry registers.");
        NativeBattleScriptedColorRegisters colors = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect65 caster points require their current color registers.");
        geometry = NativeBattleGeometry.LoadMatrix(geometry, matrix, true, true, null);
        NativeBattleGeometryRegisterStep registers = new(geometry, colors);
        NativeBattleVector point = new(Word(0), Word(2), Word(4));
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, Word(6));
        registers = NativeBattleGeometry.Transform(registers);
        for (int lane = 0; lane < 3; lane++)
        {
            int value = (wide ? registers.Colors.Mac[lane] : registers.Colors.Ir[lane]) ?? throw new InvalidDataException("Effect65 caster point transform did not publish its current output register.");
            output = wide ? output.Write(lane * 4, BitConverter.GetBytes(value)) : output.Write(lane * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, output);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(source8.Read(offset, 2));
    }
}
