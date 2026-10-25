using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicPath179
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint target = resolveSlot(frame, selector);
        short x = unchecked((short)SlotWord(frame, 0x94));
        short y = unchecked((short)SlotWord(frame, 0x96));
        short z = unchecked((short)SlotWord(frame, 0x98));
        short targetX = Word(target + 0x94);
        short targetY = Word(target + 0x96);
        short targetZ = Word(target + 0x98);
        uint owner = CoreDword(frame, 0x27973B8);
        if (owner != frame.TemporaryFrame.Address + 0x100)
        {
            throw new NotSupportedException("Native cinematic path requires its current callback control binding.");
        }
        if ((frame.ReadTemporaryByte(0x14B) & 0x80) != 0)
        {
            frame = WriteSlotWord(frame, 0xA0, unchecked((ushort)x));
            frame = WriteSlotWord(frame, 0xA2, unchecked((ushort)y));
            frame = WriteSlotWord(frame, 0xA4, unchecked((ushort)z));
            int horizontal = Direction(x, z, targetX, targetZ);
            frame = WriteSlotWord(frame, 0xA6, unchecked((ushort)horizontal));
            int vertical = Direction(y, z, targetY, targetZ);
            frame = WriteSlotWord(frame, 0xA8, unchecked((ushort)vertical));
        }
        else
        {
            int initialX = unchecked((short)SlotWord(frame, 0xA0));
            int initialY = unchecked((short)SlotWord(frame, 0xA2));
            int initialZ = unchecked((short)SlotWord(frame, 0xA4));
            int horizontal = TurnB67B80(unchecked((short)SlotWord(frame, 0xA6)), Direction(initialX, initialZ, targetX, targetZ), y);
            frame = frame.WriteTemporaryDword(0xE0, unchecked((uint)horizontal));
            int vertical = TurnB67B80(unchecked((short)SlotWord(frame, 0xA8)), Direction(initialY, initialZ, targetY, targetZ), y);
            frame = frame.WriteTemporaryDword(0xE4, unchecked((uint)vertical));
            int dy = unchecked(NativeBattleCameraMath.Sine(vertical) * x) >> 12;
            int dz = unchecked(NativeBattleCameraMath.Cosine(horizontal) * x) >> 12;
            int dx = unchecked(NativeBattleCameraMath.Sine(horizontal) * x) >> 12;
            frame = frame.WriteTemporaryWord(0xF0, unchecked((ushort)(initialX + dx))).WriteTemporaryWord(0xF2, unchecked((ushort)(initialY + dy))).WriteTemporaryWord(0xF4, unchecked((ushort)(initialZ + dz)));
            frame = frame.WriteTemporaryWord(0xF8, unchecked((ushort)dx)).WriteTemporaryWord(0xFA, unchecked((ushort)dy)).WriteTemporaryWord(0xFC, unchecked((ushort)dz));
            int remainingZ = targetZ - unchecked((short)frame.ReadTemporaryWord(0xF4));
            int remainingY = targetY - unchecked((short)frame.ReadTemporaryWord(0xF2));
            int distance = SquareRoot(unchecked((remainingZ + 1) * remainingZ + remainingY));
            if (distance <= z)
            {
                byte flags = ReadSlot(frame, 0x4A, 1)[0];
                frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x4A, 1), new byte[] { (byte)(flags | 1) }) };
            }
            else
            {
                frame = WriteSlotWord(frame, 0x4A, unchecked((ushort)(SlotWord(frame, 0x4A) & ~1)));
            }
            for (int lane = 0; lane < 3; lane++)
            {
                ushort value = frame.ReadTemporaryWord(0xF0 + 2 * lane);
                frame = WriteSlotWord(frame, 0xA0 + 2 * lane, value);
                frame = WriteSlotWord(frame, 0x94 + 2 * lane, value);
            }
            frame = WriteSlotWord(frame, 0xA6, unchecked((ushort)horizontal));
            frame = WriteSlotWord(frame, 0xA8, unchecked((ushort)vertical));
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 4);

        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        int Direction(int fromFirst, int fromSecond, int toFirst, int toSecond)
        {
            int angle;
            (frame, angle) = DirectionB66B30(frame, fromFirst, fromSecond, toFirst, toSecond);
            return angle;
        }
    }

    public static (NativeBattleWaterExecutionFrame Frame, int Angle) DirectionB66B30(NativeBattleWaterExecutionFrame frame, int fromFirst, int fromSecond, int toFirst, int toSecond)
    {
        int first = unchecked(toFirst - fromFirst);
        int second = unchecked(toSecond - fromSecond);
        frame = frame.WriteTemporaryDword(0xFC, unchecked((uint)SquareRoot(unchecked(first * first + second * second))));
        int angle = NativePrecision53.RoundInteger(Math.Atan2(first, second) * (double)BitConverter.Int32BitsToSingle(0x4422F983)) & 0xFFF;
        return (frame, angle);
    }

    public static int TurnB67B80(int current, int target, int step)
    {
        current &= 0xFFF;
        target &= 0xFFF;
        int adjustedTarget = current > target ? target + 4096 : target;
        if (adjustedTarget - current < 2048)
        {
            current = unchecked(current + step) & 0xFFF;
            if (current > target)
            {
                target += 4096;
            }
            if (target - current >= 2048)
            {
                current = target;
            }
        }
        else
        {
            current = unchecked(current - step) & 0xFFF;
            if (current > target)
            {
                target += 4096;
            }
            if (target - current < 2048)
            {
                current = target;
            }
        }
        return current & 0xFFF;
    }

    private static int SquareRoot(int value)
    {
        return unchecked((int)(long)Math.Sqrt(value));
    }
}
