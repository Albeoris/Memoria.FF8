using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleJunk44SelectedMeshTests
{
    private const uint Address = 0x10000000;
    private const uint Selected = 0x20000000;

    [Theory]
    [InlineData(0x2000)]
    [InlineData(0x20C0)]
    public void Programme_flags_preserve_selected_vertices_reset_UV_and_keep_physical_depths(uint flags)
    {
        List<int> reads = [];
        NativeBattleVector[] points = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        var context = Context(flags).MarkUnknown(0x10, 4).MarkUnknown(0x18, 4);
        var actual = Draw(3, context, reader: offset =>
        {
            reads.Add(offset);
            return (points[offset / 8], null);
        });
        var packet = Assert.Single(actual.Batch.Attempts);
        Assert.NotNull(packet.Primitive);
        Assert.Equal(40, actual.Batch.AllocationBytes);
        Assert.Equal(new[] { 0, 8, 16, 24 }, reads);
        Assert.Equal(Selected, Dword(actual.Context, 4));
        Assert.Equal(0u, Dword(actual.Context, 0x18));
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, packet.Primitive.VertexPayload4);
        Assert.Throws<NotSupportedException>(() => actual.Context.Read(0x10, 4));
        Assert.False(actual.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.Equal(flags == 0x20C0 ? 2048u : 0u, actual.Memory.ScriptedColorRegisters.Ir0);
    }

    [Fact]
    public void New_selected_flags_reject_negative_area_before_fourth_vertex_and_cue()
    {
        NativeBattleVector[] points = [new(-400, -400, 0), new(-400, 400, 0), new(400, -400, 0), new(400, 400, 0)];
        var actual = Draw(3, Context(0x20C0).MarkUnknown(8, 8), reader: offset =>
        {
            Assert.NotEqual(24, offset);
            return (points[offset / 8], null);
        });
        Assert.Equal(0, actual.Batch.AllocationBytes);
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.True(unchecked((int)Dword(actual.Context, 0x24)) < 0);
    }

    [Fact]
    public void Zero_area_is_accepted_and_fourth_near_does_not_replace_first_flags()
    {
        NativeBattleVector[] points = [new(-400, 0, 0), new(0, 0, 0), new(400, 0, 0), new(400, 400, -1999)];
        var actual = Draw(3, Context(0x2000), reader: offset => (points[offset / 8], null));
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0u, Dword(actual.Context, 0x24));
        Assert.Equal(0u, Dword(actual.Context, 0x30) & 0x60000);
        Assert.Equal(new[] { 2000, 2000, 2000, 1 }, Assert.Single(actual.Batch.Attempts).Primitive!.VertexPayload4);
    }

    [Fact]
    public void First_three_near_rejection_preserves_unknown_cue_and_never_loads_fourth()
    {
        var actual = Draw(3, Context(0x20C0).MarkUnknown(12, 4), reader: offset =>
        {
            Assert.NotEqual(24, offset);
            return (new((short)offset, 0, -1999), null);
        });
        Assert.Null(Assert.Single(actual.Batch.Attempts).Primitive);
        Assert.Equal(0, actual.Batch.AllocationBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0xC0)]
    public void Default_inline_path_does_not_invoke_selected_reader(uint flags)
    {
        var actual = Draw(3, Context(flags).MarkUnknown(4, 4), reader: _ => throw new InvalidOperationException());
        Assert.Equal(Address + 8, Dword(actual.Context, 4));
        Assert.NotNull(Assert.Single(actual.Batch.Attempts).Primitive);
    }

    private static NativeBattleStaticMeshContext88Step Draw(int group, NativeBattleEffectArena context, int z = 2000, NativeBattleEffectMemory? memory = null, Func<int, (NativeBattleVector Vertex, short? W)>? reader = null) => NativeBattleStaticMeshContext88.DrawLoaded(memory ?? Memory(z), Mesh(group), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270, selectedVerticesAddress: Selected, readSelectedVertex: reader);
    private static NativeBattleEffectMemory Memory(int z) => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() with { Ir0 = 777 }, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
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
