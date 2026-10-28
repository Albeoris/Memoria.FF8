using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareImageTransferTests
{
    [Theory]
    [InlineData(0, 0, 1, 0, 5, 1)]
    [InlineData(0, 0, 0, 1, 5, 2)]
    [InlineData(3, 2, 700, 300, 7, 3)]
    [InlineData(1023, 511, 700, 300, 100, 100)]
    [InlineData(3, 2, 1023, 511, 100, 100)]
    [InlineData(1027, 516, 1034, 532, 3, 2)]
    [InlineData(0, 0, 0, 0, 5, 2)]
    [InlineData(0, 0, 10, 10, 0, 0)]
    public void Destination_publication_matches_full_scan_bytes_coverage_and_invalidation(int sourceX, int sourceY, int destinationX, int destinationY, ushort width, ushort height)
    {
        byte[] seed = Enumerable.Range(0, 4 * 12 * 2).Select(index => (byte)(index % 23 == 0 ? 0 : index)).ToArray();
        NativeBattleEffectTextureState original = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 12, 4, seed).Upload45BD30(1023, 511, 1, 1, [0x34, 0x12]);
        NativeBattleEffectTextureState reference = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(original));
        NativeBattlePcTextureWindowCache cache = CleanCache();
        NativeBattlePcTextureWindowCache expectedCache = cache;
        byte[] image = reference.ReadVram();
        byte[] packet = Packet(sourceX, sourceY, destinationX, destinationY, width, height);
        NativeBattleEffectTextureState before = reference;
        NativeSoftwareImageTransfer.Transfer45BB00(image, packet, (left, top, right, bottom) =>
        {
            reference = FullScanPublish(reference, image);
            expectedCache = expectedCache.Invalidate464850(left, top, right, bottom, before);
        });
        NativeBattleSoftwarePrimitiveExecution execution = Execution(original, cache);
        execution.ExecuteSoftware(Command(packet));
        Assert.Equal(image, execution.Textures.ReadVram());
        Assert.Equal(expectedCache.Rows.Select(row => row.DirtyMask), execution.Cache!.Rows.Select(row => row.DirtyMask));
        AssertCoverageEqual(reference, execution.Textures);
        if (ReferenceEquals(before.VramPublicationIdentity, reference.VramPublicationIdentity))
        {
            Assert.Same(original.VramPublicationIdentity, execution.Textures.VramPublicationIdentity);
        }
        object published = execution.Textures.VramPublicationIdentity;
        execution.FlushSoftwareImage();
        Assert.Same(published, execution.Textures.VramPublicationIdentity);
    }

    [Fact]
    public void Pending_fill_is_fully_published_before_the_transfer_reads_and_changes_its_destination()
    {
        NativeBattleEffectTextureState original = NativeBattleEffectTextureState.FromColdProcess();
        NativeBattleSoftwarePrimitiveExecution execution = Execution(original, CleanCache());
        NativeBattleLogicalCommand fill = new(new("test", 0, 0), 1, 0x02, [0x020000FF, 700u | 300u << 16, 3u | 1u << 16], 1, [], null);
        execution.ExecuteNormal(fill, new(0), 1);
        byte[] packet = Packet(700, 300, 800, 400, 3, 1);
        execution.ExecuteNormal(Command(packet), new(0), 1);
        Assert.Equal((ushort)31, execution.Textures.ReadVramWord(700, 300));
        Assert.Equal((ushort)31, execution.Textures.ReadVramWord(802, 400));
        Assert.Equal((ushort)0, execution.Textures.ReadVramWord(803, 400));
        ImmutableArray<NativeBattleEffectMemoryRegion> regions = NativeBattleStorageSnapshots.Capture(execution.Textures).VramRegions;
        Assert.Equal(2, regions.Length);
        Assert.Equal(2 * (300 * 1024 + 700), regions[0].Offset);
        Assert.Equal(2 * (400 * 1024 + 800), regions[1].Offset);
    }

    [Fact]
    public void Repeated_one_word_self_transfers_read_only_the_destination_and_keep_the_published_version()
    {
        NativeBattleEffectTextureState original = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [31, 0]);
        NativeBattleSoftwarePrimitiveExecution execution = Execution(original, CleanCache());
        NativeBattleLogicalCommand command = Command(Packet(0, 0, 0, 0, 1, 1));
        execution.ExecuteSoftware(command);
        ReadTraffic traffic = new();
        using (NativeBattleTelemetry.BeginScope(traffic))
        {
            for (int index = 0; index < 16; index++)
            {
                execution.ExecuteSoftware(command);
            }
        }
        Assert.Equal(16 * 2, traffic.VramReadBytes);
        Assert.Same(original.VramPublicationIdentity, execution.Textures.VramPublicationIdentity);
    }

    [Fact]
    public void Transfer_publishes_before_a_missing_cache_callback_fails_and_invalid_sources_still_fail()
    {
        NativeBattleEffectTextureState original = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 1, 1, [31, 0]);
        NativeBattleSoftwarePrimitiveExecution execution = Execution(original, CleanCache());
        execution.Cache = null;
        NativeBattleLogicalCommand command = Command(Packet(0, 0, 3, 0, 1, 1));
        Assert.Throws<NotSupportedException>(() => execution.ExecuteSoftware(command));
        Assert.Equal((ushort)31, execution.Textures.ReadVramWord(3, 0));
        object published = execution.Textures.VramPublicationIdentity;
        execution.FlushSoftwareImage();
        Assert.Same(published, execution.Textures.VramPublicationIdentity);
        NativeBattleSoftwarePrimitiveExecution invalid = Execution(original with { Slots = [] }, CleanCache());
        Assert.Throws<InvalidDataException>(() => invalid.ExecuteSoftware(command));
    }

    [Fact]
    public void HorizontalOverlapUsesLiveDwordGroupsThenOddWord()
    {
        byte[] image = new byte[1024 * 512 * 2];
        for (int x = 0; x < 6; x++)
        {
            Put(image, x, 0, (ushort)(x + 1));
        }
        int calls = 0;
        Assert.True(NativeSoftwareImageTransfer.Transfer45BB00(image, Packet(0, 0, 1, 0, 5, 1), (left, top, right, bottom) =>
        {
            Assert.Equal((1, 0, 5, 0), (left, top, right, bottom));
            Assert.Equal(new ushort[] { 1, 1, 2, 2, 4, 4 }, Enumerable.Range(0, 6).Select(x => Word(image, x, 0)));
            calls++;
        }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void DownwardOverlapReadsPreviousDestinationRow()
    {
        byte[] image = new byte[1024 * 512 * 2];
        Put(image, 0, 0, 7);
        Put(image, 0, 1, 9);
        Assert.True(NativeSoftwareImageTransfer.Transfer45BB00(image, Packet(0, 0, 0, 1, 1, 2), (_, _, _, _) => { }));
        Assert.Equal((ushort)7, Word(image, 0, 1));
        Assert.Equal((ushort)7, Word(image, 0, 2));
    }

    [Theory]
    [InlineData(1023, 511, 10, 20)]
    [InlineData(10, 20, 1023, 511)]
    public void BothSourceAndDestinationEdgesClampExtent(int sourceX, int sourceY, int destinationX, int destinationY)
    {
        byte[] image = new byte[1024 * 512 * 2];
        Put(image, sourceX, sourceY, 0x3456);
        Assert.True(NativeSoftwareImageTransfer.Transfer45BB00(image, Packet(sourceX, sourceY, destinationX, destinationY, 100, 100), (left, top, right, bottom) => Assert.Equal((destinationX, destinationY, destinationX, destinationY), (left, top, right, bottom))));
        Assert.Equal((ushort)0x3456, Word(image, destinationX, destinationY));
        Assert.Equal((ushort)0, Word(image, 9, 19));
    }

    [Fact]
    public void CoordinateMasksApplyBeforeCopyAndZeroExtentsStillInvalidate()
    {
        byte[] image = new byte[1024 * 512 * 2];
        Put(image, 3, 4, 0x5A5A);
        Assert.True(NativeSoftwareImageTransfer.Transfer45BB00(image, Packet(1027, 516, 1034, 532, 1, 1), (left, top, right, bottom) => Assert.Equal((10, 20, 10, 20), (left, top, right, bottom))));
        Assert.Equal((ushort)0x5A5A, Word(image, 10, 20));
        int calls = 0;
        Assert.False(NativeSoftwareImageTransfer.Transfer45BB00(image, Packet(0, 0, 10, 20, 0, 0), (left, top, right, bottom) =>
        {
            Assert.Equal((10, 20, 9, 19), (left, top, right, bottom));
            calls++;
        }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void BothExecutionRoutesUseCanonicalVramAndInvalidateCacheWithoutViewportChanges()
    {
        NativeBattleSoftwarePrimitiveState state = NativeBattleSoftwarePrimitiveState.FromInitializedRenderer();
        byte[] seed = new byte[1024 * 512 * 2];
        Put(seed, 0, 0, 0x1234);
        Put(seed, 1, 0, 0x5678);
        NativeBattleEffectTextureState textures = NativeBattleEffectTextureState.FromColdProcess().Upload45BD30(0, 0, 2, 1, seed.AsSpan(0, 4).ToArray());
        uint[] handlers = new uint[256];
        handlers[0x80] = 0x4670D0;
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null);
        cache = cache with { Rows = cache.Rows.Select(row => row with { DirtyMask = 0 }).ToImmutableArray() };
        NativeBattleSoftwarePrimitiveExecution execution = new(state, textures, new(handlers.ToImmutableArray()), true, cache);
        NativeBattleLogicalCommand command = new(new("test", 0, 0), 1, 0x80, [0x80000000, 0, 1, 0x00010002], 1, [], null);
        execution.ExecuteNormal(command, new(0), 0);
        Assert.Equal((ushort)0, Word(execution.Textures.ReadVram(), 2, 0));
        execution.ExecuteSoftware(command);
        Assert.Equal((ushort)0x1234, Word(execution.Textures.ReadVram(), 1, 0));
        Assert.Equal((ushort)0x5678, Word(execution.Textures.ReadVram(), 2, 0));
        execution.ExecuteNormal(command, new(0), 1);
        Assert.Equal((ushort)0x1234, Word(execution.Textures.ReadVram(), 2, 0));
        Assert.Equal(uint.MaxValue, execution.Cache!.Rows[0].DirtyMask);
        Assert.Same(state, execution.State);
        Assert.Throws<NotSupportedException>(() => execution.ExecuteNormal(command, new(0), 2));
    }

    [Fact]
    public void SoftwareDeliveryRequiresAuthenticHandler()
    {
        NativeBattleSoftwarePrimitiveExecution execution = new(NativeBattleSoftwarePrimitiveState.FromInitializedRenderer(), NativeBattleEffectTextureState.FromColdProcess(), new(new uint[256].ToImmutableArray()), true, NativeBattlePcTextureWindowCache.Reset4647A0(null));
        NativeBattleLogicalCommand command = new(new("test", 0, 0), 1, 0x80, [0x80000000, 0, 0, 0], 1, [], null);
        Assert.Throws<NotSupportedException>(() => execution.ExecuteSoftware(command));
    }

    private static byte[] Packet(int sourceX, int sourceY, int destinationX, int destinationY, ushort width, ushort height)
    {
        byte[] packet = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x80000000);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), unchecked((uint)(sourceX | (sourceY << 16))));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), unchecked((uint)(destinationX | (destinationY << 16))));
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(16), width | ((uint)height << 16));
        return packet;
    }

    private static NativeBattleLogicalCommand Command(byte[] packet) => new(new("test", 0, 0), 1, 0x80, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4 + index * 4))).ToImmutableArray(), 1, [], null);

    private static NativeBattlePcTextureWindowCache CleanCache()
    {
        NativeBattlePcTextureWindowCache cache = NativeBattlePcTextureWindowCache.Reset4647A0(null);
        return cache with { Rows = cache.Rows.Select(row => row with { DirtyMask = 0 }).ToImmutableArray() };
    }

    private static NativeBattleSoftwarePrimitiveExecution Execution(NativeBattleEffectTextureState textures, NativeBattlePcTextureWindowCache cache)
    {
        uint[] handlers = new uint[256];
        handlers[0x80] = 0x4670D0;
        return new(NativeBattleSoftwarePrimitiveState.FromInitializedRenderer(), textures, new(handlers.ToImmutableArray()), true, cache);
    }

    private static NativeBattleEffectTextureState FullScanPublish(NativeBattleEffectTextureState textures, byte[] image)
    {
        byte[] previous = textures.ReadVram();
        int first = 0;
        while (first < image.Length && image[first] == previous[first])
        {
            first++;
        }
        int end = image.Length;
        while (end > first && image[end - 1] == previous[end - 1])
        {
            end--;
        }
        return end > first ? textures.PublishVramSpan(first, image.AsSpan(first, end - first)) : textures;
    }

    private static void AssertCoverageEqual(NativeBattleEffectTextureState expected, NativeBattleEffectTextureState actual)
    {
        ImmutableArray<NativeBattleEffectMemoryRegion> left = NativeBattleStorageSnapshots.Capture(expected).VramRegions;
        ImmutableArray<NativeBattleEffectMemoryRegion> right = NativeBattleStorageSnapshots.Capture(actual).VramRegions;
        Assert.Equal(left.Length, right.Length);
        for (int index = 0; index < left.Length; index++)
        {
            Assert.Equal(left[index].Offset, right[index].Offset);
            Assert.Equal(left[index].Data, right[index].Data);
        }
    }

    private sealed class ReadTraffic : INativeBattleTelemetrySink
    {
        internal long VramReadBytes { get; private set; }

        public void Record(int siteId, NativeBattleTrafficKind kind, long payloadBytes)
        {
            if (siteId == 1242 && kind == NativeBattleTrafficKind.ZeroFill)
            {
                VramReadBytes += payloadBytes;
            }
        }
    }

    private static ushort Word(byte[] image, int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x)));
    private static void Put(byte[] image, int x, int y, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x)), value);
}
