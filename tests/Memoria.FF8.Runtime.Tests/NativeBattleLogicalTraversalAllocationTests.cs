using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalTraversalAllocationTests
{
    [Fact]
    public void EqualRootIndicesInDifferentBanksRemainDistinctAndTheirActualCycleStillFails()
    {
        NativeBattlePrimitiveReference first = new("20DFAB8", 0, 0);
        NativeBattlePrimitiveReference second = new("20DFAB8", 0, 16);
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        source = source with
        {
            Table = source.Table.SetItem(source.Table.Length - 1, new(false, 0)).SetItem(0, new(true, 0)),
            OtherTable = source.OtherTable!.Value.SetItem(0, new(true, 1)),
            Proxies = [new(first, 0, [], null) { NextHigh22 = 1 }, new(second, 0, [], null) { NextHigh22 = 255 }],
            Packets = [new(first, Tile(1), new(false, 0)) { NextTableBuffer = 1 }, new(second, Tile(2), new(false, -1)) { NextTableBuffer = 0 }]
        };
        Assert.Equal(new[] { first, second }, source.Traverse(Lengths()).Select(command => command.Packet));
        NativeBattleLogicalOrdering cyclic = source with
        {
            Proxies = source.Proxies.SetItem(1, source.Proxies[1] with { NextHigh22 = 1 }),
            Packets = source.Packets.SetItem(1, source.Packets[1] with { Next = new(false, 0) })
        };
        Assert.Contains("link cycle", Assert.Throws<InvalidDataException>(() => cyclic.Traverse(Lengths())).Message);
    }

    [Fact]
    public void RootProxyAndAddressedCyclesKeepTheirOwnVisitedIdentities()
    {
        NativeBattleLogicalOrdering roots = NativeBattleLogicalOrdering.Clear();
        roots = roots with { Table = roots.Table.SetItem(roots.Table.Length - 1, new(false, roots.Table.Length - 1)) };
        Assert.Contains("link cycle", Assert.Throws<InvalidDataException>(() => roots.Traverse(Lengths())).Message);
        NativeBattlePrimitiveReference reference = new("20DFAB8", 0, 0);
        NativeBattleLogicalOrdering proxy = NativeBattleLogicalOrdering.Clear().Prepend(17, reference, Tile(1), 0, []).Prepend(17, reference, Tile(2), 0, []);
        Assert.Contains("link cycle", Assert.Throws<InvalidDataException>(() => proxy.Traverse(Lengths())).Message);
        uint address = NativeBattleEffectNodeMemory.ArenaAddress;
        int reads = 0;
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => NativeBattleLogicalOrdering.Clear().Traverse(Lengths(), initialOrderingAddress: address, readAddressedRoot: current =>
        {
            Assert.Equal(address, current);
            reads++;
            return new(address, 17);
        }));
        Assert.Contains("addressed-root link cycle", error.Message);
        Assert.Equal(1, reads);
    }

    [Fact]
    public void PhysicalAliasesRereadBothCurrentHeadersAndPayloadsWithoutChangingEarlierCommands()
    {
        NativeBattlePrimitiveReference reference = new("off_B6D070", 1, 0);
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear().Prepend(17, reference, Tile(1), 0, []).ResetAfterDraw45D080();
        source = source with { Proxies = source.Proxies.SetItem(0, source.Proxies[0] with { Packet = new("off_B6D070", 0, 114688) }) };
        byte[] physical = new byte[16];
        BinaryPrimitives.WriteUInt32LittleEndian(physical, 0x03000000 | ((0x1D8E058u + 16 * 4) & 0xFFFFFF));
        BinaryPrimitives.WriteUInt32LittleEndian(physical.AsSpan(4), 0x62010203);
        BinaryPrimitives.WriteUInt32LittleEndian(physical.AsSpan(12), 0xABCD1234);
        List<int> reads = [];
        NativeBattleLogicalCommand first = Assert.Single(source.Traverse(Lengths(), Read));
        BinaryPrimitives.WriteUInt32LittleEndian(physical.AsSpan(4), 0x62040506);
        NativeBattleLogicalCommand second = Assert.Single(source.Traverse(Lengths(), Read));
        physical.AsSpan().Clear();
        Assert.Equal(new[] { 4, 16, 4, 16 }, reads);
        Assert.Equal(0x62010203u, first.Words[0]);
        Assert.Equal(0x62040506u, second.Words[0]);
        Assert.Equal(0xABCD1234u, first.Words[2]);
        Assert.Equal(0x62000001u, source.Packets[0].Words[1]);

        byte[] Read(NativeBattlePrimitiveReference selected, int length)
        {
            Assert.Equal(reference, selected);
            reads.Add(length);
            return physical.AsSpan(0, length).ToArray();
        }
    }

    [Fact]
    public void MissingOwnersAndDuplicatePacketValidationRetainTheirFailureBoundaries()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        NativeBattleLogicalOrdering missingProxy = source with { Table = source.Table.SetItem(source.Table.Length - 1, new(true, 0)) };
        Assert.Contains("missing proxy", Assert.Throws<InvalidDataException>(() => missingProxy.Traverse(Lengths())).Message);
        NativeBattlePrimitiveReference reference = new("20DFAB8", 0, 0);
        NativeBattleLogicalOrdering missingPacket = missingProxy with { Proxies = [new(reference, 0, [], null)] };
        Assert.Contains("unobserved", Assert.Throws<NotSupportedException>(() => missingPacket.Traverse(Lengths())).Message);
        NativeBattleLogicalPacket packet = new(reference, Tile(1), new(false, -1));
        NativeBattleLogicalOrdering duplicate = source with { Packets = [packet, packet], TableBuffer = null };
        Assert.Throws<ArgumentException>(() => duplicate.Traverse(Lengths()));
    }

    [Fact]
    public void EmptyBoundedRootWalkDoesNotAllocateAHeapVisitedSetPerDisplay()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear();
        ImmutableArray<byte> lengths = Lengths();
        _ = source.Traverse(lengths);
        long before = GC.GetAllocatedBytesForCurrentThread();
        int commands = 0;
        for (int callback = 0; callback < 100; callback++)
        {
            commands += source.Traverse(lengths).Length;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, commands);
        Assert.True(allocated < 64 * 1024, $"Bounded root walks allocated {allocated} bytes.");
    }

    private static ImmutableArray<uint> Tile(uint value) => [0x03000000, 0x62000000 | value, 0, 1];

    private static ImmutableArray<byte> Lengths()
    {
        byte[] lengths = new byte[256];
        lengths[0x62] = 3;
        return lengths.ToImmutableArray();
    }
}
