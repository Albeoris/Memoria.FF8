using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalOrderingPhysicalAliasTests
{
    private const int Length = NativeBattlePrimitiveOrdering.BattleTableLength;
    private static readonly ImmutableArray<uint> Words = [0x04000000, 0x20010203, 1, 2, 3];

    [Theory]
    [InlineData((byte)0, Length + 1, (byte)1, 1)]
    [InlineData((byte)0, Length, (byte)1, 0)]
    [InlineData((byte)0, Length * 2 - 1, (byte)1, Length - 1)]
    [InlineData((byte)1, -1, (byte)0, Length - 1)]
    [InlineData((byte)1, -Length, (byte)0, 0)]
    public void Relative_root_addresses_reach_the_adjacent_retained_table_without_changing_the_active_selector(byte active, int relative, byte target, int index)
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear(active);
        using (NativeBattleLogicalOrdering.Builder builder = new(ordering))
        {
            builder.Prepend(relative, new("packet", active, 0), Words, 0, []);
            uint expectedPrevious = index == 0 ? uint.MaxValue : 0x1D8E058u + 17544u * target + 4u * (uint)(index - 1);
            Assert.Equal(expectedPrevious, builder.LastHeaderLink);
            builder.ToImmutable();
        }
        Assert.Equal(active, ordering.TableBuffer);
        Assert.False(ordering.Table[index].Proxy);
        Assert.True(ordering.OtherTable!.Value[index].Proxy);
        NativeBattleLogicalPacket packet = Assert.Single(ordering.Packets);
        Assert.Equal(target, packet.NextTableBuffer);
        Assert.Equal(new NativeBattlePrimitiveLink(false, index - 1), packet.Next);
        Assert.Empty(ordering.Traverse(Lengths()));
        ordering.SelectTable(target);
        NativeBattleLogicalCommand command = Assert.Single(ordering.Traverse(Lengths()));
        Assert.Equal(index, command.OrderingBucket);
    }

    [Theory]
    [InlineData((byte)0, -1)]
    [InlineData((byte)0, Length * 2)]
    [InlineData((byte)1, Length)]
    [InlineData((byte)1, -Length - 1)]
    [InlineData((byte)0, int.MaxValue)]
    [InlineData((byte)1, int.MinValue)]
    public void Addresses_outside_both_known_physical_tables_fail_without_any_insertion(byte active, int relative)
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear(active);
        string original = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        Assert.Throws<InvalidDataException>(() => ordering.Prepend(relative, new("packet", 0, 0), Words, 0, []));
        Assert.Equal(original, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)));
    }

    [Fact]
    public void Adjacent_table_aliases_require_both_full_banks_and_a_known_selector()
    {
        ImmutableArray<NativeBattlePrimitiveLink> roots = NativeBattleLogicalOrdering.Clear().Table.ToImmutableArray();
        NativeBattleLogicalOrdering missing = new(roots, [], [], 0, 0);
        NativeBattleLogicalOrdering truncated = new(roots, [], [], 0, 0, [new(false, -1)]);
        NativeBattleLogicalOrdering unknown = new(roots, [], [], 0, null, roots);
        Assert.Throws<InvalidDataException>(() => missing.Prepend(Length, new("packet", 0, 0), Words, 0, []));
        Assert.Throws<InvalidDataException>(() => truncated.Prepend(Length, new("packet", 0, 0), Words, 0, []));
        Assert.Throws<NotSupportedException>(() => unknown.Prepend(Length, new("packet", 0, 0), Words, 0, []));
        Assert.Empty(missing.Packets);
        Assert.Empty(truncated.Packets);
        Assert.Empty(unknown.Packets);
    }

    [Fact]
    public void Physical_alias_resolution_preserves_packet_identity_alignment_and_depth_payload_validation()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        Assert.Throws<InvalidDataException>(() => ordering.Prepend(Length + 1, new("packet", 2, 0), Words, 0, []));
        Assert.Throws<InvalidDataException>(() => ordering.Prepend(Length + 1, new("packet", 0, 1), Words, 0, []));
        Assert.Throws<InvalidDataException>(() => ordering.Prepend(Length + 1, new("packet", 0, 0), Words, 0, [1]));
        Assert.Empty(ordering.Packets);
        Assert.Empty(ordering.Proxies);
        Assert.False(ordering.OtherTable!.Value[1].Proxy);
    }

    [Fact]
    public void Alternate_root_mutations_obey_callback_prefix_commit_suffix_rollback_and_outer_disposal()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrderingTransaction transaction = new(ordering))
        {
            transaction.BeginCheckpoint();
            transaction.Writer.Prepend(Length + 1, new("packet", 0, 0), Words, 0, []);
            transaction.CommitCheckpoint();
            transaction.BeginCheckpoint();
            transaction.Writer.Prepend(Length + 1, new("packet", 0, 20), Words, 0, []);
            transaction.RollbackCheckpoint();
            transaction.Freeze();
        }
        Assert.Equal(1, ordering.ProxyCursor);
        Assert.Equal(new NativeBattlePrimitiveLink(true, 0), ordering.OtherTable!.Value[1]);
        string accepted = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        using (NativeBattleLogicalOrderingTransaction transaction = new(ordering))
        {
            transaction.View.SelectTable(1);
            transaction.Writer.Prepend(-1, new("packet", 1, 40), Words, 0, []);
        }
        Assert.Equal(accepted, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)));
    }

    [Fact]
    public void Captured_alternate_roots_and_link_bank_provenance_restore_independently_and_continue_identically()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering.Prepend(Length + 1, new("packet", 0, 0), Words, 0, []);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        NativeBattleLogicalOrdering restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(json)!);
        ordering.Prepend(Length + 1, new("packet", 0, 20), Words, 0, []);
        Assert.Equal(1, restored.ProxyCursor);
        Assert.Equal((byte)1, restored.Packets[0].NextTableBuffer);
        restored.Prepend(Length + 1, new("packet", 0, 20), Words, 0, []);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(restored)));
        restored.SelectTable(1);
        Assert.Equal(2, restored.Traverse(Lengths()).Length);
    }

    private static ImmutableArray<byte> Lengths()
    {
        byte[] lengths = new byte[256];
        lengths[0x20] = 4;
        return lengths.ToImmutableArray();
    }
}
