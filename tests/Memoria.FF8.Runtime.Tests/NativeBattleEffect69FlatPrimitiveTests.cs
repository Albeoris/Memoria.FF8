using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect69FlatPrimitiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Near_rejection_keeps_exact_raw_prefix_and_does_not_read_fourth_or_fade(bool quad)
    {
        List<int> reads = [];
        NativeBattleGfGeometryBatch batch = Emit(quad, 0x45, corner =>
        {
            reads.Add(corner);
            return new(Screen(100, 100), 2000, 0x20000);
        }, null, null);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, batch.AllocationBytes);
        Assert.Equal(new[] { 0, 1, 2 }, reads);
        Assert.Equal(quad ? 0x05000000u : 0x04000000u, Word(attempt, 0));
        Assert.Equal(0x22112233u, Word(attempt, 4));
        Assert.All(attempt.WriteMask.Take(8), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.All(attempt.WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Negative_area_rejects_before_screen_stores_but_zero_area_is_admitted(bool quad)
    {
        List<int> reads = [];
        NativeBattleGfGeometryBatch rejected = Emit(quad, 5, corner =>
        {
            reads.Add(corner);
            return Point(corner switch { 0 => (100, 100), 1 => (100, 200), _ => (200, 100) });
        });
        Assert.Null(Assert.Single(rejected.Attempts).Primitive);
        Assert.Equal(new[] { 0, 1, 2 }, reads);
        Assert.All(rejected.Attempts[0].WriteMask.Skip(8), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        NativeBattleGfGeometryBatch accepted = Emit(quad, 0, _ => Point((100, 100)));
        Assert.NotNull(Assert.Single(accepted.Attempts).Primitive);
        Assert.Equal(quad ? 24 : 20, accepted.AllocationBytes);
    }

    [Theory]
    [InlineData(false, 0x41u, 0x22081119u)]
    [InlineData(false, 0x44u, 0x20081119u)]
    [InlineData(false, 0x45u, 0x20081119u)]
    [InlineData(true, 0x41u, 0x22081119u)]
    [InlineData(true, 0x44u, 0x20081119u)]
    [InlineData(true, 0x45u, 0x20081119u)]
    public void Accepted_color_fades_raw_command_then_applies_flat_masks_with_clear_winning(bool quad, uint flags, uint expected)
    {
        NativeBattleGfGeometryBatch batch = Emit(quad, flags, corner => Point(Corners[corner]), 2048, 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(expected, Word(attempt, 4));
        Assert.Equal(expected, attempt.Primitive!.Words[1]);
        Assert.Equal(new[] { 2000, 2000, 2000, quad ? 2000 : 0 }, attempt.Primitive.VertexPayload4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Screen_reject_keeps_raw_color_and_all_screens_including_unchecked_fourth_flags(bool quad)
    {
        int reads = 0;
        NativeBattleGfGeometryBatch rejected = Emit(quad, 0x55, corner =>
        {
            reads++;
            return new(Screen((corner & 1) == 0 ? -1 : 2561, 100 + corner), 2000, corner == 3 ? 0x60000u : 0);
        }, null, null);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(rejected.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(quad ? 4 : 3, reads);
        Assert.Equal(0x22112233u, Word(attempt, 4));
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        NativeBattleGfGeometryBatch accepted = Emit(quad, 0x10, corner => new(Screen(Corners[corner].X, Corners[corner].Y), 2000, corner == 3 ? 0x60000u : 0));
        Assert.NotNull(Assert.Single(accepted.Attempts).Primitive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Inclusive_edges_are_accepted_and_wrapped_negative_depth_bias_clamps_zero(bool quad)
    {
        (int X, int Y)[] edges = [(0, 0), (2560, 0), (0, 1728), (2560, 1728)];
        NativeBattleEsunaMeshContext context = Operands(0) with { DepthBias = int.MaxValue, DepthShift = 2 };
        NativeBattleGfGeometryBatch batch = Emit(quad, corner => Point(edges[corner]), context);
        Assert.Equal(17, Assert.Single(batch.Attempts).Primitive!.Bucket);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Stateful_dispatch_publishes_actual_gte_depth_raw_material_cursor_and_preserves_unused_context(int group)
    {
        NativeBattleStaticMeshResource mesh = Mesh(group);
        NativeBattleWindowedMeshContext108Step result = Draw(mesh, false);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(group == 0 ? 20 : 24, result.Batch.AllocationBytes);
        Assert.Equal(mesh.Address + (uint)mesh.Bytes.Length, U(result.Context108, 0x34));
        Assert.Equal(0u, U(result.Context108, 0x44) & 0x60000);
        Assert.True(unchecked((int)U(result.Context108, 0x38)) > 0);
        Assert.Equal(result.Memory.GeometryRegisters!.Otz, U(result.Context108, 0x40));
        Assert.Equal(17 + ((int)result.Memory.GeometryRegisters.Otz!.Value >> 2), attempt.Primitive!.Bucket);
        Assert.Equal(0x22112233u, result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(0x22081119u, result.Memory.ScriptedColorRegisters.Rgb[2]);
        Assert.Equal(0x22081119u, Word(attempt, 4));
        Assert.DoesNotContain(true, result.Context108.ReadSnapshot(0x20, 0x14).Known);
        Assert.DoesNotContain(true, result.Context108.ReadSnapshot(0x3C, 4).Known);
        Assert.False(result.Context108.ReadSnapshot(11, 1).Known[0]);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleWindowedMeshContext108Step>(JsonSerializer.Serialize(result))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Stateful_near_reject_preserves_unreached_area_depth_and_color_register_input(int group)
    {
        NativeBattleWindowedMeshContext108Step result = Draw(Mesh(group), true);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.NotEqual(0u, U(result.Context108, 0x44) & 0x60000);
        Assert.DoesNotContain(true, result.Context108.ReadSnapshot(0x38, 4).Known);
        Assert.DoesNotContain(true, result.Context108.ReadSnapshot(0x40, 4).Known);
        Assert.Null(result.Memory.ScriptedColorRegisters!.Material);
        Assert.Equal(0x22112233u, Word(attempt, 4));
    }

    private static readonly (int X, int Y)[] Corners = [(100, 100), (200, 100), (100, 200), (200, 200)];
    private static NativeBattleModuleProjectedVertex Point((int X, int Y) point) => new(Screen(point.X, point.Y), 2000, 0);
    private static uint Screen(int x, int y) => unchecked((ushort)x) | unchecked((uint)(ushort)y << 16);
    private static uint Word(NativeBattleGfGeometryAttempt attempt, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(offset));
    private static uint U(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));
    private static NativeBattleEsunaMeshContext Operands(uint flags, int? fade = null, uint? far = null) => new(flags, far, fade, 0, 0, 0, new(0, 0, 0, 0), new(0, 0, 0, 0), 17, 2);
    private static NativeBattleGfGeometryBatch Emit(bool quad, uint flags, Func<int, NativeBattleModuleProjectedVertex> read, int? fade = null, uint? far = null) => Emit(quad, read, Operands(flags, fade, far));
    private static NativeBattleGfGeometryBatch Emit(bool quad, Func<int, NativeBattleModuleProjectedVertex> read, NativeBattleEsunaMeshContext context) => quad ? NativeBattleWindowedFlat701DD0.EmitF4_702130(Face(), read, context, NativeBattleGteColorInterpolation.CodeConstants45F270) : NativeBattleWindowedFlat701DD0.EmitF3_701F00(Face(), read, context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    private static byte[] Face()
    {
        byte[] bytes = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x22112233);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4 + corner * 2), (ushort)(corner * 2));
        }
        return bytes;
    }

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        byte[] bytes = new byte[12 + 32 + 12];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 12);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        int cursor = 12 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), 1);
        Face().CopyTo(bytes, cursor + 4);
        return new(0, 0x10000000, bytes.ToImmutableArray());
    }

    private static NativeBattleWindowedMeshContext108Step Draw(NativeBattleStaticMeshResource mesh, bool reject)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = reject ? -2000 : 2000 }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(0x20E0100u)).Write(8, new byte[3]).Write(12, BitConverter.GetBytes(2048)).Write(24, BitConverter.GetBytes(0)).Write(28, BitConverter.GetBytes(0x20F0u));
        return NativeBattleWindowedMeshContext108.DrawLoaded(memory, mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, 4, offset =>
        {
            NativeBattleVector point = offset switch { 0 => new(-400, -400, 0), 8 => new(400, -400, 0), 16 => new(-400, 400, 0), 24 => new(400, 400, 0), _ => throw new InvalidDataException() };
            return (point, (short?)null);
        });
    }
}
