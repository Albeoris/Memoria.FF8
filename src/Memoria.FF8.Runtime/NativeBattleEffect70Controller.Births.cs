using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect70Controller
{
    public static NativeBattleEffect70Frame BirthOrbitSparks(NativeBattleEffect70Frame frame, bool capturePresentation = false)
    {
        int x = unchecked((short)frame.State.ReadWord(CenterAddress));
        int z = unchecked((short)frame.State.ReadWord(CenterAddress + 4));
        int side = unchecked((int)frame.State.ReadDword(SideGlobal));
        for (int remaining = 4; remaining > 0; remaining--)
        {
            (frame, uint spark) = NativeBattleEffect70Particles.AllocateSpark(frame, capturePresentation);
            if (spark == 0)
            {
                break;
            }
            frame = DrawRandom(frame, out _);
            bool odd = (remaining & 1) != 0;
            frame = PutWord(frame, spark + 6, odd ? (short)4096 : (short)-4096);
            frame = PutByte(frame, spark + 13, odd ? unchecked((byte)-12) : (byte)12);
            frame = DrawRandom(frame, out ushort angle);
            frame = PutWord(frame, spark + 4, unchecked((short)(x + (NativeBattleCameraMath.Sine(angle) >> 3))));
            frame = PutWord(frame, spark + 8, unchecked((short)(z - side * 2048 + (NativeBattleCameraMath.Cosine(angle) >> 3))));
            frame = PutWord(frame, spark + 10, 512);
            frame = DrawRandom(frame, out _);
            frame = PutDword(frame, spark, 0x1324B98);
            frame = PutByte(PutByte(PutByte(frame, spark + 12, 0), spark + 14, 0), spark + 15, 80);
        }
        return frame;
    }

    public static NativeBattleEffect70Frame BirthRings(NativeBattleEffect70Frame frame, uint main, NativeBattleEffect70Inputs inputs)
    {
        int count = Signed(Read(frame, main), 14);
        for (int index = 0; index < count; index++)
        {
            uint record = checked(main + 0x14 + (uint)index * 24);
            int actor = Actor(ReadDword(frame, record));
            int extent = ReadShort(frame, record + 22) - ReadShort(frame, record + 20);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                (frame, uint ring) = NativeBattleEffect70Particles.AllocateRing(frame, inputs.CapturePresentation);
                if (ring == 0)
                {
                    break;
                }
                frame = PutWord(frame, ring, 6);
                frame = DrawRandom(frame, out ushort selector);
                int bones = checked((byte)inputs.ReadPoseSource(frame, actor).BoneCount);
                frame = PutWord(frame, ring + 2, unchecked((short)(selector * bones >> 15)));
                frame = DrawRandom(frame, out ushort draw);
                short height = unchecked((short)(unchecked(draw * extent) / 65536 + extent));
                short half = unchecked((short)(height >> 1));
                frame = PutDword(frame, ring + 12, record);
                frame = PutWord(PutWord(PutWord(PutWord(frame, ring + 6, height), ring + 4, half), ring + 8, unchecked((short)(half >> 4))), ring + 10, unchecked((short)(half >> 2)));
            }
        }
        return frame;
    }

    public static NativeBattleEffect70Frame BirthReturnSparks(NativeBattleEffect70Frame frame, int actor, NativeBattleEffect70Inputs inputs)
    {
        for (int angle = 4096; angle > 0; angle -= 256)
        {
            (frame, uint spark) = NativeBattleEffect70Particles.AllocateSpark(frame, inputs.CapturePresentation);
            if (spark == 0)
            {
                break;
            }
            frame = DrawRandom(frame, out ushort radiusDraw);
            NativeBattleVector position = Position(frame, actor, inputs);
            frame = PutWord(PutWord(PutWord(frame, spark + 4, position.X), spark + 6, position.Y), spark + 8, position.Z);
            int radius = (radiusDraw & 15) + 16;
            frame = DrawRandom(frame, out ushort sizeDraw);
            frame = PutWord(frame, spark + 10, unchecked((short)((sizeDraw & 0x800) + 0xC00)));
            frame = PutByte(frame, spark + 12, unchecked((byte)(NativeBattleCameraMath.Sine(angle) * radius >> 12)));
            frame = PutByte(frame, spark + 14, unchecked((byte)(NativeBattleCameraMath.Cosine(angle) * radius >> 12)));
            frame = PutByte(frame, spark + 13, 0);
            frame = PutDword(frame, spark, 0x13249EC);
            frame = PutByte(frame, spark + 15, 16);
        }
        return frame;
    }

    private static NativeBattleEffect70Frame DrawRandom(NativeBattleEffect70Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
}
