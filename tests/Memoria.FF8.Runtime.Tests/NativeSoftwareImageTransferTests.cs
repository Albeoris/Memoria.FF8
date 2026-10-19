using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwareImageTransferTests
{
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

    private static ushort Word(byte[] image, int x, int y) => BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x)));
    private static void Put(byte[] image, int x, int y, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(2 * (y * 1024 + x)), value);
}
