using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect68Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Lower
{
    public static NativeBattleEffect68Frame DrawLines(NativeBattleEffect68Frame frame, NativeBattleModulePacketBatch batch, NativeBattleEffect68Inputs inputs)
    {
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>(batch.Primitives.Length);
        foreach (NativeBattleAllocatedPrimitive line in batch.Primitives)
        {
            byte[] bytes = new byte[16];
            for (int word = 0; word < 4; word++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), line.Primitive.Words[word]);
            }
            NativeBattleGeometryPrimitive primitive = line.Primitive with { VertexPayload4 = [0, 0, 0, 0] };
            attempts.Add(new(line.ByteOffset, bytes.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 16).ToImmutableArray(), primitive));
        }
        return inputs.Execute(frame, new NativeBattleEffect68DrawBatch(new(attempts.MoveToImmutable(), batch.AllocationBytes), Cursor));
    }

    internal static (NativeBattleEffect68Frame Frame, NativeBattleScanReticlePoint? Point) Ring(NativeBattleEffect68Frame frame, NativeBattleModulePoolTask task, short radius, short angle, short count, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleScanReticleRing ring = NativeBattleScanReticleLines.Ring84D6D0(S(task, 0x1C), S(task, 0x1E), radius, angle, count);
        return (DrawLines(frame, ring.Packets, inputs), ring.FinalPoint);
    }

    internal static NativeBattleEffect68Frame Line(NativeBattleEffect68Frame frame, NativeBattleScanReticlePoint first, NativeBattleScanReticlePoint second, NativeBattleEffect68Inputs inputs)
    {
        return DrawLines(frame, new([new(NativeBattleScanReticleLines.Line84E290(first, second), 0, 0)], 16), inputs);
    }

    public static NativeBattleModulePacketBatch RecordLines(NativeBattleModulePoolTask task, ReadOnlySpan<byte> records)
    {
        ImmutableArray<NativeBattleAllocatedPrimitive>.Builder lines = ImmutableArray.CreateBuilder<NativeBattleAllocatedPrimitive>();
        for (int offset = 0; offset <= records.Length - 2; offset += 16)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(records.Slice(offset)) == 0xFF)
            {
                return new(lines.ToImmutable(), lines.Count * 16);
            }
            if (offset > records.Length - 16)
            {
                break;
            }
            ReadOnlySpan<byte> record = records.Slice(offset, 16);
            int scale = S(task, 0x64);
            short x1 = unchecked((short)(S(task, 0x1C) + BinaryPrimitives.ReadInt16LittleEndian(record.Slice(4)) * scale / 4096 + 160));
            short y1 = unchecked((short)(S(task, 0x1E) + BinaryPrimitives.ReadInt16LittleEndian(record.Slice(6)) * scale / 4096 + 108));
            short x2 = unchecked((short)(S(task, 0x1C) + BinaryPrimitives.ReadInt16LittleEndian(record.Slice(8)) * scale / 4096 + 160));
            short y2 = unchecked((short)(S(task, 0x1E) + BinaryPrimitives.ReadInt16LittleEndian(record.Slice(10)) * scale / 4096 + 108));
            uint color = 0x40000000u | record[12] | (uint)record[13] << 8 | (uint)record[14] << 16;
            NativeBattleGeometryPrimitive primitive = new(5, [0x03000000, color, (uint)(ushort)x1 | (uint)(ushort)y1 << 16, (uint)(ushort)x2 | (uint)(ushort)y2 << 16], [0, 0, 0, 0]);
            lines.Add(new(primitive, lines.Count * 16, 0));
        }
        throw new NotSupportedException("Effect68 line records require their actual terminating WORD.");
    }

    private static NativeBattleEffect68Frame Reticle(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = Phase(task, 6);
        if (phase is 0 or 1)
        {
            uint row = checked(0x15956F0u + (uint)(S(task, 0x24) * 4));
            short x = Table(resources, row);
            if (phase == 1 && x == short.MaxValue)
            {
                task = Next(task.WriteWord(0x66, 0));
            }
            else
            {
                task = task.WriteWord(0x1C, unchecked((ushort)x)).WriteWord(0x1E, unchecked((ushort)Table(resources, row + 2)));
            }
            frame = Write(frame, address, task);
            if (phase == 0)
            {
                for (ushort corner = 0; corner < 4; corner++)
                {
                    (frame, uint child) = Child(frame, 0x25FFC40, 0x7DD150, 0x78, address, inputs);
                    frame = Write(frame, child, Read(frame, child).WriteWord(0x74, corner));
                }
                for (ushort corner = 0; corner < 4; corner++)
                {
                    (frame, uint child) = Child(frame, 0x25FFC40, 0x7DCF10, 0x78, address, inputs);
                    frame = Write(frame, child, Read(frame, child).WriteWord(0x74, corner));
                }
                (frame, _) = Child(frame, 0x25FD6E0, 0x7DCBE0, 0x6C, address, inputs);
                (frame, _) = Child(frame, 0x25FD6E0, 0x7DC950, 0x6C, address, inputs);
            }
            frame = Ring(frame, Read(frame, address), 8, 0, 32, inputs).Frame;
            task = Read(frame, address);
            frame = DrawLines(frame, NativeBattleScanReticleLines.FourLines84E1F0(S(task, 0x1C), S(task, 0x1E)), inputs);
            task = Read(frame, address);
            task = phase == 0 ? Next(task) : task.WriteWord(0x66, 8);
        }
        else if (phase == 2)
        {
            task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 2)));
            frame = Write(frame, address, task);
            if (S(task, 0x66) > 0)
            {
                frame = Ring(frame, task, S(task, 0x66), 0, 32, inputs).Frame;
                task = Read(frame, address);
                frame = DrawLines(frame, NativeBattleScanReticleLines.FourLines84E1F0(S(task, 0x1C), S(task, 0x1E)), inputs);
                task = Read(frame, address);
            }
            else
            {
                task = Next(task);
            }
        }
        else if (phase == 3)
        {
            (frame, _) = Child(frame, 0x25FFC40, 0x7DD510, 0x78, address, inputs);
            task = Next(Read(frame, address));
        }
        else if (phase == 4 && W(frame, Highlight) == 0)
        {
            task = Done(task);
        }
        return Write(frame, address, task);
    }

    private static NativeBattleEffect68Frame Corner(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs, bool bracket)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = Phase(task, bracket ? 5 : 3);
        if (phase == 0)
        {
            if (bracket)
            {
                NativeBattleModulePoolTask parent = Read(frame, task.ReadDword(0x18));
                task = task.WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20));
            }
            int corner = task.ReadWord(0x74);
            if (corner < 4)
            {
                task = task.WriteDword(0x50, (bracket ? 0x1595630u : 0x1595570u) + (uint)(corner * 0x30));
                task = task.WriteWord(0x1C, bracket ? (ushort)0 : unchecked((ushort)((corner & 1) == 0 ? -144 : 144))).WriteWord(0x1E, bracket ? (ushort)0 : unchecked((ushort)(corner < 2 ? -96 : 96)));
                if (bracket)
                {
                    task = task.WriteWord(0x48, unchecked((ushort)((corner & 1) == 0 ? -1 : 1))).WriteWord(0x4A, unchecked((ushort)(corner < 2 ? -1 : 1)));
                }
            }
            task = Next(task.WriteWord(0x64, 4096));
            if (bracket)
            {
                task = task.WriteWord(0x66, 30);
            }
        }
        else if (phase != (bracket ? 4 : 2))
        {
            if (bracket && phase == 1)
            {
                NativeBattleModulePoolTask parent = Read(frame, task.ReadDword(0x18));
                task = task.WriteDword(0x1C, parent.ReadDword(0x1C)).WriteDword(0x20, parent.ReadDword(0x20));
            }
            if (bracket && phase == 2)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + S(task, 0x48)))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + S(task, 0x4A)))).WriteWord(0x64, unchecked((ushort)(task.ReadWord(0x64) + 1024))).WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (S(task, 0x66) < 0)
                {
                    task = Next(task);
                }
            }
            frame = Write(frame, address, task);
            frame = DrawLines(frame, RecordLines(task, resources.ReadStatic(task.ReadDword(0x50), 34)), inputs);
            task = Read(frame, address);
            if (bracket && phase == 1)
            {
                task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                if (S(task, 0x66) < 0)
                {
                    task = Next(task.WriteWord(0x66, 4));
                }
            }
            else if ((!bracket || phase == 3) && W(frame, Highlight) == 0)
            {
                task = Done(task);
            }
        }
        return Write(frame, address, task);
    }

    private static NativeBattleEffect68Frame Annotation(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = Phase(task, 5);
        if (phase == 0)
        {
            return Write(frame, address, Next(task.WriteWord(0x66, 0)));
        }
        if (phase == 4)
        {
            return frame;
        }
        if (phase == 1)
        {
            task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) + 4)));
            if (S(task, 0x66) >= 24)
            {
                task = Next(task.WriteWord(0x66, 24));
            }
        }
        frame = Write(frame, address, task);
        (frame, NativeBattleScanReticlePoint? output) = Ring(frame, task, 48, 0x600, S(task, 0x66), inputs);
        if (phase >= 2)
        {
            NativeBattleScanReticlePoint first = output ?? throw new NotSupportedException("Effect68 annotation requires its reached ring endpoint.");
            NativeBattleScanReticlePoint second = new(unchecked((short)(first.X + 24)), unchecked((short)(first.Y + 24)));
            frame = Line(frame, first, second, inputs);
            if (phase == 2)
            {
                (frame, _) = Child(frame, 0x25FFA90, 0x7DD650, 0x58, address, inputs);
                frame = Write(frame, address, Next(Read(frame, address)));
            }
            else
            {
                frame = Line(frame, second, new(unchecked((short)(second.X + 88)), second.Y), inputs);
                if (W(frame, Highlight) == 0)
                {
                    frame = Write(frame, address, Done(Read(frame, address)));
                }
            }
        }
        return frame;
    }
}
