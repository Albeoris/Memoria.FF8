using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePandemona291SurfaceTests
{
    [Fact]
    public void Inactive_node_returns_before_source_and_texture_operands_are_read()
    {
        NativeBattleGfAnimationNode node = Node();
        byte[] data = node.Data.ToArray();
        data.AsSpan(24, 6).Clear();
        NativeBattlePandemona291PreparedSurface prepared = NativeBattlePandemona291Surface.Prepare6F06F0(new(0, []), node with { Data = data.ToImmutableArray() }, Identity, 255, 0, 0);
        Assert.False(prepared.Active);
    }

    [Fact]
    public void Morph_selection_and_full_translation_use_current_node_and_parent()
    {
        NativeBattleGfAnimationSource source = Source();
        NativeBattleGfAnimationNode node = Node();
        byte[] data = node.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 20);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(10), 30);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 40);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(38), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(42), 1);
        NativeBattlePandemona291PreparedSurface prepared = NativeBattlePandemona291Surface.Prepare6F06F0(source, node with { Data = data.ToImmutableArray() }, Identity with { X = 1000, Y = 2000, Z = 3000 }, 1, 64, 0);
        Assert.True(prepared.Active);
        Assert.Equal(4, prepared.Morph.Length);
        Assert.Equal(new NativeBattleVector(50, 0, 0), prepared.Morph[0]);
        Assert.Equal((1020, 2030, 3040), (prepared.Matrix.X, prepared.Matrix.Y, prepared.Matrix.Z));
        Assert.Equal((ushort)64, prepared.Context.UOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 128, 256), prepared.Context.WindowAfter);
    }

    [Theory]
    [InlineData(0, 0, 64, 0, 128, 256, 128)]
    [InlineData(1, 64, 0, 0, 0, 128, 256)]
    [InlineData(2, 0, 64, 0, 0, 256, 128)]
    [InlineData(3, 0, 64, 0, 128, 256, 128)]
    [InlineData(4, 0, 0, 0, 0, 256, 256)]
    public void Texture_selector_preserves_literal_window_and_byte_phase_modes(byte kind, ushort u, ushort v, short x, short y, short width, short height)
    {
        NativeBattlePandemona291PreparedSurface prepared = NativeBattlePandemona291Surface.Prepare6F06F0(Source(), Node(), Identity, kind, 192, 0);
        Assert.Equal(u, prepared.Context.UOffset);
        Assert.Equal(v, prepared.Context.VOffset);
        Assert.Equal(new NativeBattleTextureWindow(x, y, width, height), prepared.Context.WindowAfter);
    }

    [Fact]
    public void FT4_and_GT4_use_current_ZSF4_and_mode_polygon_mode_links_with_shared_wrap()
    {
        NativeBattleGfAnimationSource source = Source();
        NativeBattlePandemona291PreparedSurface prepared = NativeBattlePandemona291Surface.Prepare6F06F0(source, Node(), Identity with { Z = 1000 }, 1, 64, 0);
        List<int> inputs = [];
        NativeBattleGfGeometryBatch batch = NativeBattlePandemona291Surface.Emit701DD0(source, prepared, new(Identity, 100, 160, 100), new(0, 0, 1f / 4096f, 4503601774854144d), 0x200, offset =>
        {
            ReadOnlySpan<byte> xyz = source.Data.AsSpan().Slice(prepared.SelectedOffset + offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..]));
        }, inputs.Add);
        Assert.Equal(new[] { 0, 24, 0, 24 }, inputs);
        Assert.Equal(140, batch.AllocationBytes);
        Assert.Equal(8, batch.Attempts.Length);
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400(), NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(6, written.Ordering.Proxies.Length);
        Assert.Equal(140, written.Process.ByteCursor);
        Assert.All(batch.Attempts.Where(attempt => attempt.Primitive is not null), attempt => Assert.Equal(142, attempt.Primitive!.Bucket));
        Assert.Equal((byte)186, batch.Attempts[0].Data[12]);
        Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[1].Data.AsSpan().Slice(4)));
        Assert.Equal(0xE2000010u, BinaryPrimitives.ReadUInt32LittleEndian(batch.Attempts[3].Data.AsSpan().Slice(4)));
    }

    private static NativeBattleActionMatrix Identity => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    private static NativeBattleGfAnimationNode Node()
    {
        byte[] data = new byte[44];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(24 + index * 2), 4096);
        }
        return new(0, 0, data.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source()
    {
        byte[] data = new byte[212];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8), 12);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12), 108);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(16), 4);
        for (int shape = 0; shape < 3; shape++)
        {
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int offset = 24 + shape * 32 + vertex * 8;
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), (short)(shape * 100 + (vertex & 1) * 100));
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 2), (short)((vertex >> 1) * 100));
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(132), 1);
        Face(136, false);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(172), 1);
        Face(176, true);
        return new(0x13AFF48, data.ToImmutableArray());

        void Face(int offset, bool gouraud)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), gouraud ? 0x3C112233u : 0x2C112233u);
            for (int vertex = 0; vertex < 4; vertex++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 4 + vertex * 2), (ushort)(vertex * 2));
            }
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 12), 0x0100FAFA);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 16), 0x0200FBFA);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 20), 0xFBFBFAFA);
            if (gouraud)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 24), 0x00445566);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 28), 0x00778899);
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 32), 0x00AABBCC);
            }
        }
    }
}
