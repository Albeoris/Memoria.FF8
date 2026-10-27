using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleStaticMeshContext88SelectedTests
{
    private const uint Address = 0x10000000;
    private const uint Selected = 0x20000000;

    [Fact]
    public void Selected_pointer_uses_byte_offsets_and_retains_unknown_W_in_current_GTE()
    {
        List<int> reads = [];
        NativeBattleStaticMeshContext88Step actual = Draw(3, Context(0x2033), reader: offset =>
        {
            reads.Add(offset);
            return (new((short)(5 + offset), (short)(6 + offset), (short)(7 + offset)), null);
        });
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(new int[] { 0, 8, 16, 24 }, reads);
        Assert.Equal(Selected, Dword(actual.Context, 4));
        Assert.Equal((short)29, BinaryPrimitives.ReadInt16LittleEndian(actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.AsSpan()));
        Assert.False(actual.Memory.ScriptedColorRegisters.NormalInputKnown!.Value[6]);
        Assert.False(actual.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
    }

    [Fact]
    public void Selected_near_rejection_reads_only_three_corners_and_never_requires_cue()
    {
        List<int> reads = [];
        var actual = Draw(3, Context(0x20F3).MarkUnknown(12, 4), reader: offset =>
        {
            Assert.NotEqual(24, offset);
            reads.Add(offset);
            return (new((short)offset, 0, -2000), null);
        });
        Assert.Equal(new int[] { 0, 8, 16 }, reads);
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
    }

    [Fact]
    public void Reached_selected_vertex_requires_the_matching_physical_owner()
    {
        Assert.Throws<NotSupportedException>(() => Draw(3, Context(0x2033)));
        Assert.Throws<NotSupportedException>(() => Draw(3, Context(0x2033).Write(4, BitConverter.GetBytes(Selected + 8)), reader: _ => (new(0, 0, 0), 0)));
    }

    [Fact]
    public void Retained_UV_full_DWORD_carry_and_TPAGE_addition_consume_only_actual_WORD()
    {
        NativeBattleEffectArena context = Context(0x1433).Write(0x10, BitConverter.GetBytes((ushort)3)).MarkUnknown(0x12, 2).Write(0x18, BitConverter.GetBytes(0xC080u));
        var actual = Draw(3, context);
        NativeBattleGfGeometryAttempt packet = Assert.Single(actual.Batch.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(0x123516F8u, BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(12)));
        Assert.Equal(0x9AC09F70u, BinaryPrimitives.ReadUInt32LittleEndian(packet.Data.AsSpan().Slice(20)));
        Assert.Equal(0xC080u, Dword(actual.Context, 0x18));
        Assert.Throws<NotSupportedException>(() => actual.Context.Read(0x12, 2));
    }

    [Fact]
    public void Near_rejection_consumes_retained_UV_prefix_but_not_TPAGE_or_cue()
    {
        NativeBattleEffectArena context = Context(0x14F3).MarkUnknown(0x10, 4).MarkUnknown(12, 4);
        var actual = Draw(3, context, z: 0);
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Throws<NotSupportedException>(() => Draw(3, context.MarkUnknown(0x18, 4), z: 0));
        Assert.Throws<NotSupportedException>(() => Draw(3, Context(0x1433).MarkUnknown(0x10, 2)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Untextured_groups_do_not_read_UV_or_TPAGE_even_with_retention_flags(int group)
    {
        var actual = Draw(group, Context(0x14F3).MarkUnknown(0x10, 4).MarkUnknown(0x18, 4));
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Throws<NotSupportedException>(() => actual.Context.Read(0x10, 4));
        Assert.Throws<NotSupportedException>(() => actual.Context.Read(0x18, 4));
    }

    [Fact]
    public void Default_flags_reset_selected_pointer_and_UV_without_invoking_an_extra_reader()
    {
        var actual = Draw(3, Context(0x33).MarkUnknown(4, 4).MarkUnknown(0x10, 4).MarkUnknown(0x18, 4), reader: _ => throw new InvalidOperationException());
        Assert.Equal(Address + 8, Dword(actual.Context, 4));
        Assert.Equal(0u, Dword(actual.Context, 0x18));
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
    }

    [Fact]
    public void Selected_context_and_current_registers_restore_the_same_next_step()
    {
        var first = Draw(3, Context(0x20F3), reader: offset => (new((short)offset, (short)(offset * 2), 0), null));
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(first.Memory))!;
        var expected = Draw(3, first.Context, memory: first.Memory, reader: offset => (new((short)offset, (short)(offset * 2), 0), null));
        var actual = Draw(3, first.Context, memory: restored, reader: offset => (new((short)offset, (short)(offset * 2), 0), null));
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
    }

    private static NativeBattleStaticMeshContext88Step Draw(int group, NativeBattleEffectArena context, int z = 2000, NativeBattleEffectMemory? memory = null, Func<int, (NativeBattleVector Vertex, short? W)>? reader = null) => NativeBattleStaticMeshContext88.DrawLoaded(memory ?? Memory(z), Mesh(group), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, selectedVerticesAddress: Selected, readSelectedVertex: reader);
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(uint flags) => NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(0, BitConverter.GetBytes(Address)).Write(4, BitConverter.GetBytes(Selected)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(2048)).Write(0x10, BitConverter.GetBytes((ushort)1)).Write(0x18, new byte[4]).Write(0x1C, BitConverter.GetBytes(flags));
    private static uint Dword(NativeBattleEffectArena context, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(context.Read(offset, 4));

    private static NativeBattleStaticMeshResource Mesh(int group)
    {
        int stride = group == 4 ? 20 : 24;
        byte[] bytes = new byte[72 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        short[] points = [-400, -400, 0, 11, 400, -400, 0, 22, -400, 400, 0, 33, 400, 400, 0, 44];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), points[index]);
        }
        int face = 44 + group * 4;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), (group == 3 ? 0x2C000000u : group == 4 ? 0x30000000u : 0x38000000u) | 0x112233);
        for (int corner = 0; corner < (group == 4 ? 3 : 4); corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x9ABCDEF0);
        if (group != 4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 20), 0x11112222);
        }
        return new(0, Address, bytes.ToImmutableArray());
    }
}
