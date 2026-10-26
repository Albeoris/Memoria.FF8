using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect218Frame Frame) DrawNativeEffect218CaptureStage(NativeBattleGraphicsState graphics, NativeBattleEffect218Frame frame, NativeBattlePackedMatrix32 matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool hardware)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect218 capture requires its actual stage rows.");
        NativeBattleStageGroup group = stage.Groups[1];
        int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect218 capture requires its actual vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)capacity);
        (frame, uint temporary) = Allocate(frame, 44);
        NativeBattleEffectArena context = Context(frame, temporary, 44)
            .Write(4, BitConverter.GetBytes(scratchAddress))
            .Write(0x10, BitConverter.GetBytes(uint.MaxValue))
            .Write(0x14, BitConverter.GetBytes((short)0))
            .Write(0x16, BitConverter.GetBytes((short)0))
            .Write(0x18, BitConverter.GetBytes((short)320))
            .Write(0x1A, BitConverter.GetBytes((short)216))
            .Write(0x1C, BitConverter.GetBytes(group.ColorFlags))
            .Write(0x20, BitConverter.GetBytes(uint.MaxValue))
            .Write(0x24, BitConverter.GetBytes(group.ColorDepth))
            .Write(0x28, BitConverter.GetBytes(0));
        frame = PublishContext(frame, temporary, context, 44);
        if ((group.Flags & 1) != 0)
        {
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Effect218 capture requires its current stage skeleton.");
            NativeBattleDecodedPose decoded = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            ImmutableArray<NativeBattleActionMatrix> local = NativeBattlePoseMatrices.StageBoneMatrices(source, decoded);
            (frame, uint boneTemporary) = Allocate(frame, 88);
            NativeBattleEffectArena prepared = NativeBattleBodyBoneTemporary.Prepare508C90(source, decoded, local, Context(frame, boneTemporary, 88));
            frame = Release(PublishContext(frame, boneTemporary, prepared, 88), 88);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(local, matrix.ReadMatrix(), 0) };
            NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
            NativeBattleStagePhysicalSource physical = definition.NativeStageSource?.PhysicalSource ?? throw new NotSupportedException("Effect218 capture requires its loaded physical stage source.");
            physical.Validate();
            uint geometryAddress = physical.GroupGeometryAddress(1) ?? throw new NotSupportedException("Effect218 capture requires its current stage geometry address.");
            int geometryLength = physical.GroupGeometryLength(1) ?? throw new NotSupportedException("Effect218 capture requires its current stage geometry extent.");
            NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Effect218 capture requires its actual projected workspace.");
            byte[] ReadGeometry(int offset, int length)
            {
                if (offset < 0 || length < 0 || offset > geometryLength - length)
                {
                    throw new InvalidDataException("Effect218 capture leaves its current loaded stage geometry.");
                }
                return staging.Read(checked(geometryAddress + (uint)offset), length);
            }
            NativeBattleModelContext44Step drawn = NativeBattleModelContext44.Draw(frame.Memory, context, group.Matrices.Select(bone => NativeBattlePackedMatrix32.FromMatrix(bone, null)).ToImmutableArray(), ReadGeometry, geometryAddress, NativeDeformedBodyView(staging, scratchAddress, capacity), capacity, projection, colors, hardware, 17, 16, NativeEffect218CommonCursor(frame.Memory));
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context44, 44);
            frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, scratchAddress, drawn.ProjectedScratch, capacity) };
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect218 capture requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect218 capture requires its current COMMON packet process."), primitive.Pending, drawn.Batch, 0);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = written.Process } };
        }
        return (graphics, Release(frame, 44));
    }
}
