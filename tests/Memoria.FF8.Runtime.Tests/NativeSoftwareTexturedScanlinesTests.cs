using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareTexturedScanlinesTests
{
    private const int Texture = 8192;
    private const int Palette = 12288;

    [Fact]
    public void CrossedEdgesKeepTheirPreviousLocalOrderOnAnEqualXRowAndResetOnlyBetweenWholeCalls()
    {
        NativeSoftwareScanlineEdges edges = new(Edge(4 << 16, 0, 0, -(1 << 16)), Edge(2 << 16, 1024, 0, 1 << 16));
        byte[] continuous = Image();
        NativeSoftwareScanlineEdges next = Draw(continuous, edges, 0, 3);
        Assert.Equal((ushort)0x2222, Word(continuous, 2048 + 3 * 2));
        Assert.Equal((ushort)0x1111, Word(continuous, 4096 + 2 * 2));
        Assert.Equal(1 << 16, Read(next.Left.Current, 0));
        Assert.Equal(5 << 16, Read(next.Right.Current, 0));
        Assert.Equal(edges.Left.Step.ToArray(), next.Left.Step.ToArray());
        byte[] separate = Image();
        NativeSoftwareScanlineEdges boundary = Draw(separate, edges, 0, 1);
        Draw(separate, boundary, 1, 1);
        Assert.Equal((ushort)0x1111, Word(separate, 2048 + 3 * 2));
    }

    [Fact]
    public void ZeroWidthOddSpanIsCalledAndEvenEmptySpansStillAdvanceBothCanonicalEdges()
    {
        byte[] image = Image();
        Draw(image, new(Edge(3 << 16, 0, 0), Edge(3 << 16, 1024, 0)), 0, 1);
        Assert.Equal((ushort)0x1111, Word(image, 6));
        NativeSoftwareScanlineEdges edges = new(Edge(2 << 16, 1, 2, 0, 10, 20), Edge(2 << 16, 3, 4, 0, 30, 40));
        byte[] untouched = new byte[] { 0x5A };
        NativeSoftwareScanlineEdges advanced = NativeSoftwareTexturedScanlines.Draw463BC0(untouched, edges, NativeSoftwareReciprocalTable.Initialize463280(), 0, 3, 0, 0, 1, -1, -1);
        Assert.Equal(new byte[] { 0x5A }, untouched);
        Assert.Equal(31, Read(advanced.Left.Current, 0xC));
        Assert.Equal(62, Read(advanced.Left.Current, 0x10));
        Assert.Equal(93, Read(advanced.Right.Current, 0xC));
        Assert.Equal(124, Read(advanced.Right.Current, 0x10));
    }

    [Fact]
    public void FixedXUsesSignedFloorAndUvStepsUseStoredFloatReciprocalWithoutFractionalXCorrection()
    {
        byte[] negative = Image();
        Draw(negative, new(Edge(-1, 0, 0), Edge(0, 0, 0)), 1, 1);
        Assert.Equal((ushort)0x1111, Word(negative, 2046));
        byte[] image = Image();
        Draw(image, new(Edge(1, 1023, 0), Edge((6 << 16) + 1, 1026, 0)), 0, 1);
        Assert.Equal(new ushort[] { 0x1111, 0x2222, 0x2222, 0x2222, 0x2222, 0x2222 }, Enumerable.Range(0, 6).Select(index => Word(image, index * 2)));
    }

    [Fact]
    public void EachRowSeesActualEarlierVramWritesWhenItsTextureAliasesTheDestination()
    {
        byte[] image = Image();
        image[0] = 1;
        image[1] = 2;
        Put(image, Palette + 0x11 * 2, 0x7777);
        NativeSoftwareTexturedScanlines.Draw463BC0(image, new(Edge(0, 0, 0), Edge(2 << 16, 2048, 0)), NativeSoftwareReciprocalTable.Initialize463280(), 0, 2, 0, 0, 1, 0, Palette);
        Assert.Equal((ushort)0x1111, Word(image, 0));
        Assert.Equal((ushort)0x2222, Word(image, 2));
        Assert.Equal((ushort)0x7777, Word(image, 2048));
        Assert.Equal((ushort)0x7777, Word(image, 2050));
    }

    [Theory]
    [InlineData(0, 255, 1)]
    [InlineData(4, 0, 1)]
    [InlineData(4, 3, 1)]
    public void DispatchAdmitsOnlyTheActualSelectedOpaque8BitWriter(byte flags, byte variant, int depth)
    {
        byte[] image = Image();
        NativeSoftwareTexturedScanlines.Draw463BC0(image, new(Edge(0, 0, 0), Edge(1 << 16, 0, 0)), NativeSoftwareReciprocalTable.Initialize463280(), 0, 1, flags, variant, depth, Texture, Palette);
        Assert.Equal((ushort)0x1111, Word(image, 0));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(0, 0, 2)]
    [InlineData(0, 0, 3)]
    [InlineData(0, 0, -1)]
    [InlineData(4, 1, 1)]
    [InlineData(4, 2, 1)]
    [InlineData(4, 4, 1)]
    public void UnsupportedOrOutOfTableDispatchFailsBeforeWriting(byte flags, byte variant, int depth)
    {
        byte[] image = Image();
        byte[] before = image.ToArray();
        Assert.Throws<NotSupportedException>(() => NativeSoftwareTexturedScanlines.Draw463BC0(image, new(Edge(0, 0, 0), Edge(1 << 16, 0, 0)), NativeSoftwareReciprocalTable.Initialize463280(), 0, 1, flags, variant, depth, Texture, Palette));
        Assert.Equal(before, image);
    }

    [Fact]
    public void NonpositiveBatchReturnsUnchangedAndLiteralWidthOrWrappedUvCannotBeClamped()
    {
        NativeSoftwareScanlineEdges unknown = new(new(default, default), new(default, default));
        byte[] empty = [];
        Assert.Same(unknown, NativeSoftwareTexturedScanlines.Draw463BC0(empty, unknown, new(default), int.MaxValue, 0, 0, 0, 1, -1, -1));
        Assert.Same(unknown, NativeSoftwareTexturedScanlines.Draw463BC0(empty, unknown, new(default), int.MinValue, -1, 0, 0, 1, -1, -1));
        Assert.Throws<NotSupportedException>(() => Draw(Image(), new(Edge(0, 0, 0), Edge(3000 << 16, 0, 0)), 0, 1));
        byte[] wrapped = new byte[3 * 1024 * 1024];
        wrapped[2097151] = 1;
        Put(wrapped, 514, 0x1111);
        Assert.Throws<NotSupportedException>(() => NativeSoftwareTexturedScanlines.Draw463BC0(wrapped, new(Edge(0, int.MaxValue, 0), Edge(2 << 16, int.MinValue + 1, 0)), NativeSoftwareReciprocalTable.Initialize463280(), 0, 1, 0, 0, 1, 0, 512));
        Assert.Equal((ushort)0, Word(wrapped, 0));
    }

    [Fact]
    public void IndependentJsonImageAndCanonicalEdgeRestorePreservesTheNextWholeBatchAndUntouchedFields()
    {
        NativeSoftwareScanlineEdges edges = new(Edge(1 << 16, 0, 0, 100, 17, 29), Edge(4 << 16, 1024, 0, -100, 31, 43));
        byte[] image = Image();
        NativeSoftwareScanlineEdges boundary = Draw(image, edges, 0, 2);
        Saved saved = new(image, boundary);
        Saved restored = JsonSerializer.Deserialize<Saved>(JsonSerializer.Serialize(saved))!;
        Assert.NotSame(saved.Image, restored.Image);
        NativeSoftwareScanlineEdges next = Draw(saved.Image, saved.Edges, 2, 2);
        NativeSoftwareScanlineEdges coldNext = Draw(restored.Image, restored.Edges, 2, 2);
        Assert.Equal(saved.Image, restored.Image);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(coldNext));
        Assert.Equal(edges.Left.Current.AsSpan().Slice(4, 8).ToArray(), next.Left.Current.AsSpan().Slice(4, 8).ToArray());
        Assert.Equal(edges.Right.Step.ToArray(), next.Right.Step.ToArray());
    }

    private static NativeSoftwareScanlineEdges Draw(byte[] image, NativeSoftwareScanlineEdges edges, int startY, int count) => NativeSoftwareTexturedScanlines.Draw463BC0(image, edges, NativeSoftwareReciprocalTable.Initialize463280(), startY, count, 0, 0, 1, Texture, Palette);
    private static NativeSoftwareEdgeBuffers Edge(int x, int u, int v, int dx = 0, int du = 0, int dv = 0)
    {
        byte[] current = Enumerable.Repeat((byte)0xA5, 0x28).ToArray();
        byte[] step = Enumerable.Repeat((byte)0xB6, 0x28).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(current, x);
        BinaryPrimitives.WriteInt32LittleEndian(current.AsSpan(0xC), u);
        BinaryPrimitives.WriteInt32LittleEndian(current.AsSpan(0x10), v);
        BinaryPrimitives.WriteInt32LittleEndian(step, dx);
        BinaryPrimitives.WriteInt32LittleEndian(step.AsSpan(0xC), du);
        BinaryPrimitives.WriteInt32LittleEndian(step.AsSpan(0x10), dv);
        return new(current.ToImmutableArray(), step.ToImmutableArray());
    }
    private static byte[] Image()
    {
        byte[] image = Enumerable.Repeat((byte)0xA5, 16384).ToArray();
        image[Texture] = 1;
        image[Texture + 1] = 2;
        image[Texture + 2] = 3;
        Put(image, Palette, 0);
        Put(image, Palette + 2, 0x1111);
        Put(image, Palette + 4, 0x2222);
        Put(image, Palette + 6, 0x3333);
        return image;
    }
    private static int Read(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    private static ushort Word(byte[] image, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset, 2));
    private static void Put(byte[] image, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(offset, 2), value);
    private sealed record Saved(byte[] Image, NativeSoftwareScanlineEdges Edges);
}
