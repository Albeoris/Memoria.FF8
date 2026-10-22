using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleBadBreath131Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleBadBreath131Controller
{
    public static ImmutableArray<byte> Preprocess6B41D0(ImmutableArray<byte> source)
    {
        NativeBattleBadBreath131Resources.ValidateMesh(source);
        byte[] bytes = source.ToArray();
        int minimum = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(0x1C));
        int maximum = minimum;
        for (int index = 0; index < 164; index++)
        {
            int at = 0x18 + index * 8;
            int z = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(at + 4));
            minimum = Math.Min(minimum, z);
            maximum = Math.Max(maximum, z);
            short x = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(at));
            short y = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(at + 2));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), unchecked((short)(x + (x >> 2))));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), unchecked((short)(y + (y >> 2))));
        }
        int range = maximum - minimum;
        for (int index = 0; index < 165; index++)
        {
            int z = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(12 + index * 8));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 8), unchecked((short)((z << 12) / range)));
        }
        return bytes.ToImmutableArray();
    }

    public static NativeBattleBadBreath131Frame Deform6B4140(NativeBattleBadBreath131Frame frame, uint sourceAddress, int phase, NativeBattleBadBreath131Resources resources)
    {
        int destination = NativeBattleWaterExecutionFields.ArenaOffset(checked(Working(frame) + ScratchOffset), ScratchLength);
        return frame with { Memory = NativeBattleStripDeformation.Transform(frame.Memory, frame.State.ReadMesh(sourceAddress), phase, resources.TrigTable, destination) };
    }
}
