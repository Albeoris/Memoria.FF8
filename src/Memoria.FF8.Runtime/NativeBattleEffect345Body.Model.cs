using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect345Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Body
{
    public static NativeBattleEffect345Frame Advance7061F0(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        if ((task.ReadByte(0x26) & 8) == 0)
        {
            return Draw706270(frame, address, resources, inputs);
        }
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(unchecked((short)task.ReadWord(0x3C)), unchecked((short)task.ReadWord(0x3E)), unchecked((short)task.ReadWord(0x40))));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x114)), unchecked((int)task.ReadDword(0x118)), unchecked((int)task.ReadDword(0x11C))) with { X = unchecked((short)task.ReadWord(0x4C)), Y = unchecked((short)task.ReadWord(0x4E)), Z = unchecked((short)task.ReadWord(0x50)) };
        task = CopyTask(task, 0x70, PackedArena(NativeBattlePackedMatrix32.FromMatrix(world, null)), 32);
        frame = Write(frame, address, task);
        return Draw706450(frame, address, resources, inputs);
    }

    public static NativeBattleEffect345Frame Draw706270(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectOwnedModelState model = Owned(frame, address);
        int length = resources.Model.Geometry.Max(mesh => mesh.Vertices.Length) * 8;
        NativeBattleEffectOwnedModelRenderFrame current = new(frame.Memory, task, model, resources.Model, ReadBytes(frame, NormalProjectedRows, length), NormalProjectedRows, length, frame.State.ReadDword(SecondaryCursorGlobal), frame.State.ReadDword(PrimaryCursorGlobal));
        NativeBattleEffectOwnedModelRenderStep drawn = NativeBattleEffectOwnedModelRendering.DrawNormal(current, RequireStaging(frame), inputs.ReadModelRenderInputs(frame), inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(345, task) with { ResourceGeneration = resources.Model.Address } : null);
        frame = frame with { Memory = drawn.Frame.Memory, Staging = drawn.Staging };
        frame = Write(frame, address, drawn.Frame.Task);
        frame = WriteBytes(frame, NormalProjectedRows, drawn.Frame.ProjectedScratch, length);
        uint first = frame.State.ReadDword(PrimaryCursorGlobal);
        frame = inputs.Execute(frame, new NativeBattleEffect345DrawBatch(inputs.CapturePresentation ? NativeBattleEffect345Presentation.PreserveNativeDepths(drawn.Batch) : drawn.Batch, PrimaryCursorGlobal));
        if (frame.State.ReadDword(PrimaryCursorGlobal) != drawn.Frame.PacketAddress || drawn.Frame.PacketAddress != checked(first + (uint)drawn.Batch.AllocationBytes))
        {
            throw new InvalidDataException("Effect345 normal model publication differs from its native private cursor advancement.");
        }
        return frame;
    }

    public static NativeBattleEffect345Frame Draw706450(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectOwnedModelState model = Owned(frame, address);
        NativeBattleEffectOwnedModel.Validate(task, model, resources.Model);
        NativeBattleEffectOwnedModelRendering.RequireStagedPose(RequireStaging(frame), model, resources.Model);
        NativeBattlePackedMatrix32 world = Packed(TaskView(task, 0x70, 32));
        frame = frame with { Staging = NativeBattleEffectOwnedModelRendering.PrepareLocalBones508C90(RequireStaging(frame), model, resources.Model) };
        NativeBattleEffectOwnedModelCameraStep camera = NativeBattleEffectOwnedModelRendering.PrepareCamera84(frame.Memory, ReadBytes(frame, AppearanceContext, 84), world, inputs.ReadView(frame));
        frame = frame with { Memory = camera.Memory };
        frame = WriteBytes(frame, AppearanceContext, camera.Scratch, 84);
        frame = DrawAppearanceShadow(frame, address, world.ReadMatrix(), inputs);
        frame = Draw706550(frame, address, resources, inputs);
        if (Read(frame, address).ReadDword(0xA8) != 0)
        {
            throw new NotSupportedException("Effect345 linked appearance geometry requires its selected concrete model owner.");
        }
        return frame with { Staging = NativeBattleEffectOwnedModelRendering.PrepareLocalBones508C90(RequireStaging(frame), model, resources.Model) };
    }

    private static NativeBattleEffect345Frame DrawAppearanceShadow(NativeBattleEffect345Frame frame, uint address, NativeBattleActionMatrix world, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectOwnedModelRenderInputs drawing = inputs.ReadModelRenderInputs(frame);
        NativeBattleEffectLocalBounds bounds = new(new(unchecked((short)task.ReadWord(0x64)), unchecked((short)task.ReadWord(0x66)), unchecked((short)task.ReadWord(0x68))), new(unchecked((short)task.ReadWord(0x6A)), unchecked((short)task.ReadWord(0x6C)), unchecked((short)task.ReadWord(0x6E))));
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(RequireStaging(frame), Registers(frame), world, unchecked((short)task.ReadWord(0x54)), bounds, task.ReadDword(0x5C), drawing.Projection with { View = drawing.Camera.ReadMatrix() }, frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect345 shadow drawing requires its current geometry controls."), drawing.Shadow, drawing.Capabilities.GouraudAlpha, 4112, 16, inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(345, task, 1) with { ResourceGeneration = -1 } : null);
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
        frame = Publish(frame, shadow.Registers);
        frame = Release(frame, 0x78);
        return inputs.Execute(frame, new NativeBattleEffect345DrawBatch(inputs.CapturePresentation ? NativeBattleEffect345Presentation.PreserveNativeDepths(shadow.Batch) : shadow.Batch, PrimaryCursorGlobal));
    }

    public static NativeBattleEffect345Frame Draw706550(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena descriptor = ReadBytes(frame, AppearanceContext, 84);
        if (Dword345(descriptor, 0x48) != address + 0x30 || Dword345(descriptor, 0x44) != AppearanceDescriptor || Dword345(descriptor, 0x4C) != PrimaryCursorGlobal)
        {
            throw new InvalidDataException("Effect345 appearance requires its actual model, descriptor and cursor-variable bindings.");
        }
        uint projected = frame.State.ReadDword(AppearanceDescriptor + 4);
        int projectedLength = checked(resources.Model.Geometry.Max(mesh => mesh.Vertices.Length) * 8);
        uint temporaryAddress;
        (frame, temporaryAddress) = Allocate(frame, 0x5C);
        NativeBattleEffectArena temporary = ReadBytes(frame, temporaryAddress, 0x5C);
        uint color = task.ReadDword(0x58) & 0xFFFFFF;
        uint mask = task.ReadDword(0xAC);
        temporary = Put32(temporary, 0x48, color | 0x24000000);
        temporary = Put32(temporary, 0x4C, color | 0x2C000000);
        temporary = Put32(temporary, 0x50, mask);
        frame = LoadMatrix(frame, Packed(CopyBytes(descriptor, 0x20, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, 32)));
        ImmutableArray<NativeBattlePackedMatrix32> bones = NativeBattleEffectOwnedModelRendering.CaptureBones(RequireStaging(frame), resources.Model);
        ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleEffectOwnedModelRendering.ReadGeometry(RequireStaging(frame), resources.Model);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect345 appearance drawing requires current geometry controls.");
        NativeBattleCameraProjection projection = inputs.ReadProjection(frame);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocationBytes = 0;
        for (int objectIndex = 0; objectIndex < meshes.Length; objectIndex++)
        {
            if ((mask & (1u << (objectIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = meshes[objectIndex];
            int vertexOffset = 0;
            foreach (NativeBattleRawVertexGroup group in mesh.PackedVertexGroups ?? throw new NotSupportedException("Effect345 appearance requires its actual packed bone groups."))
            {
                if ((uint)group.Bone >= bones.Length)
                {
                    throw new InvalidDataException("Effect345 appearance bone index leaves its selected current pose.");
                }
                frame = frame with { Memory = NativeBattleLightBackTransform.LoadBone(frame.Memory, bones[group.Bone]) };
                for (int vertex = 0; vertex < group.Count; vertex++)
                {
                    if (vertexOffset > projectedLength - 8)
                    {
                        throw new InvalidDataException("Effect345 appearance exceeds its current transformed vertex owner.");
                    }
                    temporary = temporary.Write(0x28, group.Bytes.AsSpan().Slice(vertex * 6, 6));
                    NativeBattleGeometryRegisterStep registers = LoadPackedVector(Registers(frame), 0, temporary, 0x28);
                    frame = Publish(frame, registers);
                    frame = frame with { Memory = NativeBattleLightBackTransform.Transform(frame.Memory, 0) };
                    for (int axis = 0; axis < 3; axis++)
                    {
                        temporary = Put16(temporary, 0x28 + axis * 2, unchecked((ushort)Require(frame.Memory.ScriptedColorRegisters!.Ir[axis])));
                    }
                    frame = WriteBytes(frame, checked(projected + (uint)vertexOffset), CopyBytes(temporary, 0x28, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8), 8);
                    vertexOffset += 8;
                }
            }
            int faceOrdinal = 0;
            foreach (NativeBattleRawPolygon face in mesh.Polygons)
            {
                int currentFace = faceOrdinal++;
                if (face.Bytes.Length != (face.Quad ? 20 : 16))
                {
                    throw new NotSupportedException("Effect345 appearance requires its selected FT3 and FT4 model records.");
                }
                uint[] indices = Enumerable.Range(0, 3).Select(corner => (uint)(BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(corner * 2, 2)) & 0xFFF)).ToArray();
                for (int corner = 0; corner < 3; corner++)
                {
                    temporary = Put32(temporary, 0x38 + corner * 4, indices[corner]);
                }
                NativeBattleEffectArena ReadPoint(uint index, int length = 8, int offset = 0)
                {
                    uint pointAddress = unchecked(projected + index * 8 + (uint)offset);
                    if (pointAddress >= projected && (ulong)pointAddress + (uint)length <= (ulong)projected + (uint)projectedLength)
                    {
                        return ReadBytes(frame, pointAddress, length);
                    }
                    return inputs.ReadPhysical(frame, pointAddress, length);
                }
                short ReadEarlyY(uint index)
                {
                    try
                    {
                        return Word345(ReadPoint(index, 2, 2), 0);
                    }
                    catch (Exception error) when (error is NotSupportedException or InvalidDataException)
                    {
                        (byte[] oldBytes, bool[] oldKnown) = temporary.ReadSnapshot(0x44, 4);
                        string oldIndex = oldKnown.All(value => value) ? $"0x{BinaryPrimitives.ReadUInt32LittleEndian(oldBytes):X8}" : $"unresolved({Convert.ToHexString(oldBytes)},known={string.Concat(oldKnown.Select(value => value ? '1' : '0'))})";
                        uint yAddress = unchecked(projected + index * 8 + 2);
                        throw new NotSupportedException($"Effect345 appearance early-Y input is unavailable: task=0x{address:X8}, object={objectIndex}, face={currentFace}, quad={face.Quad}, index=0x{index:X8}, Y=0x{yAddress:X8}, projected=0x{projected:X8}, projectedLength=0x{projectedLength:X}, temporary=0x{temporaryAddress:X8}, oldFourthAddress=0x{unchecked(temporaryAddress + 0x44):X8}, oldFourth={oldIndex}, poseAge={task.ReadWord(0x136)}. The reached unmasked index and physical WORD require their current owner.", error);
                    }
                }
                if (!AdmitEarlyCorners7067EE(temporary, indices, face.Quad, ReadEarlyY))
                {
                    continue;
                }
                System.Numerics.Vector3[]? world = inputs.CapturePresentation ? new System.Numerics.Vector3[face.Quad ? 4 : 3] : null;
                NativeBattleGeometryRegisterStep step = Registers(frame);
                NativeBattleActionMatrix? matrix = inputs.CapturePresentation ? NativeBattleGeometry.CaptureMatrix(step.Geometry) : null;
                for (int corner = 0; corner < 3; corner++)
                {
                    int selectedCorner = corner;
                    step = LoadPackedVector(step, corner, ReadPoint(indices[corner]), 0, world is not null && matrix is not null ? point => world[selectedCorner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection) : null);
                }
                step = NativeBattleGeometry.ProjectTriple(step, projection, controls.Dqa, controls.Dqb);
                step = NativeBattleGeometry.Clip45EE10(step);
                temporary = Put32(temporary, 0x54, step.Geometry.Mac0 ?? throw new NotSupportedException("Effect345 appearance requires its reached clip result."));
                frame = Publish(frame, step);
                if (unchecked((int)Dword345(temporary, 0x54)) <= 0)
                {
                    continue;
                }
                uint[] screens = step.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Effect345 appearance requires its current projected screen FIFO.")).ToArray();
                if (face.Quad)
                {
                    uint fourth = (uint)(BinaryPrimitives.ReadUInt16LittleEndian(face.Bytes.AsSpan().Slice(6, 2)) & 0xFFF);
                    temporary = Put32(temporary, 0x44, fourth);
                    step = LoadPackedVector(step, 0, ReadPoint(fourth), 0, world is not null && matrix is not null ? point => world[3] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection) : null);
                    step = NativeBattleGeometry.ProjectSingle(step, projection, controls.Dqa, controls.Dqb);
                    step = NativeBattleGeometry.Average45E610(step, controls.Zsf4 ?? throw new NotSupportedException("Effect345 appearance requires its current ZSF4."));
                    screens = [.. screens, step.Geometry.Sxy[2] ?? throw new NotSupportedException("Effect345 appearance requires its current fourth screen value.")];
                }
                else
                {
                    step = NativeBattleGeometry.Average45E5C0(step, controls.Zsf3 ?? throw new NotSupportedException("Effect345 appearance requires its current ZSF3."));
                }
                uint depth = (step.Geometry.Otz ?? throw new NotSupportedException("Effect345 appearance requires its current averaged depth.")) >> 2;
                temporary = Put32(temporary, 0x58, depth);
                frame = Publish(frame, step);
                NativeBattleGfGeometryAttempt packet = AppearancePacket(face, screens, step.Geometry, color, allocationBytes, checked(17 + (int)depth));
                if (world is not null && packet.Primitive is { } primitive)
                {
                    var identity = NativeBattlePilotVisualLifetimes.Identity(345, task, unchecked((2 * 397 + objectIndex) * 397 + currentFace)) with { ResourceGeneration = resources.Model.Address };
                    packet = packet with { Primitive = primitive with { PilotGeometry = new(identity, world[0], world[1], world[2], face.Quad ? world[3] : default, checked((byte)world.Length)) } };
                }
                attempts.Add(packet);
                allocationBytes += packet.PacketLength;
            }
        }
        frame = WriteBytes(frame, temporaryAddress, temporary, 0x5C);
        frame = Release(frame, 0x5C);
        return inputs.Execute(frame, new NativeBattleEffect345DrawBatch(inputs.CapturePresentation ? NativeBattleEffect345Presentation.PreserveNativeDepths(new(attempts.ToImmutable(), allocationBytes)) : new(attempts.ToImmutable(), allocationBytes), PrimaryCursorGlobal));
    }

    private static NativeBattleEffectOwnedModelState Owned(NativeBattleEffect345Frame frame, uint address) => frame.State.OwnedModels.TryGetValue(address, out NativeBattleEffectOwnedModelState? model) ? model : throw new NotSupportedException("Effect345 drawing requires its current effect-owned decoded model.");
    private static int Require(int? value) => value ?? throw new NotSupportedException("Effect345 drawing requires its reached arithmetic output.");

    public static bool AdmitEarlyCorners7067EE(NativeBattleEffectArena temporary, IReadOnlyList<uint> firstThree, bool quad, Func<uint, short> readY)
    {
        if (firstThree.Count != 3)
        {
            throw new ArgumentException("Effect345 appearance requires exactly three leading corner indices.", nameof(firstThree));
        }
        foreach (uint index in firstThree)
        {
            if (readY(index) > 0)
            {
                return false;
            }
        }
        return !quad || readY(Dword345(temporary, 0x44)) <= 0;
    }

    private static NativeBattleGeometryRegisterStep LoadPackedVector(NativeBattleGeometryRegisterStep step, int slot, NativeBattleEffectArena source, int offset, Action<NativeBattleVector>? reachedLoad = null)
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

    private static NativeBattleGfGeometryAttempt AppearancePacket(NativeBattleRawPolygon face, uint[] screens, NativeBattleGeometryRegisters geometry, uint color, int offset, int bucket)
    {
        int length = face.Quad ? 40 : 32;
        byte[] bytes = new byte[length];
        NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, length).ToArray();
        void Store(int target, ReadOnlySpan<byte> data)
        {
            data.CopyTo(bytes.AsSpan(target));
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, target, data.Length);
        }
        Store(0, BitConverter.GetBytes(face.Quad ? 0x09000000u : 0x07000000u));
        Store(4, BitConverter.GetBytes(color | (face.Quad ? 0x2C000000u : 0x24000000u)));
        for (int corner = 0; corner < screens.Length; corner++)
        {
            Store(8 + corner * 8, BitConverter.GetBytes(screens[corner]));
        }
        Store(12, face.Bytes.AsSpan().Slice(8, 4));
        Store(20, face.Bytes.AsSpan().Slice(12, 4));
        Store(28, face.Bytes.AsSpan().Slice(face.Quad ? 16 : 6, 2));
        if (face.Quad)
        {
            Store(36, face.Bytes.AsSpan().Slice(18, 2));
        }
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4, 4))).ToImmutableArray();
        ImmutableArray<int> payload = face.Quad ? geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Effect345 appearance requires its reached depth FIFO.")))).ToImmutableArray() : [unchecked((int)geometry.Sz[1]!.Value), unchecked((int)geometry.Sz[2]!.Value), unchecked((int)geometry.Sz[3]!.Value), 0];
        NativeBattleGeometryPrimitive primitive = new(bucket, words, payload) { PhysicalWriteMask = mask.ToImmutableArray() };
        return new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
    }
}
