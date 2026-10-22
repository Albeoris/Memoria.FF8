using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDoom8ModelDrawInputs(NativeBattlePackedMatrix32 Camera, NativeBattleCameraProjection Projection, NativeBattleShadowSource Shadow, NativeBattlePrimitiveCapabilities Capabilities, NativeBattleSummonColors Colors);
public sealed record NativeBattleDoom8ModelDrawStep(NativeBattleDoom8Frame Frame, NativeBattleFileStaging Staging, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleDoom8ModelDrawing
{
    public const uint ProjectedAddress = 0x275E838;
    public const int ProjectedLength = 302 * 8;
    public const uint FarInputAddress = 0x2758884;

    public static NativeBattleFileStaging PublishDecodedPose(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelState state, NativeBattleEffectOwnedModelResource resource) => NativeBattleEffectOwnedModelRendering.PublishDecodedPose(staging, state, resource);

    public static NativeBattleDoom8ModelDrawStep Draw8CC4D0(NativeBattleDoom8Frame frame, NativeBattleFileStaging staging, uint address, NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource, NativeBattleDoom8ModelDrawInputs inputs)
    {
        NativeBattleModulePoolTask task = RequireModel(frame, address, model, resource);
        NativeBattleEffectOwnedModelRenderFrame current = new(frame.Memory, task, model, resource, frame.State.ReadView(ProjectedAddress, ProjectedLength), ProjectedAddress, ProjectedLength, frame.State.ReadDword(FarInputAddress), frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.CursorAddress));
        NativeBattleEffectOwnedModelRenderStep drawn = NativeBattleEffectOwnedModelRendering.DrawNormal(current, staging, new(inputs.Camera, inputs.Projection, inputs.Shadow, inputs.Capabilities, inputs.Colors));
        frame = WriteTask(frame with { Memory = drawn.Frame.Memory, State = frame.State.WriteView(ProjectedAddress, drawn.Frame.ProjectedScratch, ProjectedLength).WriteDword(NativeBattleDoom8ReaperAppearance.CursorAddress, drawn.Frame.PacketAddress), Staging = drawn.Staging }, address, drawn.Frame.Task);
        return new(frame, drawn.Staging, drawn.Batch);
    }

    public static NativeBattleDoom8ModelDrawStep Draw8CC6B0(NativeBattleDoom8Frame frame, NativeBattleFileStaging staging, uint address, NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource, NativeBattleDoom8ModelDrawInputs inputs)
    {
        NativeBattleModulePoolTask task = RequireModel(frame, address, model, resource);
        RequireStagedPose(staging, model, resource);
        NativeBattlePackedMatrix32 world = CaptureTaskMatrix(task, 0x70);
        staging = PrepareLocalBones508C90(staging, model, resource);
        frame = NativeBattleDoom8ReaperAppearance.PrepareCamera8CC6B0(frame, world, inputs.Camera);
        NativeBattleEffectLocalBounds bounds = new(new(Signed(task, 0x64), Signed(task, 0x66), Signed(task, 0x68)), new(Signed(task, 0x6A), Signed(task, 0x6C), Signed(task, 0x6E)));
        NativeBattleDoom8ModelDrawStep shadow = DrawShadow(frame, staging, task, world.ReadMatrix(), bounds, inputs, 4112);
        frame = shadow.Frame;
        staging = shadow.Staging;
        ImmutableArray<NativeBattlePackedMatrix32> bones = CaptureBones(staging, resource);
        NativeBattleDoom8AppearanceModel appearance = new(checked(address + 0x30), CaptureTaskModel(task), ReadGeometry(staging, resource), bones);
        NativeBattleDoom8AppearanceStep drawn = NativeBattleDoom8ReaperAppearance.Draw8CC7B0(frame, staging, appearance, inputs.Projection);
        staging = PrepareLocalBones508C90(drawn.Staging, model, resource);
        return new(drawn.Frame with { Staging = staging }, staging, Combine(shadow.Batch, drawn.Batch));
    }

    public static NativeBattleFileStaging PrepareLocalBones508C90(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource) => NativeBattleEffectOwnedModelRendering.PrepareLocalBones508C90(staging, model, resource);

    private static NativeBattleDoom8ModelDrawStep DrawShadow(NativeBattleDoom8Frame frame, NativeBattleFileStaging staging, NativeBattleModulePoolTask task, NativeBattleActionMatrix world, NativeBattleEffectLocalBounds bounds, NativeBattleDoom8ModelDrawInputs inputs, int bucket)
    {
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(staging, Registers(frame.Memory), world, Signed(task, 0x54), bounds, task.ReadDword(0x5C), inputs.Projection with { View = inputs.Camera.ReadMatrix() }, frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Doom shadow requires its current depth controls."), inputs.Shadow, inputs.Capabilities.GouraudAlpha, bucket, 16);
        staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 0x78);
        uint cursor = frame.State.ReadDword(NativeBattleDoom8ReaperAppearance.CursorAddress);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, State = frame.State.WriteDword(NativeBattleDoom8ReaperAppearance.CursorAddress, checked(cursor + (uint)shadow.Batch.AllocationBytes)) };
        return new(frame, staging, shadow.Batch);
    }

    private static NativeBattleModulePoolTask RequireModel(NativeBattleDoom8Frame frame, uint address, NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource)
    {
        NativeBattleModulePoolTask task = NativeBattleDoom8Controller.Read(frame, address);
        if (model.TaskAddress != address)
        {
            throw new InvalidDataException("Doom model drawing requires the selected canonical task owner.");
        }
        NativeBattleEffectOwnedModel.Validate(task, model, resource);
        _ = RequirePose(model, resource);
        return task;
    }

    private static NativeBattleDecodedPose RequirePose(NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource) => NativeBattleEffectOwnedModelRendering.RequirePose(model, resource);

    private static void RequireStagedPose(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelState model, NativeBattleEffectOwnedModelResource resource) => NativeBattleEffectOwnedModelRendering.RequireStagedPose(staging, model, resource);

    private static NativeBattleFileStaging PublishMatrices(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource, ImmutableArray<NativeBattleActionMatrix> matrices, Func<int, ushort?> padding) => NativeBattleEffectOwnedModelRendering.PublishMatrices(staging, resource, matrices, padding);

    private static ImmutableArray<NativeBattlePackedMatrix32> CaptureBones(NativeBattleFileStaging staging, NativeBattleEffectOwnedModelResource resource) => NativeBattleEffectOwnedModelRendering.CaptureBones(staging, resource);

    private static uint SkeletonAddress(NativeBattleEffectOwnedModelResource resource) => NativeBattleEffectOwnedModelRendering.SkeletonAddress(resource);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => NativeBattleEffectOwnedModelRendering.Signed(task, offset);
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleEffectMemory memory) => NativeBattleEffectOwnedModelRendering.Registers(memory);
    private static NativeBattleDoom8Frame WriteTask(NativeBattleDoom8Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };

    private static NativeBattleGfGeometryBatch Combine(NativeBattleGfGeometryBatch first, NativeBattleGfGeometryBatch second) => NativeBattleEffectOwnedModelRendering.Combine(first, second);

    private static NativeBattlePackedMatrix32 CaptureTaskMatrix(NativeBattleModulePoolTask task, int offset) => NativeBattleEffectOwnedModelRendering.CaptureTaskMatrix(task, offset);

    private static NativeBattleEffectArena CaptureTaskModel(NativeBattleModulePoolTask task) => NativeBattleEffectOwnedModelRendering.CaptureTaskModel(task);

    private static NativeBattleEffectArena StoreMatrix(NativeBattleEffectArena arena, int offset, NativeBattlePackedMatrix32 matrix) => NativeBattleEffectOwnedModelRendering.StoreMatrix(arena, offset, matrix);

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => NativeBattleEffectOwnedModelRendering.Dword(arena, offset, value);

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value) => NativeBattleEffectOwnedModelRendering.Word(arena, offset, value);
}
