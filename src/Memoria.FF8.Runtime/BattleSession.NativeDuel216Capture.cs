using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleDuel216CaptureStep DrawNativeDuel216CaptureGround(NativeBattleDuel216Frame frame, NativeBattleDuel216CameraTransform transform, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native69A790 requires its actual stage rows.");
        NativeBattleStageGroup group = stage.Groups[1];
        if ((group.Flags & 1) == 0)
        {
            return new(frame, ordering);
        }
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 44);
        uint context = allocation.Address;
        int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native69A790 requires its actual projected CPU workspace.")) * 8);
        uint workspace = checked(0x196B018u - (uint)capacity);
        NativeBattleFileStaging staging = allocation.Staging.Write(context + 4, BitConverter.GetBytes(workspace)).Write(context + 16, frame.Staging.Read(0x2508878, 4));
        staging = staging.Write(context + 20, BitConverter.GetBytes((short)0)).Write(context + 22, BitConverter.GetBytes((short)0)).Write(context + 24, BitConverter.GetBytes((short)320)).Write(context + 26, BitConverter.GetBytes((short)216));
        staging = staging.Write(context + 28, BitConverter.GetBytes(group.ColorFlags)).Write(context + 32, BitConverter.GetBytes(uint.MaxValue)).Write(context + 36, BitConverter.GetBytes(group.ColorDepth)).Write(context + 40, new byte[4]);
        frame = frame with { Staging = staging };
        NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native69A790 requires the current ground skeleton.");
        NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
        group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), transform.Matrix, 0) };
        NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native69A790 requires its current primitive process.");
        var gate = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native69A790 requires its actual renderer capability."));
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = gate.Process } };
        if (!gate.Draw)
        {
            return new(frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 44) }, ordering);
        }
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Native69A790 requires its authenticated model color arithmetic.");
        ImmutableArray<NativeBattleRawMesh> meshes = RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
        (byte[] retained, _) = frame.Staging.ReadSnapshot(workspace, capacity);
        NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, group.Matrices, transform.Projection, group.ColorFlags, uint.MaxValue, retained.ToImmutableArray(), colors, 16, new(0, 0, 320, 216), 17, group.ColorDepth);
        NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native69A790 requires its actual private cursor."), ordering, drawn.Packets);
        uint cursor = checked(0x20DFAB8u + (uint)written.Cursor);
        staging = frame.Staging.Write(workspace, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten)).Write(0x2508874, BitConverter.GetBytes(cursor));
        frame = frame with { Memory = written.Memory with { PrimitiveProcess = gate.Process.PublishModelColor(group.ColorFlags).Publish50FDF0(cursor, null) }, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 44), Effect = frame.Effect with { DrawArenaOffset = written.Cursor } };
        return new(frame, written.Ordering);
    }
}
