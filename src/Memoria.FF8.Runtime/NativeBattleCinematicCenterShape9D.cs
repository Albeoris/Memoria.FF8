using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Move the current origin to scaled shape bounds and recenter the original vertex coordinates.</summary>
public static class NativeBattleCinematicCenterShape9D
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveShape, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> update)
    {
        short selector = Operand(2);
        uint shape = resolveShape(frame, selector);
        uint points = unchecked(shape + Dword(unchecked(shape + 0x14)));
        int count = unchecked((int)Dword(unchecked(shape + 0x18)));
        int[] minimum = [32767, 32767, 32767];
        int[] maximum = [-32768, -32768, -32768];
        for (int index = 0; index < count; index++)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                short coordinate = Word(unchecked(points + (uint)(index * 8 + lane * 2)));
                minimum[lane] = Math.Min(minimum[lane], coordinate);
                maximum[lane] = Math.Max(maximum[lane], coordinate);
            }
        }
        int[] center = new int[3];
        for (int lane = 0; lane < 3; lane++)
        {
            center[lane] = minimum[lane] + (maximum[lane] - minimum[lane]) / 2;
            int delta = unchecked(Operand((uint)(4 + lane * 2)) * center[lane]) / 256;
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0x5C + lane * 4, 4));
            frame = WriteSlotDword(frame, 0x5C + lane * 4, unchecked(value + (uint)(delta << 16)));
        }
        frame = update(frame);
        points = unchecked(shape + Dword(unchecked(shape + 0x14)));
        count = unchecked((int)Dword(unchecked(shape + 0x18)));
        for (int index = 0; index < count; index++)
        {
            for (int lane = 0; lane < 3; lane++)
            {
                uint address = unchecked(points + (uint)(index * 8 + lane * 2));
                byte[] bytes = new byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)(Word(address) - center[lane])));
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), bytes) };
            }
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 10);

        short Operand(uint offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, offset, 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    }
}
