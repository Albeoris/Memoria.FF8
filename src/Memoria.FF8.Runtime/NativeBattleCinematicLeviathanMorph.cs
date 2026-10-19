using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Original packed source/target vertex pairs with independently controlled interpolation axes.</summary>
public static class NativeBattleCinematicLeviathanMorph
{
    public static NativeBattleWaterExecutionFrame AllocatePairs(NativeBattleWaterExecutionFrame frame)
    {
        uint source = frame.ReadTemporaryDword(0xF0);
        uint target = frame.ReadTemporaryDword(0xF4);
        int factor = unchecked((int)frame.ReadTemporaryDword(0xF8));
        int divisor = unchecked((int)frame.ReadTemporaryDword(0xFC));
        uint beginning = CoreDword(frame, unchecked(CoreDword(frame, 0x27973EC) + 0x74));
        uint output = beginning;
        frame = frame.WriteTemporaryWord(0xD0, Word(frame, source)).WriteTemporaryWord(0xD2, 0);
        while (unchecked((short)frame.ReadTemporaryWord(0xD0)) > unchecked((short)frame.ReadTemporaryWord(0xD2)))
        {
            int section = unchecked((short)frame.ReadTemporaryWord(0xD2));
            uint first = unchecked(source + Dword(frame, unchecked(source + (uint)(section * 4 + 4))));
            uint second = unchecked(target + Dword(frame, unchecked(target + (uint)(section * 4 + 4))));
            frame = frame.WriteTemporaryWord(0xD4, Word(frame, first));
            first = unchecked(first + 2);
            second = unchecked(second + 2);
            while (unchecked((short)frame.ReadTemporaryWord(0xD4)) > 0)
            {
                int vertices = unchecked((short)Word(frame, first + 2));
                first = unchecked(first + 4);
                second = unchecked(second + 4);
                for (int vertex = 0; vertex < vertices; vertex++)
                {
                    for (uint axis = 0; axis < 3; axis++)
                    {
                        frame = StoreWord(frame, output + axis * 2, Word(frame, first + axis * 2));
                    }
                    first = unchecked(first + 6);
                    output = unchecked(output + 12);
                    for (uint axis = 0; axis < 3; axis++)
                    {
                        int product = unchecked((short)Word(frame, second + axis * 2) * factor);
                        frame = StoreWord(frame, output - 6 + axis * 2, unchecked((ushort)(product / divisor)));
                    }
                    second = unchecked(second + 6);
                }
                frame = frame.WriteTemporaryWord(0xD4, unchecked((ushort)(frame.ReadTemporaryWord(0xD4) - 1)));
            }
            frame = frame.WriteTemporaryWord(0xD2, unchecked((ushort)(frame.ReadTemporaryWord(0xD2) + 1)));
        }
        uint size = unchecked(output - beginning);
        uint allocation;
        (frame, allocation) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, size);
        return frame.WriteTemporaryDword(0xFC, allocation).WriteTemporaryDword(0xF8, size);
    }

    public static NativeBattleWaterExecutionFrame Apply(NativeBattleWaterExecutionFrame frame)
    {
        int[] factors = [unchecked((int)frame.ReadTemporaryDword(0xE0)), unchecked((int)frame.ReadTemporaryDword(0xE4)), unchecked((int)frame.ReadTemporaryDword(0xE8))];
        uint source = frame.ReadTemporaryDword(0xF0);
        uint pair = frame.ReadTemporaryDword(0xF8);
        frame = frame.WriteTemporaryWord(0xD0, Word(frame, source)).WriteTemporaryWord(0xD2, 0);
        while (unchecked((short)frame.ReadTemporaryWord(0xD0)) > unchecked((short)frame.ReadTemporaryWord(0xD2)))
        {
            int section = unchecked((short)frame.ReadTemporaryWord(0xD2));
            uint node = unchecked(source + Dword(frame, unchecked(source + (uint)(section * 4 + 4))));
            frame = frame.WriteTemporaryWord(0xD4, Word(frame, node));
            node = unchecked(node + 2);
            while (unchecked((short)frame.ReadTemporaryWord(0xD4)) > 0)
            {
                int vertices = unchecked((short)Word(frame, node + 2));
                node = unchecked(node + 4);
                for (int vertex = 0; vertex < vertices; vertex++)
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        short origin = unchecked((short)Word(frame, pair + (uint)(axis * 2)));
                        short target = unchecked((short)Word(frame, pair + (uint)(axis * 2 + 6)));
                        int difference = target - origin;
                        int value = unchecked(difference * factors[axis]) / 256;
                        frame = StoreWord(frame, node + (uint)(axis * 2), unchecked((ushort)(origin + value)));
                    }
                    node = unchecked(node + 6);
                    pair = unchecked(pair + 12);
                }
                frame = frame.WriteTemporaryWord(0xD4, unchecked((ushort)(frame.ReadTemporaryWord(0xD4) - 1)));
            }
            frame = frame.WriteTemporaryWord(0xD2, unchecked((ushort)(frame.ReadTemporaryWord(0xD2) + 1)));
        }
        return frame;
    }

    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
    private static uint Dword(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    private static NativeBattleWaterExecutionFrame StoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
}
