using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareFlatScanlinesTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Odd_and_even_widths_write_every_low_WORD_and_preserve_adjacent_pixels(int width)
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 4096).ToArray();
        NativeSoftwareScanlineEdges edges = new(Edge(3 << 16, 0), Edge((3 + width) << 16, 0));
        NativeSoftwareFlatScanlines.Draw463D50(image, edges, 1, 1, 0x98761234);
        for (int offset = 0; offset < image.Length; offset++)
        {
            byte expected = offset >= 2054 && offset < 2054 + width * 2 ? (offset % 2 == 0 ? (byte)0x34 : (byte)0x12) : (byte)0xA5;
            Assert.Equal(expected, image[offset]);
        }
    }

    [Fact]
    public void Black_is_opaque_but_equal_odd_span_does_not_write_a_pixel()
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 2048).ToArray();
        NativeSoftwareFlatScanlines.Draw463D50(image, new(Edge(1 << 16, 0), Edge(3 << 16, 0)), 0, 1, 0xDEAD0000);
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0, 0, 0, 0, 0xA5, 0xA5 }, image.AsSpan(0, 8).ToArray());
        byte[] unchanged = image.ToArray();
        NativeSoftwareFlatScanlines.Draw463D50(image, new(Edge(7 << 16, 0), Edge((7 << 16) + 65535, 0)), 0, 1, 0xFFFF);
        Assert.Equal(unchanged, image);
    }

    [Fact]
    public void Crossing_edges_swap_local_order_but_advance_only_canonical_X_and_preserve_UV_and_steps()
    {
        byte[] image = new byte[3 * 2048];
        NativeSoftwareScanlineEdges edges = new(Edge(3 << 16, -(1 << 16)), Edge(1 << 16, 1 << 16));
        NativeSoftwareScanlineEdges result = NativeSoftwareFlatScanlines.Draw463D50(image, edges, 0, 3, 0x1234);
        Assert.Equal(0, Read(result.Left.Current));
        Assert.Equal(4 << 16, Read(result.Right.Current));
        Assert.Equal(edges.Left.Current.AsSpan().Slice(4).ToArray(), result.Left.Current.AsSpan().Slice(4).ToArray());
        Assert.Equal(edges.Right.Current.AsSpan().Slice(4).ToArray(), result.Right.Current.AsSpan().Slice(4).ToArray());
        Assert.Equal(edges.Left.Step, result.Left.Step);
        Assert.Equal(edges.Right.Step, result.Right.Step);
        Assert.Equal(new byte[] { 0x34, 0x12, 0x34, 0x12 }, image.AsSpan(2, 4).ToArray());
        Assert.All(image.AsSpan(2048, 2048).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal(new byte[] { 0x34, 0x12, 0x34, 0x12 }, image.AsSpan(4098, 4).ToArray());
    }

    [Fact]
    public void Equal_span_still_advances_X_with_DWORD_wrap_without_reading_an_invalid_destination()
    {
        byte[] image = [0xA5];
        NativeSoftwareScanlineEdges edges = new(Edge(int.MaxValue, 1), Edge(int.MaxValue, -1));
        NativeSoftwareScanlineEdges result = NativeSoftwareFlatScanlines.Draw463D50(image, edges, -1, 1, 0);
        Assert.Equal(int.MinValue, Read(result.Left.Current));
        Assert.Equal(int.MaxValue - 1, Read(result.Right.Current));
        Assert.Equal(new byte[] { 0xA5 }, image);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Nonpositive_rows_return_unchanged_without_reading_edge_fields(int rows)
    {
        byte[] image = [0xA5];
        NativeSoftwareScanlineEdges edges = new(new(default, default), new(default, default));
        Assert.Same(edges, NativeSoftwareFlatScanlines.Draw463D50(image, edges, int.MaxValue, rows, 0));
        Assert.Equal(new byte[] { 0xA5 }, image);
    }

    [Fact]
    public void Signed_negative_X_is_not_clipped_and_unowned_destination_fails()
    {
        byte[] image = new byte[4096];
        NativeSoftwareFlatScanlines.Draw463D50(image, new(Edge(-(2 << 16), 0), Edge(0, 0)), 1, 1, 0x7654);
        Assert.Equal(new byte[] { 0x54, 0x76, 0x54, 0x76 }, image.AsSpan(2044, 4).ToArray());
        Assert.Throws<NotSupportedException>(() => NativeSoftwareFlatScanlines.Draw463D50(image, new(Edge(-(2 << 16), 0), Edge(0, 0)), 0, 1, 0));
        Assert.Throws<InvalidDataException>(() => NativeSoftwareFlatScanlines.Draw463D50(image, new(new([], []), Edge(0, 0)), 0, 1, 0));
    }

    [Fact]
    public void Cold_image_and_edges_produce_the_exact_next_batch_and_match_one_contiguous_call()
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 5 * 2048).ToArray();
        byte[] continuous = image.ToArray();
        NativeSoftwareScanlineEdges initial = new(Edge(1 << 16, 1 << 15), Edge(6 << 16, -(1 << 15)));
        NativeSoftwareScanlineEdges first = NativeSoftwareFlatScanlines.Draw463D50(image, initial, 1, 2, 0xBEEF);
        Saved saved = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(new Saved(image.ToImmutableArray(), first)))!;
        byte[] coldImage = saved.Image.ToArray();
        NativeSoftwareScanlineEdges next = NativeSoftwareFlatScanlines.Draw463D50(image, first, 3, 2, 0xBEEF);
        NativeSoftwareScanlineEdges cold = NativeSoftwareFlatScanlines.Draw463D50(coldImage, saved.Edges, 3, 2, 0xBEEF);
        NativeSoftwareScanlineEdges whole = NativeSoftwareFlatScanlines.Draw463D50(continuous, initial, 1, 4, 0xBEEF);
        Assert.Equal(image, coldImage);
        Assert.Equal(image, continuous);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(cold));
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(whole));
    }

    private sealed record Saved(ImmutableArray<byte> Image, NativeSoftwareScanlineEdges Edges);
    private static int Read(ImmutableArray<byte> bytes) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan());
    private static NativeSoftwareEdgeBuffers Edge(int x, int stepX)
    {
        byte[] current = Enumerable.Repeat((byte)0xA5, 24).ToArray();
        byte[] step = Enumerable.Repeat((byte)0x5A, 24).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(current, x);
        BinaryPrimitives.WriteInt32LittleEndian(step, stepX);
        return new(current.ToImmutableArray(), step.ToImmutableArray());
    }
}
