using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect290Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect290Lower;
using static Memoria.FF8.Runtime.NativeBattleEffect290Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect290Fragments
{
    public static NativeBattleEffect290Frame Collect(NativeBattleEffect290Frame frame, Func<int, int, byte[]> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, uint scratchAddress, uint contextAddress, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, contextAddress, 144);
        int objects = I32(0);
        for (int obj = 0; obj < objects; obj++)
        {
            if ((Dword290(context, 0x10) & (1u << (obj & 31))) == 0)
            {
                continue;
            }
            int cursor = I32(4 + obj * 4);
            while (true)
            {
                int groups = I16(cursor);
                cursor += 2;
                if (groups <= 0)
                {
                    break;
                }
                int total = 0;
                bool any = false;
                for (int group = 0; group < groups; group++)
                {
                    int bone = I16(cursor);
                    int count = I16(cursor + 2);
                    cursor += 4;
                    if ((uint)bone >= (uint)bones.Length || count < 0)
                    {
                        throw new InvalidDataException("The fragment collector requires its actual current bone and vertex count.");
                    }
                    NativeBattlePackedMatrix32 matrix = bones[bone];
                    short flag = BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2));
                    if (!matrix.Known[18] || !matrix.Known[19])
                    {
                        throw new NotSupportedException("The fragment collector requires the current physical bone selector word.");
                    }
                    any |= flag != 0;
                    if (flag != 0)
                    {
                        frame = LoadMatrix(frame, matrix);
                    }
                    for (int index = 0; index < count; index++)
                    {
                        uint destination = checked(scratchAddress + (uint)(total * 8));
                        if (flag != 0)
                        {
                            NativeBattleGeometryRegisterStep step = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(I16(cursor), I16(cursor + 2), I16(cursor + 4)), 0));
                            NativeBattleEffectArena vertex = PutWord290(NativeBattleEffectArena.Clear(), 6, 0);
                            for (int axis = 0; axis < 3; axis++)
                            {
                                vertex = PutWord290(vertex, axis * 2, step.Colors.Ir[axis]!.Value);
                            }
                            frame = WriteBytes(Publish(frame, step), destination, vertex, 8);
                        }
                        else
                        {
                            frame = WriteBytes(frame, destination + 6, PutWord290(NativeBattleEffectArena.Clear(), 0, 1), 2);
                        }
                        total++;
                        cursor += 6;
                    }
                }
                if (!any)
                {
                    break;
                }
                cursor = (cursor + 3) & ~3;
                int triangles = U16(cursor);
                int quads = U16(cursor + 2);
                if (U16(cursor + 4) != 0 || U16(cursor + 6) != 0)
                {
                    throw new NotSupportedException("The fragment collector requires its current triangle and quad faces.");
                }
                cursor += 12;
                int polygonStart = cursor;
                context = Put290(context, 0, checked(geometryAddress + (uint)cursor));
                context = PutWord290(PutWord290(context, 8, triangles), 10, quads);
                for (int face = 0; face < triangles + quads; face++)
                {
                    bool quad = face >= triangles;
                    byte[] raw = readGeometry(cursor, quad ? 20 : 16);
                    byte flags = 0;
                    for (int corner = 0; corner < (quad ? 4 : 3); corner++)
                    {
                        int index = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(corner * 2)) & 0xFFF;
                        if (index >= total)
                        {
                            throw new InvalidDataException("The fragment face leaves its actual current scratch rows.");
                        }
                        NativeBattleEffectArena vertex = ReadBytes(frame, checked(scratchAddress + (uint)(index * 8)), 8);
                        context = CopyBytes(vertex, 0, context, 0x34 + corner * 8, 8);
                        flags |= vertex.Read(6, 1)[0];
                    }
                    if (flags == 0)
                    {
                        bool accepted = true;
                        if (Dword290(context, 0x18) == 0)
                        {
                            frame = PublishContext(frame, contextAddress, context, 144);
                            NativeBattleEffect290RandomStep sample = inputs.ProcessRandom7059E0(frame);
                            frame = sample.Frame;
                            accepted = sample.Value <= unchecked((int)Dword290(context, 0x24));
                            if (accepted)
                            {
                                int oldLimit = unchecked((int)Dword290(context, 0x14));
                                context = Put290(context, 0x14, unchecked((uint)(oldLimit - 1)));
                                if (oldLimit <= 0)
                                {
                                    return PublishContext(frame, contextAddress, context, 144);
                                }
                                context = Put290(context, 0x18, 8);
                                frame = PublishContext(frame, contextAddress, context, 144);
                                (frame, uint record) = AllocateRecord675FD0(frame, Dword290(context, 0x30));
                                if (record == 0)
                                {
                                    return PublishContext(frame, contextAddress, Put290(context, 0x14, 0), 144);
                                }
                                context = Put290(Put290(context, 0x28, record), 0x2C, record + 0x1C);
                                frame = PublishContext(frame, contextAddress, context, 144);
                                frame = Initialize6760A0(frame, contextAddress, resources, inputs);
                                context = Context(frame, contextAddress, 144);
                            }
                        }
                        if (accepted)
                        {
                            uint materials = Dword290(context, 0x2C);
                            NativeBattleEffectArena uv = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(0, raw.AsSpan(8, 8)).Write(8, raw.AsSpan(quad ? 16 : 6, 2));
                            frame = WriteBytes(frame, materials, uv, 10);
                            context = Put290(Put290(context, 0x2C, materials + 12), 0x18, unchecked(Dword290(context, 0x18) - 1));
                        }
                    }
                    context = PutWord290(context, quad ? 10 : 8, (quad ? quads - (face - triangles) : triangles - face) - 1);
                    cursor += quad ? 20 : 16;
                }
                frame = PublishContext(frame, contextAddress, context, 144);
                if (unchecked((int)Dword290(context, 0x14)) <= 0)
                {
                    return PublishContext(frame, contextAddress, context, 144);
                }
                cursor = polygonStart;
            }
        }
        return PublishContext(frame, contextAddress, context, 144);

        short I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(offset, 2));
        ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(readGeometry(offset, 2));
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(offset, 4));
    }

    public static NativeBattleEffect290Frame Initialize6760A0(NativeBattleEffect290Frame frame, uint contextAddress, NativeBattleEffect290Resources resources, NativeBattleEffect290Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, contextAddress, 144);
        uint address = Dword290(context, 0x28);
        NativeBattleEffectArena record = ReadBytes(frame, address, 0x7C);
        record = CopyBytes(context, 0x34, record, 0, 8);
        NativeBattleEffect290RandomStep life = inputs.ProcessRandom7059E0(frame);
        frame = life.Frame;
        record = PutWord290(record, 6, 16 + (life.Value >> 12));
        int[] a = Enumerable.Range(0, 3).Select(axis => Word290(context, 0x34 + axis * 2) - Word290(context, 0x3C + axis * 2)).ToArray();
        int[] b = Enumerable.Range(0, 3).Select(axis => Word290(context, 0x44 + axis * 2) - Word290(context, 0x34 + axis * 2)).ToArray();
        NativeBattleGeometryRegisterStep step = Registers(frame);
        byte?[] rotation = step.Geometry.Rotation20.ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put290(Put290(context, 0x64 + axis * 4, unchecked((uint)a[axis])), 0x74 + axis * 4, unchecked((uint)b[axis]));
            byte[] bytes = BitConverter.GetBytes(a[axis]);
            for (int lane = 0; lane < 4; lane++)
            {
                rotation[axis * 8 + lane] = bytes[lane];
            }
        }
        step = step with { Geometry = step.Geometry with { Rotation20 = rotation.ToImmutableArray() }, Colors = step.Colors with { Ir = b.Select(value => (int?)value).ToImmutableArray() } };
        step = NativeBattleGeometry.OuterProduct45E670(step);
        int[] cross = step.Colors.Mac.Select(value => value!.Value).ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put290(context, 0x84 + axis * 4, unchecked((uint)cross[axis]));
        }
        int maximum = cross.Select(value => value < 0 ? unchecked(-value) : value).Max();
        step = step with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(step.Geometry, unchecked((uint)maximum)) };
        frame = Publish(frame, step);
        for (int index = 0; index < 4; index++)
        {
            NativeBattleEffect290RandomStep random = InternalRandom676080(frame);
            frame = random.Frame;
            record = PutWord290(record, 0x10 + index * 2, index < 2 ? random.Value : (random.Value >> 5) - 512);
        }
        step = Registers(frame);
        int leading = checked((int)step.Geometry.Lzcr!.Value);
        int[] reduced = cross.Select(value => leading < 18 ? value >> (18 - leading) : value).ToArray();
        step = step with { Colors = step.Colors with { Ir = reduced.Select(value => (int?)value).ToImmutableArray() } };
        step = NativeBattleReflectBeam135Geometry.Square45F930(step);
        int sum = unchecked(step.Colors.Mac[0]!.Value + step.Colors.Mac[1]!.Value + step.Colors.Mac[2]!.Value);
        step = step with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(step.Geometry, unchecked((uint)sum)) };
        int even = checked((int)step.Geometry.Lzcr!.Value) & ~1;
        int tableIndex = even <= 24 ? sum >> (24 - even) : unchecked(sum << (even - 24));
        if (tableIndex != 0 && (tableIndex < 64 || tableIndex > 255))
        {
            throw new NotSupportedException("The fragment normal leaves its actual normalization table.");
        }
        int table = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(checked(NormalizationAddress - 128u + (uint)(tableIndex * 2)), 2));
        int length = unchecked(table << ((31 - even) >> 1)) >> 12;
        int speed = unchecked((int)Dword290(context, 0x1C));
        for (int axis = 0; axis < 3; axis++)
        {
            record = PutWord290(record, 8 + axis * 2, unchecked(speed * reduced[axis]) / length);
        }
        frame = Publish(frame, step);
        uint main = Dword290(context, 0x30);
        (frame, uint smoke) = AllocateSmoke(frame, main);
        if (smoke != 0)
        {
            NativeBattleEffectArena row = CopyBytes(record, 0, ReadBytes(frame, smoke, 12), 0, 8);
            row = PutWord290(row, 6, 12);
            for (int axis = 0; axis < 3; axis++)
            {
                row = row.Write(8 + axis, [unchecked((byte)(Word290(record, 8 + axis * 2) >> 2))]);
            }
            frame = WriteBytes(frame, smoke, row, 12);
        }
        record = PutWord290(record, 10, Word290(record, 10) - unchecked((short)(speed << 1)));
        NativeBattleEffect290RandomStep scale = inputs.ProcessRandom7059E0(frame);
        frame = scale.Frame;
        int size = unchecked((int)Dword290(context, 0x20));
        record = PutWord290(record, 14, unchecked((scale.Value * size) >> 15) + unchecked((short)size));
        frame = WriteBytes(frame, address, record, 0x7C);
        return PublishContext(frame, contextAddress, context, 144);
    }

    public static (NativeBattleEffect290Frame Frame, uint Address) AllocateSmoke(NativeBattleEffect290Frame frame, uint main)
    {
        uint hint = Dword290(ReadBytes(frame, main + 0x64DC, 4), 0);
        uint found = 0;
        if (hint >= main + 0x5ED8 && hint < main + 0x64D8 && (hint - main - 0x5ED8) % 12 == 0 && Word290(ReadBytes(frame, hint, 12), 6) == 0)
        {
            found = hint;
        }
        else
        {
            for (int index = 0; index < 128; index++)
            {
                uint row = checked(main + 0x5ED8 + (uint)(index * 12));
                if (Word290(ReadBytes(frame, row, 12), 6) == 0)
                {
                    found = row;
                    break;
                }
            }
        }
        if (found != 0)
        {
            uint next = found == main + 0x64CC ? main + 0x5ED8 : found + 12;
            frame = WriteBytes(frame, main + 0x64DC, Put290(NativeBattleEffectArena.Clear(), 0, next), 4);
        }
        return (frame, found);
    }
}
