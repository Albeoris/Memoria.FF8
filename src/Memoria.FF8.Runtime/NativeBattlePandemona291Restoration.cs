using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePandemona291Restoration
{
    public static NativeBattleActionMatrix Rotation701220(int angle)
    {
        short sine = unchecked((short)NativeBattleCameraMath.Sine(unchecked((short)angle)));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(unchecked((short)angle)));
        return new([4096, 0, 0, 0, sine, cosine, 0, unchecked((short)-cosine), sine], 0, 0, 0, 4096);
    }

    public static NativeBattleActionMatrix Rotation701270(int angle)
    {
        int negative = -unchecked((short)angle);
        short sine = unchecked((short)NativeBattleCameraMath.Sine(negative));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(negative));
        return new([sine, 0, unchecked((short)-cosine), 0, 4096, 0, cosine, 0, sine], 0, 0, 0, 4096);
    }

    public static NativeBattleActionMatrix Basis50CBA0(int x, int y, int z, NativeBattleVector translation)
    {
        var forward = NativeBattleActionGeometryRules.NormalizeVector56BC50(x, y, z);
        var right = NativeBattleActionGeometryRules.NormalizeVector56BC50(forward.Z, 0, -forward.X);
        int upX = unchecked(forward.Y * right.Z - forward.Z * right.Y) >> 12;
        int upY = unchecked(forward.Z * right.X - forward.X * right.Z) >> 12;
        int upZ = unchecked(forward.X * right.Y - forward.Y * right.X) >> 12;
        var up = NativeBattleActionGeometryRules.NormalizeVector56BC50(upX, upY, upZ);
        return new([unchecked((short)right.X), unchecked((short)right.Y), unchecked((short)right.Z), unchecked((short)up.X), unchecked((short)up.Y), unchecked((short)up.Z), unchecked((short)forward.X), unchecked((short)forward.Y), unchecked((short)forward.Z)], translation.X, translation.Y, translation.Z, 4096);
    }

    public static ImmutableArray<NativeBattlePandemona291LateLayerRequest> Layers335(int elapsed)
    {
        int ratio = unchecked((int)((uint)(elapsed << 12) / 42));
        int amplitude = ratio >> 2;
        int horizontal = unchecked((elapsed + 4) << 9);
        int phase = elapsed << 5;
        int angle = elapsed << 6;
        int radialPhase = elapsed << 7;
        int height = unchecked(elapsed * 93 + 4096);
        return [new(0x13A8508, angle, 5120, amplitude, horizontal, 8192, 2, unchecked((byte)phase), Math.Min(unchecked(elapsed * 372), 8192)), new(0x13AB5C0, angle, radialPhase, amplitude, horizontal, 8192, 0, unchecked((byte)phase), height), new(0x13AC1B8, -angle, radialPhase, amplitude, horizontal, 8192, 1, unchecked((byte)phase), height), new(0x13ACDB0, angle, radialPhase, amplitude, horizontal, 8192, 2, unchecked((byte)phase), height)];
    }

    public static ImmutableArray<NativeBattlePandemona291LateLayerRequest> Layers377(int elapsed)
    {
        int height = elapsed > 24 ? unchecked((elapsed - 8) << 9) : 8192;
        int angle = elapsed << 6;
        int radialPhase = elapsed << 7;
        byte phase = unchecked((byte)(elapsed << 5));
        return [new(0x13A8508, angle, radialPhase, 1024, 22528, 8192, 2, phase, height), new(0x13AB5C0, angle, radialPhase, 1024, 22528, 8192, 2, phase, height), new(0x13AC1B8, -angle, radialPhase, 1024, 22528, 8192, 2, phase, height), new(0x13ACDB0, angle, radialPhase, 1024, 22528, 8192, 2, phase, height)];
    }
    public static NativeBattlePandemona291TrailAllocation Spawn335(NativeBattleEffectArena arena, NativeCrtRandomState random, uint address, int center)
    {
        for (int index = 0; index < 4; index++)
        {
            NativeBattlePandemona291TrailAllocation allocated = NativeBattlePandemona291Trails.Allocate6F1550(arena, random, address);
            arena = allocated.Arena;
            random = allocated.Random;
            if (allocated.Address is not uint selected)
            {
                break;
            }
            int record = NativeBattleWaterExecutionFields.ArenaOffset(selected, 24);
            random = random.Draw(out ushort x);
            Store(0, (x & 0xFFF) - 2048);
            Store(2, 0);
            Store(4, center - 1000);
            Store(6, 12);
            Store(8, 0);
            random = random.Draw(out ushort y);
            Store(0xA, -32 - (y & 64));
            Store(0xC, -400);
            Store(0xE, 4096);

            void Store(int field, int value) => arena = arena.Write(record + field, BitConverter.GetBytes(unchecked((ushort)value)));
        }
        return new(arena, random, null);
    }

    public static NativeBattleEffectArena Decelerate417(NativeBattleEffectArena arena, uint address)
    {
        int start = NativeBattleWaterExecutionFields.ArenaOffset(address, 0x1030);
        for (int index = 0; index < 128; index++)
        {
            int record = start + 0x42C + index * 24;
            ushort life = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 6, 2));
            if (life == 0)
            {
                continue;
            }
            arena = arena.Write(record + 6, BitConverter.GetBytes(unchecked((ushort)(life + 40))));
            for (int offset = 8; offset <= 12; offset += 2)
            {
                short value = System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(arena.Read(record + offset, 2));
                arena = arena.Write(record + offset, BitConverter.GetBytes(unchecked((short)(value >> 2))));
            }
        }
        return arena;
    }

}
