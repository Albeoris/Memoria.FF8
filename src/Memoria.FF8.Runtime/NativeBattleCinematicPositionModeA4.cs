using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Select a relative position representation and rebuild its current cached coordinates.</summary>
public static class NativeBattleCinematicPositionModeA4
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleWaterExecutionFrame> update)
    {
        short mode = Operand(2);
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x18, 1), new byte[] { unchecked((byte)mode) }) };
        short selector = Operand(4);
        frame = WriteSlotWord(frame, 0xB0, unchecked((ushort)selector));
        uint selected = resolveSlot(frame, selector);
        if (mode == 3)
        {
            short selectedZ = Coordinate(selected, 0x98);
            short currentZ = unchecked((short)SlotWord(frame, 0x98));
            short currentX = unchecked((short)SlotWord(frame, 0x94));
            short selectedX = Coordinate(selected, 0x94);
            int angle;
            (frame, angle) = NativeBattleCinematicRibbonBasis.Angle(frame, selectedX, selectedZ, currentX, currentZ);
            frame = WriteSlotDword(frame, 0x64, unchecked((uint)(angle << 16)));
            frame = WriteSlotDword(frame, 0x5C, unchecked(frame.ReadTemporaryDword(0xFC) << 16));
            short selectedY = Coordinate(selected, 0x96);
            int y = unchecked((short)SlotWord(frame, 0x96)) - selectedY;
            frame = WriteSlotDword(frame, 0x60, unchecked((uint)(y << 16)));
        }
        frame = update(frame);
        return NativeBattleScriptedCommandReads.Advance(frame, 6);

        short Operand(uint offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, offset, 2));
        short Coordinate(uint address, uint offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(unchecked(address + offset), 2), 2));
    }
}
