using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect316Body
{
    private const uint CommonCursorGlobal = 0x1D8E054;
    private const uint ProjectedGlobal = 0x1D98B3C;
    private const uint PrivateCursorGlobal = 0x2505564;

    public static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    public static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    public static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    public static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int offset, int value) => arena.Write(offset, BitConverter.GetBytes(unchecked((short)value)));
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset = 0) => new(Word(arena, offset), Word(arena, offset + 2), Word(arena, offset + 4));
    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int from, NativeBattleEffectArena target, int to, int length) => NativeBattleEffectNodeContextMemory.Copy(source, from, target, to, length);
    private static uint ReadDword(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, uint address) => Dword(inputs.ReadArena(frame, address, 4), 0);
    private static NativeBattleEffect316Frame Write(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, uint address, NativeBattleEffectArena data, int length) => inputs.Execute(frame, new NativeBattleEffect316WriteArena(address, data, length));
    private static NativeBattleEffect316Frame WriteDword(NativeBattleEffect316Frame frame, NativeBattleEffect316Inputs inputs, uint address, uint value) => Write(frame, inputs, address, PutDword(NativeBattleEffectArena.Clear(), 0, value), 4);
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffect316Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("The body requires current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("The body requires current arithmetic registers."));
    private static NativeBattleEffect316Frame Publish(NativeBattleEffect316Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static short? Padding(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }

    private static NativeBattleEffect316Frame LoadMatrix(NativeBattleEffect316Frame frame, NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                arena = arena.MarkUnknown(index, 1);
            }
        }
        return LoadMatrix(frame, arena);
    }

    private static NativeBattleEffect316Frame LoadMatrix(NativeBattleEffect316Frame frame, NativeBattleEffectArena arena)
    {
        NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(index => Word(arena, index * 2)).ToImmutableArray(), unchecked((int)Dword(arena, 20)), unchecked((int)Dword(arena, 24)), unchecked((int)Dword(arena, 28)), 4096);
        return frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, matrix, true, true, Padding(arena, 18)) } };
    }

    public static NativeBattleEffect316Frame NextRandom662570(NativeBattleEffect316Frame frame, out int value)
    {
        uint next = unchecked(frame.State.ReadDword(0x2505568) * 125 + 14) & 0x7FFF;
        value = (int)next;
        return frame with { State = frame.State.WriteDword(0x2505568, next) };
    }

    public static byte Gray(int coordinate, int threshold)
    {
        int gray = ((coordinate - threshold) >> 2) + 64;
        short signed = unchecked((short)gray);
        return signed < 0 ? (byte)0 : signed > 128 ? (byte)128 : unchecked((byte)gray);
    }
}
