using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDeathModelDark
{
    public static NativeBattleDeathFrame Initialize(NativeBattleDeathFrame frame, uint taskAddress, NativeBattleModulePoolTask task, NativeBattleDeathResources resources)
    {
        NativeBattleDeathFamilyProfile profile = resources.Profile;
        NativeBattleDeathState state = profile.RequireState(frame.Memory);
        NativeBattleEffectArena module = state.Module.Write(profile.ModuleOffset(0x273AE90), new byte[84]);
        void Dword(uint address, uint value)
        {
            module = module.Write(profile.ModuleOffset(address), BitConverter.GetBytes(value));
        }
        void Word(uint address, ushort value)
        {
            module = module.Write(profile.ModuleOffset(address), BitConverter.GetBytes(value));
        }
        Dword(0x273AED0, profile.ModuleAddress(0x273B550));
        Dword(0x273AED4, profile.ModuleAddress(0x2742878));
        Dword(0x273AED8, checked(taskAddress + 0x30));
        Dword(0x273AEDC, profile.ModuleAddress(0x273AE84));
        Dword(0x273AEE0, profile.ModuleAddress(0x273AE84));
        Dword(0x274287C, profile.ModuleAddress(0x273B550));
        Word(0x274288C, 0);
        Word(0x274288E, 0);
        Word(0x2742890, 320);
        Word(0x2742892, 0);
        module = module.Write(profile.ModuleOffset(0x2742894), [128, 128, 128]);
        Dword(0x2742898, uint.MaxValue);
        Word(0x274289C, 0);
        return frame with { Memory = profile.WithState(frame.Memory, state with { Module = module }) };
    }

    public static NativeBattleDeathDraw Draw(NativeBattleDeathFrame frame, NativeBattleModulePoolTask task, NativeBattleEffectOwnedModelState model, NativeBattleDeathResources resources, NativeBattlePackedMatrix32 camera, NativeBattleCameraProjection projection, NativeBattleShadowSource shadowSource, NativeBattlePrimitiveCapabilities capabilities)
    {
        NativeBattleDeathFamilyProfile profile = resources.Profile;
        NativeBattleEffectOwnedModel.Validate(task, model, resources.Model);
        NativeBattleDeathState state = profile.RequireState(frame.Memory);
        _ = state.Cursor ?? throw new NotSupportedException("Native Death dark drawing requires its actual private cursor.");
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Native Death dark drawing requires its actual addressed model and temporary memory.");
        NativeBattleEffectOwnedModelRendering.RequireStagedPose(staging, model, resources.Model);
        NativeBattleEffectArena scratch = View(state.Module, profile.ModuleOffset(0x273AE90), 84);
        if (U32(scratch, 0x48) != checked(model.TaskAddress + 0x30) || U32(scratch, 0x40) != profile.ModuleAddress(0x273B550) || U32(scratch, 0x44) != profile.ModuleAddress(0x2742878) || U32(scratch, 0x4C) != profile.ModuleAddress(0x273AE84) || U32(scratch, 0x50) != profile.ModuleAddress(0x273AE84) || U32(state.Module, profile.ModuleOffset(0x274287C)) != profile.ModuleAddress(0x273B550))
        {
            throw new InvalidDataException("Native Death dark drawing lost its actual scratch, descriptor, model or cursor-variable owner.");
        }
        if (task.ReadDword(0xA8) != 0)
        {
            throw new NotSupportedException("Native Death linked-model drawing requires its actual linked source owner.");
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(Signed(task, 0x3C), Signed(task, 0x3E), Signed(task, 0x40)));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x114)), unchecked((int)task.ReadDword(0x118)), unchecked((int)task.ReadDword(0x11C))) with { X = Signed(task, 0x4C), Y = Signed(task, 0x4E), Z = Signed(task, 0x50) };
        NativeBattlePackedMatrix32 packedWorld = NativeBattlePackedMatrix32.FromMatrix(world, null);
        task = task.WriteBytes(0x70, packedWorld.Bytes.AsSpan()).MarkPayloadUnknown(0x82, 2);
        staging = NativeBattleEffectOwnedModelRendering.PrepareLocalBones508C90(staging, model, resources.Model);
        NativeBattleEffectOwnedModelCameraStep prepared = NativeBattleEffectOwnedModelRendering.PrepareCamera84(frame.Memory, scratch, packedWorld, camera);
        frame = frame with { Memory = prepared.Memory };
        scratch = prepared.Scratch;
        NativeBattleEffectLocalBounds bounds = new(new(Signed(task, 0x64), Signed(task, 0x66), Signed(task, 0x68)), new(Signed(task, 0x6A), Signed(task, 0x6C), Signed(task, 0x6E)));
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(staging, Registers(frame.Memory), world, Signed(task, 0x54), bounds, task.ReadDword(0x5C), projection with { View = camera.ReadMatrix() }, frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Native Death dark drawing requires its actual depth controls."), shadowSource, capabilities.GouraudAlpha, 4112, 16);
        staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x78);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors } };
        ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattleEffectOwnedModelRendering.CaptureBones(staging, resources.Model);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleEffectOwnedModelRendering.ReadGeometry(staging, resources.Model);
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x5C);
        staging = allocated.Staging;
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, allocated.Address, 0x5C);
        uint color = task.ReadDword(0x58) & 0xFFFFFF;
        temporary = Dword(temporary, 0x48, color | 0x24000000);
        temporary = Dword(temporary, 0x4C, color | 0x2C000000);
        temporary = Dword(temporary, 0x50, task.ReadDword(0xAC));
        NativeBattlePackedMatrix32 composed = Packed(scratch, 0x20);
        NativeBattleGeometryRegisterStep registers = Registers(frame.Memory);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, composed.ReadMatrix(), true, true, composed.Known[18] && composed.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(composed.Bytes.AsSpan().Slice(18, 2)) : null) };
        NativeBattleEffectMemory memory = Publish(frame.Memory, registers);
        NativeBattleScriptedGraphicsMemory controls = memory.ScriptedGraphics!;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocationBytes = shadow.Batch.AllocationBytes;
        int currentVertexCount = 0;
        attempts.AddRange(shadow.Batch.Attempts);
        NativeBattleEffectArena module = state.Module;
        for (int objectIndex = 0; objectIndex < meshes.Length; objectIndex++)
        {
            if ((task.ReadDword(0xAC) & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[objectIndex];
            ImmutableArray<NativeBattleRawVertexGroup> groups = mesh.PackedVertexGroups ?? throw new NotSupportedException("Native Death dark drawing requires its actual packed bone groups.");
            int vertexOffset = 0;
            foreach (NativeBattleRawVertexGroup group in groups)
            {
                if (group.Bone >= bones.Length || group.Bytes.IsDefault || group.Bytes.Length < group.Count * 6)
                {
                    throw new InvalidDataException("Native Death dark bone group leaves its actual selected model.");
                }
                memory = NativeBattleLightBackTransform.LoadBone(memory, bones[group.Bone]);
                for (int vertex = 0; vertex < group.Count; vertex++)
                {
                    temporary = temporary.Write(0x28, group.Bytes.AsSpan().Slice(vertex * 6, 6));
                    registers = LoadVector(Registers(memory), 0, Vector(temporary, 0x28));
                    memory = NativeBattleLightBackTransform.Transform(Publish(memory, registers), 0);
                    for (int axis = 0; axis < 3; axis++)
                    {
                        temporary = Word(temporary, 0x28 + axis * 2, unchecked((ushort)Require(memory.ScriptedColorRegisters!.Ir[axis])));
                    }
                    module = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x28, module, checked(profile.ModuleOffset(0x273B550) + vertexOffset), 8);
                    vertexOffset += 8;
                }
            }
            currentVertexCount = vertexOffset / 8;
            foreach (NativeBattleRawPolygon face in mesh.Polygons)
            {
                if (face.Bytes.Length != (face.Quad ? 20 : 16))
                {
                    throw new NotSupportedException("Native Death dark drawing requires original FT3 or FT4 source records.");
                }
                uint[] indices = new uint[face.Quad ? 4 : 3];
                for (int corner = 0; corner < 3; corner++)
                {
                    indices[corner] = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(corner * 2, 2)) & 0xFFF);
                    temporary = Dword(temporary, 0x38 + corner * 4, indices[corner]);
                }
                if (indices.Take(3).Any(index => ReadY(index) > 0) || face.Quad && ReadY(U32(temporary, 0x44)) > 0)
                {
                    continue;
                }
                registers = Registers(memory);
                for (int corner = 0; corner < 3; corner++)
                {
                    registers = LoadVector(registers, corner, ReadVector(indices[corner]));
                }
                registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
                registers = NativeBattleGeometry.Clip45EE10(registers);
                temporary = Dword(temporary, 0x54, registers.Geometry.Mac0 ?? throw new NotSupportedException("Native Death dark drawing requires its reached clipping result."));
                memory = Publish(memory, registers);
                if (unchecked((int)U32(temporary, 0x54)) <= 0)
                {
                    continue;
                }
                uint[] screen = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Native Death dark drawing requires its reached screen FIFO.")).ToArray();
                if (face.Quad)
                {
                    attempts.Add(ScreenPrefix(allocationBytes, screen));
                    indices[3] = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(6, 2)) & 0xFFF);
                    temporary = Dword(temporary, 0x44, indices[3]);
                    registers = LoadVector(registers, 0, ReadVector(indices[3]));
                    registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
                    registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Native Death dark drawing requires its actual ZSF4."));
                }
                else
                {
                    registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Native Death dark drawing requires its actual ZSF3."));
                }
                uint depth = (registers.Geometry.Otz ?? throw new NotSupportedException("Native Death dark drawing requires its actual averaged depth.")) >> 2;
                temporary = Dword(temporary, 0x58, depth);
                memory = Publish(memory, registers);
                NativeBattleGfGeometryAttempt packet = Packet(face, color, registers, screen, allocationBytes, checked(17 + (int)depth));
                attempts.Add(packet);
                allocationBytes += packet.PacketLength;
            }
        }
        module = NativeBattleEffectNodeContextMemory.Copy(scratch, 0, module, profile.ModuleOffset(0x273AE90), 84);
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocated.Address, temporary, 0x5C);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x5C);
        staging = NativeBattleEffectOwnedModelRendering.PrepareLocalBones508C90(staging, model, resources.Model);
        return new(frame with { Memory = profile.WithState(memory, state with { Module = module }), Staging = staging }, task, new(attempts.ToImmutable(), allocationBytes));

        short ReadY(uint index)
        {
            uint address = checked(profile.ModuleAddress(0x273B550) + index * 8 + 2);
            foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
            {
                long delta = (long)address - pool.BankAddress;
                if (delta < 0 || delta + 2 > (long)pool.Stride * pool.Slots.Length)
                {
                    continue;
                }
                int slot = (int)(delta / pool.Stride);
                uint slotAddress = checked(pool.BankAddress + (uint)(slot * pool.Stride));
                NativeBattleModulePoolTask selected = slotAddress == model.TaskAddress ? task : pool.Slots[slot];
                int field = (int)(delta % pool.Stride);
                uint next = selected.Next is int linked ? checked(pool.BankAddress + (uint)(linked * pool.Stride)) : 0;
                ushort value = field switch
                {
                    0 => selected.Flags,
                    2 => selected.State,
                    4 => unchecked((ushort)next),
                    6 => unchecked((ushort)(next >> 16)),
                    8 => unchecked((ushort)selected.Callback),
                    10 => unchecked((ushort)(selected.Callback >> 16)),
                    _ => selected.ReadWord(field)
                };
                return unchecked((short)value);
            }
            if (address < profile.ModuleBase || (ulong)address + 2 > (ulong)profile.ModuleBase + (uint)profile.ModuleLength)
            {
                throw new NotSupportedException("Native Death previous-quad ground test leaves its retained executable owner.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(module.Read(checked((int)(address - profile.ModuleBase)), 2));
        }

        ImmutableArray<byte?> ReadVector(uint index)
        {
            if (index >= currentVertexCount)
            {
                throw new InvalidDataException("Native Death dark face leaves its transformed vertex rows.");
            }
            return Vector(module, checked(profile.ModuleOffset(0x273B550) + (int)index * 8));
        }

    }

    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => NativeBattleEffectOwnedModelRendering.Registers(memory);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => NativeBattleEffectOwnedModelRendering.Signed(task, offset);
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value) => NativeBattleEffectOwnedModelRendering.Word(arena, offset, value);
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => NativeBattleEffectOwnedModelRendering.Dword(arena, offset, value);
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static int Require(int? value) => value ?? throw new NotSupportedException("Native Death dark drawing requires its reached arithmetic output.");
    private static NativeBattleEffectArena View(NativeBattleEffectArena arena, int offset, int length) => NativeBattleEffectNodeContextMemory.Copy(arena, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    private static ImmutableArray<byte?> Vector(NativeBattleEffectArena arena, int offset)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(offset, 8);
        return bytes.Select((value, index) => known[index] ? (byte?)value : null).ToImmutableArray();
    }
    private static NativeBattleEffectMemory Publish(NativeBattleEffectMemory memory, NativeBattleGeometryRegisterStep registers) => memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors };
    private static NativeBattleGeometryRegisterStep LoadVector(NativeBattleGeometryRegisterStep registers, int slot, ImmutableArray<byte?> bytes)
    {
        return slot switch
        {
            0 => registers with { Colors = registers.Colors with { NormalInput = bytes.Select(value => value ?? 0).ToImmutableArray(), NormalInputKnown = bytes.Select(value => value.HasValue).ToImmutableArray() } },
            1 => registers with { Geometry = registers.Geometry with { V1 = bytes } },
            2 => registers with { Geometry = registers.Geometry with { V2 = bytes } },
            _ => throw new ArgumentOutOfRangeException(nameof(slot))
        };
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

    private static NativeBattleGfGeometryAttempt Packet(NativeBattleRawPolygon face, uint color, NativeBattleGeometryRegisterStep registers, uint[] screen, int offset, int bucket)
    {
        byte[] bytes = new byte[face.Quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        Store(bytes, mask, 0, face.Quad ? 0x09000000u : 0x07000000u, 4);
        Store(bytes, mask, 4, color | (face.Quad ? 0x2C000000u : 0x24000000u), 4);
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

}
