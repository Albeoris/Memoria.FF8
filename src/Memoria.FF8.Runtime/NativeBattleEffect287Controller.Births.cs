using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect287Controller
{
    public static NativeBattleEffect287Frame BirthBoneSprites(NativeBattleEffect287Frame frame, NativeBattleEffect287Inputs inputs)
    {
        for (int index = 5; index >= 0; index--)
        {
            (frame, uint child) = Append(frame, SpriteDescriptor, 0x6F3B80);
            if (child == 0)
            {
                continue;
            }
            frame = Write(frame, child, Read(frame, child).WriteDword(12, SecondSpriteAddress));
            NativeBattleEffect287PointStep point = inputs.LocalPoint6F3AD0(frame, frame.State.ReadDword(CasterGlobal), index + 22, TaskView(Read(frame, child), 20, 8));
            frame = Write(point.Frame, child, CopyTask(Read(point.Frame, child), 20, point.Output8, 8).WriteWord(32, 0).WriteWord(28, 0));
            int velocity = -20 - (Roll(ref frame) & 15);
            frame = Write(frame, child, Read(frame, child).WriteWord(34, 12).WriteWord(30, unchecked((ushort)velocity)).WriteWord(26, 0));
            int scale = 4096 + (Roll(ref frame) & 1023);
            frame = Write(frame, child, Read(frame, child).WriteDword(16, (uint)scale));
        }
        return frame;
    }

    public static NativeBattleEffect287Frame BirthSmallDebris(NativeBattleEffect287Frame frame, NativeBattleEffect287Inputs inputs)
    {
        (frame, uint child) = Append(frame, SmallDescriptor, 0x6F3C20);
        if (child == 0)
        {
            return frame;
        }
        int angle = Roll(ref frame) & 2047;
        NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, frame.State.ReadDword(CasterGlobal));
        int x = BinaryPrimitives.ReadInt16LittleEndian(model.Read(28, 2)) + (NativeBattleCameraMath.Cosine(angle) >> 1);
        int z = BinaryPrimitives.ReadInt16LittleEndian(model.Read(32, 2)) - (NativeBattleCameraMath.Sine(angle) >> 1) + 5000;
        frame = Write(frame, child, Read(frame, child).WriteWord(16, unchecked((ushort)x)).WriteWord(18, 0).WriteWord(20, unchecked((ushort)z)));
        int velocity = -32 - (Roll(ref frame) & 31);
        frame = Write(frame, child, Read(frame, child).WriteWord(22, unchecked((ushort)velocity)));
        for (int axis = 0; axis < 3; axis++)
        {
            int value = Roll(ref frame);
            frame = Write(frame, child, Read(frame, child).WriteWord(24 + axis * 2, unchecked((ushort)value)));
        }
        int spin = 512 + (Roll(ref frame) & 511);
        return Write(frame, child, Read(frame, child).WriteWord(30, (ushort)spin));
    }

    public static NativeBattleEffect287Frame BirthEarlySprites(NativeBattleEffect287Frame frame, int age, NativeBattleActionMatrix world)
    {
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(world, 0), true, false);
        for (int burst = 0; burst < 4; burst++)
        {
            (frame, uint child) = Append(frame, SpriteDescriptor, 0x6F3B80);
            if (child == 0)
            {
                break;
            }
            frame = Write(frame, child, Read(frame, child).WriteDword(12, FirstSpriteAddress));
            int angle = ((age + 32) << 7) - burst * 1024 + (Roll(ref frame) & 63) - 32;
            int radius = (Roll(ref frame) & 2047) + 1024;
            int x = unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12;
            int y = unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12;
            int z = (Roll(ref frame) & 255) - 128;
            frame = Write(frame, child, Read(frame, child).WriteWord(20, unchecked((ushort)x)).WriteWord(22, unchecked((ushort)y)).WriteWord(24, unchecked((ushort)z)));
            NativeBattleEffectArena packed = TaskView(Read(frame, child), 20, 8);
            NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(NativeBattleEffect287Attachments.LoadPacked(Registers(frame), packed), false);
            frame = Publish(frame, transformed);
            int px = transformed.Colors.Ir[0] ?? throw new NotSupportedException("Effect287 requires its transformed sprite X.");
            int py = transformed.Colors.Ir[1] ?? throw new NotSupportedException("Effect287 requires its transformed sprite Y.");
            int pz = transformed.Colors.Ir[2] ?? throw new NotSupportedException("Effect287 requires its transformed sprite Z.");
            frame = Write(frame, child, Read(frame, child).WriteWord(28, unchecked((ushort)(-px / 14))).WriteWord(30, unchecked((ushort)(-py / 14))).WriteWord(32, unchecked((ushort)(-pz / 14))).WriteWord(34, 14).WriteWord(20, unchecked((ushort)(px + world.X))).WriteWord(22, unchecked((ushort)(py + world.Y))).WriteWord(24, unchecked((ushort)(pz + world.Z))).WriteWord(26, 0).WriteDword(16, 2000));
        }
        return frame;
    }

    private static int Roll(ref NativeBattleEffect287Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort value) };
        return value;
    }
}
