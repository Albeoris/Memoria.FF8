using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleRinoa89Frame Frame, NativeBattleGraphicsState Graphics) CaptureNativeRinoa89Scene(NativeBattleRinoa89Frame frame, NativeBattleGraphicsState graphics, NativeBattleRinoa89Resources resources, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Wishing Star capture requires its initialized primitive frame.");
        NativeBattleRinoa89CaptureStep begun = NativeBattleRinoa89Capture.Begin6FBEE0(frame, primitive.Pending);
        frame = begun.Frame;
        graphics = graphics with { PrimitiveFrame = primitive with { Pending = begun.Ordering } };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging, 76);
        uint context = allocation.Address;
        frame = frame with { Staging = allocation.Staging };
        int capacity = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native6FC0A0 requires its actual projected model workspace.")) * 8);
        uint workspace = checked(0x196B018u - (uint)capacity);
        frame = frame with { Staging = frame.Staging.Write(context + 36, BitConverter.GetBytes(workspace)).Write(context + 52, BitConverter.GetBytes((short)0)).Write(context + 54, BitConverter.GetBytes((short)0)).Write(context + 56, BitConverter.GetBytes((short)320)).Write(context + 58, BitConverter.GetBytes((short)216)).Write(context + 64, BitConverter.GetBytes(uint.MaxValue)) };
        (byte[] stateBytes, bool[] stateKnown) = frame.Staging.ReadSnapshot(0x1D969A8, 4);
        for (int offset = 0; offset < 4; offset++)
        {
            frame = frame with { Staging = stateKnown[offset] ? frame.Staging.Write(context + 48 + (uint)offset, stateBytes.AsSpan(offset, 1)) : frame.Staging.MarkUnknown(context + 48 + (uint)offset, 1) };
        }
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native6FC0A0 requires its current four stage rows.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6FC0A0 requires its previously published camera matrix.");
        for (int index = 0; index < 4; index++)
        {
            NativeBattleStageGroup group = stage.Groups[index];
            if ((group.Flags & 1) == 0)
            {
                continue;
            }
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native6FC0A0 requires the current stage skeleton.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), view, 0) };
            frame = frame with { Staging = frame.Staging.Write(context + 60, BitConverter.GetBytes(group.ColorFlags)).Write(context + 68, BitConverter.GetBytes(group.ColorDepth)) };
            int directory = checked((int)(0x14AF6E0 - NativeBattleRinoa89Resources.StaticAddress)) + index * 8;
            int bucket = BinaryPrimitives.ReadInt32LittleEndian(resources.StaticImage.Data.AsSpan().Slice(directory));
            byte shift = checked((byte)BinaryPrimitives.ReadInt32LittleEndian(resources.StaticImage.Data.AsSpan().Slice(directory + 4)));
            ImmutableArray<NativeBattleRawMesh> meshes = (definition.NativeStageSource?.Geometry ?? throw new NotSupportedException("Native6FC0A0 requires its original stage geometry.")).Where(mesh => mesh.StageGroup == index).ToImmutableArray();
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native6FC0A0 requires its current renderer process.");
            var gate = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native6FC0A0 requires its actual renderer capability."));
            frame = frame with { Memory = frame.Memory with { PrimitiveProcess = gate.Process } };
            if (gate.Draw)
            {
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = gate.Process.PublishModelColor(group.ColorFlags) } };
                (byte[] retained, _) = frame.Staging.ReadSnapshot(workspace, capacity);
                NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(meshes, group.Matrices, projection, group.ColorFlags, uint.MaxValue, retained.ToImmutableArray(), colors, shift, new(0, 0, 320, 216), bucket, group.ColorDepth);
                NativeBattleRinoa89CaptureStep written = NativeBattleRinoa89Capture.AppendCommon(frame, graphics.PrimitiveFrame!.Pending, drawn.Packets);
                frame = written.Frame with { Staging = written.Frame.Staging.Write(workspace, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten)) };
                NativeBattlePrimitiveProcess published = frame.Memory.PrimitiveProcess!;
                uint bank = published.PacketBank switch { "off_B6D070" => 0x1901018u, "off_B6D094" => 0x199B018u, _ => throw new NotSupportedException("Native6FC0A0 requires its actual COMMON bank.") };
                frame = frame with { Memory = frame.Memory with { PrimitiveProcess = published.Publish50FDF0(checked(bank + (uint)published.PacketBuffer * (uint)published.CommonPacketNominalCapacity + (uint)published.ByteCursor!.Value), null) } };
                graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            }
            stage = stage with { Groups = stage.Groups.SetItem(index, group with { Flags = (byte)(group.Flags & ~2) }) };
        }
        NativeStage = stage;
        for (int index = 3; index < 7; index++)
        {
            NativeBattleGraphicsModel model = graphics.Models[index];
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            model = model with { Flags0 = (ushort)(model.Flags0 | 4) };
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(view, model.GeometryWorldMatrix ?? throw new NotSupportedException("Native6FC0A0 requires its actual retained enemy world matrix."));
            frame = frame with { Staging = NativeBattleRinoa89Capture.PublishMatrix56C2F0(frame.Staging, context, matrix) };
            frame = frame with { Staging = frame.Staging.Write(context + 20, BitConverter.GetBytes(matrix.X)).Write(context + 24, BitConverter.GetBytes(matrix.Y)).Write(context + 28, BitConverter.GetBytes(matrix.Z)).Write(context + 36, BitConverter.GetBytes(workspace)).Write(context + 52, BitConverter.GetBytes((short)0)).Write(context + 54, BitConverter.GetBytes((short)0)).Write(context + 56, BitConverter.GetBytes((short)320)).Write(context + 58, BitConverter.GetBytes((short)216)).Write(context + 60, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Native6FC0A0 requires its actual enemy color."))).Write(context + 64, BitConverter.GetBytes(model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native6FC0A0 requires its actual enemy object mask."))).Write(context + 72, new byte[4]) };
            NativeEffectMemory = frame.Memory;
            NativeFileStaging = frame.Staging;
            short depth = BinaryPrimitives.ReadInt16LittleEndian(frame.Staging.Read(context + 68, 2));
            graphics = DrawNativeEffectModel5099D0(graphics, index, matrix, projection, 4112, 16, 4, NativeBattleRinoa89Capture.TableAddress, depth, context + 64);
            frame = frame with { Memory = NativeEffectMemory!, Staging = NativeFileStaging! };
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging, 76) };
        NativeBattleRinoa89CaptureStep ended = NativeBattleRinoa89Capture.End6FBEE0(frame, graphics.PrimitiveFrame!.Pending);
        return (ended.Frame, graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = ended.Ordering } });
    }
}
