using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleCinematicPoseTranslation(short Scale, NativeBattleVector Translation);

public static class NativeBattleCinematicPosePosition9
{
    public static NativeBattleWaterExecutionFrame Update(NativeBattleWaterExecutionFrame frame, Func<uint, NativeBattleCinematicPoseTranslation> readCurrentPose)
    {
        uint directory = CoreDword(frame, 0x279744C);
        byte selector = ReadSlot(frame, 0x1B, 1)[0];
        uint actor = CoreDword(frame, unchecked(directory + 0x60u + 4u * selector));
        NativeBattleCinematicPoseTranslation pose = readCurrentPose(actor);
        uint block = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xA0, 4));
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(block, 2), 2));
        for (int lane = 0; lane < 3; lane++)
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(ReadSlot(frame, 0x5C + 4 * lane, 4)) >> 16;
            if ((flags & (0x1000 >> lane)) != 0)
            {
                short translation = lane switch
                {
                    0 => pose.Translation.X,
                    1 => pose.Translation.Y,
                    _ => pose.Translation.Z
                };
                value = unchecked(value + translation * pose.Scale / 256);
            }
            frame = WriteSlotWord(frame, 0x94 + 2 * lane, unchecked((ushort)value));
        }
        return frame;
    }
}
