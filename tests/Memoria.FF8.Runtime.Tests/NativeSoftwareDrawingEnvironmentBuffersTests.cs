using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using Xunit;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareDrawingEnvironmentBuffersTests
{
    [Fact]
    public void InitializationWritesBothCompleteRecordsWithoutPublishingOrReplacingRetainedGlobal()
    {
        ImmutableArray<byte> retained = Enumerable.Repeat((byte)0xA5, 92).ToImmutableArray();
        NativeSoftwareDrawingEnvironmentBuffers buffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(retained);
        byte[] expected = new byte[92];
        expected[4] = 0x40;
        expected[5] = 1;
        expected[6] = 216;
        expected[0x14] = 10;
        expected[0x16] = 1;
        expected[0x17] = 1;
        Assert.Equal(expected, buffers.Buffer0.ToArray());
        Assert.Equal(expected, buffers.Buffer1.ToArray());
        Assert.Equal(retained, buffers.CurrentGlobal!.Value);
        Assert.Null(NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(null).CurrentGlobal);
    }

    [Fact]
    public void ApplyingRetainedWarmRecordCopiesAllBytesAndPreservesOtherBufferAndOrigin()
    {
        NativeSoftwareDrawingEnvironmentBuffers buffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(null);
        byte[] custom = Enumerable.Repeat((byte)0xA5, 92).ToArray();
        Word(custom, 0, -20);
        Word(custom, 2, 12);
        Word(custom, 4, 400);
        Word(custom, 6, 220);
        Word(custom, 8, 7);
        Word(custom, 10, -9);
        buffers = buffers.ReplaceBuffer(1, custom.ToImmutableArray());
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironmentState.FromColdProcess() with { OriginX = 3, OriginY = -4, Debug = 1 };
        NativeSoftwareDrawingEnvironmentApplication applied = buffers.Apply45B660(1, environment);
        Assert.Equal(custom, applied.Buffers.CurrentGlobal!.Value.ToArray());
        Assert.Equal(buffers.Buffer0, applied.Buffers.Buffer0);
        Assert.Equal(custom, applied.Buffers.Buffer1.ToArray());
        Assert.Equal<short?>(-20, applied.Environment.TopLeftX);
        Assert.Equal<uint?>(379, applied.Environment.RightX);
        Assert.Equal<uint?>(231, applied.Environment.BottomY);
        Assert.Equal(10 << 16, applied.Environment.Derived!.DrawOffsetX);
        Assert.Equal(-13 << 16, applied.Environment.Derived.DrawOffsetY);
        Assert.Equal((short)-17, applied.Environment.Derived.Rectangle.X);
        Assert.Equal((short)8, applied.Environment.Derived.Rectangle.Y);
        Assert.Equal<int?>(3, applied.Environment.OriginX);
        Assert.Equal<uint?>(1, applied.Environment.Debug);
        Assert.Equal(new NativeSoftwareTextureCacheInvalidation(-17, 8, 382, 227), applied.Invalidation);
        string json = JsonSerializer.Serialize(applied.Buffers);
        NativeSoftwareDrawingEnvironmentBuffers restored = JsonSerializer.Deserialize<NativeSoftwareDrawingEnvironmentBuffers>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        NativeSoftwareDrawingEnvironmentApplication next = restored.Apply45B660(0, applied.Environment);
        Assert.Equal(buffers.Buffer0.ToArray(), next.Buffers.CurrentGlobal!.Value.ToArray());
        Assert.Equal(custom, next.Buffers.Buffer1.ToArray());
        Assert.Equal<uint?>(319, next.Environment.RightX);
    }

    [Fact]
    public void ApplyInvalidatesOffscreenModeWithoutDebugAndLeavesOrdinaryModeWithoutReceipt()
    {
        NativeSoftwareDrawingEnvironmentBuffers buffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(null);
        NativeSoftwareDrawingEnvironmentState environment = NativeSoftwareDrawingEnvironmentState.FromColdProcess();
        NativeSoftwareDrawingEnvironmentApplication ordinary = buffers.Apply45B660(0, environment);
        Assert.Null(ordinary.Invalidation);
        byte[] offscreen = buffers.Buffer1.ToArray();
        Word(offscreen, 0, 704);
        Word(offscreen, 2, 256);
        Word(offscreen, 4, 128);
        Word(offscreen, 6, 128);
        buffers = buffers.ReplaceBuffer(1, offscreen.ToImmutableArray());
        NativeSoftwareDrawingEnvironmentApplication captured = buffers.Apply45B660(1, ordinary.Environment);
        Assert.Equal(2u, captured.Environment.Derived!.Mode);
        Assert.Equal<uint?>(0, captured.Environment.Debug);
        Assert.Equal(new NativeSoftwareTextureCacheInvalidation(704, 256, 831, 383), captured.Invalidation);
        Assert.Equal(offscreen, captured.Buffers.CurrentGlobal!.Value.ToArray());
        NativeSoftwareDrawingEnvironmentApplication debug = buffers.Apply45B660(0, ordinary.Environment with { Debug = 1 });
        Assert.Equal(1u, debug.Environment.Derived!.Mode);
        Assert.Equal(new NativeSoftwareTextureCacheInvalidation(0, 0, 319, 215), debug.Invalidation);
    }

    [Fact]
    public void InvalidRecordOrMissingRetainedOriginFailsWithoutInventingWarmInputs()
    {
        NativeSoftwareDrawingEnvironmentBuffers buffers = NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(null);
        Assert.Throws<InvalidDataException>(() => buffers.ReplaceBuffer(0, new byte[91].ToImmutableArray()));
        Assert.Throws<InvalidDataException>(() => NativeSoftwareDrawingEnvironmentBuffers.Initialize500400(default(ImmutableArray<byte>)));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffers.Apply45B660(2, NativeSoftwareDrawingEnvironmentState.FromColdProcess()));
        NativeSoftwareDrawingEnvironmentState unknownOrigin = NativeSoftwareDrawingEnvironmentState.FromColdProcess() with { OriginX = null };
        Assert.Throws<NotSupportedException>(() => buffers.Apply45B660(0, unknownOrigin));
        Assert.Null(buffers.CurrentGlobal);
    }

    private static void Word(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), value);
}
