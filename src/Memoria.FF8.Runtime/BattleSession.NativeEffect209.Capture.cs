using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect209Frame CaptureNativeEffect209Stage(ref NativeBattleGraphicsState graphics, NativeBattleEffect209Frame frame, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs)
    {
        frame = frame with { Memory = NativeBattleGravija214Environment.Clear45BEE0(frame.Memory, new(576, 256, 320, 216), 0, 0, 0) };
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Effect209 capture requires its current stage rows.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect209 capture requires its current camera.");
        int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect209 capture requires its actual projected vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)capacity);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Effect209 capture requires its current temporary memory.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 44);
        NativeBattleEffectArena temporary = NativeDeformedBodyView(allocation.Staging, allocation.Address, 44);
        NativeBattleEffectArena flags = NativeDeformedBodyView(allocation.Staging, 0x1D969A8, 4);
        temporary = NativeBattleEffect209Lower.CreateCaptureContext7D30(temporary, scratchAddress, flags);
        staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 44);
        frame = frame with { Staging = staging };
        int minimum = 0x1121;
        for (int index = 0; index < 4; index++)
        {
            if ((stage.Groups[index].Flags & 1) != 0)
            {
                minimum = Math.Min(minimum, resources.StageDepthRows[index].TableBaseBucket);
            }
        }
        frame = EmitEnvironment(ref graphics, frame, 0, 0, minimum);
        for (int index = 0; index < 4; index++)
        {
            stage = NativeStage ?? throw new InvalidDataException("Effect209 capture lost its current stage rows.");
            NativeBattleStageGroup group = NativeBattleCinematicStageShatter49.RestoreStageGroup508C90(stage.Groups[index]);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(group.Matrices, camera.CapturePackedView1D97778().ReadMatrix(), 0) };
            NativeStage = stage with { Groups = stage.Groups.SetItem(index, group) };
            if ((group.Flags & 1) == 0)
            {
                continue;
            }
            temporary = NativeDeformedBodyView(frame.Staging!, allocation.Address, 44).Write(0x1C, BitConverter.GetBytes(group.ColorFlags)).Write(0x24, BitConverter.GetBytes(group.ColorDepth));
            frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, allocation.Address, temporary, 44) };
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect209 capture requires its ordinary renderer process.");
            var gate = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect209 capture requires its actual renderer capability."));
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = gate.Process } };
            if (!gate.Draw)
            {
                continue;
            }
            process = gate.Process.PublishModelColor(group.ColorFlags);
            ImmutableArray<NativeBattleRawMesh> meshes = RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == index).ToImmutableArray();
            foreach (NativeBattleRawMesh mesh in meshes)
            {
                staging = frame.Staging ?? throw new InvalidDataException("Effect209 capture lost its current scratch memory.");
                (byte[] retained, _) = staging.ReadSnapshot(scratchAddress, capacity);
                NativeBattleEffect209StageDepth depth = resources.StageDepthRows[index];
                NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(mesh, group.Matrices, camera.Projection ?? throw new NotSupportedException("Effect209 capture requires its current projection registers."), group.ColorFlags, uint.MaxValue, retained.ToImmutableArray(), inputs.Colors, depth.DepthShift, new(0, 0, 320, 216), depth.TableBaseBucket, group.ColorDepth, pilotIdentity: frame.PresentationIdentity is { } identity ? identity with { ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(mesh), Primitive = index } : null, pilotContinuous: false);
                frame = frame with { Memory = NativeBattleEffect66StageRegisters.Publish5099D0(frame.Memory with { PrimitiveProcess = process }, mesh, group.Matrices, drawn.ProjectedScratch, group.ColorFlags, temporary, inputs.Colors, depth.TableBaseBucket, depth.DepthShift) };
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
                temporary = NativeDeformedBodyView(staging, allocation.Address, 44).MarkUnknown(0, 4);
                for (int count = 0; count < counts.Length; count++)
                {
                    temporary = temporary.Write(8 + count * 2, BitConverter.GetBytes(counts[count]));
                }
                staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 44);
                frame = frame with { Staging = staging };
                NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect209 capture requires its current ordering table.");
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(frame.Memory.PrimitiveProcess!, primitive.Pending, drawn.Packets);
                process = written.Process;
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = process } };
            }
        }
        frame = EmitEnvironment(ref graphics, frame, 576, 256, 0x1121);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 44) };
    }

    private static NativeBattleEffect209Frame EmitEnvironment(ref NativeBattleGraphicsState graphics, NativeBattleEffect209Frame frame, short x, short y, int bucket)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect209 capture requires its ordinary packet process.");
        int cursor = process.ByteCursor ?? throw new NotSupportedException("Effect209 capture requires its actual ordinary cursor.");
        (byte[] bytes, bool[] known) = process.ReadCommonPacketSnapshot(cursor, 64);
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 64);
        for (int index = 0; index < 64; index++)
        {
            if (known[index])
            {
                retained = retained.Write(index, bytes.AsSpan(index, 1));
            }
        }
        ImmutableArray<byte> environment = process.Software?.EnvironmentBuffers?.CurrentGlobal ?? throw new NotSupportedException("Effect209 capture requires its actual current DRAWENV.");
        NativeBattleGfGeometryBatch batch = NativeBattleEffect209CapturePackets.Environment7E90(retained, environment, x, y, bucket);
        return DeliverNativeEffect209Batch(ref graphics, frame, new(batch, 0x1D8E054));
    }
}
