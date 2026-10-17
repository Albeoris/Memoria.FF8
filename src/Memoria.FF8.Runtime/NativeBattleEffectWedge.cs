using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectWedgeFrame(NativeBattleEffectMemory Memory, ImmutableArray<NativeBattleEffectTaskPool> Pools);
public sealed record NativeBattleEffectWedgeStep(NativeBattleEffectWedgeFrame Frame, NativeCrtRandomState CrtRandomAfter, int Result);

public static class NativeBattleEffectWedge
{
    /// <summary>8DCC20's original task, immediate packet and post-draw completion contract, independent of its spell frame.</summary>
    public static NativeBattleEffectWedgeStep Advance8DCC20(NativeBattleEffectWedgeFrame frame, uint address, NativeCrtRandomState random, Func<byte, NativeBattleModelTransform> readModel, NativeBattleCameraProjection? projection, Func<NativeBattleEffectWedgeFrame, NativeBattleModulePacketBatch, NativeBattleEffectWedgeFrame> execute)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x8DCC20)
        {
            throw new InvalidDataException("Native8DCC20 requires its original callback task.");
        }
        NativeBattleModulePoolTask first = hierarchy.Read(task.ReadDword(0x14));
        NativeBattleModelTransform model = readModel(task.ReadByte(0x2D)) ?? throw new NotSupportedException("Native8DCB80 requires the target's original position WORDs.");
        short centerY = unchecked((short)((Word(first, 0x4A) + Word(first, 0x52)) / 2 + model.Position28.Y));
        task = task.WriteDword(0x1C, first.ReadDword(0x38)).WriteDword(0x20, first.ReadDword(0x3C)).WriteWord(0x1E, unchecked((ushort)centerY));
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x38, 0).WriteWord(0x3A, 0).WriteWord(0x54, task.ReadWord(0x56));
                task = task.WriteWord(0x32, RandAngle()).WriteWord(0x30, RandAngle()).WriteWord(0x34, RandAngle());
                foreach (int offset in new[] { 0x44, 0x45, 0x46, 0x48, 0x49, 0x4A })
                {
                    task = task.WriteByte(offset, 0);
                }
                IncrementPhase();
                break;
            case 1:
                ResizeAndColor(true);
                if (Word(task, 0x54) <= 0)
                {
                    task = task.WriteWord(0x54, task.ReadWord(0x56));
                    IncrementPhase();
                }
                break;
            case 2:
                ResizeAndColor(false);
                if (Word(task, 0x54) <= 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                    IncrementPhase();
                }
                break;
        }
        task = task.WriteWord(0x32, unchecked((ushort)((task.ReadWord(0x32) + task.ReadWord(0x58)) & 0xFFF)));
        if ((task.ReadByte(0x26) & 4) == 0)
        {
            NativeBattleModulePacketBatch batch = Emit8DCC20(task, projection ?? throw new NotSupportedException("Native8DCC20 requires its prior GTE projection registers."));
            hierarchy = hierarchy.Write(address, task);
            frame = execute(frame with { Pools = hierarchy.Pools }, batch);
            hierarchy = new(frame.Pools);
            task = hierarchy.Read(address);
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Pools = hierarchy.Pools }, random, complete ? 2 : 0);

        ushort RandAngle()
        {
            random = random.Draw(out ushort value);
            return (ushort)(value & 0xFFF);
        }

        void IncrementPhase()
        {
            task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        }

        void ResizeAndColor(bool growing)
        {
            int sign = growing ? 1 : -1;
            int sizeOffset = growing ? 0x3C : 0x40;
            task = Add(task, 0x38, sign * Word(task, sizeOffset));
            task = Add(task, 0x3A, sign * Word(task, sizeOffset + 2));
            for (int channel = 0; channel < 3; channel++)
            {
                task = task.WriteByte(0x44 + channel, unchecked((byte)(task.ReadByte(0x44 + channel) + sign * task.ReadByte(0x4C + channel))));
                task = task.WriteByte(0x48 + channel, unchecked((byte)(task.ReadByte(0x48 + channel) + sign * task.ReadByte(0x50 + channel))));
            }
            task = Add(task, 0x54, -1);
        }
    }

    public static NativeBattleModulePacketBatch Emit8DCC20(NativeBattleModulePoolTask task, NativeBattleCameraProjection projection)
    {
        NativeBattleActionMatrix camera = projection.View ?? throw new NotSupportedException("Native8DCC20 requires its prior published camera matrix.");
        short width = Word(task, 0x38);
        short height = Word(task, 0x3A);
        NativeBattleVector[] local = [new(0, 0, 0), new(unchecked((short)(width / 2)), unchecked((short)(height / 2)), 0), new(unchecked((short)(width / 2)), unchecked((short)(-height / 2)), 0), new(width, height, 0), new(width, unchecked((short)-height), 0)];
        NativeBattleVector rotation = new(Word(task, 0x30), Word(task, 0x32), Word(task, 0x34));
        NativeBattleModuleProjectedVertex[] points = local.Select(point =>
        {
            NativeBattleVector rotated = NativeBattlePoseMatrices.RotateVector(rotation, point);
            NativeBattleVector world = new(unchecked((short)(rotated.X + Word(task, 0x1C))), unchecked((short)(rotated.Y + Word(task, 0x1E))), unchecked((short)(rotated.Z + Word(task, 0x20))));
            return NativeBattleModuleProjection.Project45FE10(camera, world, projection);
        }).ToArray();
        int otz = Math.Clamp(unchecked((points[0].Depth + points[1].Depth + points[2].Depth) * 0x155) >> 12, 0, 65535);
        int bucket = checked(17 + (otz >> 2));
        uint bright = Color(0x44);
        uint dim = Color(0x48);
        NativeBattleGeometryPrimitive triangle = new(bucket, [0x06000000, 0x32000000 | bright, points[0].Screen, dim, points[1].Screen, dim, points[2].Screen], [points[0].Depth, points[1].Depth, points[2].Depth, 0]);
        NativeBattleGeometryPrimitive quad = new(bucket, [0x08000000, 0x3A000000 | dim, points[1].Screen, dim, points[2].Screen, 0, points[3].Screen, 0, points[4].Screen], [points[1].Depth, points[2].Depth, points[3].Depth, points[4].Depth]);
        //45BFC0 writes two body words: E1 plus its zero NOP. Its insertion is last, so it traverses before both polygons.
        NativeBattleGeometryPrimitive mode = new(bucket, [0x02000000, 0xE100002A, 0], [0, 0, 0, 0]);
        return new([new(triangle, 0, 0), new(quad, 28, 0), new(mode, 64, 0)], 76);

        uint Color(int offset) => (uint)(task.ReadByte(offset) | task.ReadByte(offset + 1) << 8 | task.ReadByte(offset + 2) << 16);
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    private static NativeBattleModulePoolTask Add(NativeBattleModulePoolTask task, int offset, int value) => task.WriteWord(offset, unchecked((ushort)(task.ReadWord(offset) + value)));
}
