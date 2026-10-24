using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect63Controller
{
    public static NativeBattleEffect63Frame BirthSprites(NativeBattleEffect63Frame frame, NativeBattleEffectArena point8)
    {
        for (int index = 0; index < 10; index++)
        {
            (frame, uint address) = Append(frame, SpriteDescriptor, SpriteCallback);
            if (address == 0)
            {
                break;
            }
            NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 12, point8, 6);
            NativeCrtRandomState random = frame.Random.Draw(out ushort yaw).Draw(out ushort pitch).Draw(out ushort speed);
            frame = frame with { Random = random };
            NativeBattleVector direction = NativeBattleRinoa89Controller.Direction6A6020((pitch & 0x3FF) - 0x200, yaw);
            int scale = (speed & 0xFF) + 0x100;
            int x = unchecked((short)(unchecked(direction.X * scale) / 4096));
            int y = unchecked((short)(unchecked(direction.Y * scale) / 4096));
            int z = unchecked((short)(unchecked(direction.Z * scale) / 4096));
            task = task.WriteWord(0x12, unchecked((ushort)x)).WriteWord(0x14, unchecked((ushort)y)).WriteWord(0x16, unchecked((ushort)z)).WriteByte(0x18, unchecked((byte)(x / -12))).WriteByte(0x19, unchecked((byte)(y / -12))).WriteByte(0x1A, unchecked((byte)(z / -12))).WriteByte(0x1B, 0);
            frame = Write(frame, address, task);
        }
        return frame;
    }
}
