using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>The retained two-reference planar interpolation and radial offset update.</summary>
public static class NativeBattleCinematicBetweenUpdate4
{
    public static NativeBattleWaterExecutionFrame Update(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, short, uint> resolveNode)
    {
        ArgumentNullException.ThrowIfNull(resolveNode);
        uint block = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xA0, 4));
        if (block == 0)
        {
            return frame;
        }
        uint first = resolveNode(frame, Word(block));
        uint second = resolveNode(frame, Word(unchecked(block + 2)));
        int firstX = Word(unchecked(first + 0x94));
        int firstY = Word(unchecked(first + 0x96));
        int firstZ = Word(unchecked(first + 0x98));
        int secondX = Word(unchecked(second + 0x94));
        int secondY = Word(unchecked(second + 0x96));
        int secondZ = Word(unchecked(second + 0x98));
        int angle;
        (frame, angle) = NativeBattleCinematicPath179.DirectionB66B30(frame, firstX, firstZ, secondX, secondZ);
        angle = unchecked(angle + FixedInteger(0x50));
        int fraction = FixedInteger(0x64);
        int y = unchecked(firstY + unchecked((secondY - firstY) * fraction) / 256 + FixedInteger(0x60));
        frame = WriteSlotWord(frame, 0x96, unchecked((ushort)y));
        int radius = unchecked((int)frame.ReadTemporaryDword(0xFC) * fraction) / 256;
        int xOffset = unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12;
        int zOffset = unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12;
        int offsetAngle = unchecked(angle + FixedInteger(0x54));
        int extension = FixedInteger(0x5C);
        xOffset = unchecked(xOffset + (unchecked(NativeBattleCameraMath.Cosine(offsetAngle) * extension) >> 12));
        zOffset = unchecked(zOffset + (unchecked(NativeBattleCameraMath.Sine(offsetAngle) * extension) >> 12));
        frame = WriteSlotWord(frame, 0x94, unchecked((ushort)(firstX + xOffset)));
        return WriteSlotWord(frame, 0x98, unchecked((ushort)(firstZ + zOffset)));

        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        int FixedInteger(int offset) => BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, offset, 4)) >> 16;
    }
}
