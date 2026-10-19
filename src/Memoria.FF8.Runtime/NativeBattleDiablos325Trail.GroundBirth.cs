using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleDiablos325Trail
{
    public static NativeBattleDiablos325Frame SpawnGround655D04(NativeBattleDiablos325Frame frame, ushort counter)
    {
        if (counter is < 376 or > 405)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        if (counter == 376)
        {
            frame = Initialize657E70(frame, 7, 4096, unchecked((ushort)-256));
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeCrtRandomState random = frame.Random ?? throw new NotSupportedException("Native Diablos ground trails require their current CRT owner.");
        int ratio = (counter - 376 + 8) * 4096 / 38;
        for (int index = 0; index < 2; index++)
        {
            random = random.Draw(out ushort angle);
            random = random.Draw(out ushort radiusRandom);
            int radius = (radiusRandom & 1023) + 1024;
            int x = NativeBattleCameraMath.Cosine(angle) * radius / 4096;
            int z = NativeBattleCameraMath.Sine(angle) * radius / 4096;
            int targetY = NativeBattleDiablos325Recipients.Curve(ratio) * 4 - 16896 - NativeBattleCameraMath.Sine(radius / 4);
            random = random.Draw(out ushort speedRandom);
            int speed = speedRandom * ratio >> 15;
            byte[] context = GroundContext(state.CenterX51D8, state.CenterZ51D4, x, z, targetY, x * speed >> 12, z * speed >> 12, ref random);
            frame = Spawn658890(frame with { Random = random }, context).Frame;
        }
        return frame;
    }

    public static NativeBattleDiablos325Frame SpawnWide655F2B(NativeBattleDiablos325Frame frame, ushort counter)
    {
        if (counter is < 406 or > 421)
        {
            throw new ArgumentOutOfRangeException(nameof(counter));
        }
        if (counter == 406)
        {
            frame = Initialize657E70(frame, 7, 4096, 0);
        }
        NativeBattleDiablos325State state = NativeBattleDiablos325Entry.RequireState(frame.Memory);
        NativeCrtRandomState random = frame.Random ?? throw new NotSupportedException("Native Diablos wide trails require their current CRT owner.");
        int radius = (counter - 406) * 4096 / 30 / 4 + 3000;
        for (int index = 0; index < 2; index++)
        {
            random = random.Draw(out ushort angle);
            int x = NativeBattleCameraMath.Cosine(angle) * radius >> 12;
            int z = NativeBattleCameraMath.Sine(angle) * radius >> 12;
            random = random.Draw(out ushort xRandom);
            int vx = xRandom * x >> 22;
            random = random.Draw(out ushort zRandom);
            int vz = zRandom * z >> 22;
            byte[] context = GroundContext(state.CenterX51D8, state.CenterZ51D4, x, z, -radius * 4, vx, vz, ref random);
            frame = Spawn658890(frame with { Random = random }, context).Frame;
        }
        return frame;
    }

    private static byte[] GroundContext(int centerX, int centerZ, int x, int z, int targetY, int vx, int vz, ref NativeCrtRandomState random)
    {
        byte[] context = new byte[48];
        Word(8, centerX + x);
        Word(10, 0);
        Word(12, centerZ + z);
        Word(14, 6000);
        Word(16, centerX + x + (x >> 1));
        Word(18, targetY);
        Word(20, centerZ + z + (z >> 1));
        Word(22, 0);
        Word(24, vx);
        Word(26, 0);
        Word(28, vz);
        Word(30, 512);
        for (int axis = 0; axis < 3; axis++)
        {
            random = random.Draw(out ushort value);
            Word(32 + axis * 2, (value & 255) - 128);
        }
        Word(38, 2);
        Word(40, 12288);
        Word(42, -512);
        Word(46, 4096);
        return context;

        void Word(int offset, int value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(offset), unchecked((short)value));
        }
    }
}
