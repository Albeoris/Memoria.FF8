using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Select an object frame and enqueue its rectangle through the shared native copy ring.</summary>
public static class NativeBattleCinematicBlitB8
{
    public static NativeBattleWaterExecutionFrame ExecuteRelativeB6(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, NativeBattleEffectVramCopy, NativeBattleWaterExecutionFrame> enqueue)
    {
        short relative = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint rectangle = unchecked(CoreDword(frame, 0x2797450) + (uint)relative);
        short Word(uint offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(rectangle + offset), 2), 2));
        short destinationY = Word(10);
        short destinationX = Word(8);
        NativeBattleEffectVramCopy copy = new(Word(0), Word(2), Word(4), Word(6), destinationX, destinationY);
        frame = enqueue(frame, copy);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, INativeBattleScriptedTextureProfile resources, Func<NativeBattleWaterExecutionFrame, short, uint> resolveShape, Func<NativeBattleWaterExecutionFrame, NativeBattleEffectVramCopy, NativeBattleWaterExecutionFrame> enqueue)
    {
        short selector = Operand(2);
        uint shape = resolveShape(frame, selector);
        uint directory = unchecked(shape + Dword(shape + 0x10));
        short index = Operand(4);
        short period = Operand(6);
        uint selected = unchecked(directory + Byte(unchecked(directory + (uint)index)));
        int remainder = unchecked((short)SlotWord(frame, 0x94)) % period;
        byte kind = Byte(selected);
        ushort originX = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(0x1870FC0 + (uint)(kind * 2), 2));
        ushort originY = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(0x1870FCC + (uint)(kind * 2), 2));
        uint position = unchecked(selected + (uint)(remainder * 2 + 5));
        frame = frame.WriteTemporaryWord(0xA0, unchecked((ushort)(originX + Byte(position))));
        frame = frame.WriteTemporaryWord(0xA2, unchecked((ushort)(originY + Byte(unchecked(position + 1)))));
        frame = frame.WriteTemporaryWord(0xA4, Byte(selected + 3));
        frame = frame.WriteTemporaryWord(0xA6, Byte(selected + 4));
        short destinationY = unchecked((short)(originY + Byte(selected + 2)));
        short destinationX = unchecked((short)(originX + Byte(selected + 1)));
        frame = CopyB66FE0(frame, destinationX, destinationY, enqueue);
        return NativeBattleScriptedCommandReads.Advance(frame, 8);

        short Operand(int offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, unchecked((uint)offset), 2));
        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        byte Byte(uint address) => frame.Arena.Read(ArenaOffset(address, 1), 1)[0];
    }

    public static NativeBattleWaterExecutionFrame CopyB66FE0(NativeBattleWaterExecutionFrame frame, short destinationX, short destinationY, Func<NativeBattleWaterExecutionFrame, NativeBattleEffectVramCopy, NativeBattleWaterExecutionFrame> enqueue)
    {
        byte counter = frame.Core.Read(0x2798BF4, 1)[0];
        uint ring = 0x27978E8 + (uint)((counter & 15) * 8);
        for (int lane = 0; lane < 4; lane++)
        {
            frame = WriteCoreWord(frame, ring + (uint)(lane * 2), frame.ReadTemporaryWord(0xA0 + lane * 2));
        }
        NativeBattleEffectVramCopy copy = new(unchecked((short)frame.ReadTemporaryWord(0xA0)), unchecked((short)frame.ReadTemporaryWord(0xA2)), unchecked((short)frame.ReadTemporaryWord(0xA4)), unchecked((short)frame.ReadTemporaryWord(0xA6)), destinationX, destinationY);
        frame = enqueue(frame, copy);
        counter = frame.Core.Read(0x2798BF4, 1)[0];
        return frame with { Core = frame.Core.Write(0x2798BF4, [unchecked((byte)(counter + 1))]) };
    }
}
