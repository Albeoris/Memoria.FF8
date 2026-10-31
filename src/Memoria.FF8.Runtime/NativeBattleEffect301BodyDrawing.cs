using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect301Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect301Lower;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect301BodyStep(NativeBattleEffect301Frame Frame, NativeBattleEffectArena Context136, NativeBattleGfGeometryBatch Batch);

public static class NativeBattleEffect301BodyDrawing
{
    public static NativeBattleEffect301BodyStep DrawLoaded(NativeBattleEffect301Frame frame, Func<int, int, byte[]> readGeometry, uint geometrySourceAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattleEffectArena context136, uint temporaryAddress, int ordinaryByteCursor, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs)
    {
        return DrawLoaded(frame, readGeometry, geometrySourceAddress, bones, context136, temporaryAddress, ordinaryByteCursor, resources, inputs, null);
    }

    public static NativeBattleEffect301BodyStep DrawLoaded(NativeBattleEffect301Frame frame, Func<int, int, byte[]> readGeometry, uint geometrySourceAddress, ImmutableArray<NativeBattlePackedMatrix32> bones, NativeBattleEffectArena context136, uint temporaryAddress, int ordinaryByteCursor, NativeBattleEffect301Resources resources, NativeBattleEffect301Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        uint scratch = Dword301(context136, 4);
        if (scratch != checked(Main301(frame) + 0x1494) || ordinaryByteCursor < 0)
        {
            throw new InvalidDataException("The body requires its current physical cache and packet cursor.");
        }
        NativeBattleEffectArena context = context136;
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("The body requires its current projection controls.");
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int objects = I32(0);
        for (int obj = 0; obj < Math.Max(objects, 0); obj++)
        {
            int cursor = I32(checked(4 + obj * 4));
            context = Put301(context, 0x78, checked(geometrySourceAddress + (uint)cursor));
            if ((Dword301(context, 24) & (1u << (obj & 31))) == 0)
            {
                continue;
            }
            int chunks = I16(cursor);
            cursor = checked(cursor + 2);
            int total = 0;
            Dictionary<int, System.Numerics.Vector3>? worlds = pilotIdentity is null ? null : new();
            context = Put301(context, 0x84, scratch);
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                int bone = I16(cursor);
                cursor = checked(cursor + 2);
                if ((uint)bone >= (uint)bones.Length)
                {
                    throw new InvalidDataException("The body requires its actual current composed bone.");
                }
                NativeBattlePackedMatrix32 matrix = bones[bone];
                registers = LoadMatrix(registers, matrix);
                int count = I16(cursor);
                cursor = checked(cursor + 2);
                if (count < 0)
                {
                    throw new InvalidDataException("The body leaves its bounded current cache.");
                }
                int first = total;
                for (int index = 0; index < count; index++)
                {
                    byte[] input = readGeometry(cursor, 6);
                    registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(input), BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(input.AsSpan(4))), 0);
                    registers = NativeBattleGeometry.Transform(registers);
                    NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear();
                    for (int axis = 0; axis < 3; axis++)
                    {
                        xyz = PutWord301(xyz, axis * 2, registers.Colors.Ir[axis]!.Value);
                    }
                    frame = WriteBytes(frame, checked(scratch + (uint)(total * 16 + 8)), xyz, 6);
                    total++;
                    cursor = checked(cursor + 6);
                }
                registers = LoadMatrix(registers, view);
                for (int index = first; index < total; index++)
                {
                    uint address = checked(scratch + (uint)(index * 16));
                    NativeBattleEffectArena row = ReadBytes(frame, address, 16);
                    (byte[] bytes, bool[] known) = row.ReadSnapshot(8, 8);
                    NativeBattleVector point = new(Word301(row, 8), Word301(row, 10), Word301(row, 12));
                    short? w = known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : null;
                    if (worlds is not null)
                    {
                        worlds[index] = NativeBattleEffect32Presentation.World(view.ReadMatrix(), point, projection);
                    }
                    registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
                    registers = registers with { Colors = registers.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } };
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    uint screen = registers.Geometry.Sxy[2]!.Value;
                    uint depth = registers.Geometry.Sz[3]!.Value >> 2;
                    context = Put301(Put301(context, 0x1C, screen), 0x20, depth);
                    if (unchecked((ushort)depth) == 0)
                    {
                        row = row.Write(6, [0x10]);
                    }
                    else
                    {
                        byte classification = point.Y > unchecked((int)Dword301(context, 0x6C)) ? (byte)2 : (byte)1;
                        context = context.Write(0x23, [classification]);
                        int x = unchecked((short)screen);
                        int y = unchecked((short)(screen >> 16));
                        byte outcodes = context.Read(0x22, 1)[0];
                        if (x < (Word301(context, 12) << 3))
                        {
                            outcodes |= 1;
                        }
                        else if (x >= (Word301(context, 16) << 3))
                        {
                            outcodes |= 2;
                        }
                        if (y < (Word301(context, 14) << 3))
                        {
                            outcodes |= 4;
                        }
                        else if (y >= (Word301(context, 18) << 3))
                        {
                            outcodes |= 8;
                        }
                        context = context.Write(0x22, [outcodes]);
                        row = CopyBytes(context, 0x1C, row, 0, 8);
                    }
                    frame = WriteBytes(frame, address, row, 16);
                }
                context = Put301(Put301(context, 0x78, checked(geometrySourceAddress + (uint)cursor)), 0x84, checked(scratch + (uint)(total * 16)));
            }
            cursor = checked((cursor + 3) & ~3);
            int triangles = U16(cursor);
            int quads = U16(cursor + 2);
            if (U16(cursor + 4) != 0 || U16(cursor + 6) != 0 || triangles + quads > 2400)
            {
                throw new NotSupportedException("The current body requires its selected triangle and quad formats.");
            }
            context = PutWord301(PutWord301(context, 8, triangles), 10, quads);
            cursor = checked(cursor + 12);
            context = Put301(Put301(context, 0x78, checked(geometrySourceAddress + (uint)cursor)), 0, checked(geometrySourceAddress + (uint)cursor));
            DrawFaces(false, triangles);
            DrawFaces(true, quads);

            void DrawFaces(bool quad, int count)
            {
                int countOffset = quad ? 10 : 8;
                context = PutWord301(context, countOffset, count - 1);
                for (int face = 0; face < count; face++)
                {
                    int remaining = count - face - 1;
                    byte[] source = readGeometry(cursor, quad ? 20 : 16);
                    int corners = quad ? 4 : 3;
                    NativeBattleEffectArena[] rows = new NativeBattleEffectArena[corners];
                    byte codesOr = 0;
                    byte codesAnd = 0xFF;
                    byte classAnd = 0xFF;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
                        if (index >= total)
                        {
                            throw new InvalidDataException("The body face leaves its current projected cache.");
                        }
                        rows[corner] = ReadBytes(frame, checked(scratch + (uint)(index * 16)), 16);
                        context = CopyBytes(rows[corner], 0, context, 0x1C + corner * 16, 16);
                        byte code = rows[corner].Read(6, 1)[0];
                        codesOr |= code;
                        codesAnd &= code;
                    }
                    bool rejected = codesOr >= 0x10 || codesAnd != 0;
                    if (!rejected)
                    {
                        for (int corner = 0; corner < corners; corner++)
                        {
                            classAnd &= rows[corner].Read(7, 1)[0];
                        }
                        rejected = classAnd == 2;
                    }
                    byte[] packet = new byte[quad ? 40 : 32];
                    NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[packet.Length];
                    NativeBattleGeometryPrimitive? primitive = null;
                    if (!rejected)
                    {
                        int identity = remaining + (quad ? triangles : 0);
                        if (classAnd == 0 && ((Dword301(context, 0x70) + (uint)remaining) & 7) == 0 && Word301(rows[0], 12) > unchecked((int)Dword301(context, 0x74)))
                        {
                            int x = rows.Sum(row => (int)Word301(row, 8)) / corners;
                            int z = rows.Sum(row => (int)Word301(row, 12)) / corners;
                            frame = NativeBattleEffect301Records.Birth66E930(frame, x, z, identity);
                        }
                        registers = registers with { Geometry = registers.Geometry with { Sxy = rows.Take(3).Select(row => (uint?)Dword301(row, 0)).ToImmutableArray() } };
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        uint area = registers.Geometry.Mac0!.Value;
                        context = Put301(context, 0x5C, area);
                        if (unchecked((int)area) >= 0)
                        {
                            ImmutableArray<uint?> depths = quad ? rows.Select(row => (uint?)unchecked((ushort)Word301(row, 4))).ToImmutableArray() : [registers.Geometry.Sz[0], unchecked((ushort)Word301(rows[0], 4)), unchecked((ushort)Word301(rows[1], 4)), unchecked((ushort)Word301(rows[2], 4))];
                            registers = registers with { Geometry = registers.Geometry with { Sz = depths } };
                            registers = quad ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("The body requires current ZSF4.")) : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("The body requires current ZSF3."));
                            uint depth = registers.Geometry.Otz!.Value;
                            context = Put301(context, 0x64, depth);
                            uint opcode = (quad ? 0x2C000000u : 0x24000000u) | ((uint)(BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(14)) & 0x200) << 16);
                            Store32(0, quad ? 0x09000000u : 0x07000000u);
                            Store32(4, opcode | Dword301(context, 20));
                            for (int corner = 0; corner < corners; corner++)
                            {
                                Store32(8 + corner * 8, Dword301(rows[corner], 0));
                            }
                            Store32(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
                            Store32(20, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12)) & 0xFDFFFFFF);
                            Store16(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(quad ? 16 : 6)));
                            if (quad)
                            {
                                Store16(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(18)));
                            }
                            int bucket = checked(17 + (unchecked((int)depth) >> (int)(Dword301(context, 0x80) & 31)));
                            ImmutableArray<int> payload = quad ? registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray() : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
                            ImmutableArray<uint> words = Enumerable.Range(0, packet.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(index * 4))).ToImmutableArray();
                            primitive = new(bucket, words, payload);
                            if (worlds is not null)
                            {
                                System.Numerics.Vector3[] accepted = Enumerable.Range(0, corners).Select(corner => worlds[BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF]).ToArray();
                                primitive = NativeBattleEffect32Presentation.Capture(primitive, NativeBattleEffect32Presentation.Resource(pilotIdentity, geometrySourceAddress, cursor), accepted);
                            }
                        }
                    }
                    attempts.Add(new(allocated, packet.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                    if (primitive is not null)
                    {
                        allocated = checked(allocated + packet.Length);
                    }
                    cursor = checked(cursor + (quad ? 20 : 16));
                    context = PutWord301(context, countOffset, remaining - 1);
                    void Store32(int offset, uint value)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(offset), value);
                        Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
                    }
                    void Store16(int offset, ushort value)
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(offset), value);
                        Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
                    }
                }
            }
        }
        frame = Publish(frame, registers);
        return new(frame, context, new(attempts.ToImmutable(), allocated));
        int I32(int offset) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(offset, 4));
        int I16(int offset) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(offset, 2));
        int U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(readGeometry(offset, 2));
    }

    private static NativeBattleGeometryRegisterStep LoadMatrix(NativeBattleGeometryRegisterStep registers, NativeBattlePackedMatrix32 packed)
    {
        short? padding = packed.Known[18] && packed.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(packed.Bytes.AsSpan().Slice(18)) : null;
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, packed.ReadMatrix(), true, true, padding);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() };
        return registers with { Geometry = geometry };
    }
}
