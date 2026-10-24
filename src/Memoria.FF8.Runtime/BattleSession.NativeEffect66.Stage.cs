using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect66Frame Frame) DrawNativeEffect66Stage(NativeBattleGraphicsState graphics, NativeBattleEffect66Frame frame, uint temporaryAddress)
    {
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect66 requires its current stage rows.");
        NativeBattleStageGroup group = stage.Groups[1] with { Flags = (byte)(stage.Groups[1].Flags & 0xFD) };
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect66 stage drawing requires its current camera.");
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Effect66 stage drawing requires its current temporary memory.");
        int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect66 stage drawing requires its actual projected CPU capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)capacity);
        NativeBattleEffectArena temporary = NativeBattleEffect66Controller.Context(frame, temporaryAddress, 44);
        temporary = temporary.Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216)).Write(0x20, BitConverter.GetBytes(uint.MaxValue));
        temporary = NativeBattleEffectNodeContextMemory.Copy(NativeDeformedBodyView(staging, 0x1D969A8, 4), 0, temporary, 0x10, 4);
        group = NativeBattleCinematicStageShatter49.RestoreStageGroup508C90(group);
        group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(group.Matrices, camera.CapturePackedView1D97778().ReadMatrix(), 0) };
        NativeStage = stage with { Groups = stage.Groups.SetItem(1, group) };
        frame = NativeBattleEffect66Controller.PublishContext(frame, temporaryAddress, temporary, 44);
        if ((group.Flags & 1) == 0)
        {
            return (graphics, frame);
        }
        temporary = temporary.Write(0x1C, BitConverter.GetBytes(group.ColorFlags)).Write(0x24, BitConverter.GetBytes(group.ColorDepth));
        frame = NativeBattleEffect66Controller.PublishContext(frame, temporaryAddress, temporary, 44);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect66 stage drawing requires its ordinary renderer process.");
        var gate = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect66 stage drawing requires its actual renderer capability."));
        frame = frame with { Memory = frame.Memory with { PrimitiveProcess = gate.Process } };
        if (!gate.Draw)
        {
            return (graphics, frame);
        }
        process = gate.Process.PublishModelColor(group.ColorFlags);
        NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect66 stage drawing requires its current color arithmetic.");
        ImmutableArray<NativeBattleRawMesh> meshes = RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == 1).ToImmutableArray();
        foreach (NativeBattleRawMesh mesh in meshes)
        {
            staging = frame.Staging ?? throw new InvalidDataException("Effect66 stage drawing lost its current addressed memory.");
            (byte[] retained, _) = staging.ReadSnapshot(scratchAddress, capacity);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, group.Matrices, camera.Projection ?? throw new NotSupportedException("Effect66 stage drawing requires its current projection registers."), group.ColorFlags, 1, retained.ToImmutableArray(), colors, 4, new(0, 0, 320, 216), 17, group.ColorDepth);
            if (drawn.ProjectedBytesWritten != 0)
            {
                staging = staging.Write(scratchAddress, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
            }
            ushort[] counts = new ushort[4];
            foreach (NativeBattleRawPolygon polygon in mesh.Polygons)
            {
                int kind = (polygon.Quad ? 1 : 0) + (polygon.Bytes.Length == (polygon.Quad ? 24 : 20) ? 2 : 0);
                counts[kind] = checked((ushort)(counts[kind] + 1));
            }
            temporary = NativeBattleEffect66Controller.Context(frame, temporaryAddress, 44).MarkUnknown(0, 4);
            for (int index = 0; index < counts.Length; index++)
            {
                temporary = temporary.Write(8 + index * 2, BitConverter.GetBytes(counts[index]));
            }
            frame = NativeBattleEffect66Controller.PublishContext(frame with { Staging = staging }, temporaryAddress, temporary, 44);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect66 stage drawing requires its current ordinary ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(process, primitive.Pending, drawn.Packets);
            process = written.Process;
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process } };
        }
        return (graphics, frame);
    }
}
