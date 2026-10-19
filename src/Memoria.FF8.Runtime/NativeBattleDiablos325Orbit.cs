using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDiablos325Orbit
{
    public static NativeBattleDiablos325TailStep Advance656309(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera, ushort counter)
    {
        if (counter is < 60 or >= 90)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).CenterZ51D4;
        if (counter == 60)
        {
            frame = RaisePuffs656321(frame);
        }
        frame = NativeBattleDiablos325Puffs.Shrink658C80(frame);
        int ratio = 68 + (counter - 60) * 4096 / 30;
        NativeBattleCameraPose first = new(new(-429, -10851, unchecked((short)(z + 1718))), new(2302, -1616, unchecked((short)(z - 2103))), camera.FocalLength, camera.Roll);
        NativeBattleCameraPose second = new(new(1771, -10909, unchecked((short)(z + 1263))), new(-1376, -2165, unchecked((short)(z - 3246))), camera.FocalLength, camera.Roll);
        return new(frame, camera.WriteEffectGlobals(Interpolate657DF0(first, second, ratio)));
    }

    public static NativeBattleDiablos325TailStep Reset6563D1(NativeBattleDiablos325Frame frame, NativeBattleCameraState camera)
    {
        int z = NativeBattleDiablos325Entry.RequireState(frame.Memory).FarZ51DC;
        frame = NativeBattleDiablos325Puffs.Reset6545B0(frame);
        NativeBattleCameraPose pose = new(new(0, -7931, unchecked((short)(z - 0x62C))), new(0, -8211, unchecked((short)(z - 0xD6))), camera.FocalLength, camera.Roll);
        return new(frame, camera.WriteEffectGlobals(pose));
    }

    public static NativeBattleDiablos325Frame RaisePuffs656321(NativeBattleDiablos325Frame frame)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos puff transition requires its actual arena.");
        NativeBattleEffectArenaWriter writer = new(arena);
        for (int index = 0; index < NativeBattleDiablos325Puffs.Capacity; index++)
        {
            int offset = NativeBattleDiablos325Puffs.BaseOffset + index * 16;
            if (arena.Read(offset + 15, 1)[0] == 0)
            {
                continue;
            }
            short y = BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset + 6, 2));
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, unchecked((short)(y + 8192)));
            writer.Write(offset + 6, bytes);
        }
        return frame with { Memory = frame.Memory with { Arena = writer.Freeze() } };
    }

    public static NativeBattleCameraPose Interpolate657DF0(NativeBattleCameraPose first, NativeBattleCameraPose second, int ratio)
    {
        NativeBattleCameraPoint Point(NativeBattleCameraPoint a, NativeBattleCameraPoint b)
        {
            NativeBattleVector blended = NativeBattleQuezacotlRibbonMath.Blend(new(a.X, a.Y, a.Z), new(b.X, b.Y, b.Z), unchecked(4096 - ratio), ratio);
            return new(blended.X, blended.Y, blended.Z);
        }
        return new(Point(first.Eye, second.Eye), Point(first.Target, second.Target), first.FocalLength, first.Roll);
    }
}
