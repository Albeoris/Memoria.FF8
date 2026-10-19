using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShot198DrawingTests
{
    [Theory]
    [InlineData(3, 44)]
    [InlineData(7, 56)]
    public void Every_textured_group_keeps_its_original_extra_E1_word_inside_the_polygon_packet(int group, int length)
    {
        NativeBattleGfGeometryAttempt attempt = NativeBattleShot198Drawing.EmitFace5AC8F0(Face(group), Corners(group), group, 0x33, null, NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(length, attempt.PacketLength);
        Assert.Equal((uint)(length / 4 - 1) << 24, primitive.Words[0]);
        Assert.Equal(0xE1000220u, primitive.Words[^1]);
        Assert.Equal(0xE1000220u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(length - 4)));
        Assert.All(attempt.WriteMask.Skip(length - 4), state => Assert.Equal(NativeBattleGfPacketByteState.Known, state));
    }

    [Fact]
    public void Native_zero_area_rejection_precedes_fourth_vertex_and_retains_unwritten_E1_tail()
    {
        NativeBattleModuleProjectedVertex vertex = new(100u | 100u << 16, 4096, 0);
        NativeBattleGfGeometryAttempt rejected = NativeBattleShot198Drawing.EmitFace5AC8F0(Face(3), new NoFourth([vertex, vertex, vertex]), 3, 0x33, null, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Null(rejected.Primitive);
        Assert.Equal(0xA000000u, BinaryPrimitives.ReadUInt32LittleEndian(rejected.Data.AsSpan()));
        Assert.All(rejected.WriteMask.Skip(40), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }

    [Fact]
    public void Near_rejection_preserves_original_prefix_without_consuming_the_fourth_source_or_E1_store()
    {
        NativeBattleModuleProjectedVertex[] corners = Corners(7);
        corners[0] = corners[0] with { Flags = 0x20000 };
        NativeBattleGfGeometryAttempt rejected = NativeBattleShot198Drawing.EmitFace5AC8F0(Face(7), new NoFourth(corners), 7, 0xF3, null, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Null(rejected.Primitive);
        Assert.Equal(0xD000000u, BinaryPrimitives.ReadUInt32LittleEndian(rejected.Data.AsSpan()));
        Assert.All(rejected.WriteMask.Skip(52), state => Assert.Equal(NativeBattleGfPacketByteState.Untouched, state));
    }

    [Theory]
    [InlineData(3, 48)]
    [InlineData(4, 28)]
    [InlineData(5, 36)]
    [InlineData(6, 48)]
    [InlineData(7, 60)]
    public void Original_scrolling_path_keeps_window_words_only_for_its_textured_groups(int group, int length)
    {
        byte[] face = Face(group);
        NativeBattleModuleProjectedVertex[] corners = Corners(group);
        NativeBattleGfGeometryBatch batch = NativeBattleShot198Drawing.EmitFace5AAD60(face, corner => corners[corner], group, new(0x33, null, 0, -16, 17, 2, new(0, 128, 64, 128)), NativeBattleGteColorInterpolation.CodeConstants45F270);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(length, batch.AllocationBytes);
        Assert.Equal((uint)(length / 4 - 1) << 24, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan()));
        if ((group & 2) != 0)
        {
            Assert.Equal(NativeBattleEffectTextureWindows.Encode(new(0, 128, 64, 128)), BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
            Assert.Equal(0xE2000000u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(length - 4)));
            Assert.Equal(unchecked((byte)(2 - 16 + 128)), attempt.Data[17]);
        }
        else
        {
            Assert.Equal(0x32404040u | (group == 5 ? 0x8000000u : 0), BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        }
    }

    private static NativeBattleModuleProjectedVertex[] Corners(int group)
    {
        NativeBattleModuleProjectedVertex[] first = [new(100u | 100u << 16, 4096, 0), new(200u | 100u << 16, 4096, 0), new(100u | 200u << 16, 4096, 0)];
        return (group & 1) == 0 ? first : [.. first, new(200u | 200u << 16, 4096, 0)];
    }

    private static byte[] Face(int group)
    {
        int length = group switch { 2 => 20, 3 => 24, 4 => 20, 5 => 24, 6 => 28, 7 => 36, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
        byte command = group switch { 2 => 0x24, 3 => 0x2C, 4 => 0x30, 5 => 0x38, 6 => 0x34, 7 => 0x3C, _ => throw new ArgumentOutOfRangeException(nameof(group)) };
        byte[] face = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(face, (uint)command << 24 | 0x404040);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x10000201);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00040302);
        if ((group & 4) != 0)
        {
            int offset = (group & 2) == 0 ? 12 : group == 6 ? 20 : 24;
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset), 0x11402010);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset + 4), 0x22804020);
            if ((group & 1) != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(offset + 8), 0x33C06030);
            }
        }
        return face;
    }

    private sealed class NoFourth(IReadOnlyList<NativeBattleModuleProjectedVertex> vertices) : IReadOnlyList<NativeBattleModuleProjectedVertex>
    {
        public int Count => 4;
        public NativeBattleModuleProjectedVertex this[int index] => index == 3 ? throw new InvalidDataException("The rejected native fourth source must not be read.") : vertices[index];
        public IEnumerator<NativeBattleModuleProjectedVertex> GetEnumerator() => throw new InvalidOperationException();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
