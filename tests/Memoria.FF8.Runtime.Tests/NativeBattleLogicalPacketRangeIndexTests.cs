using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalPacketRangeIndexTests
{
    [Fact]
    public void RandomizedRangesMatchIndependentFullScanAcrossDuplicateOffsetsAndPhysicalAliases()
    {
        Random random = new(82317);
        ImmutableArray<NativeBattleLogicalPacket> packets = Enumerable.Range(0, 600).Select(index =>
        {
            string bank = index % 5 == 0 ? "20DFAB8" : "off_B6D070";
            byte buffer = (byte)(index % 2);
            int offset = random.Next(256) * 4;
            return new NativeBattleLogicalPacket(new(bank, buffer, offset), Words(random.Next(1, 24)), new(false, -1))
            {
                RequiresPhysicalRead = index % 7 == 0,
                ProjectedCoordinates = index % 3 == 0 ? [new(1, 2)] : null
            };
        }).ToImmutableArray();
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear() with { Packets = packets };
        string original = JsonSerializer.Serialize(source);
        NativeBattleLogicalOrdering.Builder builder = new(source);
        ImmutableArray<NativeBattleLogicalPacket> expected = packets;
        for (int query = 0; query < 300; query++)
        {
            NativeBattlePrimitiveReference reference = new(query % 5 == 0 ? "20DFAB8" : "off_B6D070", (byte)(query % 2), random.Next(300) * 4);
            int length = random.Next(0, 65);
            expected = Invalidate(expected, reference, length);
            builder.InvalidatePacketRange(reference, length);
            Assert.Equal(expected, builder.ToImmutable().Packets);
        }
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void TouchingBoundariesAndNormalizedBufferAliasesInvalidateOnlyActualOverlaps()
    {
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear().Prepend(17, new("off_B6D070", 1, 0), Words(5), 0, [], projectedCoordinates: [new(1, 2)]);
        NativeBattleLogicalOrdering.Builder builder = new(source);
        builder.InvalidatePacketRange(new("off_B6D070", 0, 114688 - 4), 4);
        builder.InvalidatePacketRange(new("off_B6D070", 0, 114688 + 20), 4);
        builder.InvalidatePacketRange(new("off_B6D094", 0, 114688), 20);
        Assert.Same(source.Packets[0], builder.ToImmutable().Packets[0]);
        builder.InvalidatePacketRange(new("off_B6D070", 0, 114688 + 19), 1);
        Assert.True(builder.ToImmutable().Packets[0].RequiresPhysicalRead);
        Assert.Null(builder.ToImmutable().Packets[0].ProjectedCoordinates);
        Assert.False(source.Packets[0].RequiresPhysicalRead);
        Assert.NotNull(source.Packets[0].ProjectedCoordinates);
    }

    [Fact]
    public void RollbackRemovesRandomInsertionsAndRestoresReplacementsAndFrozenPredecessors()
    {
        NativeBattleLogicalOrdering.Builder seed = new(NativeBattleLogicalOrdering.Clear());
        for (int index = 0; index < 64; index++)
        {
            seed.Prepend(17, new("20DFAB8", 0, index * 32), Words(5), 0, []);
        }
        NativeBattleLogicalOrdering source = seed.ToImmutable();
        string original = JsonSerializer.Serialize(source);
        using NativeBattleLogicalOrderingTransaction transaction = new(source);
        Random random = new(9573);
        for (int callback = 0; callback < 40; callback++)
        {
            NativeBattleLogicalOrdering before = transaction.Freeze();
            string frozen = JsonSerializer.Serialize(before);
            transaction.BeginCheckpoint();
            int[] offsets = Enumerable.Range(0, 40).OrderBy(_ => random.Next()).Select(index => 4096 + index * 32).ToArray();
            foreach (int offset in offsets)
            {
                transaction.View.Prepend(18, new("20DFAB8", 0, offset), Words(random.Next(2, 64)), 0, []);
            }
            transaction.View.Prepend(19, new("20DFAB8", 0, random.Next(64) * 32), Words(128), 0, []);
            transaction.RollbackCheckpoint();
            Assert.Equal(frozen, JsonSerializer.Serialize(transaction.Freeze()));
            transaction.View.Prepend(20, new("20DFAB8", 0, callback * 32), Words(callback % 2 == 0 ? 2 : 16), 0, []);
            NativeBattleLogicalOrdering.Builder current = new(transaction.View);
            ImmutableArray<NativeBattleLogicalPacket> expected = transaction.Freeze().Packets;
            for (int query = 0; query < 20; query++)
            {
                NativeBattlePrimitiveReference reference = new("20DFAB8", 0, random.Next(2000) * 4);
                int length = random.Next(1, 70);
                expected = Invalidate(expected, reference, length);
                current.InvalidatePacketRange(reference, length);
            }
            Assert.Equal(expected, transaction.Freeze().Packets);
            Assert.Equal(frozen, JsonSerializer.Serialize(before));
        }
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void WarmRangeQueriesAllocateNoPerQueryViewsOrTraversalStacks()
    {
        NativeBattleLogicalOrdering.Builder builder = new(NativeBattleLogicalOrdering.Clear());
        for (int index = 0; index < 2048; index++)
        {
            builder.Prepend(17, new("20DFAB8", 0, index * 32), Words(5), 0, []);
        }
        NativeBattlePrimitiveReference reference = new("20DFAB8", 0, 32768);
        builder.InvalidatePacketRange(reference, 20);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int query = 0; query < 5000; query++)
        {
            builder.InvalidatePacketRange(reference, 20);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < 64 * 1024, $"Warm range queries allocated {allocated} bytes.");
    }

    private static ImmutableArray<NativeBattleLogicalPacket> Invalidate(ImmutableArray<NativeBattleLogicalPacket> packets, NativeBattlePrimitiveReference reference, int length)
    {
        if (length == 0)
        {
            return packets;
        }
        reference = NativeBattlePrimitiveProcess.NormalizeCommonReference(reference);
        ImmutableArray<NativeBattleLogicalPacket>.Builder result = packets.ToBuilder();
        for (int index = 0; index < result.Count; index++)
        {
            NativeBattleLogicalPacket packet = result[index];
            NativeBattlePrimitiveReference physical = NativeBattlePrimitiveProcess.NormalizeCommonReference(packet.Reference);
            if ((!packet.RequiresPhysicalRead || packet.ProjectedCoordinates is not null) && physical.Bank == reference.Bank && physical.Buffer == reference.Buffer && physical.ByteOffset < reference.ByteOffset + length && reference.ByteOffset < physical.ByteOffset + packet.Words.Length * 4)
            {
                result[index] = packet with { RequiresPhysicalRead = true, ProjectedCoordinates = null };
            }
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<uint> Words(int length)
    {
        return ImmutableArray.CreateRange(Enumerable.Range(0, length).Select(index => index == 0 ? (uint)(length - 1) << 24 : (uint)index));
    }
}
