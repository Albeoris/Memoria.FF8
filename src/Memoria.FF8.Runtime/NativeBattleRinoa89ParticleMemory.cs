using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89Frame ClearEmitters6FA2C0(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectArenaWriter writer = new(RequireArena(frame));
        for (int index = 0; index < 128; index++)
        {
            writer.Write(16960 + index * 48, new byte[1]);
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Staging = Dword(frame.Staging, 0x2556594, ArenaAddress + 16960) };
    }

    public static NativeBattleRinoa89Frame ClearSparks6FA2F0(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectArenaWriter writer = new(RequireArena(frame));
        for (int index = 0; index < 512; index++)
        {
            writer.Write(4672 + index * 24 + 10, new byte[2]);
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Staging = Dword(frame.Staging, 0x2556590, ArenaAddress + 4672) };
    }

    public static NativeBattleRinoa89Frame InitializeShardRotations6FA320(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectArenaWriter writer = new(RequireArena(frame));
        for (int index = 0; index < 120; index++)
        {
            writer.Write(24228 + index * 52 + 6, new byte[2]);
        }
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < 64; index++)
        {
            short x = Angle();
            short y = Angle();
            short z = Angle();
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(x, y, z));
            random = random.Draw(out ushort scaleDraw);
            int scale = 3840 - (scaleDraw & 511);
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale);
            byte[] bytes = new byte[20];
            for (int element = 0; element < 9; element++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(element * 2), rotation.Rotation[element]);
            }
            writer.Write(30468 + index * 20, bytes);
            writer.MarkUnknown(30468 + index * 20 + 18, 2);
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Random = random };

        short Angle()
        {
            random = random.Draw(out ushort draw);
            return unchecked((short)((draw & 511) - 256));
        }
    }

    private static NativeBattleEffectArena RequireArena(NativeBattleRinoa89Frame frame)
    {
        return frame.Memory.Arena ?? throw new NotSupportedException("Native Wishing Star requires its retained work arena.");
    }
}
