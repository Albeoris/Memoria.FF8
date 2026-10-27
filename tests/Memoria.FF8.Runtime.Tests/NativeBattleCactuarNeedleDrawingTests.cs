using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCactuarNeedleDrawingTests
{
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(Identity, 128, 0, 0);

    [Fact]
    public void Crossed_packets_keep_physical_order_unknown_UV_halves_and_individual_depth_proxies()
    {
        NativeBattleActionMatrix node = Identity with { Rotation = [4096, 0, 0, 0, 4096, 0, 1024, 2048, 4096] };
        NativeBattleCactuarNeedleDraw draw = Draw(node, Identity with { Z = 2000 });
        Assert.Equal(80, draw.Packets.AllocationBytes);
        Assert.Equal(new[] { 0, 40 }, draw.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative).ToArray());
        foreach (NativeBattleGfGeometryAttempt attempt in draw.Packets.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
            Assert.Equal(17, primitive.Bucket);
            Assert.Equal(0x2E123456u, primitive.Words[1]);
            Assert.Equal(0x3F540000u, primitive.Words[3]);
            Assert.Equal(0x00B9000Fu, primitive.Words[5]);
            Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[30]);
            Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[31]);
            Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[38]);
            Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[39]);
        }
        Assert.Equal(new[] { 2496, 2528, 1984, 2016 }, draw.Packets.Attempts[0].Primitive!.VertexPayload4.ToArray());
        Assert.Equal(new[] { 2480, 2544, 1968, 2032 }, draw.Packets.Attempts[1].Primitive!.VertexPayload4.ToArray());
        Assert.NotEqual(draw.Packets.Attempts[0].Primitive!.Words[2], draw.Packets.Attempts[1].Primitive!.Words[2]);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), draw.Packets);
        Assert.Equal(80, written.Process.ByteCursor);
        Assert.Equal(2, written.Ordering.Proxies.Length);
        NativeBattlePrimitiveProcess coldProcess = NativeBattleTestSnapshots.RestoreJson<NativeBattlePrimitiveProcess>(NativeBattleTestSnapshots.Json(written.Process))!;
        NativeBattleLogicalOrdering coldOrdering = NativeBattleTestSnapshots.RestoreJson<NativeBattleLogicalOrdering>(NativeBattleTestSnapshots.Json(written.Ordering))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleCommonPacketArena.Append(written.Process, written.Ordering, draw.Packets)), NativeBattleTestSnapshots.Json(NativeBattleCommonPacketArena.Append(coldProcess, coldOrdering, draw.Packets)));
    }

    [Fact]
    public void Camera_columns_saturate_signed_IR_while_translation_retains_full_MAC()
    {
        NativeBattleActionMatrix large = Identity with { Rotation = [32767, 0, 0, 0, 32767, 0, 0, 0, 32767] };
        NativeBattleVector position = new(32767, -32768, 1000);
        NativeBattleActionMatrix view = large with { X = 500000, Y = -500000, Z = 10000 };
        NativeBattleCactuarNeedleDraw draw = NativeBattleCactuarNeedleDrawing.Emit5A9C10(large, position, 0x2E123456, view, Projection);
        Assert.Equal(new short[] { 32767, 0, 0, 0, 32767, 0, 0, 0, 32767 }, draw.CameraMatrix.Rotation.ToArray());
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(view, position);
        Assert.Equal(unchecked(x + view.X), draw.CameraMatrix.X);
        Assert.Equal(unchecked(y + view.Y), draw.CameraMatrix.Y);
        Assert.Equal(unchecked(z + view.Z), draw.CameraMatrix.Z);
        Assert.True(draw.CameraMatrix.X > short.MaxValue);
        Assert.Equal(position, draw.PreparedPosition);
    }

    [Fact]
    public void Rejected_A_retains_prefix_only_and_never_attempts_B()
    {
        NativeBattleCactuarNeedleDraw draw = Draw(Identity, Identity with { Z = 1 });
        NativeBattleGfGeometryAttempt attempt = Assert.Single(draw.Packets.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, draw.Packets.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Unknown, attempt.WriteMask[30]);
    }

    [Fact]
    public void Rejected_B_keeps_A_and_reuses_the_next_cursor_without_allocating_B()
    {
        NativeBattleActionMatrix node = Identity with { Rotation = [4096, 0, 0, 0, 4096, 0, 0, 4096, 0] };
        NativeBattleCactuarNeedleDraw draw = Draw(node, Identity with { Z = 100 });
        Assert.Equal(40, draw.Packets.AllocationBytes);
        Assert.Equal(2, draw.Packets.Attempts.Length);
        Assert.NotNull(draw.Packets.Attempts[0].Primitive);
        Assert.Null(draw.Packets.Attempts[1].Primitive);
        Assert.Equal(40, draw.Packets.Attempts[1].ByteOffsetRelative);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), draw.Packets);
        Assert.Equal(40, written.Process.ByteCursor);
        Assert.Single(written.Ordering.Proxies);
    }

    [Fact]
    public void Fourth_vertex_clip_and_screen_edge_flags_do_not_reject_or_backface_cull()
    {
        NativeBattleActionMatrix node = Identity with { Rotation = [4096, 0, 0, 0, 4096, 0, -4096, 0, 4096] };
        NativeBattleCactuarNeedleDraw draw = Draw(node, Identity with { X = 500000, Z = 100 });
        Assert.Equal(80, draw.Packets.AllocationBytes);
        Assert.Equal(36, draw.Packets.Attempts[0].Primitive!.VertexPayload4[3]);
        Assert.All(draw.Packets.Attempts, attempt => Assert.NotNull(attempt.Primitive));
    }

    private static NativeBattleCactuarNeedleDraw Draw(NativeBattleActionMatrix node, NativeBattleActionMatrix view) => NativeBattleCactuarNeedleDrawing.Emit5A9C10(node, default, 0x2E123456, view, Projection);
}
