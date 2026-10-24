using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect213Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect213Controller
{
    public static NativeBattleEffect213Frame BirthMouth(NativeBattleEffect213Frame frame, NativeBattleActionMatrix world, int angle)
    {
        for (int birth = 0; birth < 2; birth++)
        {
            (frame, uint child) = Append(frame, ParticleDescriptor, 0x6A0AF0);
            if (child == 0)
            {
                continue;
            }
            frame = ClearDwords701200(frame, child + 12, 5);
            NativeBattleVector local = new(unchecked((short)(NativeBattleCameraMath.Cosine(angle) >> 2)), unchecked((short)(NativeBattleCameraMath.Sine(angle) >> 2)), unchecked((short)((Roll(ref frame) & 1023) + 1024)));
            NativeBattleVector point = Transform(ref frame, local);
            int size = (Roll(ref frame) & 1023) + 2048;
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, PointView(point), 6).WriteWord(0x16, (ushort)size);
            frame = Write(frame, child, task);
            angle += 2048;
        }
        return frame;
    }

    public static NativeBattleEffect213Frame BirthAtVertices(NativeBattleEffect213Frame frame, uint parent, int dx, int dz, int distance, NativeBattleEffect213Inputs inputs)
    {
        NativeBattleModulePoolTask main = Read(frame, parent);
        int ordinal = unchecked(Roll(ref frame) * Signed(main, 0x2C)) >> 16;
        for (int birth = 0; birth < 2; birth++)
        {
            (frame, uint child) = Append(frame, ParticleDescriptor, 0x6A0C80);
            if (child == 0)
            {
                continue;
            }
            frame = ClearDwords701200(frame, child + 12, 5);
            main = Read(frame, parent);
            uint model = main.ReadDword(16 + Signed(main, 0x20) * 4);
            NativeBattleEffect213PointStep selected = inputs.PoseVertex6A06C0(frame, model, ordinal, TaskView(Read(frame, child), 16, 8));
            frame = selected.Frame;
            int size = (Roll(ref frame) & 2047) + 3072;
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, selected.Output8, 8).WriteWord(0x16, (ushort)size).WriteWord(0x18, unchecked((ushort)((dx << 7) / distance))).WriteWord(0x1A, 0).WriteWord(0x1C, unchecked((ushort)((dz << 7) / distance)));
            frame = Write(frame, child, task);
            ordinal += Signed(main, 0x2C) >> 1;
        }
        return frame;
    }

    public static NativeBattleEffect213Frame BirthFromStream(NativeBattleEffect213Frame frame, NativeBattleActionMatrix world, int age, int dx, int dz, int distance)
    {
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(world, null));
        for (int index = 0; index < StreamVertexCount; index++)
        {
            NativeBattleEffectArena source = ReadBytes(frame, MutableStreamAddress + (uint)(index * 8), 8);
            (byte[] word, bool[] known) = source.ReadSnapshot(6, 2);
            short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(word) : null;
            NativeBattleVector point = Transform(ref frame, Vector(source), w);
            if (point.Y < 0)
            {
                continue;
            }
            if (Roll(ref frame) < 2048)
            {
                (frame, uint spark) = Append(frame, ParticleDescriptor, 0x6A0A50);
                if (spark != 0)
                {
                    frame = ClearDwords701200(frame, spark + 12, 5);
                    int size = (Roll(ref frame) & 2047) + 2048;
                    NativeBattleModulePoolTask task = CopyTask(Read(frame, spark), 16, PointView(point), 6).WriteWord(0x16, (ushort)size).WriteWord(0x18, unchecked((ushort)((dx << 7) / distance))).WriteWord(0x1A, unchecked((ushort)-50)).WriteWord(0x1C, unchecked((ushort)((dz << 7) / distance)));
                    frame = Write(frame, spark, task);
                }
            }
            if (age >= 60 || Roll(ref frame) >= 2048)
            {
                continue;
            }
            (frame, uint debris) = Append(frame, DebrisDescriptor, 0x6A0760);
            if (debris == 0)
            {
                continue;
            }
            frame = ClearDwords701200(frame, debris + 12, 9);
            int scale = (Roll(ref frame) & 2047) + 2048;
            int vertical = -128 - (Roll(ref frame) & 127);
            NativeBattleModulePoolTask fragment = CopyTask(Read(frame, debris), 16, PointView(point), 6).WriteWord(0x16, (ushort)scale).WriteWord(0x18, unchecked((ushort)((dx << 6) / distance))).WriteWord(0x1A, unchecked((ushort)vertical)).WriteWord(0x1C, unchecked((ushort)((dz << 6) / distance)));
            for (int axis = 0; axis < 3; axis++)
            {
                fragment = fragment.WriteWord(0x28 + axis * 2, unchecked((ushort)((Roll(ref frame) & 1023) - 512)));
            }
            frame = Write(frame, debris, fragment);
        }
        return frame;
    }

    public static NativeBattleEffect213Frame BirthRadial(NativeBattleEffect213Frame frame, short yaw, NativeBattleVector mouth, short depth)
    {
        NativeBattleActionMatrix world = new(NativeBattleDuelSharedPackets.Rotation6D9510(yaw), mouth.X, mouth.Y, mouth.Z, 4096);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(world, null));
        int angle = Roll(ref frame);
        for (int birth = 0; birth < 2; birth++)
        {
            (frame, uint child) = Append(frame, ParticleDescriptor, 0x6A0BE0);
            if (child == 0)
            {
                continue;
            }
            frame = ClearDwords701200(frame, child + 12, 5);
            short x = unchecked((short)(NativeBattleCameraMath.Cosine(angle) >> 2));
            short y = unchecked((short)(NativeBattleCameraMath.Sine(angle) >> 2));
            NativeBattleVector far = Transform(ref frame, new(x, y, depth));
            NativeBattleVector point = Transform(ref frame, new(unchecked((short)(x >> 3)), unchecked((short)(y >> 3)), 0));
            int size = (Roll(ref frame) & 2047) + 2048;
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, PointView(point), 6).WriteWord(0x16, (ushort)size);
            NativeBattleVector velocity = new(unchecked((short)((point.X - far.X) >> 4)), unchecked((short)((point.Y - far.Y) >> 4)), unchecked((short)((point.Z - far.Z) >> 4)));
            task = CopyTask(task, 0x18, PointView(velocity), 6);
            frame = Write(frame, child, task);
            angle += 2048;
        }
        return frame;
    }
}
