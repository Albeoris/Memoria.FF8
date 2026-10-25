using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Interpolates the current node between two selected positions and a signed planar extension.</summary>
public static class NativeBattleCinematicBetween10
{
    public static NativeBattleWaterExecutionFrame Update(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, short, uint> resolveNode)
    {
        uint block = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xA0, 4));
        uint first = resolveNode(frame, Word(block));
        uint second = resolveNode(frame, Word(unchecked(block + 2)));
        int extension = Word(unchecked(block + 4));
        int firstX = Word(unchecked(first + 0x94));
        int firstY = Word(unchecked(first + 0x96));
        int firstZ = Word(unchecked(first + 0x98));
        int secondX = Word(unchecked(second + 0x94));
        int secondY = Word(unchecked(second + 0x96));
        int secondZ = Word(unchecked(second + 0x98));
        int angle;
        (frame, angle) = NativeBattleCinematicPath179.DirectionB66B30(frame, secondX, secondZ, firstX, firstZ);
        int endX = unchecked(secondX + (unchecked(NativeBattleCameraMath.Sine(angle) * extension) >> 12));
        int endZ = unchecked(secondZ + (unchecked(NativeBattleCameraMath.Cosine(angle) * extension) >> 12));
        WritePosition(0, firstX, endX);
        WritePosition(1, firstY, secondY);
        WritePosition(2, firstZ, endZ);
        return frame;

        short Word(uint address)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        }

        void WritePosition(int lane, int start, int end)
        {
            int fraction = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x50 + lane * 4, 4)) >> 16;
            int offset = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x5C + lane * 4, 4)) >> 16;
            int interpolated = unchecked((end - start) * fraction) / 256;
            frame = WriteSlotWord(frame, 0x94 + lane * 2, unchecked((ushort)(start + interpolated + offset)));
        }
    }
}
