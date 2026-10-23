using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static (NativeBattleEffect298Frame Frame, uint Next) BirthFragments6710E0(NativeBattleEffect298Frame frame, uint script, uint model, uint offsets, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        List<int> selected = [];
        while (true)
        {
            short bone = ScriptWord298(frame, resources, script);
            script += 2;
            if (bone == -1)
            {
                break;
            }
            selected.Add(bone);
        }
        short[] parameters = Enumerable.Range(0, 4).Select(index => ScriptWord298(frame, resources, script + (uint)(index * 2))).ToArray();
        (frame, uint temporary) = Allocate(frame, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect298ApplyOffsetPose662C00(model, offsets));
        NativeBattleEffect298BodySource body = inputs.ReadBody(frame, model);
        NativeBattleGraphicsModel actor = inputs.ReadModel(frame, model);
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Effect298 fragments require their actual model world matrix.");
        ImmutableArray<NativeBattlePackedMatrix32>.Builder matrices = body.CurrentBones.ToBuilder();
        for (int bone = 0; bone < matrices.Count; bone++)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, matrices[bone].ReadMatrix());
            NativeBattleEffectArena row = ReadBytes(frame, checked(offsets + (uint)(bone * 16)), 16);
            if (BinaryPrimitives.ReadInt16LittleEndian(row.Read(14, 2)) != 0)
            {
                NativeBattleActionMatrix scale = NativeBattleGfMatrices.Euler56CD50(new(BinaryPrimitives.ReadInt16LittleEndian(row.Read(8, 2)), BinaryPrimitives.ReadInt16LittleEndian(row.Read(10, 2)), BinaryPrimitives.ReadInt16LittleEndian(row.Read(12, 2))));
                matrix = NativeBattleGfMatrices.Multiply56C090(scale, matrix) with { X = matrix.X, Y = matrix.Y, Z = matrix.Z };
            }
            matrix = matrix with { X = unchecked(matrix.X + BinaryPrimitives.ReadInt16LittleEndian(row.Read(0, 2))), Y = unchecked(matrix.Y + BinaryPrimitives.ReadInt16LittleEndian(row.Read(2, 2))), Z = unchecked(matrix.Z + BinaryPrimitives.ReadInt16LittleEndian(row.Read(4, 2))) };
            matrices[bone] = NativeBattlePackedMatrix32.FromMatrix(matrix, 0);
        }
        foreach (int bone in selected)
        {
            if ((uint)bone >= (uint)matrices.Count)
            {
                throw new InvalidDataException("Effect298 selected fragment bone leaves its actual current rig.");
            }
            matrices[bone] = matrices[bone] with { Bytes = matrices[bone].Bytes.SetItem(18, 1).SetItem(19, 0), Known = matrices[bone].Known.SetItem(18, true).SetItem(19, true) };
        }
        uint scratchAddress = body.VertexScratchAddress ?? throw new NotSupportedException("Effect298 fragment selection requires its actual current CPU vertex scratch address.");
        int scratchLength = body.VertexScratchLength ?? throw new NotSupportedException("Effect298 fragment selection requires its actual current CPU vertex scratch capacity.");
        NativeBattleEffectArena scratch = Context(frame, scratchAddress, scratchLength);
        NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(Context(frame, temporary, 180), 32, NativeBattleEffectArena.Clear(), 0, 148);
        uint main = frame.State.ReadDword(0x2507900) + 0x1794;
        context = context.Write(4, BitConverter.GetBytes(scratchAddress)).Write(0xC, BitConverter.GetBytes(actor.Color40 ?? throw new NotSupportedException("Effect298 fragments require their actual model color DWORD."))).Write(0x10, BitConverter.GetBytes((actor.SequenceState ?? throw new NotSupportedException("Effect298 fragments require the current mesh mask.")).MeshMask124)).Write(0x14, BitConverter.GetBytes((int)parameters[0])).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes((int)parameters[1])).Write(0x20, BitConverter.GetBytes((int)parameters[2])).Write(0x24, BitConverter.GetBytes((int)parameters[3])).Write(0x30, BitConverter.GetBytes(main));
        int objects = ReadInt(body.GeometryAddress);
        for (int index = 0; index < objects; index++)
        {
            if ((Dword298(context, 0x10) & (1u << (index & 31))) == 0)
            {
                continue;
            }
            uint cursor = checked(body.GeometryAddress + (uint)ReadInt(body.GeometryAddress + 4 + (uint)(index * 4)));
            while (true)
            {
                int groups = ReadWord(cursor);
                cursor += 2;
                int vertices = 0;
                bool any = false;
                for (int group = 0; group < groups; group++)
                {
                    int bone = ReadWord(cursor);
                    int count = ReadWord(cursor + 2);
                    cursor += 4;
                    if ((uint)bone >= (uint)matrices.Count || count < 0)
                    {
                        throw new InvalidDataException("Effect298 fragment group leaves its current rig or vertex source.");
                    }
                    bool active = BinaryPrimitives.ReadInt16LittleEndian(matrices[bone].Bytes.AsSpan().Slice(18, 2)) != 0;
                    any |= active;
                    if (active)
                    {
                        frame = LoadMatrix(frame, matrices[bone]);
                    }
                    for (int vertex = 0; vertex < count; vertex++)
                    {
                        int destination = checked(vertices * 8);
                        if (destination > scratchLength - 8)
                        {
                            throw new InvalidDataException("Effect298 fragment vertices leave their actual current scratch.");
                        }
                        if (active)
                        {
                            NativeBattleGeometryRegisterStep registers = Registers298(frame);
                            NativeBattleEffectArena xyz = body.ReadGeometry(cursor, 6);
                            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, Vector298(xyz), 0));
                            for (int axis = 0; axis < 3; axis++)
                            {
                                scratch = scratch.Write(destination + axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
                            }
                            scratch = scratch.Write(destination + 6, new byte[2]);
                            frame = Publish298(frame, registers);
                        }
                        else
                        {
                            scratch = scratch.Write(destination + 6, BitConverter.GetBytes((short)1));
                        }
                        vertices++;
                        cursor += 6;
                    }
                }
                if (!any)
                {
                    break;
                }
                cursor = (cursor + 3) & ~3u;
                int triangles = ReadWord(cursor);
                int quads = ReadWord(cursor + 2);
                cursor += 12;
                uint polygonStart = cursor;
                for (int kind = 0; kind < 2; kind++)
                {
                    int count = kind == 0 ? triangles : quads;
                    for (int face = 0; face < count; face++)
                    {
                        byte[] polygon = body.ReadGeometry(cursor, kind == 0 ? 16 : 20).Read(0, kind == 0 ? 16 : 20);
                        cursor += kind == 0 ? 16u : 20u;
                        NativeBattleEffectArena[] points = new NativeBattleEffectArena[kind == 0 ? 3 : 4];
                        bool admitted = true;
                        for (int corner = 0; corner < points.Length; corner++)
                        {
                            int at = BinaryPrimitives.ReadUInt16LittleEndian(polygon.AsSpan(corner * 2)) & 0xFFF;
                            if (at >= vertices)
                            {
                                throw new InvalidDataException("Effect298 fragment polygon leaves its current model-space bank.");
                            }
                            points[corner] = NativeBattleEffectNodeContextMemory.Copy(scratch, at * 8, NativeBattleEffectArena.Clear(), 0, 8);
                            admitted &= Word298(points[corner], 6) == 0;
                            context = NativeBattleEffectNodeContextMemory.Copy(points[corner], 0, context, 0x34 + corner * 8, 8);
                        }
                        if (!admitted)
                        {
                            continue;
                        }
                        int materialRemaining = Int298(context, 0x18);
                        if (materialRemaining == 0)
                        {
                            frame = ProcessRandom298(frame, out int sample);
                            if (sample > Int298(context, 0x24))
                            {
                                continue;
                            }
                            int remaining = Int298(context, 0x14);
                            context = context.Write(0x14, BitConverter.GetBytes(unchecked(remaining - 1)));
                            if (remaining <= 0)
                            {
                                goto Completed;
                            }
                            (frame, uint chunk) = AllocateChunk298(frame, main, resources);
                            if (chunk == 0)
                            {
                                context = context.Write(0x14, new byte[4]);
                                goto Completed;
                            }
                            context = context.Write(0x18, BitConverter.GetBytes(12)).Write(0x28, BitConverter.GetBytes(chunk)).Write(0x2C, BitConverter.GetBytes(chunk + 0x1C));
                            (frame, context) = PrepareChunk6716C0(frame, context, chunk, resources);
                            materialRemaining = 12;
                        }
                        uint material = Dword298(context, 0x2C);
                        NativeBattleEffectArena uv = NativeBattleEffectArena.Clear().Write(0, polygon.AsSpan(8, 8)).Write(8, polygon.AsSpan(kind == 0 ? 6 : 16, 2));
                        frame = WriteBytes(frame, material, uv, 10);
                        context = context.Write(0x18, BitConverter.GetBytes(materialRemaining - 1)).Write(0x2C, BitConverter.GetBytes(material + 12));
                    }
                }
                if (Int298(context, 0x14) <= 0)
                {
                    goto Completed;
                }
                cursor = polygonStart;
            }
        }
    Completed:
        frame = PublishContext(frame, scratchAddress, scratch, scratchLength);
        NativeBattleEffectArena whole = NativeBattleEffectNodeContextMemory.Copy(context, 0, Context(frame, temporary, 180), 32, 148);
        frame = PublishContext(frame, temporary, whole, 180);
        frame = inputs.Execute(frame, new NativeBattleEffect298RestorePose508C90(model));
        if (Int298(context, 0x18) > 0)
        {
            uint last = Dword298(context, 0x28);
            frame = WriteBytes(frame, last + 6, NativeBattleEffectArena.Clear().Write(0, new byte[2]), 2);
            frame = WriteBytes(frame, main + 0x89EC, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(last)), 4);
        }
        return (Release(frame, 180), script + 8);

        int ReadInt(uint address) => BinaryPrimitives.ReadInt32LittleEndian(body.ReadGeometry(address, 4).Read(0, 4));
        short ReadWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(body.ReadGeometry(address, 2).Read(0, 2));
    }

    private static (NativeBattleEffect298Frame Frame, uint Address) AllocateChunk298(NativeBattleEffect298Frame frame, uint main, NativeBattleEffect298Resources resources)
    {
        uint address = Dword298(ReadBytes(frame, main + 0x89EC, 4), 0);
        if (Word298(ReadBytes(frame, address + 6, 2), 0) != 0)
        {
            address = main + 0x2EC;
            int remaining = 192;
            while (Word298(ReadBytes(frame, address + 6, 2), 0) != 0)
            {
                address += 172;
                remaining--;
                if (remaining == 0)
                {
                    return (frame, 0);
                }
            }
        }
        uint next = address >= main + 0x8340 ? main + 0x2EC : address + 172;
        frame = WriteBytes(frame, main + 0x89EC, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(next)), 4);
        frame = RandomLocal298(frame, out int sample);
        uint mesh = resources.DebrisMeshes[unchecked(sample * 3) >> 15].Address;
        return (WriteBytes(frame, address + 24, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(mesh)), 4), address);
    }

    private static (NativeBattleEffect298Frame Frame, uint Address) AllocateSmoke298(NativeBattleEffect298Frame frame, uint main)
    {
        uint address = Dword298(ReadBytes(frame, main + 0x89F0, 4), 0);
        if (Word298(ReadBytes(frame, address + 6, 2), 0) != 0)
        {
            address = main + 0x83EC;
            int remaining = 128;
            while (Word298(ReadBytes(frame, address + 6, 2), 0) != 0)
            {
                address += 12;
                remaining--;
                if (remaining == 0)
                {
                    return (frame, 0);
                }
            }
        }
        uint next = address >= main + 0x89E0 ? main + 0x83EC : address + 12;
        return (WriteBytes(frame, main + 0x89F0, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(next)), 4), address);
    }

    private static (NativeBattleEffect298Frame Frame, NativeBattleEffectArena Context) PrepareChunk6716C0(NativeBattleEffect298Frame frame, NativeBattleEffectArena context, uint address, NativeBattleEffect298Resources resources)
    {
        NativeBattleEffectArena chunk = NativeBattleEffectNodeContextMemory.Copy(context, 0x34, ReadBytes(frame, address, 172), 0, 8);
        frame = ProcessRandom298(frame, out int lifetime);
        chunk = chunk.Write(6, BitConverter.GetBytes(unchecked((short)((lifetime >> 12) + 16))));
        int[] a = new int[3];
        int[] b = new int[3];
        for (int axis = 0; axis < 3; axis++)
        {
            a[axis] = Word298(context, 0x34 + axis * 2) - Word298(context, 0x3C + axis * 2);
            b[axis] = Word298(context, 0x44 + axis * 2) - Word298(context, 0x34 + axis * 2);
            context = context.Write(0x64 + axis * 4, BitConverter.GetBytes(a[axis])).Write(0x74 + axis * 4, BitConverter.GetBytes(b[axis]));
        }
        NativeBattleGeometryRegisterStep registers = Registers298(frame);
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
        int[] cross = registers.Colors.Mac.Select(value => value ?? throw new NotSupportedException("Effect298 chunk normal requires its current OP result.")).ToArray();
        int maximum = cross.Select(value => value < 0 ? unchecked(-value) : value).Max();
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)maximum)) };
        int leading = checked((int)registers.Geometry.Lzcr!.Value);
        for (int index = 0; index < 4; index++)
        {
            frame = RandomLocal298(frame, out int value);
            chunk = chunk.Write(16 + index * 2, BitConverter.GetBytes(unchecked((short)(index < 2 ? value : (value >> 5) - 512))));
        }
        for (int axis = 0; axis < 3; axis++)
        {
            if (leading < 18)
            {
                cross[axis] >>= 18 - leading;
            }
            context = context.Write(0x84 + axis * 4, BitConverter.GetBytes(cross[axis]));
        }
        registers = registers with { Colors = registers.Colors with { Ir = cross.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int sum = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)sum)) };
        int even = checked((int)registers.Geometry.Lzcr!.Value) & ~1;
        int lookup = even <= 24 ? sum >> (24 - even) : unchecked(sum << (even - 24));
        int length = unchecked(resources.ReadNormalizationWord(lookup) << ((31 - even) >> 1)) >> 12;
        for (int axis = 0; axis < 3; axis++)
        {
            int value = unchecked(Int298(context, 0x1C) * cross[axis]) / length;
            chunk = chunk.Write(8 + axis * 2, BitConverter.GetBytes(unchecked((short)value)));
        }
        frame = Publish298(frame, registers);
        (frame, uint smoke) = AllocateSmoke298(frame, Dword298(context, 0x30));
        if (smoke != 0)
        {
            NativeBattleEffectArena row = NativeBattleEffectNodeContextMemory.Copy(context, 0x34, ReadBytes(frame, smoke, 12), 0, 8);
            frame = RandomLocal298(frame, out int type);
            bool reduced = type > 12000;
            row = row.Write(6, BitConverter.GetBytes((short)(reduced ? 12 : 11))).Write(11, [(byte)(reduced ? 0 : 1)]);
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = Word298(chunk, 8 + axis * 2);
                row = row.Write(8 + axis, [unchecked((byte)(reduced ? velocity >> 2 : velocity))]);
            }
            frame = WriteBytes(frame, smoke, row, 12);
        }
        chunk = chunk.Write(10, BitConverter.GetBytes(unchecked((short)(Word298(chunk, 10) - (Word298(context, 0x1C) << 1)))));
        frame = ProcessRandom298(frame, out int size);
        int baseSize = Int298(context, 0x20);
        chunk = chunk.Write(14, BitConverter.GetBytes(unchecked((short)((unchecked(size * baseSize) >> 15) + baseSize))));
        return (WriteBytes(frame, address, chunk, 172), context);
    }

    private static NativeBattleEffect298Frame ProcessRandom298(NativeBattleEffect298Frame frame, out int value)
    {
        NativeBattleSharedEffectRandomState current = frame.Memory.SharedEffectRandom ?? throw new NotSupportedException("Effect298 requires its process random owner.");
        current = current.Draw7059E0(out ushort drawn);
        value = drawn;
        return frame with { Memory = frame.Memory with { SharedEffectRandom = current } };
    }

    private static NativeBattleGeometryRegisterStep Registers298(NativeBattleEffect298Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect298 requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect298 requires its current arithmetic registers."));
    private static NativeBattleEffect298Frame Publish298(NativeBattleEffect298Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static short Word298(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword298(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static int Int298(NativeBattleEffectArena arena, int offset) => unchecked((int)Dword298(arena, offset));
    private static NativeBattleVector Vector298(NativeBattleEffectArena arena) => new(Word298(arena, 0), Word298(arena, 2), Word298(arena, 4));
}
