using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static NativeBattleEffect298Frame DrawSmoke672C40(NativeBattleEffect298Frame frame, uint main, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        foreach (int offset in new[] { 0x92, 0x90, 0x8E, 0x8A, 0x88, 0x86, 0x60, 0x58, 0x50, 0x48 })
        {
            context = context.Write(offset, new byte[2]);
        }
        frame = PublishContext(frame, temporary, context, 180);
        for (int index = 0; index < 128; index++)
        {
            uint row = main + 0x83EC + (uint)(index * 12);
            if (Word298(ReadBytes(frame, row, 12), 6) != 0)
            {
                frame = DrawSmoke672CD0(frame, row, temporary, resources, inputs);
            }
        }
        return Release(frame, 180);
    }

    public static NativeBattleEffect298Frame DrawSmoke672CD0(NativeBattleEffect298Frame frame, uint rowAddress, uint temporary, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleEffectArena row = ReadBytes(frame, rowAddress, 12);
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers298(frame), 0, Vector298(row), Word298(row, 6)));
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        for (int axis = 0; axis < 3; axis++)
        {
            context = context.Write(0x98 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis] ?? throw new NotSupportedException("Effect298 smoke requires its current camera translation.")));
        }
        foreach (int offset in new[] { 0x94, 0x8C, 0x84 })
        {
            context = context.Write(offset, BitConverter.GetBytes((short)4096));
        }
        uint sourceAddress = row.Read(11, 1)[0] == 0 ? 0x104EA64u : 0x104E74Cu;
        NativeBattleModuleResource source = resources.ReadSprite(sourceAddress);
        int phases = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(8));
        int phase = phases - Word298(row, 6);
        int pointerOffset = checked(8 + phase * 2);
        if (pointerOffset < 0 || pointerOffset > source.Bytes.Length - 2)
        {
            throw new InvalidDataException("Effect298 reversed smoke phase leaves its actual source directory.");
        }
        int at = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan().Slice(pointerOffset));
        if (at > source.Bytes.Length - 4)
        {
            throw new InvalidDataException("Effect298 smoke frame pointer leaves its actual source.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan().Slice(at));
        context = context.Write(0, BitConverter.GetBytes(sourceAddress)).Write(0x2C, BitConverter.GetBytes(checked(sourceAddress + (uint)at + 4))).Write(0x30, BitConverter.GetBytes(count));
        frame = PublishContext(Publish298(frame, registers), temporary, context, 180);
        frame = DrawSmokeQuads672DB0(frame, temporary, source, at + 4, count, inputs);
        row = ReadBytes(frame, rowAddress, 12);
        row = row.Write(6, BitConverter.GetBytes(unchecked((ushort)(Word298(row, 6) - 1))));
        for (int axis = 0; axis < 3; axis++)
        {
            int velocity = unchecked((sbyte)row.Read(8 + axis, 1)[0]);
            row = row.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(Word298(row, axis * 2) + (velocity << 2)))));
        }
        return WriteBytes(frame, rowAddress, row, 12);
    }

    public static NativeBattleEffect298Frame DrawSmokeQuads672DB0(NativeBattleEffect298Frame frame, uint temporary, NativeBattleModuleResource source, int firstQuad, int count, NativeBattleEffect298Inputs inputs)
    {
        if (count <= 0)
        {
            return frame;
        }
        if (firstQuad < 0 || firstQuad + 20L * count > source.Bytes.Length)
        {
            throw new InvalidDataException("Effect298 smoke quads leave their selected source frame.");
        }
        NativeBattleEffectArena context = Context(frame, temporary, 180);
        NativeBattleGeometryRegisterStep registers = Registers298(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect298 smoke requires its actual projection controls.");
        var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < count; index++)
        {
            int at = firstQuad + index * 20;
            ReadOnlySpan<byte> quad = source.Bytes.AsSpan().Slice(at, 20);
            int width = quad[0];
            int height = quad[2];
            foreach (int offset in new[] { 0x72, 0x70, 0x6E, 0x6A, 0x68, 0x66 })
            {
                context = context.Write(offset, new byte[2]);
            }
            context = context.Write(0x74, BitConverter.GetBytes((short)4096)).Write(0x64, quad.Slice(16, 2)).Write(0x6C, quad.Slice(18, 2));
            foreach (int offset in new[] { 0x54, 0x44 })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(-width * 8))));
            }
            foreach (int offset in new[] { 0x5C, 0x4C })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(width * 8))));
            }
            foreach (int offset in new[] { 0x4E, 0x46 })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(-height * 8))));
            }
            foreach (int offset in new[] { 0x5E, 0x56 })
            {
                context = context.Write(offset, BitConverter.GetBytes(unchecked((short)(height * 8))));
            }
            context = context.Write(0x78, BitConverter.GetBytes(unchecked((BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(8)) << 4) + width * 8))).Write(0x7C, BitConverter.GetBytes(unchecked((BinaryPrimitives.ReadInt16LittleEndian(quad.Slice(10)) << 4) + height * 8))).Write(0x80, new byte[4]);
            NativeBattlePackedMatrix32 parent = SmokeMatrix298(context, 0x84);
            registers = SmokeLoadMatrix298(registers, parent, true, false);
            for (int column = 0; column < 3; column++)
            {
                registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)Word298(context, 0x64 + column * 2)), unchecked((ushort)Word298(context, 0x6A + column * 2)), unchecked((ushort)Word298(context, 0x70 + column * 2)));
                registers = NativeBattleGeometry.Transform(registers, false, true);
                for (int axis = 0; axis < 3; axis++)
                {
                    context = context.Write(0x64 + axis * 6 + column * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
                }
            }
            registers = SmokeLoadMatrix298(registers, parent, false, true);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)Int298(context, 0x78)), unchecked((short)Int298(context, 0x7C)), unchecked((short)Int298(context, 0x80))), unchecked((short)(Int298(context, 0x80) >> 16))));
            for (int axis = 0; axis < 3; axis++)
            {
                context = context.Write(0x78 + axis * 4, BitConverter.GetBytes(registers.Colors.Mac[axis]!.Value));
            }
            registers = SmokeLoadMatrix298(registers, SmokeMatrix298(context, 0x64), true, true);
            foreach ((int slot, int offset) in new[] { (0, 0x44), (1, 0x4C), (2, 0x54) })
            {
                registers = SmokeLoadVertex298(registers, context, slot, offset);
            }
            byte[] data = new byte[40];
            var mask = new NativeBattleGfPacketByteState[40];
            void Store(int offset, ReadOnlySpan<byte> bytes)
            {
                bytes.CopyTo(data.AsSpan(offset));
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, bytes.Length);
            }
            Store(0, BitConverter.GetBytes(0x09000000u));
            Store(4, quad.Slice(0, 4));
            Store(12, quad.Slice(4, 4));
            Store(28, quad.Slice(4, 1));
            Store(21, quad.Slice(5, 1));
            registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            int right = Math.Min(width + quad[4], 255);
            Store(36, [(byte)right]);
            Store(20, [(byte)right]);
            uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Effect298 smoke requires its actual first projection flags.");
            context = context.Write(0x40, BitConverter.GetBytes(flags));
            NativeBattleGeometryPrimitive? primitive = null;
            if ((flags & 0x60000) == 0)
            {
                Store(22, BitConverter.GetBytes(unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(quad.Slice(14)) & 0x1FF))));
                for (int vertex = 0; vertex < 3; vertex++)
                {
                    Store(8 + vertex * 8, BitConverter.GetBytes(registers.Geometry.Sxy[vertex]!.Value));
                }
                registers = NativeBattleGeometry.ProjectSingle(SmokeLoadVertex298(registers, context, 0, 0x5C), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                int bottom = Math.Min(height + quad[5], 255);
                Store(37, [(byte)bottom]);
                Store(29, [(byte)bottom]);
                Store(32, BitConverter.GetBytes(registers.Geometry.Sxy[2]!.Value));
                ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                ImmutableArray<int> depths = registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect298 smoke requires its current four-depth insertion payload.")))).ToImmutableArray();
                primitive = new(19, words, depths);
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated += 40;
            }
        }
        frame = PublishContext(Publish298(frame, registers), temporary, context, 180);
        return inputs.Execute(frame, new NativeBattleEffect298DeliverBatch(new(attempts.ToImmutable(), allocated), NativeBattleEffect298Profile.CursorGlobal));
    }

    private static NativeBattlePackedMatrix32 SmokeMatrix298(NativeBattleEffectArena context, int offset)
    {
        (byte[] bytes, bool[] known) = context.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleGeometryRegisterStep SmokeLoadMatrix298(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 matrix, bool rotation, bool translation)
    {
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix.ReadMatrix(), rotation, translation, null);
        if (rotation)
        {
            geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => matrix.Known[index] ? (byte?)matrix.Bytes[index] : null).ToImmutableArray() };
        }
        return registers with { Geometry = geometry };
    }

    private static NativeBattleGeometryRegisterStep SmokeLoadVertex298(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena context, int slot, int offset)
    {
        (byte[] w, bool[] known) = context.ReadSnapshot(offset + 6, 2);
        return NativeBattleGeometry.LoadVector(registers, slot, new(Word298(context, offset), Word298(context, offset + 2), Word298(context, offset + 4)), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
    }
}
