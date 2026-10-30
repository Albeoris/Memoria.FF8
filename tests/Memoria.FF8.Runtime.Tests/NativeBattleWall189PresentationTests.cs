using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWall189PresentationTests
{
    private const uint Temporary = 0x2793DF0;
    private const uint Source = 0x1000000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_shield_and_clipped_shield_capture_keep_context_packet_ledger_and_native_depths(bool clip)
    {
        NativeBattleModuleMeshTemporaryResult Run(bool capture)
        {
            var mesh = Mesh();
            NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 5, 7, 4096], 0, 0, 4000, 4096);
            NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
            var staging = NativeBattleFileStaging.Unknown();
            NativeBattlePilotVisualIdentity? identity = capture ? new(189, 1, 2, 3, 4) : null;
            if (!clip)
            {
                return NativeBattleWall189ShieldPackets.Emit64F6D0(staging, Temporary, mesh, matrix, projection, 20, NativeBattleGteColorInterpolation.CodeConstants45F270, identity);
            }
            NativeBattleWall189Resources resources = new(null!, null!, null!, mesh, null!, 0, [], Selectors());
            return NativeBattleWall189ClipPackets.Emit5BD1C0(staging, Temporary, resources, matrix, projection, 19, 0x1D8E058, 0x2296A30, NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Zsf3 = 0x155, Zsf4 = 0x100, Dqa = 0, Dqb = 0 }, identity);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        var packet = Assert.Single(captured.Batch.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        var geometry = packet.PilotGeometry!;
        Assert.Equal(clip ? 4 : 3, geometry.CornerCount);
        Assert.Equal((ushort)189, geometry.Identity.Effect);
        if (clip)
        {
            Assert.All(Enumerable.Range(0, geometry.CornerCount), corner => Assert.True(geometry.Corner(corner).Y <= -101));
        }
        else
        {
            Assert.Equal(new Vector3(-100, -200, 4000 - 1900f / 4096), geometry.A);
            Assert.Equal(0xE1000020u, packet.Words[1]);
            Assert.Equal(0xE1000220u, packet.Words[^1]);
        }
        var finalized = NativeBattleWall189Presentation.Depths(captured.Batch);
        var result = Assert.Single(finalized.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        Assert.True(result.PilotGeometry!.UseSourceDepths);
        Assert.Equal(new Vector4(packet.VertexPayload4[0], packet.VertexPayload4[1], packet.VertexPayload4[2], packet.VertexPayload4[3]), result.PilotGeometry.SourceDepths);
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
    }

    [Fact]
    public void Particle_rows_retain_actual_task_owner_and_authored_row_and_break_on_successful_rebirth()
    {
        var pool = NativeBattleEffectTaskPool.Initialize(0x2295C00, 0x2295C10, 36, 100);
        (pool, int slot) = pool.Append(0x5BDCC0);
        var task = pool.Slots[slot];
        string baseline = NativeBattleTestSnapshots.Json(pool);
        var first = NativeBattleWall189Presentation.Row(task, 0x5BDCC0, 149, 0x123456);
        Assert.Equal(first, NativeBattleWall189Presentation.Row(task, 0x5BDCC0, 149, 0x123456));
        Assert.NotEqual(first, NativeBattleWall189Presentation.Row(task, 0x5BDCC0, 148, 0x123456));
        Assert.NotEqual(first, NativeBattleWall189Presentation.Row(task, 0x5BDF60, 149, 0x123456));
        Assert.NotEqual(first, NativeBattleWall189Presentation.Row(task, 0x5BDCC0, 149, 0x123458));
        NativeBattleWall189Presentation.Allocated(task, 0x5BDCC0, 149);
        var next = NativeBattleWall189Presentation.Row(task, 0x5BDCC0, 149, 0x123456);
        Assert.Equal(first.Generation, next.Generation);
        Assert.Equal(first.Primitive, next.Primitive);
        Assert.NotEqual(first.ResourceGeneration, next.ResourceGeneration);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(pool));
    }

    [Fact]
    public void Offscreen_capture_clear_keeps_actual_partial_256_pixel_extent_discrete_under_flags_one()
    {
        var state = NativeBattleWall189State.FromColdProcess();
        ImmutableArray<uint> words = Enumerable.Range(0, state.ClearPacket.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(state.ClearPacket.AsSpan(index * 4, 4))).ToImmutableArray();
        var geometry = NativeBattleWall189Presentation.CaptureClear(words, new(189, 1, 2, 3, 4));
        Assert.True(geometry.ScreenSpace);
        Assert.False(geometry.Continuous);
        Assert.Equal(Vector4.Zero, geometry.SourceDepths);
        Assert.True(geometry.UseSourceDepths);
        for (int corner = 0; corner < 4; corner++)
        {
            uint word = words[2 + corner];
            Assert.Equal(unchecked((short)word), geometry.Corner(corner).X * geometry.ScreenScale * 0.125f);
            Assert.Equal(unchecked((short)(word >> 16)), geometry.Corner(corner).Y * geometry.ScreenScale * 0.125f);
        }
        Assert.Equal(new Vector3(256, 256, 0), geometry.D);
    }

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        short[] xy = [-100, -200, 100, -200, 0, 100];
        for (int vertex = 0; vertex < 3; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), xy[vertex * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + vertex * 8), xy[vertex * 2 + 1]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x24808080);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(48), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 4);
        return new(0, Source, bytes.ToImmutableArray());
    }

    private static ImmutableArray<int> Selectors() => [0, 1, 2, 0, 0, 1, 2, 0, 0, 3, 2, 3, 3, 1, 2, 0, 0, 1, 4, 4, 0, 4, 2, 0, 3, 1, 4, 4, 0, 3, 2, 4, 5, 1, 2, 5, 0, 1, 5, 0, 0, 3, 5, 3, 5, 3, 2, 1, 5, 4, 2, 5, 0, 1, 5, 4];
}
