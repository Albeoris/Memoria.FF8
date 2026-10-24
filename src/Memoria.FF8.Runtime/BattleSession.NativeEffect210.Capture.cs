using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect210Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect210Frame CaptureNativeEffect210Stage(ref NativeBattleGraphicsState graphics, NativeBattleEffect210Frame frame, NativeBattleEffect210Capture9440 operation)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect210 capture requires its current stage rows.");
        stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0x8FC, ColorFlags = 0 }).ToImmutableArray() };
        NativeStage = stage;
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect210 capture requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect210 capture requires its current projection.");
        NativeBattleEffect210CapturePlan plan = NativeBattleEffect210CaptureMath.Prepare(NativeBattleEffect210Controller.ReadBytes(frame, operation.PointAddress, 8), operation.Bounds12, camera.FocalLength ?? throw new NotSupportedException("Effect210 capture requires its current focal length."));
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect210 capture requires its primitive process.");
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Effect210 capture requires its actual drawing environments.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Effect210 capture requires its current buffer selector.");
        frame = DeliverNativeEffect210Batch(ref graphics, frame, new(NativeBattleEffect210CapturePackets.Environment(ReadNativeEffect210PacketBytes(frame, 64), buffer == 0 ? environments.Buffer1 : environments.Buffer0), CursorGlobal));
        NativeBattleCameraProjection captureProjection = projection with { OffsetX = 96, OffsetY = 128 };
        NativeCamera = camera with { Projection = captureProjection };
        frame = NativeBattleEffect210Controller.LoadMatrix(frame, plan.Matrix);
        if ((stage.Groups[0].Flags & 1) != 0)
        {
            int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect210 capture requires its current vertex capacity.")) * 8);
            uint scratchAddress = checked(0x196B018u - (uint)capacity);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect210 capture requires its temporary memory."), 44);
            NativeBattleEffectArena temporary = NativeDeformedBodyView(allocation.Staging, allocation.Address, 44)
                .Write(4, BitConverter.GetBytes(scratchAddress))
                .Write(0x10, BitConverter.GetBytes(frame.State.ReadDword(SecondCursorGlobal)))
                .Write(0x14, BitConverter.GetBytes((short)0))
                .Write(0x16, BitConverter.GetBytes((short)0))
                .Write(0x18, BitConverter.GetBytes((short)320))
                .Write(0x1A, BitConverter.GetBytes((short)216))
                .Write(0x1C, BitConverter.GetBytes(stage.Groups[0].ColorFlags))
                .Write(0x20, BitConverter.GetBytes(uint.MaxValue))
                .Write(0x24, BitConverter.GetBytes(stage.Groups[0].ColorDepth))
                .Write(0x28, BitConverter.GetBytes(0));
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 44);
            NativeBattleStageGroup group = stage.Groups[0];
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Effect210 capture requires its current stage skeleton.");
            NativeBattleDecodedPose decoded = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            ImmutableArray<NativeBattleActionMatrix> local = NativeBattlePoseMatrices.StageBoneMatrices(source, decoded);
            NativeBattleTemporaryAllocation boneAllocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            NativeBattleEffectArena boneTemporary = NativeBattleBodyBoneTemporary.Prepare508C90(source, decoded, local, NativeDeformedBodyView(boneAllocation.Staging, boneAllocation.Address, 88));
            staging = NativeBattleTemporaryMemory.PublishView(boneAllocation.Staging, boneAllocation.Address, boneTemporary, 88);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(local, plan.Matrix.ReadMatrix(), 0) };
            NativeStage = stage with { Groups = stage.Groups.SetItem(0, group) };
            frame = frame with { Staging = staging };
            NativeBattleStagePhysicalSource physical = definition.NativeStageSource?.PhysicalSource ?? throw new NotSupportedException("Effect210 capture requires its current physical stage source.");
            physical.Validate();
            uint geometryAddress = physical.GroupGeometryAddress(0) ?? throw new NotSupportedException("Effect210 capture requires its current stage geometry address.");
            int geometryLength = physical.GroupGeometryLength(0) ?? throw new NotSupportedException("Effect210 capture requires its current stage geometry extent.");
            NativeBattleFileStaging geometryStaging = frame.Staging!;
            byte[] ReadGeometry(int offset, int length)
            {
                if (offset < 0 || length < 0 || offset > geometryLength - length)
                {
                    throw new InvalidDataException("Effect210 capture leaves its current stage geometry extent.");
                }
                return geometryStaging.Read(checked(geometryAddress + (uint)offset), length);
            }
            NativeBattleModelContext44Step drawn = NativeBattleModelContext44.Draw(frame.Memory, temporary, group.Matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), ReadGeometry, geometryAddress, NativeDeformedBodyView(frame.Staging!, scratchAddress, capacity), capacity, captureProjection, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect210 capture requires its color tables."), definition.NativeGraphics.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect210 capture requires its renderer capability."), 17, 16, frame.State.ReadDword(CursorGlobal));
            staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, allocation.Address, drawn.Context44, 44);
            staging = NativeBattleTemporaryMemory.PublishView(staging, scratchAddress, drawn.ProjectedScratch, capacity);
            frame = frame with { Memory = drawn.Memory, Staging = staging };
            frame = DeliverNativeEffect210Batch(ref graphics, frame, new(drawn.Batch, CursorGlobal));
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 44) };
        }
        frame = DeliverNativeEffect210Batch(ref graphics, frame, new(NativeBattleEffect210CapturePackets.Black(ReadNativeEffect210PacketBytes(frame, 24)), CursorGlobal, 1));
        NativeCamera = NativeCamera! with { Projection = NativeCamera.Projection! with { OffsetX = projection.OffsetX, OffsetY = projection.OffsetY } };
        frame = DeliverNativeEffect210Batch(ref graphics, frame, new(NativeBattleEffect210CapturePackets.Area(ReadNativeEffect210PacketBytes(frame, 12)), CursorGlobal));
        return DeliverNativeEffect210Batch(ref graphics, frame, new(NativeBattleEffect210CapturePackets.Offset(ReadNativeEffect210PacketBytes(frame, 12)), CursorGlobal));
    }

    private static NativeBattleEffectArena ReadNativeEffect210PacketBytes(NativeBattleEffect210Frame frame, int length)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Effect210 capture requires its current packet arena.");
        return NativeBattleEffectNodeContextMemory.Copy(arena, NativeBattleWaterExecutionFields.ArenaOffset(frame.State.ReadDword(CursorGlobal), length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }
}
