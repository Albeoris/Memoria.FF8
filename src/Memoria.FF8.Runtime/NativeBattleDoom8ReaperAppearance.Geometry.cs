using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDoom8ReaperAppearance
{
    public static NativeBattleDoom8AppearanceStep Draw8CC7B0(NativeBattleDoom8Frame frame, NativeBattleFileStaging staging, NativeBattleDoom8AppearanceModel model, NativeBattleCameraProjection projection, int tableBaseBucket = 17, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (model.Address == 0 || frame.State.ReadDword(ScratchAddress + 0x48) != model.Address || frame.State.ReadDword(ScratchAddress + 0x40) != VertexAddress || frame.State.ReadDword(ScratchAddress + 0x44) != DescriptorAddress || frame.State.ReadDword(DescriptorAddress + 4) != VertexAddress || frame.State.ReadDword(ScratchAddress + 0x4C) != CursorAddress || frame.State.ReadDword(ScratchAddress + 0x50) != CursorAddress)
        {
            throw new InvalidDataException("Doom appearance lost its actual scratch, descriptor, model or cursor-variable ownership.");
        }
        if (model.Meshes.IsDefault || model.Bones.IsDefault || U32(model.Bytes, 0x78) != 0)
        {
            throw new NotSupportedException("Doom appearance requires its current body objects and bones; linked models require their own reached source owner.");
        }
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Doom appearance requires its current projection controls.");
        uint packetAddress = frame.State.ReadDword(CursorAddress);
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x100);
        staging = allocated.Staging;
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, allocated.Address, 0x100);
        uint color = U32(model.Bytes, 0x28) & 0xFFFFFF;
        uint objectMask = U32(model.Bytes, 0x7C);
        temporary = Dword(temporary, 0x48, color | 0x24000000).Write(0x4C, DwordBytes(color | 0x2C000000));
        temporary = Dword(temporary, 0x50, objectMask);
        temporary = Word(temporary, 0xF4, frame.State.ReadWord(ScratchAddress + 0x54));
        temporary = Word(temporary, 0xFE, 0);
        NativeBattleDoom8State state = PreparePalettes(frame.State);
        temporary = PrepareDisplacements(state, temporary);
        NativeBattlePackedMatrix32 composed = Matrix(state, ScratchAddress + 0x20);
        NativeBattleGeometryRegisterStep registers = Registers(frame.Memory);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed.ReadMatrix(), true, true, Padding(composed)) };
        NativeBattleEffectMemory memory = Publish(frame.Memory, registers);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocationBytes = 0;
        int faceAgeOffset = 0x5C;
        for (int objectIndex = 0; objectIndex < model.Meshes.Length; objectIndex++)
        {
            if ((objectMask & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = model.Meshes[objectIndex];
            mesh.Validate();
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Doom appearance requires the original packed bone groups.");
            int vertexOffset = 0;
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                if (group.Bone >= model.Bones.Length || group.Bytes.IsDefault || group.Count > 0 && group.Bytes.Length < group.Count * 6)
                {
                    throw new InvalidDataException("Doom appearance bone group leaves its actual selected model.");
                }
                memory = LoadBoneLight(memory, model.Bones[group.Bone]);
                for (int vertex = 0; vertex < group.Count; vertex++)
                {
                    if (vertexOffset > VertexLength - 8)
                    {
                        throw new InvalidDataException("Doom appearance exceeds its actual transformed vertex scratch.");
                    }
                    temporary = temporary.Write(0x28, group.Bytes.AsSpan().Slice(vertex * 6, 6));
                    registers = LoadPackedVector(Registers(memory), 0, PackedVector(temporary, 0x28));
                    memory = NativeBattleLightBackTransform.Transform(Publish(memory, registers), 0);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        temporary = Word(temporary, 0x28 + axis * 2, unchecked((ushort)Require(memory.ScriptedColorRegisters!.Ir[axis])));
                    }
                    state = CopyToState(state, VertexAddress + (uint)vertexOffset, temporary, 0x28, 8);
                    vertexOffset += 8;
                }
            }
            foreach (NativeBattleRawPolygon face in mesh.Polygons)
            {
                if (face.Bytes.Length != (face.Quad ? 20 : 16))
                {
                    throw new NotSupportedException("Doom appearance requires its original FT3 or FT4 source records.");
                }
                faceAgeOffset++;
                if (faceAgeOffset >= ScratchLength)
                {
                    throw new InvalidDataException("Doom appearance face ages exceed their physical scratch owner.");
                }
                temporary = Word(temporary, 0xFE, unchecked((ushort)(U16(temporary, 0xFE) + 1)));
                int count = face.Quad ? 4 : 3;
                int sumY = 0;
                for (int corner = 0; corner < count; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(corner * 2, 2)) & 0xFFF;
                    if (index >= vertexOffset / 8)
                    {
                        throw new InvalidDataException("Doom appearance polygon references an absent transformed vertex.");
                    }
                    temporary = Dword(temporary, 0x38 + corner * 4, (uint)index);
                    temporary = Copy(state.Globals, checked((int)(VertexAddress - NativeBattleDoom8State.BaseAddress)) + index * 8, temporary, 0x54 + corner * 8, 8);
                    sumY += I16(temporary, 0x56 + corner * 8);
                }
                short mean = unchecked((short)(sumY / count));
                temporary = Word(temporary, 0xF6, unchecked((ushort)mean));
                uint ageAddress = ScratchAddress + (uint)faceAgeOffset;
                byte age = state.Read(ageAddress, 1)[0];
                if (mean >= I16(temporary, 0xF4) && age < 19)
                {
                    age++;
                    state = state.Write(ageAddress, [age]);
                }
                if (age == 0)
                {
                    continue;
                }
                if (age > 19)
                {
                    throw new InvalidDataException("Doom appearance face age leaves its actual palette and displacement tables.");
                }
                if (age < 19)
                {
                    short dx = I16(temporary, 0x74 + age * 2);
                    short dy = I16(temporary, 0x9C + age * 2);
                    short dz = I16(temporary, 0xC4 + age * 2);
                    if ((U16(temporary, 0xFE) & 1) != 0)
                    {
                        dx = unchecked((short)-dx);
                        dz = unchecked((short)-dz);
                    }
                    temporary = Word(temporary, 0xF8, unchecked((ushort)dx));
                    temporary = Word(temporary, 0xFA, unchecked((ushort)dy));
                    temporary = Word(temporary, 0xFC, unchecked((ushort)dz));
                    short[] displacement = [dx, dy, dz];
                    for (int corner = 0; corner < count; corner++)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            int offset = 0x54 + corner * 8 + axis * 2;
                            temporary = Word(temporary, offset, unchecked((ushort)(I16(temporary, offset) + displacement[axis])));
                        }
                    }
                }
                System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[count] : null;
                registers = Registers(memory);
                for (int corner = 0; corner < 3; corner++)
                {
                    registers = LoadPackedVector(registers, corner, PackedVector(temporary, 0x54 + corner * 8));
                    if (world is not null)
                    {
                        NativeBattleVector point = new(I16(temporary, 0x54 + corner * 8), I16(temporary, 0x56 + corner * 8), I16(temporary, 0x58 + corner * 8));
                        world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(composed.ReadMatrix(), point), projection);
                    }
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                registers = NativeBattleGeometry.Clip45EE10(registers);
                temporary = Dword(temporary, 0xEC, registers.Geometry.Mac0!.Value);
                memory = Publish(memory, registers);
                if (unchecked((int)registers.Geometry.Mac0.Value) <= 0)
                {
                    continue;
                }
                uint[] screen = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Doom appearance requires its reached screen FIFO.")).ToArray();
                if (face.Quad)
                {
                    attempts.Add(ScreenPrefix(allocationBytes, screen));
                    registers = LoadPackedVector(registers, 0, PackedVector(temporary, 0x6C));
                    if (world is not null)
                    {
                        NativeBattleVector point = new(I16(temporary, 0x6C), I16(temporary, 0x6E), I16(temporary, 0x70));
                        world[3] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(composed.ReadMatrix(), point), projection);
                    }
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Doom appearance requires its actual ZSF4."));
                }
                else
                {
                    registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Doom appearance requires its actual ZSF3."));
                }
                uint depth = registers.Geometry.Otz!.Value >> 2;
                temporary = Dword(temporary, 0xF0, depth);
                memory = Publish(memory, registers);
                NativeBattleGfGeometryAttempt packet = Packet(face, age, state, registers, screen, allocationBytes, checked(tableBaseBucket + (int)depth));
                if (pilotIdentity is { } identity && packet.Primitive is { } primitive)
                {
                    primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 65537 + faceAgeOffset) }, world![0], world[1], world[2], face.Quad ? world[3] : default, (byte)count) { DepthDivisor = 4, SourceDepths = new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]) } };
                    packet = packet with { Primitive = primitive };
                }
                attempts.Add(packet);
                allocationBytes += packet.PacketLength;
            }
        }
        state = state.WriteDword(CursorAddress, checked(packetAddress + (uint)allocationBytes));
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocated.Address, temporary, 0x100);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x100);
        return new(frame with { State = state, Memory = memory }, staging, new(attempts.ToImmutable(), allocationBytes), allocated.Address);
    }

    private static NativeBattleDoom8State PreparePalettes(NativeBattleDoom8State state)
    {
        for (int index = 0; index < 20; index++)
        {
            uint shade = (uint)Math.Min(index * 8, 128);
            uint rgb = shade | shade << 8 | shade << 16;
            state = state.WriteDword(TrianglePalette + (uint)(index * 4), rgb | (index == 19 ? 0x24000000u : 0x26000000u));
            state = state.WriteDword(QuadPalette + (uint)(index * 4), rgb | (index == 19 ? 0x2C000000u : 0x2E000000u));
        }
        return state;
    }

    private static NativeBattleEffectArena PrepareDisplacements(NativeBattleDoom8State state, NativeBattleEffectArena temporary)
    {
        short angle = unchecked((short)state.ReadWord(ScratchAddress + 0x58));
        ushort delta = state.ReadWord(ScratchAddress + 0x5A);
        for (int index = 0; index < 20; index++)
        {
            short x = 0;
            short z = 0;
            if (index < 16)
            {
                int distance = (15 - index) * 48;
                x = unchecked((short)(NativeBattleCameraMath.Sine(angle) * distance / 4096));
                z = unchecked((short)(NativeBattleCameraMath.Cosine(angle) * distance / 4096));
                angle = unchecked((short)((unchecked((ushort)angle) + delta) & 0xFFF));
            }
            temporary = Word(temporary, 0x74 + index * 2, unchecked((ushort)x));
            temporary = Word(temporary, 0x9C + index * 2, 0);
            temporary = Word(temporary, 0xC4 + index * 2, unchecked((ushort)z));
        }
        return temporary;
    }

    private static NativeBattleGeometryRegisterStep LoadPackedVector(NativeBattleGeometryRegisterStep step, int slot, ImmutableArray<byte?> bytes)
    {
        return slot switch
        {
            0 => step with { Colors = step.Colors with { NormalInput = bytes.Select(value => value ?? 0).ToImmutableArray(), NormalInputKnown = bytes.Select(value => value.HasValue).ToImmutableArray() } },
            1 => step with { Geometry = step.Geometry with { V1 = bytes } },
            2 => step with { Geometry = step.Geometry with { V2 = bytes } },
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
    }

    private static ImmutableArray<byte?> PackedVector(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 8);
        return bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray();
    }

    private static NativeBattleGfGeometryAttempt ScreenPrefix(int offset, uint[] screen)
    {
        byte[] bytes = new byte[40];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
        for (int corner = 0; corner < 3; corner++)
        {
            Store(bytes, mask, 8 + corner * 8, screen[corner], 4);
        }
        return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), null);
    }

    private static NativeBattleGfGeometryAttempt Packet(NativeBattleRawPolygon face, byte age, NativeBattleDoom8State state, NativeBattleGeometryRegisterStep registers, uint[] screen, int offset, int bucket)
    {
        byte[] bytes = new byte[face.Quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        Store(bytes, mask, 0, face.Quad ? 0x09000000u : 0x07000000u, 4);
        Store(bytes, mask, 4, state.ReadDword((face.Quad ? QuadPalette : TrianglePalette) + (uint)(age * 4)), 4);
        for (int corner = 0; corner < 3; corner++)
        {
            Store(bytes, mask, 8 + corner * 8, screen[corner], 4);
        }
        Store(bytes, mask, 12, BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(8, 4)), 4);
        Store(bytes, mask, 20, BinaryPrimitives.ReadUInt32LittleEndian(face.Bytes.AsSpan().Slice(12, 4)), 4);
        if (face.Quad)
        {
            Store(bytes, mask, 28, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(16, 2)), 2);
            Store(bytes, mask, 32, registers.Geometry.Sxy[2]!.Value, 4);
            Store(bytes, mask, 36, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(18, 2)), 2);
        }
        else
        {
            Store(bytes, mask, 28, BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(6, 2)), 2);
        }
        int[] payload = face.Quad
            ? registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToArray()
            : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4, 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, payload.ToImmutableArray()) { PhysicalWriteMask = mask.ToImmutableArray() };
        if (face.Quad)
        {
            for (int corner = 0; corner < 3; corner++)
            {
                mask.AsSpan(8 + corner * 8, 4).Fill(NativeBattleGfPacketByteState.Untouched);
            }
        }
        return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
    }

    private static void Store(byte[] bytes, NativeBattleGfPacketByteState[] mask, int offset, uint value, int length)
    {
        for (int index = 0; index < length; index++)
        {
            bytes[offset + index] = unchecked((byte)(value >> (index * 8)));
            mask[offset + index] = NativeBattleGfPacketByteState.Known;
        }
    }

    private static byte[] DwordBytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
}
