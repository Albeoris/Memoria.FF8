using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWall189State State, NativeBattleGraphicsState Graphics, int Cursor) CaptureNativeWall189(NativeBattleWall189State state, NativeBattleGraphicsState graphics, NativeBattleVector center, int cursor)
    {
        NativeBattlePrimitiveProcess process = NativeEffectMemory!.PrimitiveProcess ?? throw new NotSupportedException("Native Wall capture requires its actual primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native Wall capture requires its current draw environment selector.");
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native Wall capture requires its retained drawing environments.");
        ImmutableArray<byte> alternate = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        state = state with { Environment = Link(NativeBattleDrawEnvironmentPackets.Restore45C0F0(state.Environment, alternate), "2295BC0", 0) };
        NativeBattleCameraProjection projection = NativeBattleWall189Motion.Capture(center);
        NativeBattleStageState stage = NativeStage ?? throw new NotSupportedException("Native Wall capture requires its current stage.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(NativeFileStaging!, 44);
        uint context = allocation.Address;
        NativeBattleFileStaging staging = allocation.Staging.Write(context + 4, BitConverter.GetBytes(0x196B018u - checked((uint)(NativeModelCpuResources!.ScratchMaximum!.Value * 8)))).Write(context + 16, BitConverter.GetBytes(uint.MaxValue)).Write(context + 32, BitConverter.GetBytes(uint.MaxValue));
        staging = staging.Write(context + 20, BitConverter.GetBytes((short)0)).Write(context + 22, BitConverter.GetBytes((short)0)).Write(context + 24, BitConverter.GetBytes((short)320)).Write(context + 26, BitConverter.GetBytes((short)216));
        for (int index = 3; index >= 0; index--)
        {
            NativeBattleStageGroup group = stage.Groups[index];
            if ((group.Flags & 3) != 3)
            {
                continue;
            }
            NativeBattlePoseSource source = group.Source ?? throw new NotSupportedException("Native Wall capture requires its current stage skeleton.");
            NativeBattleDecodedPose pose = source.Sample(group.Animation, group.Counter, group.PoseFlags);
            group = group with { Matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(source, pose), projection.View!, 0) };
            stage = stage with { Groups = stage.Groups.SetItem(index, group) };
            staging = staging.Write(context + 28, BitConverter.GetBytes(group.ColorFlags)).Write(context + 36, BitConverter.GetBytes(group.ColorDepth));
            var gate = process.Enter5099D0(definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Wall capture requires its actual renderer capability."));
            process = gate.Process;
            if (!gate.Draw)
            {
                continue;
            }
            ImmutableArray<NativeBattleRawMesh> geometry = RequireNativeStageGeometry().Where(mesh => mesh.StageGroup == index).ToImmutableArray();
            int capacity = checked(NativeModelCpuResources!.ScratchMaximum!.Value * 8);
            uint workspace = checked(0x196B018u - (uint)capacity);
            (byte[] retained, _) = staging.ReadSnapshot(workspace, capacity);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(geometry, group.Matrices, projection, group.ColorFlags, uint.MaxValue, retained.ToImmutableArray(), definition.NativeGraphics!.Rules.SummonColors!, 16, new(0, 0, 320, 216), 17, group.ColorDepth);
            NativeBattleRegionalPacketWrite written = NativeBattleRegionalPacketArena.Append(state.Packets, "2296A30", cursor, 0x1C000, graphics.PrimitiveFrame!.Pending, drawn.Packets);
            state = state with { Packets = written.Arena };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
            cursor = written.Cursor;
            staging = staging.Write(workspace, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
            process = process.PublishModelColor(group.ColorFlags).Publish50FDF0(checked(0x2296A30u + (uint)cursor), null);
        }
        NativeStage = stage;
        NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = process };
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 44);
        state = state with { ClearPacket = Link(state.ClearPacket, "D1B490", 1) };
        state = state with { Area = Link(NativeBattleDrawEnvironmentPackets.Area45C940(state.Area, 384, 256, 256, 256), "2293F50", 0) };
        state = state with { Offset = Link(NativeBattleDrawEnvironmentPackets.Offset45C9B0(state.Offset, 384, 256), "2294DA0", 0), CaptureX = 384, CaptureY = 256 };
        return (state, graphics, cursor);

        ImmutableArray<byte> Link(ImmutableArray<byte> bytes, string bank, ushort flags)
        {
            using NativeBattleLogicalOrdering.Builder links = new(graphics.PrimitiveFrame!.Pending);
            NativeBattlePrimitiveReference reference = new(bank, 0, 0);
            links.InvalidatePacketRange(reference, bytes.Length);
            ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 4))).ToImmutableArray();
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(17, reference, words, flags, [0, 0, 0, 0]);
            byte[] result = bytes.ToArray();
            if (linked)
            {
                NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink).CopyTo(result, 0);
            }
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = links.ToImmutable() } };
            return result.ToImmutableArray();
        }
    }
}
