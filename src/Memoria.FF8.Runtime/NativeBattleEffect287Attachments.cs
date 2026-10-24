using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect287Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect287Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect287Attachments
{
    public static NativeBattleEffect287Frame Birth6F3D80(NativeBattleEffect287Frame frame, uint directoryAddress, uint descriptor, NativeBattleEffectArena point8, byte mode, byte lengthShift, byte taperShift, NativeBattleEffect287Inputs inputs)
    {
        (frame, uint address) = Append(frame, NativeBattleEffect287Profile.MeshDescriptor, 0x6F3DF0);
        if (address == 0)
        {
            return frame;
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteByte(12, 0).WriteByte(13, mode).WriteByte(14, lengthShift).WriteByte(15, taperShift).WriteDword(24, descriptor).WriteDword(28, directoryAddress);
        task = CopyTask(task, 16, point8, 8);
        for (int index = 0; index < 8; index++)
        {
            task = task.WriteWord(32 + index * 4, 128).WriteWord(34 + index * 4, 0);
        }
        return Write(frame, address, task);
    }

    public static NativeBattleEffect287Step Advance(NativeBattleEffect287Frame frame, uint address, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadByte(13) != 0)
        {
            throw new NotSupportedException("The attachment requires its supported current triangle mode.");
        }
        (frame, NativeBattleEffectArena center, NativeBattleEffectArena normal) = Triangle704130(frame, task.ReadDword(28), task.ReadDword(24), resources, inputs);
        NativeBattleEffectArena point = TaskView(task, 16, 8);
        (frame, NativeBattleEffectArena endpoint) = TransformPoint(frame, task.ReadDword(28), point, inputs);
        int[] delta = Enumerable.Range(0, 3).Select(axis => (int)Word287(endpoint, axis * 2) - Word287(center, axis * 2)).ToArray();
        (frame, int amplitude, int root) = Length(frame, delta, resources);
        NativeBattleGeometryRegisterStep registers = Weight(Registers(frame), Vector(normal), root >> task.ReadByte(14), false, inputs);
        NativeBattleEffectArena control = normal;
        for (int axis = 0; axis < 3; axis++)
        {
            control = PutWord287(control, axis * 2, registers.Colors.Ir[axis]!.Value + Word287(center, axis * 2));
        }
        frame = Publish(frame, registers);
        (frame, uint temporary) = Allocate(frame, 128);
        NativeBattleEffectArena rows = Context(frame, temporary, 128);
        rows = NativeBattleEffectNodeContextMemory.Copy(center, 0, rows, 0, 8);
        rows = NativeBattleEffectNodeContextMemory.Copy(endpoint, 0, rows, 112, 8);
        rows = PutWord287(PutWord287(rows, 14, Signed(task, 32)), 126, Signed(task, 60));
        for (int index = 1; index < 7; index++)
        {
            int fraction = index * 4096 / 7;
            registers = Weight(Registers(frame), Vector(center), 4096 - fraction, false, inputs);
            registers = Weight(registers, Vector(control), fraction, true, inputs);
            NativeBattleVector first = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
            registers = Weight(registers, first, 4096 - fraction, false, inputs);
            registers = Weight(registers, Vector(endpoint), fraction, true, inputs);
            for (int axis = 0; axis < 3; axis++)
            {
                rows = PutWord287(rows, index * 16 + axis * 2, registers.Colors.Ir[axis]!.Value);
            }
            rows = PutWord287(rows, index * 16 + 14, Signed(task, 32 + index * 4));
            frame = Publish(frame, registers);
        }
        uint taper = checked(frame.State.ReadDword(NativeBattleEffect287Profile.ArenaGlobal) + NativeBattleEffect287Profile.TaperOffset);
        for (int index = 1; index < 7; index++)
        {
            int scale = Word287(ReadBytes(frame, taper + (uint)(index * 2), 2), 0) * amplitude >> 12;
            for (int axis = 0; axis < 3; axis++)
            {
                NativeBattleEffect287RandomStep random = inputs.ProcessRandom7059E0(frame);
                frame = random.Frame;
                int displacement = unchecked((random.Value - 16384) * scale) >> 18;
                rows = PutWord287(rows, index * 16 + axis * 2, Word287(rows, index * 16 + axis * 2) + displacement);
            }
        }
        frame = PublishContext(frame, temporary, rows, 128);
        frame = NativeBattleEffect287Ribbon.Draw41C0(frame, temporary, resources, inputs);
        frame = Release(frame, 128);
        task = Read(frame, address);
        if (task.ReadByte(12) >= 10)
        {
            int survivors = 0;
            for (int index = 0; index < 8; index++)
            {
                int decrement = (5120 - unchecked((ushort)Word287(ReadBytes(frame, taper + (uint)(index * 2), 2), 0))) >> task.ReadByte(15);
                short value = unchecked((short)(Signed(task, 32 + index * 4) - decrement));
                if (value < 0)
                {
                    value = 0;
                }
                else
                {
                    survivors++;
                }
                task = task.WriteWord(32 + index * 4, unchecked((ushort)value));
            }
            frame = Write(frame, address, task);
            if (survivors <= 1)
            {
                return new(frame, 2);
            }
        }
        return new(Write(frame, address, task.WriteByte(12, unchecked((byte)(task.ReadByte(12) + 1)))), 0);
    }

    public static (NativeBattleEffect287Frame Frame, NativeBattleEffectArena Center, NativeBattleEffectArena Normal) Triangle704130(NativeBattleEffect287Frame frame, uint directory, uint source, NativeBattleEffect287Resources resources, NativeBattleEffect287Inputs inputs)
    {
        NativeBattleEffectArena raw = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(source, 24));
        int[][] points = new int[3][];
        for (int corner = 0; corner < 3; corner++)
        {
            NativeBattleEffectArena point = NativeBattleEffectNodeContextMemory.Copy(raw, corner * 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
            (frame, NativeBattleEffectArena transformed) = TransformPoint(frame, directory, point, inputs);
            points[corner] = Enumerable.Range(0, 3).Select(axis => (int)Word287(transformed, axis * 2)).ToArray();
        }
        int[] a = Enumerable.Range(0, 3).Select(axis => points[0][axis] - points[1][axis]).ToArray();
        int[] b = Enumerable.Range(0, 3).Select(axis => points[2][axis] - points[0][axis]).ToArray();
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        byte?[] rotation = registers.Geometry.Rotation20.ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            byte[] bytes = BitConverter.GetBytes(a[axis]);
            for (int index = 0; index < 4; index++)
            {
                rotation[axis * 8 + index] = bytes[index];
            }
        }
        registers = registers with { Geometry = registers.Geometry with { Rotation20 = rotation.ToImmutableArray() }, Colors = registers.Colors with { Ir = b.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleGeometry.OuterProduct45E670(registers);
        int[] cross = registers.Colors.Mac.Select(value => value ?? throw new NotSupportedException("Attachment requires its current cross product.")).ToArray();
        int maximum = cross.Select(value => value < 0 ? unchecked(-value) : value).Max();
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)maximum)) };
        int leading = checked((int)registers.Geometry.Lzcr!.Value);
        NativeBattleEffectArena center = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            center = PutWord287(center, axis * 2, unchecked((points[0][axis] + points[1][axis] + points[2][axis]) * 21845) >> 16);
            if (leading < 18)
            {
                cross[axis] >>= 18 - leading;
            }
        }
        (frame, int length, _) = Length(Publish(frame, registers), cross, resources);
        NativeBattleEffectArena normal = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            normal = PutWord287(normal, axis * 2, unchecked(cross[axis] << 12) / length);
        }
        return (frame, center, normal);
    }

    public static (NativeBattleEffect287Frame Frame, int Length, int RootQ12) Length(NativeBattleEffect287Frame frame, int[] lanes, NativeBattleEffect287Resources resources)
    {
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers with { Colors = registers.Colors with { Ir = lanes.Select(value => (int?)value).ToImmutableArray() } });
        int sum = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)sum)) };
        int even = checked((int)registers.Geometry.Lzcr!.Value) & ~1;
        int index = even <= 24 ? sum >> (24 - even) : unchecked(sum << (even - 24));
        int root = unchecked(resources.ReadNormalizationWord(index) << ((31 - even) >> 1));
        return (Publish(frame, registers), root >> 12, root);
    }

    private static (NativeBattleEffect287Frame Frame, NativeBattleEffectArena Point) TransformPoint(NativeBattleEffect287Frame frame, uint directory, NativeBattleEffectArena point, NativeBattleEffect287Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadCurrentBoneMatrix(frame, directory, Word287(point, 6)));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(LoadPacked(Registers(frame), point));
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            result = PutWord287(result, axis * 2, registers.Colors.Ir[axis]!.Value);
        }
        return (Publish(frame, registers), result);
    }

    public static NativeBattleGeometryRegisterStep LoadPacked(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(0, 8);
        registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(point), null);
        return registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
    }

    private static NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector vector, int factor, bool accumulate, NativeBattleEffect287Inputs inputs)
    {
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
        return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors);
    }

    private static NativeBattleVector Vector(NativeBattleEffectArena point) => new(Word287(point, 0), Word287(point, 2), Word287(point, 4));
}
