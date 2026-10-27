using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89Frame InitializeStars6F9D80(NativeBattleRinoa89Frame frame)
    {
        using NativeBattleEffectArenaWriter writer = new(RequireArena(frame));
        NativeCrtRandomState random = frame.Random;
        NativeBattleFileStaging staging = frame.Staging;
        for (int group = 0; group < 8; group++)
        {
            uint row = checked(0x14AF498u + (uint)group * 16);
            staging = Dword(staging, row, 0);
            byte[] colors = staging.Read(row - 8, 8);
            for (int index = 0; index < 128; index++)
            {
                int pitch = 1600 * Draw() / 32768 - 800;
                int yaw = (Draw() & 4095) - 2048;
                NativeBattleVector direction = Direction6A6020(pitch, yaw);
                short distance = unchecked((short)(int)Math.Sqrt(pitch * pitch + yaw * yaw));
                int radius = Draw() / 2 + 12000;
                byte[] star = new byte[16];
                BinaryPrimitives.WriteInt16LittleEndian(star, unchecked((short)(unchecked(radius * direction.X) >> 12)));
                BinaryPrimitives.WriteInt16LittleEndian(star.AsSpan(2), unchecked((short)(unchecked(radius * direction.Y) >> 12)));
                BinaryPrimitives.WriteInt16LittleEndian(star.AsSpan(4), unchecked((short)(unchecked(radius * direction.Z) >> 12)));
                BinaryPrimitives.WriteInt16LittleEndian(star.AsSpan(6), distance);
                colors.AsSpan(0, 4).CopyTo(star.AsSpan(8));
                star[11] = unchecked((byte)Draw());
                colors.AsSpan(4, 4).CopyTo(star.AsSpan(12));
                byte increment = star[15];
                star[15] = unchecked((byte)(increment + ((increment * Draw()) >> 17)));
                writer.Write(31748 + (group * 128 + index) * 16, star);
            }
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Staging = staging, Random = random };

        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleVector Direction6A6020(int pitch, int yaw)
    {
        int cosine = NativeBattleCameraMath.Cosine(pitch);
        return new(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(yaw) * cosine) >> 12)), unchecked((short)-NativeBattleCameraMath.Sine(pitch)), unchecked((short)(unchecked(-NativeBattleCameraMath.Cosine(yaw) * cosine) >> 12)));
    }

    public static NativeBattleRinoa89Frame AdvanceStars6FCF70(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectArena arena = RequireArena(frame);
        using NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < 1024; index++)
        {
            int offset = 31748 + index * 16;
            byte alpha = arena.Read(offset + 11, 1)[0];
            sbyte increment = unchecked((sbyte)arena.Read(offset + 15, 1)[0]);
            ushort value = unchecked((ushort)(alpha + increment));
            if (value <= 255)
            {
                writer.Write(offset + 11, [unchecked((byte)value)]);
            }
            else
            {
                writer.Write(offset + 11, [unchecked((byte)~value)]);
                writer.Write(offset + 15, [unchecked((byte)-increment)]);
            }
        }
        NativeBattleFileStaging staging = frame.Staging;
        for (int group = 0; group < 8; group++)
        {
            uint address = checked(0x14AF498u + (uint)group * 16);
            byte[] source = staging.Read(address, 8);
            for (int axis = 0; axis < 2; axis++)
            {
                short velocity = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(axis * 2));
                short value = BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(4 + axis * 2));
                BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(4 + axis * 2), unchecked((short)(value + velocity)));
            }
            staging = staging.Write(address + 4, source.AsSpan(4));
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() }, Staging = staging };
    }

    public static NativeBattleRinoa89Frame AdvanceSparks6FCCF0(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectArena arena = RequireArena(frame);
        using NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < 512; index++)
        {
            int offset = 4672 + index * 24;
            ushort life = BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset + 10, 2));
            if (life == 0)
            {
                continue;
            }
            _ = arena.Read(offset + 4, 14);
            _ = arena.Read(offset + 20, 3);
            byte[] spark = arena.ReadSnapshot(offset, 24).Data;
            short xVelocity = Word(12);
            short yVelocity = Word(14);
            Set(4, Word(4) + xVelocity);
            Set(6, Word(6) + yVelocity);
            Set(12, xVelocity + 4 * unchecked((sbyte)spark[20]));
            Set(14, yVelocity + 4 * unchecked((sbyte)spark[21]));
            Set(8, Word(8) + Word(16) + 4 * unchecked((sbyte)spark[22]));
            Set(10, life - 1);
            writer.Write(offset + 4, spark.AsSpan(4, 12));

            short Word(int wordOffset) => BinaryPrimitives.ReadInt16LittleEndian(spark.AsSpan(wordOffset));
            void Set(int wordOffset, int value)
            {
                BinaryPrimitives.WriteInt16LittleEndian(spark.AsSpan(wordOffset), unchecked((short)value));
            }
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() } };
    }
}
