using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleLogicalPrimitiveTests
{
    [Fact]
    public void Producer_batch_matches_scalar_links_and_keeps_prior_snapshots_independent()
    {
        NativeBattleLogicalOrdering initial = NativeBattleLogicalOrdering.Clear().Prepend(17, new("off_B6D070", 0, 0), Tile(1), 1, []);
        string original = NativeBattleTestSnapshots.Json(initial);
        NativeBattleLogicalOrdering preserved = NativeBattleStorageSnapshots.Capture(initial);
        NativeBattleLogicalOrdering scalar = NativeBattleStorageSnapshots.Restore(preserved);
        using NativeBattleLogicalOrdering.Builder batch = new(initial);
        for (int index = 1; index < 200; index++)
        {
            NativeBattlePrimitiveReference reference = new("off_B6D070", 0, index * 16);
            scalar = scalar.Prepend(index % 29, reference, Tile((byte)index), 1, [], index % 3);
            batch.Prepend(index % 29, reference, Tile((byte)index), 1, [], index % 3);
        }
        batch.ToImmutable();
        batch.Dispose();
        NativeBattleLogicalOrdering published = NativeBattleStorageSnapshots.Capture(initial);
        Assert.Equal(NativeBattleTestSnapshots.Json(scalar), NativeBattleTestSnapshots.Json(published));
        Assert.Equal(NativeBattleTestSnapshots.Json(scalar.Traverse(Tables().WordLengths)), NativeBattleTestSnapshots.Json(published.Traverse(Tables().WordLengths)));
        initial.Prepend(17, new("off_B6D070", 0, 4000), Tile(1), 1, []);
        Assert.Equal(200, published.Proxies.Length);
        Assert.Equal(201, initial.Proxies.Length);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    [Fact]
    public void Retained_physical_packets_need_no_repeated_invalidation_but_replacements_are_indexed()
    {
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear().Prepend(17, reference, Tile(10), 1, []).ResetAfterDraw45D080();
        string original = NativeBattleTestSnapshots.Json(source);
        NativeBattleLogicalOrdering preserved = NativeBattleStorageSnapshots.Capture(source);
        using NativeBattleLogicalOrdering.Builder builder = new(source);
        builder.InvalidatePacketRange(reference, 16);
        Assert.Same(source.Packets[0], builder.ToImmutable().Packets[0]);
        builder.Prepend(18, reference, Tile(20), 1, []);
        NativeBattleLogicalPacket replacement = builder.ToImmutable().Packets[0];
        Assert.False(replacement.RequiresPhysicalRead);
        builder.InvalidatePacketRange(new("off_B6D070", 0, 12), 4);
        NativeBattleLogicalPacket invalidated = builder.ToImmutable().Packets[0];
        Assert.True(invalidated.RequiresPhysicalRead);
        builder.InvalidatePacketRange(reference, 16);
        Assert.Same(invalidated, builder.ToImmutable().Packets[0]);
        Assert.False(replacement.RequiresPhysicalRead);
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    [Fact]
    public void Producer_batch_preserves_physical_alias_cycles_and_proxy_capacity()
    {
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering.Builder aliases = new(NativeBattleLogicalOrdering.Clear());
        aliases.Prepend(17, reference, Tile(10), 1, []);
        aliases.Prepend(17, reference, Tile(20), 1, []);
        Assert.Single(aliases.ToImmutable().Packets);
        Assert.Throws<InvalidDataException>(() => aliases.ToImmutable().Traverse(Tables().WordLengths));

        NativeBattleLogicalOrdering.Builder full = new(NativeBattleLogicalOrdering.Clear());
        for (int index = 0; index < NativeBattlePrimitiveOrdering.ProxyCapacity; index++)
        {
            full.Prepend(17, new("off_B6D070", 0, index * 16), Tile(10), 1, []);
        }
        NativeBattleLogicalOrdering filled = full.ToImmutable();
        full.Dispose();
        NativeBattleLogicalOrdering before = NativeBattleStorageSnapshots.Capture(filled);
        NativeBattleLogicalOrdering after = filled.Prepend(18, new("off_B6D070", 0, 0), Tile(20), 1, []);
        Assert.Equal(NativeBattleTestSnapshots.Json(before), NativeBattleTestSnapshots.Json(after));
        Assert.Throws<InvalidDataException>(() => filled.Prepend(-1, reference, Tile(20), 1, []));
    }

    [Fact]
    public void Logical_banks_preserve_native_descending_bucket_and_same_bucket_LIFO_without_fake_addresses()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering = ordering.Prepend(17, new("off_B6D070", 0, 0), Tile(10), 1, []);
        ordering = ordering.Prepend(18, new("off_B6D070", 0, 16), Tile(20), 1, []);
        ordering = ordering.Prepend(17, new("off_B6D070", 0, 32), Tile(30), 1, []);
        ImmutableArray<NativeBattleLogicalCommand> commands = ordering.Traverse(Tables().WordLengths);
        Assert.Equal(new int[] { 16, 32, 0 }, commands.Select(command => command.Packet.ByteOffset));
        Assert.Equal(new int?[] { 18, 17, 17 }, commands.Select(command => command.OrderingBucket));
        NativeBattleLogicalOrdering restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleLogicalOrdering>(NativeBattleTestSnapshots.Json(ordering))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(commands), NativeBattleTestSnapshots.Json(restored.Traverse(Tables().WordLengths)));
    }

    [Fact]
    public void Preprocessing_and_opaque_pass_publish_texture_page_for_the_earlier_second_pass_flat_draw()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering = ordering.Prepend(10, new("off_B6D070", 0, 0), Triangle(0x20), 0, [100, 100, 100, 0]);
        ordering = ordering.Prepend(20, new("off_B6D070", 0, 32), Triangle(0x40), 0, [200, 200, 200, 0]);
        ordering = ordering.Prepend(30, new("off_B6D070", 0, 64), Tile(128), 1, [], 0);
        var actual = NativeBattleLogicalPasses.Execute(ordering, Tables(), new(null), 1);
        Assert.Equal((ushort)0x20, actual.Final.TexturePage);
        NativeBattleLogicalResolvedCommand flat = Assert.Single(actual.Commands, command => command.Pass == 2 && command.Command.Opcode == 0x62);
        Assert.Equal(1, flat.DrawMode.BlendMode);
        Assert.Equal(0, flat.Command.ModuleDraw);
        Assert.Equal(2, actual.Commands.Count(command => command.Pass == 0 && command.Command.Opcode == 0x24));
    }

    [Fact]
    public void Native_flagged_command_admission_does_not_allow_an_offscreen_polygon_to_change_texture_page()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(17, new("off_B6D070", 0, 0), Triangle(0x40), 0, [100, 100, 100, 0]);
        var actual = NativeBattleLogicalPasses.Execute(ordering, Tables(), new(0x20), 2);
        Assert.Empty(actual.Commands);
        Assert.Equal((ushort)0x20, actual.Final.TexturePage);
    }

    [Fact]
    public void Physical_packet_alias_cycle_is_retained_and_rejected_instead_of_flattened_into_a_sort()
    {
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(17, reference, Tile(10), 1, []).Prepend(17, reference, Tile(20), 1, []);
        Assert.Throws<InvalidDataException>(() => ordering.Traverse(Tables().WordLengths));
    }

    [Fact]
    public void Only_original_initialization_and_tail_reset_publish_the_selected_OT_descriptor()
    {
        NativeBattlePrimitiveProcess cold = NativeBattlePrimitiveProcess.FromColdProcess();
        Assert.All(cold.TablePerspective, value => Assert.Null(value));
        NativeBattlePrimitiveProcess initialized = cold.Initialize500400();
        Assert.False(initialized.TablePerspective[0]);
        Assert.Null(initialized.TablePerspective[1]);
        NativeBattlePrimitiveProcess tail = initialized.Reset500900(0x80);
        Assert.True(tail.TablePerspective[0]);
        Assert.Null(tail.TablePerspective[1]);
        Assert.Equal("off_B6D094", tail.PacketBank);
        Assert.Equal((ushort)0, tail.TexturePage);
        Assert.Equal(0, tail.Depth!.BucketMix);
        Assert.Equal(0.99f, tail.Depth.RemainingWeight);
        Assert.Throws<NotSupportedException>(() => (initialized with { DisableTableReset = null }).Reset500900(0));
        Assert.False((initialized with { DisableTableReset = true }).Reset500900(0).TablePerspective[0]);
    }

    private static ImmutableArray<uint> Tile(byte intensity) => [0x3000000, 0x62000000u | (uint)(intensity | intensity << 8 | intensity << 16), 0, 216u << 16 | 320];
    private static ImmutableArray<uint> Triangle(ushort page) => [0x7000000, 0x24808080, 0, 0, 80, (uint)page << 16, 80u << 16, 0];

    private static NativeBattlePrimitiveTables Tables()
    {
        byte[] lengths = new byte[256];
        byte[] flagged = new byte[256];
        uint[] pre = new uint[256];
        uint[] first = new uint[256];
        uint[] second = new uint[256];
        lengths[0x24] = 7;
        lengths[0x62] = 3;
        flagged[0x24] = 1;
        flagged[0x62] = 1;
        pre[0x24] = 0x462F60;
        first[0x24] = 0x4617F0;
        second[0x62] = 0x462720;
        return new(lengths.ToImmutableArray(), flagged.ToImmutableArray(), pre.ToImmutableArray(), first.ToImmutableArray(), second.ToImmutableArray());
    }
}
