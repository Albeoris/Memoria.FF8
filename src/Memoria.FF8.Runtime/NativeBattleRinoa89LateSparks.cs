using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89Frame SpawnLateSparks6FA780(NativeBattleRinoa89Frame frame, short age)
    {
        if ((uint)(age - 235) >= 24)
        {
            return frame;
        }
        int delta = age - 235;
        if (age == 235)
        {
            frame = ClearSparks6FA2F0(frame);
        }
        int count = delta >= 22 ? 4 : delta >= 16 ? 0 : 1 << (7 - (delta >> 1));
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < count; index++)
        {
            NativeBattleRinoa89SparkAllocation allocation = AllocateSpark6FCC90(frame);
            frame = allocation.Frame;
            if (allocation.Offset is not int offset)
            {
                break;
            }
            byte[] spark = new byte[23];
            BinaryPrimitives.WriteUInt32LittleEndian(spark, 0x14A067C);
            Word(4, 8000 * Draw() / 32768 - 4000);
            Word(6, -4000 * Draw() / 32768);
            Word(8, 10000 * Draw() / 32768 - 5000);
            Word(18, 1024);
            Word(10, 14);
            short x = unchecked((short)((Draw() & 63) - 32));
            short y = unchecked((short)((Draw() & 63) - 32));
            short z = unchecked((short)((Draw() & 63) - 32));
            Word(12, x);
            Word(14, y);
            Word(16, z);
            spark[20] = unchecked((byte)(-x >> 6));
            spark[21] = unchecked((byte)(-y >> 6));
            spark[22] = unchecked((byte)(-z >> 6));
            NativeBattleEffectArena arena = RequireArena(frame).Write(offset, spark.AsSpan(0, 4)).Write(offset + 4, spark.AsSpan(4, 16)).Write(offset + 20, spark.AsSpan(20, 3));
            frame = frame with { Memory = frame.Memory with { Arena = arena } };

            void Word(int wordOffset, int value) => BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(wordOffset), unchecked((short)value));
        }
        return frame with { Random = random };

        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }
}
