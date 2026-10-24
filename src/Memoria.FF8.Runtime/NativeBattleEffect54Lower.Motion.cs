using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect54Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Lower
{
    public static int Damp80FA60(int ratio, int value) => NativeBattleEffectNodeLifecycle.Damp880C00(ratio, value);

    public static NativeBattleEffect54Frame Motion80F400(NativeBattleEffect54Frame frame, uint node, byte[] descriptor, NativeBattleEffect54Resources resources)
    {
        RequireSelected(descriptor);
        int age = Read16(frame, node + 0x1CA);
        frame = WriteBytes(frame, node + 0xCC, ReadBytes(frame, node + 0x194, 8), 8);
        for (int axis = 0; axis < 3; axis++)
        {
            uint curve = BinaryPrimitives.ReadUInt32LittleEndian(descriptor.AsSpan(0xD8 + axis * 4));
            short delta = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadResource(RequireStaging(frame), unchecked(curve + (uint)(age * 2)), 2));
            Store16(ref frame, node + 0xCC + (uint)(axis * 2), unchecked((ushort)((Read16(frame, node + 0xCC + (uint)(axis * 2)) + delta) & 0xFFF)));
        }
        frame = WriteBytes(frame, node + 0x11C, ReadBytes(frame, node + 0xDC, 16), 16);
        int count = unchecked((sbyte)ReadByte(frame, node + 0x1D8));
        for (int instance = 0; instance < count; instance++)
        {
            uint scalar = node + 0x174 + (uint)(instance * 4);
            Add32(ref frame, scalar, Read32(frame, node + 0x184 + (uint)(instance * 4)));
            int friction = Read16(frame, node + 0x1DA);
            if (friction != 0)
            {
                Store32(ref frame, scalar, unchecked((uint)Damp80FA60(friction, unchecked((int)Read32(frame, scalar)))));
            }
            NativeBattleActionMatrix matrix = Rotation54(frame, node + 0xC + (uint)(instance * 32));
            (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(matrix, 0, unchecked(-(int)Read32(frame, scalar)), 0);
            uint position = node + 0x12C + (uint)(instance * 16);
            Add32(ref frame, position, unchecked((uint)x));
            Add32(ref frame, position + 4, unchecked((uint)y));
            Add32(ref frame, position + 8, unchecked((uint)z));
            uint velocity = node + 0x220 + (uint)(instance * 16);
            uint displacement = node + 0x1E0 + (uint)(instance * 16);
            for (int axis = 0; axis < 3; axis++)
            {
                uint offset = (uint)(axis * 4);
                Add32(ref frame, velocity + offset, Read32(frame, node + 0x260 + (uint)(instance * 16) + offset));
                Add32(ref frame, displacement + offset, Read32(frame, velocity + offset));
            }
            Add32(ref frame, displacement + 4, Read32(frame, node + 0x1DC));
            for (int axis = 0; axis < 3; axis++)
            {
                uint offset = (uint)(axis * 4);
                if (friction != 0)
                {
                    Store32(ref frame, displacement + offset, unchecked((uint)Damp80FA60(friction, unchecked((int)Read32(frame, displacement + offset)))));
                }
                Add32(ref frame, position + offset, Read32(frame, displacement + offset));
            }
        }
        for (int instance = 0; instance < count; instance++)
        {
            uint input = node + 0x12C + (uint)(instance * 16);
            uint output = node + 0xDC + (uint)(instance * 16);
            if (descriptor[0x22] == 0)
            {
                frame = WriteBytes(frame, output, ReadBytes(frame, input, 16), 16);
            }
            else
            {
                uint parent = Read32(frame, node + 0x1BC);
                if (parent != 0)
                {
                    NativeBattleEffectArena source = ReadBytes(frame, input, 16);
                    frame = WriteBytes(frame, output, ReadBytes(frame, parent + 0x4C, 16), 16);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        Add32(ref frame, output + (uint)(axis * 4), Dword54(source, axis * 4));
                    }
                }
            }
        }
        return frame;
    }

    public static NativeBattleEffect54Frame Prepare80FC10(NativeBattleEffect54Frame frame, uint node, byte[] descriptor)
    {
        RequireSelected(descriptor);
        NativeBattlePackedMatrix32 camera = frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Effect54 requires its current copied view.");
        NativeBattleActionMatrix view = camera.ReadMatrix();
        NativeBattleActionMatrix basis = view with { Rotation = Enumerable.Range(0, 9).Select(index => view.Rotation[index % 3 * 3 + index / 3]).ToImmutableArray(), X = 0, Y = 0, Z = 0 };
        for (int index = 0; index < 9; index++)
        {
            Store16(ref frame, node + 0x8C + (uint)(index * 2), unchecked((ushort)basis.Rotation[index]));
        }
        short angle = Read16(frame, node + 0xD0);
        if (angle != 0)
        {
            frame = Rotate8DD(frame, node + 0x8C, 2, angle);
            basis = Rotation54(frame, node + 0x8C);
        }
        int count = unchecked((sbyte)ReadByte(frame, node + 0x1D8));
        for (int instance = 0; instance < count; instance++)
        {
            int[] point = new int[3];
            for (int axis = 0; axis < 3; axis++)
            {
                point[axis] = unchecked((int)Read32(frame, node + 0xDC + (uint)(instance * 16 + axis * 4))) / 65536;
                Store32(ref frame, node + 0xA0 + (uint)(axis * 4), unchecked((uint)point[axis]));
            }
            frame = LoadMatrix(frame, camera, true, false);
            NativeBattleGeometryRegisterStep step = Registers(frame);
            for (int column = 0; column < 3; column++)
            {
                step = NativeBattleGeometry.LoadIrColumn(step, unchecked((ushort)basis.Rotation[column]), unchecked((ushort)basis.Rotation[3 + column]), unchecked((ushort)basis.Rotation[6 + column]));
                step = NativeBattleGeometry.Transform(step, false, true);
                for (int row = 0; row < 3; row++)
                {
                    Store16(ref frame, node + 0xAC + (uint)((row * 3 + column) * 2), unchecked((ushort)step.Colors.Ir[row]!.Value));
                }
            }
            frame = Publish(frame, step);
            frame = LoadMatrix(frame, camera, false, true);
            step = NativeBattleGeometry.LoadVector(Registers(frame), 0, new(unchecked((short)point[0]), unchecked((short)point[1]), unchecked((short)point[2])), unchecked((short)point[2]) < 0 ? (short)-1 : (short)0);
            step = NativeBattleGeometry.Transform(step);
            frame = Publish(frame, step);
            for (int axis = 0; axis < 3; axis++)
            {
                Store32(ref frame, node + 0xC0 + (uint)(axis * 4), unchecked((uint)step.Colors.Mac[axis]!.Value));
            }
            short depth = BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(0x38));
            if (count == 1)
            {
                Add32(ref frame, node + 0xC8, unchecked((uint)(int)depth));
            }
            else
            {
                Store16(ref frame, node + 0x19C + (uint)(instance * 8), unchecked((ushort)Read32(frame, node + 0xC0)));
                Store16(ref frame, node + 0x19E + (uint)(instance * 8), unchecked((ushort)Read32(frame, node + 0xC4)));
                Store16(ref frame, node + 0x1A0 + (uint)(instance * 8), unchecked((ushort)(Read32(frame, node + 0xC8) + (uint)(int)depth)));
            }
        }
        return frame;
    }

    private static NativeBattleActionMatrix Rotation54(NativeBattleEffect54Frame frame, uint address) => new(Enumerable.Range(0, 9).Select(index => Read16(frame, address + (uint)(index * 2))).ToImmutableArray(), 0, 0, 0, 4096);
}
