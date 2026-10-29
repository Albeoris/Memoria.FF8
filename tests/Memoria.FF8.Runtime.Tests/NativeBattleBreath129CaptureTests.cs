using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBreath129CaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fibre_capture_keeps_exact_native_four_SZ_payload_and_edge_offsets_with_independent_frames(bool degenerate)
    {
        NativeBattleBreath129Frame native = Run(false, degenerate, out var nativePackets);
        string nativeFrame = NativeBattleTestSnapshots.Json(native);
        string nativeWrites = JsonSerializer.Serialize(nativePackets);
        NativeBattleBreath129Frame captured = Run(true, degenerate, out var packets);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativeWrites, JsonSerializer.Serialize(packets));
        Assert.NotEmpty(packets);
        Assert.All(packets, packet =>
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(packet.PilotGeometry);
            Assert.Equal(new Vector4(packet.VertexPayload4[0], packet.VertexPayload4[1], packet.VertexPayload4[2], packet.VertexPayload4[3]), geometry.SourceDepths);
            Assert.True(geometry.UseSourceDepths);
            Assert.NotEqual(geometry.SourceDepths!.Value.X, geometry.SourceDepths.Value.W);
            int[] offsets = [8, 20, 32, 44];
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 projected = NativeBattlePilotVisualGeometry.Project(geometry.Corner(corner), Projection);
                Vector2 sampled = new Vector2(projected.X, projected.Y) + geometry.ScreenOffsets[corner];
                uint screen = BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan(offsets[corner]));
                Assert.InRange(Math.Abs(sampled.X - unchecked((short)screen)), 0, 0.51f);
                Assert.InRange(Math.Abs(sampled.Y - unchecked((short)(screen >> 16))), 0, 0.51f);
            }
        });
        Assert.Equal(packets.Count, packets.Select(p => p.PilotGeometry!.Identity).Distinct().Count());
    }

    [Fact]
    public void Linked_capture_preserves_actual_indexed_source_centers_and_carried_native_depth_FIFO()
    {
        NativeBattleBreath129Frame native = RunLinked(false, out var nativePackets);
        string nativeFrame = NativeBattleTestSnapshots.Json(native);
        string nativeWrites = JsonSerializer.Serialize(nativePackets);
        NativeBattleBreath129Frame captured = RunLinked(true, out var packets);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(nativeWrites, JsonSerializer.Serialize(packets));
        Assert.Equal(2, packets.Count);
        Assert.All(packets, packet =>
        {
            NativeBattlePilotVisualGeometry geometry = packet.PilotGeometry!;
            Assert.NotNull(geometry);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(new Vector4(4000, 8000, 8256, 8512), geometry.SourceDepths);
            Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x19C0, geometry.Identity.Bank);
            Assert.Equal(4, geometry.ScreenOffsets.Length);
        });
        Assert.Equal(new Vector3(0, 0, 8000), packets[0].PilotGeometry!.A);
        Assert.Equal(new Vector3(100, 0, 8256), packets[0].PilotGeometry!.C);
        Assert.NotEqual(packets[0].PilotGeometry!.Identity, packets[1].PilotGeometry!.Identity);
    }

    private static NativeBattleBreath129Frame RunLinked(bool capture, out List<NativeBattleBreath129AppendPacket> output)
    {
        uint arenaAddress = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear();
        for (int index = 0; index < 256; index++)
        {
            arena = arena.Write(0x19C0 + index * 28, BitConverter.GetBytes(ushort.MaxValue));
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = arena, GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sz = [1000, 2000, 3000, 4000] }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleBreath129State state = NativeBattleBreath129State.FromColdProcess().WriteDword(0x252115C, arenaAddress).WriteDword(0x252117C, arenaAddress + 0x3B70) with { MutableSources = NativeBattleEffectArena.Clear(), PendingColdSources = false };
        NativeBattleEffectArena packed = NativeBattleEffectArena.Clear().Write(0, NativeBattlePackedMatrix32.FromMatrix(View, 0).Bytes.AsSpan());
        state = state.WriteView(0x2521138, packed, 20).WriteView(0x2521110, packed, 32);
        for (int index = 0; index < 3; index++)
        {
            uint source = 0x1228F0C + (uint)(index * 8);
            state = state.WriteWord(source, (ushort)(index * 100)).WriteWord(source + 2, 0).WriteWord(source + 4, (ushort)(8000 + index * 256)).WriteWord(source + 6, 0);
        }
        NativeBattleBreath129Frame frame = new(memory, new(129, 128, new(129, 0x6B4C00, 0x6B4C10), null, 0), new(123), state);
        uint[] points = new uint[3];
        for (int index = 0; index < 3; index++)
        {
            (frame, points[index]) = NativeBattleBreath129Controller.AllocatePoint(frame, (short)index, 512, 2048, -128);
            if (index > 0)
            {
                frame = NativeBattleBreath129Controller.WriteBytes(frame, points[index - 1] + 24, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(points[index])), 4);
            }
        }
        List<NativeBattleBreath129AppendPacket> packets = [];
        NativeBattleBreath129Inputs inputs = NativeBattleBreath129LowerTests.Inputs((current, operation) =>
        {
            packets.Add(Assert.IsType<NativeBattleBreath129AppendPacket>(operation));
            return current;
        }) with { ReadProjection = _ => Projection, PilotIdentity = capture ? new(129, 0x25211B0, 0, 17, 0) : null };
        byte[] distances = Enumerable.Repeat((byte)4, 16384).ToArray();
        distances[0] = 0;
        NativeBattleBreath129Resources resources = new(new("unused", []), new(0, []), new(0, []), new(0, []), new(0, []), new(0, 0, 0, 0, []), new("unused", []), [], [], [], [], distances.ToImmutableArray(), [], new short[8192].ToImmutableArray(), 0);
        NativeBattleBreath129Frame result = NativeBattleBreath129Geometry.DrawLinked6B6450(frame, points[0], 0, 3, resources, inputs);
        output = packets;
        return result;
    }

    private static NativeBattleActionMatrix View => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(View, 256, 160, 108);

    private static NativeBattleBreath129Frame Run(bool capture, bool degenerate, out List<NativeBattleBreath129AppendPacket> output)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleBreath129State state = NativeBattleBreath129State.FromColdProcess().WriteDword(0x252117C, NativeBattleEffectNodeMemory.ArenaAddress + 0x3B70);
        NativeBattleBreath129Frame frame = new(memory, new(129, 128, new(129, 0x6B4C00, 0x6B4C10), new(0, 0, 0, 0, 0, 0), 0), new(123), state) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        (frame, uint address) = NativeBattleBreath129Controller.Allocate(frame, 256);
        NativeBattleEffectArena points = NativeBattleBreath129Controller.Context(frame, address, 256);
        for (int index = 0; index < 16; index++)
        {
            points = points.Write(index * 16, BitConverter.GetBytes((short)(degenerate && index < 2 ? 0 : index * 100))).Write(index * 16 + 2, BitConverter.GetBytes((short)0)).Write(index * 16 + 4, BitConverter.GetBytes((short)(8000 + index * 256))).Write(index * 16 + 6, BitConverter.GetBytes((short)512)).Write(index * 16 + 14, BitConverter.GetBytes((short)128));
        }
        frame = NativeBattleBreath129Controller.PublishContext(frame, address, points, 256);
        List<NativeBattleBreath129AppendPacket> packets = [];
        NativeBattleBreath129Inputs inputs = new(NativeBattleActionPacketsState.Fresh(), NativeBattleGteColorInterpolation.CodeConstants45F270, 0, _ => NativeBattlePackedMatrix32.FromMatrix(View, null), _ => Projection, (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _, _, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            packets.Add(Assert.IsType<NativeBattleBreath129AppendPacket>(operation));
            return current;
        }) { PilotIdentity = capture ? new(129, 0x25211A0, 0, 19, 0) { ResourceGeneration = 0x1100000 } : null };
        byte[] distance = Enumerable.Repeat((byte)4, 16384).ToArray();
        distance[0] = 0;
        NativeBattleBreath129Resources resources = new(new("unused", []), new(0, []), new(0, []), new(0, []), new(0, []), new(0, 0, 0, 0, []), new("unused", []), [], [], [], [], distance.ToImmutableArray(), [], [], 0);
        NativeBattleBreath129Frame result = NativeBattleBreath129Geometry.DrawFibre6B6CC0(frame, address, resources, inputs, new(address));
        output = packets;
        return result;
    }
}
