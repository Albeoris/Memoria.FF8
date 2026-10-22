using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectOwnedModelRenderInputs(NativeBattlePackedMatrix32 Camera, NativeBattleCameraProjection Projection, NativeBattleShadowSource Shadow, NativeBattlePrimitiveCapabilities Capabilities, NativeBattleSummonColors Colors);
public sealed record NativeBattleEffectOwnedModelRenderFrame(NativeBattleEffectMemory Memory, NativeBattleModulePoolTask Task, NativeBattleEffectOwnedModelState Model, NativeBattleEffectOwnedModelResource Resource, NativeBattleEffectArena ProjectedScratch, uint ProjectedAddress, int ProjectedLength, uint FarInput, uint PacketAddress);
public sealed record NativeBattleEffectOwnedModelRenderStep(NativeBattleEffectOwnedModelRenderFrame Frame, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleEffectOwnedModelRendering
{

    public static NativeBattleEffectOwnedModelRenderStep DrawNormal(NativeBattleEffectOwnedModelRenderFrame frame, NativeBattleFileStaging staging, NativeBattleEffectOwnedModelRenderInputs inputs)
    {
        NativeBattleModulePoolTask task = frame.Task;
        NativeBattleEffectOwnedModelState model = frame.Model;
        NativeBattleEffectOwnedModelResource resource = frame.Resource;
        NativeBattleEffectOwnedModel.Validate(task, model, resource);
        RequireStagedPose(staging, model, resource);
        NativeBattleDecodedPose pose = RequirePose(model, resource);
        uint firstPacket = frame.PacketAddress;
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x4C);
        staging = allocation.Staging;
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, allocation.Address, 0x4C);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(Signed(task, 0x3C), Signed(task, 0x3E), Signed(task, 0x40)));
        world = NativeBattlePoseMatrices.Scale56BEF0(world, unchecked((int)task.ReadDword(0x114)), unchecked((int)task.ReadDword(0x118)), unchecked((int)task.ReadDword(0x11C))) with { X = Signed(task, 0x4C), Y = Signed(task, 0x4E), Z = Signed(task, 0x50) };
        NativeBattlePackedMatrix32 packedWorld = NativeBattlePackedMatrix32.FromMatrix(world, null);
        task = task.WriteBytes(0x70, packedWorld.Bytes.AsSpan()).MarkPayloadUnknown(0x82, 2);
        frame = frame with { Task = task };
        NativeBattleActionMatrix modelView = NativeBattlePoseMatrices.Compose56C2F0(inputs.Camera.ReadMatrix(), world);
        temporary = StoreMatrix(temporary, 0, NativeBattlePackedMatrix32.FromMatrix(modelView, null));
        ImmutableArray<NativeBattleActionMatrix> cameraBones = NativeBattlePoseMatrices.ComposeCurrentPose(resource.PoseSource, pose, modelView);
        staging = PublishMatrices(staging, resource, cameraBones, _ => null);
        temporary = Dword(temporary, 0x24, frame.ProjectedAddress);
        temporary = Dword(temporary, 0x30, frame.FarInput);
        temporary = Dword(temporary, 0x3C, task.ReadDword(0x58));
        temporary = Dword(temporary, 0x40, task.ReadDword(0xAC));
        temporary = Word(temporary, 0x34, 0);
        temporary = Word(temporary, 0x36, 0);
        temporary = Word(temporary, 0x38, 320);
        temporary = Word(temporary, 0x3A, 216);
        temporary = Word(temporary, 0x44, 0);
        byte alpha = task.ReadByte(0x37);
        temporary = temporary.Write(0x48, [alpha, alpha, alpha]);
        NativeBattleEffectOwnedModelRenderStep body = DrawBody(frame, staging, resource, temporary, allocation.Address, inputs, firstPacket);
        frame = body.Frame;
        staging = body.Staging;
        temporary = NativeBattleTemporaryMemory.ReadView(staging, allocation.Address, 0x4C);
        NativeBattleGfGeometryBatch packets = body.Batch;
        staging = PrepareLocalBones508C90(staging, model, resource);
        NativeBattleEffectLocalBounds bounds = NativeBattleEffectOwnedModelDrawing.ScaleShadowBounds7156EE(NativeBattlePoseMatrices.ShadowBounds5086F0(resource.PoseSource, pose), Signed(task, 0x13E));
        task = task.WriteWord(0x64, unchecked((ushort)bounds.Minimum.X)).WriteWord(0x66, unchecked((ushort)bounds.Minimum.Y)).WriteWord(0x68, unchecked((ushort)bounds.Minimum.Z)).WriteWord(0x6A, unchecked((ushort)bounds.Maximum.X)).WriteWord(0x6C, unchecked((ushort)bounds.Maximum.Y)).WriteWord(0x6E, unchecked((ushort)bounds.Maximum.Z));
        frame = frame with { Task = task };
        if ((task.ReadByte(0x30) & 0x20) == 0)
        {
            NativeBattleEffectOwnedModelRenderStep shadow = DrawShadow(frame, staging, task, world, bounds, inputs, 4121);
            frame = shadow.Frame;
            staging = shadow.Staging;
            packets = Combine(packets, shadow.Batch);
        }
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 0x4C);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x4C);
        return new(frame, staging, packets);
    }

    private static NativeBattleEffectOwnedModelRenderStep DrawShadow(NativeBattleEffectOwnedModelRenderFrame frame, NativeBattleFileStaging staging, NativeBattleModulePoolTask task, NativeBattleActionMatrix world, NativeBattleEffectLocalBounds bounds, NativeBattleEffectOwnedModelRenderInputs inputs, int bucket)
    {
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(staging, Registers(frame.Memory), world, Signed(task, 0x54), bounds, task.ReadDword(0x5C), inputs.Projection with { View = inputs.Camera.ReadMatrix() }, frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect-owned model shadow requires its current depth controls."), inputs.Shadow, inputs.Capabilities.GouraudAlpha, bucket, 16);
        staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x78);
        uint cursor = frame.PacketAddress;
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, PacketAddress = checked(cursor + (uint)shadow.Batch.AllocationBytes) };
        return new(frame, staging, shadow.Batch);
    }

    public static NativeBattleFileStaging PublishDecodedPose(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource)
    {
        uint skeleton = SkeletonAddress(resource);
        NativeBattleEffectArena view = NativeBattleTemporaryMemory.ReadView(staging, skeleton, resource.PoseSource.Skeleton.Length);
        NativeBattleDecodedPose pose = RequirePose(state, resource);
        view = NativeBattleArenaPose.WritePose(view, 0, pose);
        return NativeBattleTemporaryMemory.PublishView(staging, skeleton, view, resource.PoseSource.Skeleton.Length);
    }

    public static NativeBattleFileStaging PrepareLocalBones508C90(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource)
    {
        RequireStagedPose(staging, model, resource);
        NativeBattleDecodedPose pose = RequirePose(model, resource);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, pose);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 0x58);
        staging = allocation.Staging;
        NativeBattleEffectArena temporary = NativeBattleTemporaryMemory.ReadView(staging, allocation.Address, 0x58);
        temporary = NativeBattleBodyBoneTemporary.Prepare508C90(resource.PoseSource, pose, matrices, temporary);
        staging = PublishMatrices(staging, resource, matrices, index => BinaryPrimitives.ReadInt16LittleEndian(resource.PoseSource.Skeleton.AsSpan().Slice(16 + index * 48)) < 0 && !pose.HasScale ? (ushort?)0 : null);
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 0x58);
        return NativeBattleTemporaryMemory.Release5082D0(staging, 0x58);
    }

    public static NativeBattleDecodedPose RequirePose(NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource)
    {
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Effect-owned model model drawing requires its actual selected decoded pose.");
        if (model.ResourceAddress != resource.Address || pose.Bones.IsDefault || pose.Bones.Length != resource.PoseSource.BoneCount)
        {
            throw new InvalidDataException("Effect-owned model model pose contradicts its original rig owner.");
        }
        return pose;
    }

    public static void RequireStagedPose(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource)
    {
        NativeBattleEffectArena skeleton = NativeBattleTemporaryMemory.ReadView(staging, SkeletonAddress(resource), resource.PoseSource.Skeleton.Length);
        NativeBattleDecodedPose actual = NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, resource.PoseSource.BoneCount);
        NativeBattleDecodedPose expected = RequirePose(model, resource);
        if (actual.Translation != expected.Translation || actual.HasScale != expected.HasScale || !actual.Bones.Select(bone => bone.Rotation).SequenceEqual(expected.Bones.Select(bone => bone.Rotation)) || expected.HasScale && !actual.Bones.Select(bone => bone.Scale).SequenceEqual(expected.Bones.Select(bone => bone.Scale)))
        {
            throw new InvalidDataException("Effect-owned model drawing requires the decoder's actual published skeleton pose.");
        }
    }

    public static NativeBattleFileStaging PublishMatrices(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource, ImmutableArray<NativeBattleActionMatrix> matrices, Func<int, ushort?> padding)
    {
        uint skeleton = SkeletonAddress(resource);
        for (int bone = 0; bone < matrices.Length; bone++)
        {
            uint address = checked(skeleton + 32u + (uint)(bone * 48));
            NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(matrices[bone], padding(bone));
            staging = NativeBattleTemporaryMemory.PublishView(staging, address, StoreMatrix(NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, matrix), 32);
        }
        return staging;
    }

    public static ImmutableArray<NativeBattlePackedMatrix32> CaptureBones(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource)
    {
        ImmutableArray<NativeBattlePackedMatrix32>.Builder bones = ImmutableArray.CreateBuilder<NativeBattlePackedMatrix32>(resource.PoseSource.BoneCount);
        for (int bone = 0; bone < resource.PoseSource.BoneCount; bone++)
        {
            (byte[] bytes, bool[] known) = staging.ReadSnapshot(checked(SkeletonAddress(resource) + 32u + (uint)(bone * 48)), 32);
            bones.Add(new(bytes.ToImmutableArray(), known.ToImmutableArray()));
        }
        return bones.MoveToImmutable();
    }

    public static uint SkeletonAddress(NativeBattleEffectOwnedModelResource resource) => checked(resource.Address + (uint)resource.Section(0));

    public static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));

    public static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => new(memory.GeometryRegisters ?? throw new NotSupportedException("Effect-owned model body requires its actual geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect-owned model body requires its actual data registers."));

    public static NativeBattleGfGeometryBatch Combine(NativeBattleGfGeometryBatch first, NativeBattleGfGeometryBatch second)
    {
        return new(first.Attempts.AddRange(second.Attempts.Select(attempt => attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + first.AllocationBytes) })), checked(first.AllocationBytes + second.AllocationBytes));
    }

    public static NativeBattlePackedMatrix32 CaptureTaskMatrix(NativeBattleModulePoolTask task, int offset)
    {
        byte[] bytes = task.Payload.AsSpan().Slice(offset - 12, 32).ToArray();
        bool[] known = Enumerable.Range(offset, 32).Select(index => task.UnknownPayloadRanges is not { } ranges || !ranges.Any(range => index >= range.Offset && index < range.Offset + range.Length)).ToArray();
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    public static NativeBattleEffectArena CaptureTaskModel(NativeBattleModulePoolTask task)
    {
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 128);
        for (int offset = 0; offset < 128; offset++)
        {
            int index = offset + 0x30;
            if (task.UnknownPayloadRanges is not { } ranges || !ranges.Any(range => index >= range.Offset && index < range.Offset + range.Length))
            {
                result = result.Write(offset, [task.Payload[index - 12]]);
            }
        }
        return result;
    }

    public static NativeBattleEffectArena StoreMatrix(NativeBattleEffectArena arena, int offset, NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        arena = arena.MarkUnknown(offset, 32);
        for (int index = 0; index < 32; index++)
        {
            if (matrix.Known[index])
            {
                arena = arena.Write(offset + index, [matrix.Bytes[index]]);
            }
        }
        return arena;
    }

    public static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    public static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
