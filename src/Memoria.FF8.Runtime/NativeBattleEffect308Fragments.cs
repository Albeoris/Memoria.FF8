using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect308Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect308Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect308Fragments
{
    public static (NativeBattleEffect308Frame Frame, uint Next) Birth667DB0(NativeBattleEffect308Frame frame, uint script, uint model, uint offsets, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        ImmutableArray<short>.Builder selected = ImmutableArray.CreateBuilder<short>();
        while (true)
        {
            short bone = Word308(ReadBytes(frame, script, 2), 0);
            script = checked(script + 2);
            if (bone == -1)
            {
                break;
            }
            selected.Add(bone);
        }
        NativeBattleEffectArena parameters = ReadBytes(frame, script, 8);
        frame = inputs.Execute(frame, new NativeBattleEffect308SelectFragments667DB0(model, offsets, selected.ToImmutable(), Word308(parameters, 0), Word308(parameters, 2), Word308(parameters, 4), Word308(parameters, 6)));
        return (frame, checked(script + 8));
    }

    public static NativeBattleEffect308RandomStep InternalRandom668450(NativeBattleEffect308Frame frame)
    {
        uint value = unchecked(frame.State.ReadDword(InternalRandomGlobal) * 125 + 14) & 0x7FFF;
        return new(frame with { State = frame.State.WriteDword(InternalRandomGlobal, value) }, checked((int)value));
    }

    public static (NativeBattleEffect308Frame Frame, uint Address) AllocateSmoke668470(NativeBattleEffect308Frame frame, uint main)
    {
        return AllocateSmoke668470(frame, main, false);
    }

    public static (NativeBattleEffect308Frame Frame, uint Address) AllocateSmoke668470(NativeBattleEffect308Frame frame, uint main, bool capturePresentation)
    {
        uint row = Dword308(ReadBytes(frame, main + 0xE90, 4), 0);
        if (Word308(ReadBytes(frame, row + 6, 2), 0) != 0)
        {
            row = checked(main + 0x890);
            int remaining = 128;
            while (Word308(ReadBytes(frame, row + 6, 2), 0) != 0)
            {
                row = checked(row + 12);
                remaining--;
                if (remaining == 0)
                {
                    return (frame, 0);
                }
            }
        }
        if (capturePresentation)
        {
            NativeBattleSelectedProgrammeVisuals.Allocated(frame.Effect.Pools, row);
        }
        uint next = row >= main + 0xE84 ? main + 0x890 : row + 12;
        frame = WriteBytes(frame, main + 0xE90, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(next)), 4);
        return (frame, row);
    }

    public static NativeBattleEffect308Frame Collect667F40(NativeBattleEffect308Frame frame, Func<int, int, byte[]> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, uint scratchAddress, int scratchLength, uint contextAddress, NativeBattleEffect308Resources resources, NativeBattleEffect308Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, contextAddress, 140);
        int objects = I32(0);
        for (int obj = 0; obj < objects; obj++)
        {
            if ((Dword308(context, 0x10) & (1u << (obj & 31))) == 0)
            {
                continue;
            }
            int objectStart = I32(4 + obj * 4);
            while (true)
            {
                int cursor = objectStart;
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
                        throw new InvalidDataException("Effect308 fragment group leaves its current rig.");
                    }
                    NativeBattlePackedMatrix32 matrix = bones[bone];
                    if (!matrix.Known[18] || !matrix.Known[19])
                    {
                        throw new NotSupportedException("Effect308 requires its current physical bone selector WORD.");
                    }
                    bool selected = BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) != 0;
                    if (selected)
                    {
                        frame = LoadMatrix(frame, matrix);
                        any = true;
                    }
                    for (int vertex = 0; vertex < count; vertex++)
                    {
                        int position = checked(total * 8);
                        if (position > scratchLength - 8)
                        {
                            throw new InvalidDataException("Effect308 fragments exceed the current model scratch allocation.");
                        }
                        uint destination = checked(scratchAddress + (uint)position);
                        if (selected)
                        {
                            NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(I16(cursor), I16(cursor + 2), I16(cursor + 4)), 0));
                            NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().Write(6, new byte[2]);
                            for (int axis = 0; axis < 3; axis++)
                            {
                                packed = packed.Write(axis * 2, BitConverter.GetBytes(unchecked((short)transformed.Colors.Ir[axis]!.Value)));
                            }
                            frame = WriteBytes(Publish(frame, transformed), destination, packed, 8);
                        }
                        else
                        {
                            frame = WriteBytes(frame, destination + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)1)), 2);
                        }
                        total++;
                        cursor += 6;
                    }
                }
                if (!any)
                {
                    break;
                }
                cursor = checked((cursor + 3) & ~3);
                int triangles = U16(cursor);
                int quads = U16(cursor + 2);
                cursor += 12;
                context = Put(context, 0, checked(geometryAddress + (uint)cursor));
                context = Word(Word(context, 8, triangles), 10, quads);
                for (int kind = 0; kind < 2; kind++)
                {
                    int count = kind == 0 ? triangles : quads;
                    int countOffset = kind == 0 ? 8 : 10;
                    context = Word(context, countOffset, count - 1);
                    for (int face = 0; face < count; face++)
                    {
                        byte[] polygon = readGeometry(cursor, kind == 0 ? 16 : 20);
                        byte flags = 0;
                        for (int corner = 0; corner < (kind == 0 ? 3 : 4); corner++)
                        {
                            int index = BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(corner * 2)) & 0xFFF;
                            if (index >= total)
                            {
                                throw new InvalidDataException("Effect308 fragment face leaves its current scratch rows.");
                            }
                            NativeBattleEffectArena packed = ReadBytes(frame, checked(scratchAddress + (uint)(index * 8)), 8);
                            context = CopyBytes(packed, 0, context, 0x2C + corner * 8, 8);
                            flags |= packed.Read(6, 1)[0];
                        }
                        if (flags == 0)
                        {
                            int cooldown = unchecked((int)Dword308(context, 0x18));
                            if (cooldown == 0)
                            {
                                frame = PublishContext(frame, contextAddress, context, 140);
                                NativeBattleEffect308RandomStep sample = inputs.ProcessRandom7059E0(frame);
                                frame = sample.Frame;
                                if (sample.Value <= unchecked((int)Dword308(context, 0x24)))
                                {
                                    int oldLimit = unchecked((int)Dword308(context, 0x14));
                                    context = Put(context, 0x14, unchecked((uint)(oldLimit - 1)));
                                    if (oldLimit <= 0)
                                    {
                                        return PublishContext(frame, contextAddress, context, 140);
                                    }
                                    context = Put(context, 0x18, 8);
                                    frame = PublishContext(frame, contextAddress, context, 140);
                                    frame = EmitSmoke668230(frame, contextAddress, resources, inputs.CapturePresentation);
                                    context = Context(frame, contextAddress, 140);
                                    cooldown = 8;
                                }
                            }
                            if (cooldown != 0)
                            {
                                context = Put(context, 0x18, unchecked((uint)(cooldown - 1)));
                            }
                        }
                        cursor += kind == 0 ? 16 : 20;
                        context = Word(context, countOffset, count - face - 2);
                    }
                }
                frame = PublishContext(frame, contextAddress, context, 140);
                if (unchecked((int)Dword308(context, 0x14)) <= 0)
                {
                    return frame;
                }
            }
        }
        return PublishContext(frame, contextAddress, context, 140);

        short I16(int offset)
        {
            return BinaryPrimitives.ReadInt16LittleEndian(readGeometry(offset, 2));
        }
        ushort U16(int offset)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(readGeometry(offset, 2));
        }
        int I32(int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(readGeometry(offset, 4));
        }
    }

    public static NativeBattleEffect308Frame EmitSmoke668230(NativeBattleEffect308Frame frame, uint contextAddress, NativeBattleEffect308Resources resources)
    {
        return EmitSmoke668230(frame, contextAddress, resources, false);
    }

    public static NativeBattleEffect308Frame EmitSmoke668230(NativeBattleEffect308Frame frame, uint contextAddress, NativeBattleEffect308Resources resources, bool capturePresentation)
    {
        NativeBattleEffectArena context = Context(frame, contextAddress, 140);
        int[] first = Enumerable.Range(0, 3).Select(axis => Word308(context, 0x2C + axis * 2) - Word308(context, 0x34 + axis * 2)).ToArray();
        int[] second = Enumerable.Range(0, 3).Select(axis => Word308(context, 0x3C + axis * 2) - Word308(context, 0x2C + axis * 2)).ToArray();
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        byte?[] rotation = registers.Geometry.Rotation20.ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put(Put(context, 0x5C + axis * 4, unchecked((uint)first[axis])), 0x6C + axis * 4, unchecked((uint)second[axis]));
            byte[] bytes = BitConverter.GetBytes(first[axis]);
            for (int lane = 0; lane < 4; lane++)
            {
                rotation[axis * 8 + lane] = bytes[lane];
            }
        }
        registers = registers with { Geometry = registers.Geometry with { Rotation20 = rotation.ToImmutableArray() }, Colors = registers.Colors with { Ir = second.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleGeometry.OuterProduct45E670(registers);
        int[] cross = registers.Colors.Mac.Select(value => value!.Value).ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            context = Put(context, 0x7C + axis * 4, unchecked((uint)cross[axis]));
        }
        int maximum = cross.Select(value => value < 0 ? unchecked(-value) : value).Max();
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)maximum)) };
        int leading = checked((int)registers.Geometry.Lzcr!.Value);
        int[] reduced = cross.Select(value => leading < 18 ? value >> (18 - leading) : value).ToArray();
        if (leading < 18)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                context = Put(context, 0x7C + axis * 4, unchecked((uint)reduced[axis]));
            }
        }
        registers = registers with { Colors = registers.Colors with { Ir = reduced.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int sum = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)sum)) };
        int even = checked((int)registers.Geometry.Lzcr!.Value) & ~1;
        int index = even <= 24 ? sum >> (24 - even) : unchecked(sum << (even - 24));
        if (index != 0 && (index < 64 || index > 255))
        {
            throw new InvalidDataException("Effect308 normal leaves its reached lookup domain.");
        }
        int table = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(checked(0x102E3A0u + (uint)(index * 2)), 2));
        int length = unchecked(table << ((31 - even) >> 1)) >> 12;
        frame = PublishContext(Publish(frame, registers), contextAddress, context, 140);
        if (length <= 0)
        {
            return frame;
        }
        int speed = unchecked((int)Dword308(context, 0x1C));
        short[] velocity = reduced.Select(value => unchecked((short)(unchecked(speed * value) / length))).ToArray();
        (frame, uint smoke) = AllocateSmoke668470(frame, Dword308(context, 0x28), capturePresentation);
        if (smoke == 0)
        {
            return frame;
        }
        NativeBattleEffectArena row = CopyBytes(context, 0x2C, ReadBytes(frame, smoke, 12), 0, 8);
        NativeBattleEffect308RandomStep random = InternalRandom668450(frame);
        frame = random.Frame;
        bool reducedVelocity = random.Value > 12000;
        row = Word(row, 6, reducedVelocity ? 12 : 11).Write(11, [reducedVelocity ? (byte)0 : (byte)1]);
        for (int axis = 0; axis < 3; axis++)
        {
            row = row.Write(8 + axis, [unchecked((byte)(reducedVelocity ? velocity[axis] >> 2 : velocity[axis]))]);
        }
        return WriteBytes(frame, smoke, row, 12);
    }

    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value)
    {
        return arena.Write(offset, BitConverter.GetBytes(value));
    }
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, int value)
    {
        return arena.Write(offset, BitConverter.GetBytes(unchecked((ushort)value)));
    }
}
