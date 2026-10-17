using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGteDepthTraceTests
{
    private static NativeBattleGfGeometryContext Context => new(0, 0, 0, null, null, 0, 17, 2);
    private static NativeBattleQuezacotlGeometryContext CapturedContext => new(0, 0, 0, new(0, 0, 256, 256), new(0, 0, 256, 256), 17, 2);
    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
    private static NativeBattleCameraProjection Projection => new(null, 128, 0, 0);

    [Fact]
    public void Unknown_entry_slots_are_shifted_only_by_actual_unsigned_depth_writes()
    {
        NativeBattleGteDepthTrace trace = new();
        Assert.Equal(new int?[] { null, null, null, null }, trace.DepthFifo4);
        trace.Push(65535);
        trace.Push(0);
        Assert.Equal(new int?[] { null, null, 65535, 0 }, trace.DepthFifo4);
        trace.Push(123);
        trace.Push(456);
        Assert.Equal(new int?[] { 65535, 0, 123, 456 }, trace.DepthFifo4);
        Assert.Equal(new int?[] { null, null, null, null }, new NativeBattleGteDepthTrace().DepthFifo4);
    }

    [Fact]
    public void Final_projection_receipt_preserves_unknown_slots_and_rejects_invalid_registers()
    {
        NativeBattleGteDepthTrace trace = new([1, 2, 3, 4]);
        trace.ObserveFinal([null, 20, null, 40]);
        Assert.Equal(new int?[] { null, 20, null, 40 }, trace.DepthFifo4);
        Assert.Throws<InvalidDataException>(() => trace.ObserveFinal([1, 2, 3]));
        Assert.Throws<InvalidDataException>(() => trace.ObserveFinal([1, 2, 3, 65536]));
        Assert.Throws<InvalidDataException>(() => new NativeBattleGteDepthTrace([1, -1, 3, 4]));
        Assert.Equal(new int?[] { null, 20, null, 40 }, trace.DepthFifo4);
    }

    [Fact]
    public void Generic_near_rejection_still_executes_RTPT_but_not_fourth_RTPS()
    {
        NativeBattleGteDepthTrace trace = new([1, 2, 3, 4]);
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        vertices[1] = vertices[1] with { Flags = 0x20000 };
        NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(3), vertices, 3, Context, observeDepth: trace.Push);
        Assert.Null(attempt.Primitive);
        Assert.Equal(new int?[] { 4, 10, 20, 30 }, trace.DepthFifo4);
    }

    [Fact]
    public void Generic_NCLIP_rejection_does_not_execute_fourth_RTPS()
    {
        NativeBattleGteDepthTrace trace = new([1, 2, 3, 4]);
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(3), vertices, 3, Context, observeDepth: trace.Push);
        Assert.Null(attempt.Primitive);
        Assert.Equal(new int?[] { 4, 10, 30, 20 }, trace.DepthFifo4);
    }

    [Fact]
    public void Generic_viewport_rejection_occurs_after_all_four_depth_writes()
    {
        NativeBattleGteDepthTrace trace = new();
        NativeBattleModuleProjectedVertex[] vertices = Corners().Select(vertex => vertex with { Screen = XY(3000, 100) }).ToArray();
        NativeBattleGfGeometryAttempt attempt = NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(3), vertices, 3, Context, observeDepth: trace.Push);
        Assert.Null(attempt.Primitive);
        Assert.Equal(new int?[] { 10, 20, 30, 40 }, trace.DepthFifo4);
    }

    [Fact]
    public void Captured_leaf_fourth_RTPS_is_gated_by_near_flags_but_not_generic_NCLIP()
    {
        NativeBattleModuleProjectedVertex[] vertices = Corners();
        (vertices[1], vertices[2]) = (vertices[2], vertices[1]);
        NativeBattleGteDepthTrace trace = new();
        NativeBattleQuezacotlGeometryPackets.EmitQuad6C5B50(Face(7), vertices, CapturedContext, observeDepth: trace.Push);
        Assert.Equal(new int?[] { 10, 30, 20, 40 }, trace.DepthFifo4);
        vertices[0] = vertices[0] with { Flags = 0x20000 };
        NativeBattleQuezacotlGeometryPackets.EmitQuad6C5B50(Face(7), vertices, CapturedContext, observeDepth: trace.Push);
        Assert.Equal(new int?[] { 40, 10, 30, 20 }, trace.DepthFifo4);
    }

    [Fact]
    public void Cached_vertex_values_do_not_suppress_repeated_native_per_face_RTPT_calls()
    {
        byte[] source = new byte[76];
        BinaryPrimitives.WriteInt32LittleEndian(source, 4);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(12), 2);
        Face(2).CopyTo(source, 16);
        Face(2).CopyTo(source, 36);
        NativeBattleGteDepthTrace trace = new();
        List<ushort> observed = [];
        int sourceReads = 0;
        NativeBattleGfGeometryBatch batch = NativeBattleGfGeometryPackets.EmitBatch7043B0(source.ToImmutableArray(), 0, default, Identity, Projection, Context with { Flags = 0x2000 }, readSelectedVertex: offset =>
        {
            sourceReads++;
            return new(0, 0, checked((short)((offset / 8 + 1) * 10)));
        }, observeDepth: depth =>
        {
            observed.Add(depth);
            trace.Push(depth);
        });
        Assert.Equal(3, sourceReads);
        Assert.Equal(new ushort[] { 10, 20, 30, 10, 20, 30 }, observed);
        Assert.Equal(new int?[] { 30, 10, 20, 30 }, trace.DepthFifo4);
        Assert.All(batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    [Fact]
    public void Short_path_packets_consume_full_depths_from_the_actual_preceding_geometry_call()
    {
        NativeBattleGteDepthTrace trace = new();
        NativeBattleGfGeometryPackets.EmitAttempt7043B0(Face(3), Corners(), 3, Context, observeDepth: trace.Push);
        const uint head = 0x2120000;
        Dictionary<uint, byte[]> rows = [];
        for (int index = 0; index < 2; index++)
        {
            byte[] row = new byte[28];
            BinaryPrimitives.WriteInt16LittleEndian(row, (short)index);
            BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(2), 64);
            BinaryPrimitives.WriteInt16LittleEndian(row.AsSpan(0x14), 1600);
            BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(0x18), index == 0 ? head + 28 : 0);
            rows.Add(head + (uint)(index * 28), row);
        }
        ImmutableArray<byte> distance = Enumerable.Range(0, 16384).Select(index => (byte)Math.Floor(Math.Sqrt((index & 127) * (index & 127) + (index >> 7) * (index >> 7)))).ToImmutableArray();
        NativeBattleQuezacotlPathBatch path = NativeBattleQuezacotlPathPackets.Emit6C79A0(head, 2, (pointer, offset, length) => rows[pointer].AsSpan(offset, length).ToArray().ToImmutableArray(), selector => new(checked((short)(selector * 100)), 0, selector == 0 ? (short)200 : (short)240), Identity, Projection, distance, trace.DepthFifo4);
        Assert.Equal(new[] { 30, 40, 200, 240 }, Assert.Single(path.EffectPackets.Attempts).Primitive!.VertexPayload4);
        trace.ObserveFinal(path.FinalDepthFifo4);
        Assert.Equal(new int?[] { 30, 40, 200, 240 }, trace.DepthFifo4);
    }

    private static NativeBattleModuleProjectedVertex[] Corners() => [new(XY(100, 100), 10, 0), new(XY(200, 100), 20, 0), new(XY(100, 200), 30, 0), new(XY(200, 200), 40, 0)];

    private static byte[] Face(int group)
    {
        int length = group == 2 ? 20 : group == 3 ? 24 : 36;
        byte[] face = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(face, group == 7 ? 0x3E404040u : 0x2E404040u);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(8), 4);
        if (group != 2)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(10), 6);
        }
        return face;
    }

    private static uint XY(short x, short y) => (ushort)x | unchecked((uint)(ushort)y << 16);
}
