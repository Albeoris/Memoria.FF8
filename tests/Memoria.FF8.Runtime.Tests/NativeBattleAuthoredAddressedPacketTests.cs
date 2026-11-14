using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAuthoredAddressedPacketTests
{
    private const uint Root = NativeBattleEffectNodeMemory.ArenaAddress + 0x20;
    private const uint PriorRoot = Root - 4;
    private static readonly NativeBattlePrimitiveReference First = new("off_B6D070", 0, 0);
    private static readonly NativeBattlePrimitiveReference Second = new("off_B6D070", 0, 32);
    private static readonly NativeBattleModelTextureOwner Actor = new("actor");
    private static readonly ImmutableArray<byte> Lengths = new byte[256].ToImmutableArray().SetItem(0x24, 7);
    private static readonly ImmutableArray<uint> Triangle = [0x07000000, 0x24808080, 0, 0, 4, 0x00800000, 4u << 16, 0x0102];

    [Fact]
    public void Authored_private_links_and_payload_survive_json_without_reading_physical_padding()
    {
        var first = NativeBattleLogicalOrdering.Clear().PrependAddressed(PriorRoot, First, Triangle, 0, [], modelTextureOwner: Actor, retainAuthoredPayload: true);
        var second = first.Ordering.PrependAddressed(first.ProxyAddress!.Value, Second, Triangle, 0, [], modelTextureOwner: Actor, retainAuthoredPayload: true);
        string json = NativeBattleTestSnapshots.Json(second.Ordering);
        NativeBattleLogicalOrdering restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleLogicalOrdering>(json);

        Assert.Contains("AddressedNextAddress", json);
        Assert.Equal<uint?>(PriorRoot, restored.Packets[0].AddressedNextAddress);
        Assert.Equal(first.ProxyAddress, restored.Packets[1].AddressedNextAddress);
        Assert.Equal<byte?>((byte)(PriorRoot >> 24), restored.Proxies[0].NextHigh22);
        Assert.Equal<byte?>((byte)(first.ProxyAddress.Value >> 24), restored.Proxies[1].NextHigh22);
        ImmutableArray<NativeBattleLogicalCommand> commands = Traverse(restored, second.ProxyAddress!.Value, (_, _) => throw new InvalidOperationException("The authored payload has no physical reader."));
        Assert.Equal(new[] { Second, First }, commands.Select(command => command.Packet));
        Assert.All(commands, command =>
        {
            Assert.Equal(Actor, command.ModelTextureOwner);
            Assert.Equal(Triangle.Skip(1), command.Words);
        });
    }

    [Fact]
    public void Raw_overlap_reinstates_physical_reads_until_complete_authored_replacement()
    {
        var inserted = NativeBattleLogicalOrdering.Clear().PrependAddressed(PriorRoot, First, Triangle, 0, [], modelTextureOwner: Actor, retainAuthoredPayload: true);
        using NativeBattleLogicalOrdering.Builder builder = new(inserted.Ordering);
        builder.InvalidatePacketRange(First with { ByteOffset = 4 }, 1);
        NativeBattleLogicalOrdering invalidated = builder.ToImmutable();
        NativeBattleLogicalPacket invalidatedPacket = Assert.Single(invalidated.Packets);
        Assert.True(invalidatedPacket.RequiresPhysicalRead);
        Assert.Null(invalidatedPacket.AddressedNextAddress);
        Assert.Equal(NativeBattleModelTextureOwner.InvalidatedOwner, invalidatedPacket.ModelTextureOwner);
        byte[] physical = Packet(PriorRoot, 0x24808081);
        List<int> reads = [];
        NativeBattleLogicalCommand command = Assert.Single(Traverse(invalidated, inserted.ProxyAddress!.Value, Read));
        Assert.Equal(new[] { 4, 32 }, reads);
        Assert.Equal(0x24808081u, command.Words[0]);
        Assert.Equal(NativeBattleModelTextureOwner.InvalidatedOwner, command.ModelTextureOwner);

        NativeBattleModelTextureOwner replacement = new("replacement");
        var replaced = invalidated.PrependAddressed(uint.MaxValue, First, Triangle, 0, [], modelTextureOwner: replacement, retainAuthoredPayload: true);
        NativeBattleLogicalPacket restoredPacket = Assert.Single(replaced.Ordering.Packets);
        Assert.False(restoredPacket.RequiresPhysicalRead);
        Assert.Equal<uint?>(uint.MaxValue, restoredPacket.AddressedNextAddress);
        NativeBattleLogicalCommand current = Assert.Single(Traverse(replaced.Ordering, replaced.ProxyAddress!.Value, (_, _) => throw new InvalidOperationException("The replacement is authored.")));
        Assert.Equal(Triangle.Skip(1), current.Words);
        Assert.Equal(replacement, current.ModelTextureOwner);

        byte[] Read(NativeBattlePrimitiveReference reference, int count)
        {
            Assert.Equal(First, reference);
            reads.Add(count);
            return physical[..count];
        }
    }

    [Fact]
    public void Default_addressed_insertion_and_reset_keep_physical_authority()
    {
        var native = NativeBattleLogicalOrdering.Clear().PrependAddressed(PriorRoot, First, Triangle, 0, []);
        NativeBattleLogicalPacket packet = Assert.Single(native.Ordering.Packets);
        Assert.True(packet.RequiresPhysicalRead);
        Assert.Null(packet.AddressedNextAddress);
        Assert.DoesNotContain("AddressedNextAddress", NativeBattleTestSnapshots.Json(native.Ordering));
        byte[] physical = Packet(PriorRoot, 0x24808081);
        NativeBattleLogicalCommand command = Assert.Single(Traverse(native.Ordering, native.ProxyAddress!.Value, (reference, count) =>
        {
            Assert.Equal(First, reference);
            return physical[..count];
        }));
        Assert.Equal(0x24808081u, command.Words[0]);

        var authored = NativeBattleLogicalOrdering.Clear().PrependAddressed(PriorRoot, First, Triangle, 0, [], retainAuthoredPayload: true);
        NativeBattleLogicalPacket reset = Assert.Single(authored.Ordering.ResetAfterDraw45D080().Packets);
        Assert.True(reset.RequiresPhysicalRead);
        Assert.Null(reset.AddressedNextAddress);
    }

    private static ImmutableArray<NativeBattleLogicalCommand> Traverse(NativeBattleLogicalOrdering ordering, uint head, Func<NativeBattlePrimitiveReference, int, byte[]> read)
    {
        return ordering.Traverse(Lengths, read, Root, address => address switch
        {
            Root => new(head, 2),
            PriorRoot => new(uint.MaxValue, 1),
            _ => throw new InvalidOperationException("Unexpected private root.")
        });
    }

    private static byte[] Packet(uint previous, uint command)
    {
        byte[] bytes = new byte[32];
        for (int word = 0; word < Triangle.Length; word++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(word * 4), Triangle[word]);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x07000000u | (previous & 0xFFFFFFu));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), command);
        return bytes;
    }
}
