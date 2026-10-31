using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect303Controller
{
    public static (NativeBattleEffect303Frame Frame, NativeBattleEffectArena Context88) BuildFragments66C3F0(NativeBattleEffect303Frame frame, NativeBattleEffectArena context, NativeBattleEffect303Resources resources, NativeBattleEffect303Inputs inputs)
    {
        NativeBattleStaticMeshResource mesh = resources.ReadMesh(Dword303(context, 0));
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        uint cursor = frame.State.ReadDword(0x2507644);
        uint main = Dword303(context, 4);
        int bytes = checked(count * 8 + 64 * 32);
        int arenaOffset = NativeBattleWaterExecutionFields.ArenaOffset(cursor, bytes);
        uint arenaBase = frame.State.ReadDword(0x2507658);
        int relative = checked((int)(cursor - arenaBase));
        int bankEnd = relative < 0x15688 ? 0x15688 : 0x25688;
        if (relative < 0x5688 || relative > bankEnd - bytes)
        {
            throw new NotSupportedException("Effect303 fragment construction leaves its current physical packet bank.");
        }
        NativeBattleGeometryRegisterStep registers = Registers303(frame);
        int[] minimum = [32767, 32767, 32767];
        int[] maximum = [-32767, -32767, -32767];
        context = context.Write(12, BitConverter.GetBytes(cursor));
        for (int vertex = 0; vertex < count; vertex++)
        {
            ReadOnlySpan<byte> point = mesh.Bytes.AsSpan().Slice(8 + vertex * 8, 8);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(point), BinaryPrimitives.ReadInt16LittleEndian(point.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(point.Slice(4))), BinaryPrimitives.ReadInt16LittleEndian(point.Slice(6))));
            NativeBattleEffectArena transformed = NativeBattleEffectNodeContextMemory.Copy(context, 20, NativeBattleEffectArena.Clear(), 0, 8);
            transformed = StoreIr303(transformed, registers);
            context = NativeBattleEffectNodeContextMemory.Copy(transformed, 0, context, 20, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                int value = Word303(transformed, axis * 2);
                minimum[axis] = Math.Min(minimum[axis], value);
                maximum[axis] = Math.Max(maximum[axis], value);
            }
            frame = WriteBytes(frame, cursor + (uint)(vertex * 8), transformed, 8);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            context = context.Write(52 + axis * 2, BitConverter.GetBytes(unchecked((short)((minimum[axis] + maximum[axis]) / 2))));
        }
        uint matrices = cursor + (uint)(count * 8);
        context = context.Write(60, BitConverter.GetBytes(matrices));
        for (int index = 0; index < 64; index++)
        {
            short[] angles = new short[3];
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Crt303(frame, out int random);
                angles[axis] = unchecked((short)((random & 511) - 256));
                context = context.Write(64 + axis * 2, BitConverter.GetBytes(angles[axis]));
            }
            NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler56CD50(new(angles[0], angles[1], angles[2]));
            frame = Crt303(frame, out int scaleDraw);
            int scale = 3969 - (scaleDraw & 255);
            for (int axis = 0; axis < 3; axis++)
            {
                context = context.Write(72 + axis * 4, BitConverter.GetBytes(scale));
            }
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale, scale, scale);
            NativeBattleEffectArena retained = ReadBytes(frame, matrices + (uint)(index * 32), 32);
            for (int lane = 0; lane < 9; lane++)
            {
                retained = retained.Write(lane * 2, BitConverter.GetBytes(rotation.Rotation[lane]));
            }
            frame = WriteBytes(frame, matrices + (uint)(index * 32), retained, 32);
        }
        int polygon = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan())) + 8;
        for (int group = 0; group < 2; group++)
        {
            int faces = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(polygon));
            polygon += 4;
            int corners = group == 0 ? 3 : 4;
            int stride = group == 0 ? 20 : 24;
            for (int face = 0; face < faces; face++, polygon += stride)
            {
                context = context.Write(16, BitConverter.GetBytes(mesh.Address + (uint)polygon));
                (frame, uint rowAddress) = AllocateFragment303(frame, main);
                if (rowAddress == 0)
                {
                    continue;
                }
                NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 76);
                ReadOnlySpan<byte> source = mesh.Bytes.AsSpan().Slice(polygon, stride);
                row = row.Write(16, source.Slice(0, 4));
                int[,] points = new int[corners, 3];
                for (int corner = 0; corner < corners; corner++)
                {
                    int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(4 + corner * 2, 2)) * 4;
                    NativeBattleEffectArena point = ReadBytes(frame, cursor + (uint)offset, 8);
                    context = NativeBattleEffectNodeContextMemory.Copy(point, 0, context, 20 + corner * 8, 8);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        points[corner, axis] = Word303(point, axis * 2);
                    }
                }
                for (int axis = 0; axis < 3; axis++)
                {
                    int sum = 0;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        sum += points[corner, axis];
                    }
                    int center = sum / corners;
                    row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)center)));
                    row = row.Write(8 + axis * 2, BitConverter.GetBytes(unchecked((short)((center - Word303(context, 52 + axis * 2)) >> (Int303(context, 8) & 31)))));
                    for (int corner = 0; corner < corners; corner++)
                    {
                        row = row.Write(40 + corner * 6 + axis * 2, BitConverter.GetBytes(unchecked((short)(points[corner, axis] - center))));
                    }
                }
                row = row.Write(6, BitConverter.GetBytes((ushort)(corners - 2))).Write(64, source.Slice(12, 8));
                row = corners == 3 ? row.Write(72, source.Slice(10, 2)) : row.Write(72, source.Slice(20, 4));
                frame = Local303(frame, out int selector);
                NativeBattleEffectArena matrix = ReadBytes(frame, matrices + (uint)((selector & 63) * 32), 18);
                row = NativeBattleEffectNodeContextMemory.Copy(matrix, 0, row, 20, 18);
                frame = WriteBytes(frame, rowAddress, row, 76);
                if (frame.CapturePresentation)
                {
                    NativeBattleEffect32Presentation.Allocated(frame.Effect, main, main + 32, checked((int)((rowAddress - main - 32) / 76)));
                }
            }
            context = context.Write(16, BitConverter.GetBytes(mesh.Address + (uint)polygon));
        }
        _ = arenaOffset;
        return (Publish303(frame, registers), context);
    }

    private static (NativeBattleEffect303Frame Frame, uint Address) AllocateFragment303(NativeBattleEffect303Frame frame, uint main)
    {
        uint address = Dword303(ReadBytes(frame, main + 28, 4), 0);
        uint first = main + 32;
        uint last = main + 0x25D4;
        if (address < first || address > last || (address - first) % 76 != 0)
        {
            throw new InvalidDataException("Effect303 fragment hint requires its actual current row.");
        }
        if (Word303(ReadBytes(frame, address, 8), 6) != 0)
        {
            address = first;
            int index = 0;
            while (index < 128 && Word303(ReadBytes(frame, address, 8), 6) != 0)
            {
                address += 76;
                index++;
            }
            if (index == 128)
            {
                return (frame, 0);
            }
        }
        frame = WriteBytes(frame, main + 28, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(address >= last ? first : address + 76)), 4);
        frame = Local303(frame, out int life);
        return (WriteBytes(frame, address + 14, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)((life & 7) + 12))), 2), address);
    }

    public static NativeBattleEffect303Frame AdvanceFragments66B3A0(NativeBattleEffect303Frame frame, uint main)
    {
        for (int index = 0; index < 128; index++)
        {
            uint address = main + 32 + (uint)(index * 76);
            NativeBattleEffectArena row = ReadBytes(frame, address, 76);
            if (Word303(row, 6) == 0)
            {
                continue;
            }
            row = row.Write(14, BitConverter.GetBytes(unchecked((ushort)(Word303(row, 14) - 1))));
            bool killed = Word303(row, 14) == 0;
            if (!killed)
            {
                NativeBattleEffectArena packed = NativeBattleEffectNodeContextMemory.Copy(row, 20, NativeBattleEffectArena.Clear(), 0, 20);
                frame = LoadMatrix(frame, new(packed.ReadSnapshot(0, 32).Data.ToImmutableArray(), packed.ReadSnapshot(0, 32).Known.ToImmutableArray()), true, false);
                NativeBattleGeometryRegisterStep registers = Registers303(frame);
                registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, PointAt303(row, 40), PackedWAt303(row, 40)), false);
                row = StoreAt303(row, 40, registers);
                registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, PointAt303(row, 46), 0), false);
                killed = PointAt303(row, 40) is { X: 0, Y: 0, Z: 0 };
                if (!killed)
                {
                    row = StoreAt303(row, 46, registers);
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, PointAt303(row, 52), PackedWAt303(row, 52)), false);
                    row = StoreAt303(row, 52, registers);
                    registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, PointAt303(row, 58), 0), false);
                    row = StoreAt303(row, 58, registers);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((ushort)(Word303(row, axis * 2) + Word303(row, 8 + axis * 2)))));
                    }
                    short velocity = unchecked((short)(Word303(row, 10) + 30));
                    row = row.Write(10, BitConverter.GetBytes(Word303(row, 2) >= 0 ? unchecked((short)(-velocity >> 1)) : velocity));
                }
                frame = Publish303(frame, registers);
            }
            if (killed)
            {
                row = row.Write(6, new byte[2]);
                frame = WriteBytes(frame, main + 28, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(address)), 4);
            }
            frame = WriteBytes(frame, address, row, 76);
        }
        return frame;
    }

    private static NativeBattleVector PointAt303(NativeBattleEffectArena source, int offset) => new(Word303(source, offset), Word303(source, offset + 2), Word303(source, offset + 4));
    private static short? PackedWAt303(NativeBattleEffectArena source, int offset)
    {
        var snapshot = source.ReadSnapshot(offset + 6, 2);
        return snapshot.Known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(snapshot.Data) : null;
    }
    private static NativeBattleEffectArena StoreAt303(NativeBattleEffectArena source, int offset, NativeBattleGeometryRegisterStep registers)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            source = source.Write(offset + axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
        }
        return source;
    }
    private static NativeBattleEffect303Frame Local303(NativeBattleEffect303Frame frame, out int value)
    {
        value = unchecked((int)((frame.State.ReadDword(0x2507648) * 125 + 14) & 32767));
        return frame with { State = frame.State.WriteDword(0x2507648, (uint)value) };
    }
    private static NativeBattleEffect303Frame Crt303(NativeBattleEffect303Frame frame, out int value)
    {
        NativeCrtRandomState next = frame.Random.Draw(out ushort drawn);
        value = drawn;
        return frame with { Random = next };
    }
}
