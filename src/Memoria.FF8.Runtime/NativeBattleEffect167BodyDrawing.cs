using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect167BodyStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary464, NativeBattleEffectArena Scratch, NativeBattleGfGeometryBatch Batch);
public sealed record NativeBattleEffect167DrawBody464(uint TemporaryAddress, int Actor, NativeBattlePackedMatrix32 OffsetWorld) : NativeBattleEffect167Operation;

public static class NativeBattleEffect167BodyDrawing
{
    public static NativeBattleEffect167BodyStep Draw(NativeBattleEffectMemory memory, NativeBattleEffectArena temporary464, uint temporaryAddress, Func<uint, int, NativeBattleEffectArena> readGeometry, uint geometryAddress, ImmutableArray<NativeBattlePackedMatrix32> currentCpuBones, NativeBattleEffectArena scratch, int scratchLength, NativeBattleCameraProjection projection, ImmutableArray<uint> pointerMapping, int packetCursor, int tableBaseBucket = 17, int depthShift = 2, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (pointerMapping.IsDefault || pointerMapping.Length != 56 || pointerMapping.Any(value => value > 5) || scratchLength < 0 || scratchLength > NativeBattleEffectArena.Length || (scratchLength & 7) != 0)
        {
            throw new InvalidDataException("Body drawing requires its bounded current scratch and clipping pointer table.");
        }
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? throw new NotSupportedException("Body drawing requires current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Body drawing requires current arithmetic registers."));
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics ?? throw new NotSupportedException("Body drawing requires current projection controls.");
        NativeBattleScriptedLightingMemory lighting = memory.ScriptedLighting ?? throw new NotSupportedException("Body drawing requires current plane controls.");
        NativeBattleScriptedLightingMatrix light = lighting.LightMatrix ?? new(new byte[20].ToImmutableArray(), new bool[20].ToImmutableArray());
        byte[] plane = temporary464.Read(0xA0, 8);
        byte[] lightBytes = light.Bytes.ToArray();
        bool[] lightKnown = light.Known.ToArray();
        Array.Copy(plane, lightBytes, 8);
        Array.Fill(lightKnown, true, 0, 8);
        light = new(lightBytes.ToImmutableArray(), lightKnown.ToImmutableArray());
        lighting = lighting with { LightMatrix = light, BackColor = (lighting.BackColor ?? NativeBattleScriptedBackColor.Unknown()).WriteLane(0, Signed(temporary464, 0xA6)) };
        memory = memory with { ScriptedLighting = lighting };
        for (int index = 0; index < 56; index++)
        {
            temporary464 = Put(temporary464, 0xB0 + index * 4, checked(temporaryAddress + 0x40u + pointerMapping[index] * 16));
        }
        temporary464 = Put(temporary464.MarkUnknown(0x24, 4).MarkUnknown(0x2C, 4), 0x28, unchecked((uint)depthShift));
        ArgumentOutOfRangeException.ThrowIfNegative(packetCursor);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        int pilotObject = 0;
        uint pilotFace = 0;
        int pilotHalf = 0;
        int pilotMatrixRole = 0x190;
        System.Numerics.Vector3[]? pilotWorld = pilotIdentity.HasValue ? new System.Numerics.Vector3[4] : null;
        int objects = Integer(geometryAddress);
        if (objects < 0)
        {
            throw new InvalidDataException("Body geometry requires a current object directory.");
        }
        for (int item = 0; item < objects; item++)
        {
            uint cursor = checked(geometryAddress + unchecked((uint)Integer(geometryAddress + 4u + (uint)item * 4)));
            if ((Dword(temporary464, 0x10) & (1u << (item & 31))) == 0)
            {
                continue;
            }
            pilotObject = item;
            int output = 0;
            int groups = Short(cursor);
            cursor += 2;
            for (int group = 0; group < groups; group++)
            {
                int bone = Short(cursor);
                int count = Short(cursor + 2);
                cursor += 4;
                if ((uint)bone >= (uint)currentCpuBones.Length || count < 0 || output > scratchLength - count * 8)
                {
                    throw new InvalidDataException("Body vertices leave their current bone or scratch owner.");
                }
                registers = Matrix(registers, currentCpuBones[bone], true);
                for (int vertex = 0; vertex < count; vertex++)
                {
                    registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(readGeometry(cursor, 6), 0), 0);
                    cursor += 6;
                    registers = NativeBattleGeometry.Transform(registers);
                    NativeBattleVector transformed = new(unchecked((short)Required(registers.Colors.Ir[0])), unchecked((short)Required(registers.Colors.Ir[1])), unchecked((short)Required(registers.Colors.Ir[2])));
                    scratch = Store(scratch, output, transformed);
                    output += 8;
                }
            }
            cursor = (cursor + 3) & ~3u;
            for (int format = 0; format < 4; format++)
            {
                temporary464 = temporary464.Write(8 + format * 2, BitConverter.GetBytes(Unsigned(cursor + (uint)format * 2)));
            }
            cursor += 12;
            temporary464 = Put(temporary464, 0, cursor);
            if (Word(temporary464, 0xC) != 0 || Word(temporary464, 0xE) != 0)
            {
                throw new NotSupportedException("Body drawing requires its admitted textured source formats.");
            }
            registers = Matrix(registers, Packed(temporary464, 0x190), true);
            pilotMatrixRole = 0x190;
            for (int format = 0; format < 2; format++)
            {
                bool quad = format == 1;
                int count = Word(temporary464, 8 + format * 2);
                for (int face = 0; face < count; face++)
                {
                    pilotFace = cursor;
                    pilotHalf = 0;
                    NativeBattleEffectArena source = readGeometry(cursor, quad ? 20 : 16);
                    CopyCorner(source, 0, 0);
                    CopyCorner(source, 2, 1);
                    CopyCorner(source, 4, 2);
                    Project(0x40, 0x50, 0x60);
                    temporary464 = Put(Put(temporary464, 0x1C, Dword(source, 8)), 0x20, Dword(source, 12));
                    temporary464 = UV(temporary464, 0x40, Word(source, 8));
                    temporary464 = UV(temporary464, 0x50, Word(source, 12));
                    temporary464 = UV(temporary464, 0x60, Word(source, quad ? 16 : 6));
                    uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Body drawing requires current projection flags.");
                    temporary464 = Put(temporary464, 0x3C, flags);
                    if ((flags & 0x60000) == 0)
                    {
                        registers = NativeBattleGeometry.Clip45EE10(registers);
                        temporary464 = Put(temporary464, 0x30, Required(registers.Geometry.Mac0));
                        if (unchecked((int)Required(registers.Geometry.Mac0)) > 0)
                        {
                            Classify();
                            if (quad)
                            {
                                pilotHalf = 1;
                                CopyCorner(source, 6, 0);
                                CopyCorner(source, 2, 1);
                                CopyCorner(source, 4, 2);
                                Project(0x40, 0x50, 0x60);
                                temporary464 = UV(temporary464, 0x40, Word(source, 18));
                                temporary464 = UV(temporary464, 0x50, Word(source, 12));
                                temporary464 = UV(temporary464, 0x60, Word(source, 16));
                                temporary464 = Put(temporary464, 0x3C, registers.Colors.Flags ?? throw new NotSupportedException("Body drawing requires current projection flags."));
                                Classify();
                            }
                        }
                    }
                    cursor += quad ? 20u : 16u;
                }
            }
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, temporary464, scratch, new(attempts.ToImmutable(), allocated));

        void CopyCorner(NativeBattleEffectArena source, int indexOffset, int slot)
        {
            int selected = Word(source, indexOffset) & 0xFFF;
            int offset = selected * 8;
            if (offset > scratchLength - 8)
            {
                throw new InvalidDataException("Body face leaves its current eight-byte scratch rows.");
            }
            temporary464 = NativeBattleEffectNodeContextMemory.Copy(scratch, offset, temporary464, 0x40 + slot * 16, 8);
        }

        void Project(int first, int second, int third)
        {
            NativeBattleActionMatrix? pilotMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
            int[] offsets = [first, second, third];
            for (int slot = 0; slot < 3; slot++)
            {
                int corner = slot;
                registers = LoadPoint(registers, slot, temporary464, offsets[slot], pilotWorld is not null && pilotMatrix is not null ? point => pilotWorld[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix, point), projection) : null);
            }
            registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
        }

        void Classify()
        {
            int[] distances = new int[3];
            int state = 0;
            for (int slot = 0; slot < 3; slot++)
            {
                memory = NativeBattleLightBackTransform.Transform(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, slot);
                registers = new(memory.GeometryRegisters!, memory.ScriptedColorRegisters!);
                distances[slot] = Required(registers.Colors.Ir[0]);
                temporary464 = Put(temporary464, 0x7C + slot * 16, unchecked((uint)distances[slot]));
            }
            for (int edge = 0; edge < 3; edge++)
            {
                int bit = distances[edge] != 0 && (distances[edge] ^ distances[(edge + 1) % 3]) < 0 ? 1 << edge : 0;
                temporary464 = Put(temporary464, 0x78 + edge * 16, (uint)bit);
                state |= bit;
            }
            temporary464 = Put(temporary464, 0x14, (uint)state);
            if (state == 0)
            {
                if ((distances[0] | distances[1] | distances[2]) > 0)
                {
                    Translation(0x1B0);
                    Packet([0x40, 0x50, 0x60], true);
                    Translation(0x190);
                }
                else
                {
                    Packet([0x40, 0x50, 0x60], false);
                }
                return;
            }
            for (int edge = 0; edge < 3; edge++)
            {
                if ((state & (1 << edge)) != 0)
                {
                    temporary464 = Intersect(temporary464, 0x40 + edge * 16, 0x40 + ((edge + 1) % 3) * 16, 0xA0, 0x70 + edge * 16);
                }
            }
            if (state is >= 1 and <= 6)
            {
                int pointerStart = 0xB0 + state * 32;
                int[] row = Enumerable.Range(0, 8).Select(index => checked((int)(Dword(temporary464, pointerStart + index * 4) - temporaryAddress))).ToArray();
                bool positive = unchecked((int)Dword(temporary464, row[3] + 12)) > 0;
                int[] triangle = [row[0], row[1], row[2]];
                int[] remainder = state is 1 or 2 or 4 ? [row[4], row[5], row[6]] : [row[4], row[5], row[6], row[7]];
                Packet(positive ? remainder : triangle, true);
                Translation(0x1B0);
                Packet(positive ? triangle : remainder, true);
            }
            Translation(0x190);
        }

        void Translation(int offset)
        {
            registers = Matrix(registers, Packed(temporary464, offset), false);
            pilotMatrixRole = offset;
        }

        void Packet(int[] rows, bool project)
        {
            bool quad = rows.Length == 4;
            if (project)
            {
                Project(rows[0], rows[1], rows[2]);
            }
            SkinWrites packet = new(quad ? 40 : 32, allocated);
            packet.Word(0, quad ? 0x09000000u : 0x07000000u);
            packet.Word(4, Dword(temporary464, 0x18) | (Dword(temporary464, 0x20) & 0x02000000) | (quad ? 0x2C000000u : 0x24000000u));
            packet.Word(12, Dword(temporary464, 0x1C));
            packet.Word(20, Dword(temporary464, 0x20) & 0x01FFFFFF);
            for (int corner = 0; corner < 3; corner++)
            {
                packet.Word(8 + corner * 8, Required(registers.Geometry.Sxy[corner]));
                packet.Word16(12 + corner * 8, Word(temporary464, rows[corner] + 6));
            }
            if (quad)
            {
                NativeBattleActionMatrix? pilotMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
                registers = LoadPoint(registers, 0, temporary464, rows[3], pilotWorld is not null && pilotMatrix is not null ? point => pilotWorld[3] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix, point), projection) : null);
                registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                packet.Word(32, Required(registers.Geometry.Sxy[2]));
                packet.Word16(36, Word(temporary464, rows[3] + 6));
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Body quads require current ZSF4."));
            }
            else
            {
                registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Body triangles require current ZSF3."));
            }
            uint depth = Required(registers.Geometry.Otz);
            temporary464 = Put(temporary464, 0x38, depth);
            ImmutableArray<int> sidecars = registers.Geometry.Sz.Select(value => unchecked((int)Required(value))).ToImmutableArray();
            NativeBattleGeometryPrimitive primitive = new(unchecked(tableBaseBucket + ((int)depth >> (depthShift & 31))), packet.Words(), sidecars);
            if (pilotIdentity is { } identity && pilotWorld is not null)
            {
                int role = unchecked(((identity.Primitive * 397 + pilotObject) * 397 + (int)pilotFace) * 397 + pilotHalf);
                role = unchecked(role * 397 + pilotMatrixRole);
                foreach (int row in rows)
                {
                    role = unchecked(role * 397 + row);
                }
                primitive = primitive with { PilotGeometry = new(identity with { Primitive = role }, pilotWorld[0], pilotWorld[1], pilotWorld[2], quad ? pilotWorld[3] : default, checked((byte)rows.Length)) { SourceDepths = new(sidecars[0], sidecars[1], sidecars[2], sidecars[3]), UseSourceDepths = true } };
            }
            attempts.Add(packet.Attempt(primitive));
            allocated += quad ? 40 : 32;
            temporary464 = temporary464.MarkUnknown(0x2C, 4);
        }

        int Integer(uint address) => BinaryPrimitives.ReadInt32LittleEndian(readGeometry(address, 4).Read(0, 4));
        short Short(uint address) => BinaryPrimitives.ReadInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
        ushort Unsigned(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(readGeometry(address, 2).Read(0, 2));
    }

    public static NativeBattleEffectArena Intersect(NativeBattleEffectArena context, int first, int second, int plane, int output)
    {
        int[] a = [Signed(context, first), Signed(context, first + 2), Signed(context, first + 4)];
        int[] delta = [Signed(context, second) - a[0], Signed(context, second + 2) - a[1], Signed(context, second + 4) - a[2]];
        int[] normal = [Signed(context, plane), Signed(context, plane + 2), Signed(context, plane + 4)];
        int denominator = unchecked(normal[0] * delta[0] + normal[1] * delta[1] + normal[2] * delta[2]) >> 12;
        if (denominator == 0)
        {
            denominator = 1;
        }
        int numerator = unchecked((Signed(context, plane + 6) << 12) + normal[0] * a[0] + normal[1] * a[1] + normal[2] * a[2]);
        int factor = unchecked(-(numerator / denominator));
        for (int axis = 0; axis < 3; axis++)
        {
            context = context.Write(output + axis * 2, BitConverter.GetBytes(unchecked((short)(a[axis] + (unchecked(delta[axis] * factor) >> 12)))));
        }
        int firstUv = Word(context, first + 6);
        int secondUv = Word(context, second + 6);
        int u = unchecked(((secondUv & 255) - (firstUv & 255)) * factor) >> 12;
        u = unchecked(u + (firstUv & 255));
        int v = unchecked(((secondUv & 0xFF00) - (firstUv & 0xFF00)) * factor) >> 12;
        v = unchecked(v + (firstUv & 0xFF00));
        return context.Write(output + 6, BitConverter.GetBytes(unchecked((ushort)(u | (v & 0xFF00)))));
    }

    private static NativeBattleGeometryRegisterStep LoadPoint(NativeBattleGeometryRegisterStep step, int slot, NativeBattleEffectArena source, int offset, Action<NativeBattleVector>? reachedLoad = null)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(offset, 8);
        reachedLoad?.Invoke(new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))));
        ImmutableArray<byte?> packed = bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray();
        return slot switch
        {
            0 => step with { Colors = step.Colors with { NormalInput = bytes.ToImmutableArray(), NormalInputKnown = known.ToImmutableArray() } },
            1 => step with { Geometry = step.Geometry with { V1 = packed } },
            2 => step with { Geometry = step.Geometry with { V2 = packed } },
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }

    private static NativeBattleGeometryRegisterStep Matrix(NativeBattleGeometryRegisterStep step, NativeBattlePackedMatrix32 packed, bool rotation)
    {
        NativeBattleEffectArena matrixBytes = NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
        matrixBytes = matrixBytes.Write(20, packed.Bytes.AsSpan().Slice(20, 12).ToArray());
        for (int index = 20; index < 32; index++)
        {
            if (!packed.Known[index])
            {
                matrixBytes = matrixBytes.MarkUnknown(index, 1);
            }
        }
        NativeBattleGeometryRegisters geometry = step.Geometry with { Translation = [unchecked((int)Dword(matrixBytes, 20)), unchecked((int)Dword(matrixBytes, 24)), unchecked((int)Dword(matrixBytes, 28))] };
        if (rotation)
        {
            geometry = geometry with { Rotation20 = packed.Bytes.Take(20).Select((value, index) => packed.Known[index] ? (byte?)value : null).ToImmutableArray() };
        }
        return step with { Geometry = geometry };
    }

    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffectArena UV(NativeBattleEffectArena arena, int row, ushort value) => arena.Write(row + 6, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Store(NativeBattleEffectArena arena, int offset, NativeBattleVector vector) => arena.Write(offset, BitConverter.GetBytes(vector.X)).Write(offset + 2, BitConverter.GetBytes(vector.Y)).Write(offset + 4, BitConverter.GetBytes(vector.Z));
    private static NativeBattleVector Vector(NativeBattleEffectArena arena, int offset) => new(Signed(arena, offset), Signed(arena, offset + 2), Signed(arena, offset + 4));
    private static NativeBattleEffectArena Put(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short Signed(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static int Required(int? value) => value ?? throw new NotSupportedException("Body drawing requires its current arithmetic result.");
    private static uint Required(uint? value) => value ?? throw new NotSupportedException("Body drawing requires its current geometry result.");
}
