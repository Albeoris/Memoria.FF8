using Albeoris.Games.Core.NSCompression.LZS;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Resources.Tests;

public sealed class SharedLzsKernelTests
{
    private static readonly byte[] Encoded = [7, 65, 66, 67, 0xEE, 0xF3];
    private static readonly byte[] Decoded = "ABCABCABC"u8.ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(64)]
    public async Task KnownSizeSyncAndAsyncPartialChunksMatch(int chunk)
    {
        foreach (bool asynchronous in new[] { false, true })
        {
            using OneByteInput input = new(Encoded);
            using LZSDecompressionStream stream = new(input, 0, Decoded.Length, true);
            using MemoryStream output = new();
            byte[] buffer = new byte[chunk + 4];
            int read;
            while ((read = asynchronous ? await stream.ReadAsync(buffer, 2, chunk, CancellationToken.None) : stream.Read(buffer, 2, chunk)) != 0)
            {
                Assert.Equal(0, buffer[0]);
                Assert.Equal(0, buffer[^1]);
                output.Write(buffer, 2, read);
            }
            Assert.Equal(Decoded, output.ToArray());
            Assert.Equal(Encoded.Length, input.Position);
        }
    }

    [Fact]
    public async Task MixedSyncAsyncAndKnownSizeClippingRetainPendingReference()
    {
        using MemoryStream input = new(Encoded);
        using LZSDecompressionStream stream = new(input, -1, 9, true);
        byte[] actual = new byte[9];
        Assert.Equal(1, stream.Read(actual, 0, 1));
        Assert.Equal(3, await stream.ReadAsync(actual, 1, 3, CancellationToken.None));
        Assert.Equal(2, stream.Read(actual, 4, 2));
        Assert.Equal(3, await stream.ReadAsync(actual, 6, 3, CancellationToken.None));
        Assert.Equal(Decoded, actual);
        using MemoryStream clippedInput = new([1, 65, 0xEE, 0xF2, 255]);
        using LZSDecompressionStream clipped = new(clippedInput, 0, 2, true);
        Assert.Equal(new byte[] { 65, 65 }, ReadAll(clipped));
        Assert.Equal(4, clippedInput.Position);
        Assert.Equal(0, clipped.Read(new byte[1], 0, 1));
        foreach (long length in new long[] { 0, -1 })
        {
            using MemoryStream emptyInput = new([255]);
            using LZSDecompressionStream empty = new(emptyInput, 0, length, true);
            Assert.Equal(0, empty.Read(new byte[1], 0, 1));
            Assert.Equal(0, await empty.ReadAsync(new byte[1], 0, 1, CancellationToken.None));
            Assert.Equal(0, emptyInput.Position);
        }
    }

    [Fact]
    public async Task KnownSizeEofKeepsItsExceptionAndCancellationContract()
    {
        foreach (byte[] source in new byte[][] { [], [0], [0, 0xEE] })
        {
            using MemoryStream input = new(source);
            using LZSDecompressionStream stream = new(input, source.Length, 1, true);
            Exception sync = Assert.Throws<Exception>(() => stream.Read(new byte[1], 0, 1));
            Assert.Equal("Unexpected end of stream.", sync.Message);
            using MemoryStream asyncInput = new(source);
            using LZSDecompressionStream asyncStream = new(asyncInput, source.Length, 1, true);
            Exception asynchronous = await Assert.ThrowsAsync<Exception>(() => asyncStream.ReadAsync(new byte[1], 0, 1, CancellationToken.None));
            Assert.Equal(sync.Message, asynchronous.Message);
        }
        using MemoryStream cancelInput = new([1, 65, 0xEE, 0xF2]);
        using LZSDecompressionStream canceled = new(cancelInput, 0, 7, true);
        Assert.Equal(2, canceled.Read(new byte[2], 0, 2));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Equal(0, await canceled.ReadAsync([], 0, 0, cancellation.Token));
        Assert.Equal(4, await canceled.ReadAsync(new byte[4], 0, 4, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.ReadAsync(new byte[1], 0, 1, cancellation.Token));
    }

    [Fact]
    public void KnownSizeOwnershipAndReadValidationStayUnchanged()
    {
        MemoryStream retained = new(Encoded);
        LZSDecompressionStream stream = new(retained, 1, 9, true);
        Assert.Throws<ArgumentNullException>(() => stream.Read(null!, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(new byte[2], -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Read(new byte[2], 1, 2));
        stream.Dispose();
        Assert.True(retained.CanRead);
        Assert.False(stream.CanRead);
        Assert.Throws<ObjectDisposedException>(() => stream.Read(new byte[1], 0, 1));
        retained.Dispose();
        MemoryStream owned = new(Encoded);
        new LZSDecompressionStream(owned, 0, 9, false).Dispose();
        Assert.False(owned.CanRead);
    }

    [Fact]
    public void UnknownSizeRequiresCompleteTokensAndPreservesStrictLimits()
    {
        Assert.Empty(Lzss.DecodeRaw([], 0));
        Assert.Equal(new byte[] { 65 }, Lzss.DecodeRaw([255, 65], 1));
        Assert.Equal(new byte[3], Lzss.DecodeRaw([0, 0, 0], 3));
        Assert.Equal("ABABABABABA"u8.ToArray(), Lzss.DecodeRaw([3, 65, 66, 0xEE, 0xF6]));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([255]));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([0, 0]));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([1, 65, 0]));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([1, 65], 0));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([0, 0, 0], 2));
        Assert.Throws<InvalidDataException>(() => Lzss.DecodeRaw([255, 1, 2, 3, 4, 5, 6, 7, 8, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Lzss.DecodeRaw([], -1));
        using MemoryStream input = new([1, 65, 0]);
        using LZSDecompressionStream clipped = new(input, 0, 1, true);
        Assert.Equal(new byte[] { 65 }, ReadAll(clipped));
        Assert.Equal(2, input.Position);
    }

    [Fact]
    public async Task DictionaryWrapAndZeroInitializationAgreeAcrossAdapters()
    {
        byte[] prefix = Enumerable.Range(0, 5000).Select(index => (byte)(index % 251)).ToArray();
        using MemoryStream encoded = new();
        for (int offset = 0; offset < prefix.Length; offset += 8)
        {
            encoded.WriteByte(255);
            encoded.Write(prefix, offset, 8);
        }
        int reference = (0xFEE + prefix.Length - 3) & 4095;
        encoded.WriteByte(0);
        encoded.WriteByte((byte)reference);
        encoded.WriteByte((byte)((reference >> 4) & 0xF0));
        byte[] expected = [.. prefix, .. prefix[^3..]];
        Assert.Equal(expected, Lzss.DecodeRaw(encoded.ToArray()));
        encoded.Position = 0;
        using LZSDecompressionStream stream = new(encoded, 0, expected.Length, true);
        using MemoryStream output = new();
        await stream.CopyToAsync(output, 7, TestContext.Current.CancellationToken);
        Assert.Equal(expected, output.ToArray());
        using MemoryStream zeroInput = new([0, 0, 0]);
        using LZSDecompressionStream zero = new(zeroInput, 0, 3, true);
        Assert.Equal(new byte[3], ReadAll(zero));
    }

    private static byte[] ReadAll(Stream stream)
    {
        using MemoryStream output = new();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private sealed class OneByteInput(byte[] source) : MemoryStream(source)
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => base.ReadAsync(buffer, offset, Math.Min(count, 1), cancellationToken);
    }
}
