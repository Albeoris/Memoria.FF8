using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePlacedModelCommandsTests
{
    private static readonly ImmutableArray<byte> Lengths = new byte[256].ToImmutableArray().SetItem(0x20, 4).SetItem(0x21, 4);
    private static readonly NativeBattlePrimitiveDrawMode Mode = new(0);
    private static readonly NativeBattlePrimitiveTables Tables = BuildTables();

    [Fact]
    public void Model_references_keep_multipass_order_packet_JSON_and_fork_lifetime_across_pages()
    {
        const int count = 260;
        NativeBattlePlacedModelPrimitives placed = Placement(count, 17);
        NativeBattleLogicalOrdering working = Insert(placed);
        NativeBattleLogicalOrdering literal = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrdering.Builder author = new(literal))
        {
            for (int row = 0; row < count; row++)
            {
                author.Prepend(17, placed.ReadPacket(row), placed.Batch.ReadWords(row).ToImmutableArray(), 0, [100, 110, 120, 0], 19, textureSourceOwner: "native-bank", presentationActorId: "extra-actor");
            }
            author.ToImmutable();
        }

        var expected = NativeBattleLogicalPasses.Execute(literal, Tables, Mode, 1);
        var actual = NativeBattleLogicalPasses.Execute(working, Tables, Mode, 1);
        Assert.Equal(expected.Final, actual.Final);
        Assert.Equal(2 * count, actual.Commands.Length);
        for (int delivery = 0; delivery < actual.Commands.Length; delivery++)
        {
            NativeBattleLogicalCommandReference command = actual.Commands[delivery].Command;
            Assert.True(command.HasModelPlacement);
            Assert.Equal((byte)(delivery / count + 1), actual.Commands[delivery].Pass);
            Assert.Equal((count - delivery % count - 1) * 20, command.Packet.ByteOffset);
            Assert.Equal(17, command.OrderingBucket);
            Assert.Equal(19, command.ModuleDraw);
            Assert.Equal("extra-actor", command.PresentationActorId);
        }
        string authoredJson = JsonSerializer.Serialize(expected.Commands);
        Assert.Equal(authoredJson, JsonSerializer.Serialize(actual.Commands));
        Assert.Equal(authoredJson, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleResolvedCommandList>(authoredJson)));

        NativeBattlePreparedPrimitiveFrame frame = new(actual.Commands, []);
        NativeBattlePreparedPrimitiveFrame fork = NativeBattleStorageSnapshots.Fork(frame);
        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in frame.LastCommands[256]), ref Unsafe.AsRef(in fork.LastCommands[256])));
        NativeBattleLogicalCommandReference retained = fork.LastCommands[256].Command;
        working.ResetAfterDraw45D080();
        Assert.True(retained.HasModelPlacement);
        Assert.True(retained.HasSameOwner(actual.Commands[256].Command));
        Assert.Equal(0x20000003u, retained.WordSpan[0]);
    }

    [Fact]
    public void Compound_private_and_alternate_links_use_current_literal_values()
    {
        NativeBattleLogicalOrdering compound = Insert(Placement(1, 17, compound: true));
        NativeBattleCommandPages<NativeBattleLogicalCommandReference> split = compound.TraverseReferences(Lengths);
        Assert.Equal(2, split.Length);
        Assert.False(split[0].HasModelPlacement);
        Assert.False(split[1].HasModelPlacement);
        Assert.Equal(1, split[0].WordOffset);
        Assert.Equal(5, split[1].WordOffset);
        Assert.Equal(0x210000FFu, split[1].WordSpan[0]);

        NativeBattleLogicalOrdering addressed = Insert(Placement(1, 17));
        NativeBattleCommandPages<NativeBattleLogicalCommandReference> privateCommands = addressed.TraverseReferences(Lengths, initialOrderingAddress: 0x20DFAB8u, readAddressedRoot: _ => new(0x1C48828u, 9));
        Assert.Equal(1, privateCommands.Length);
        Assert.False(privateCommands[0].HasModelPlacement);
        Assert.Equal(9, privateCommands[0].OrderingBucket);

        NativeBattleLogicalOrdering alternate = Insert(Placement(1, NativeBattlePrimitiveOrdering.BattleTableLength + 18));
        Assert.Equal(0, alternate.TraverseReferences(Lengths).Length);
        alternate.SelectTable(1);
        NativeBattleCommandPages<NativeBattleLogicalCommandReference> bankAlias = alternate.TraverseReferences(Lengths);
        Assert.Equal(1, bankAlias.Length);
        Assert.False(bankAlias[0].HasModelPlacement);
        Assert.Equal(18, bankAlias[0].OrderingBucket);
    }

    [Fact]
    public void Overlapping_native_write_and_replacement_proxy_do_not_reuse_retained_metadata()
    {
        NativeBattlePlacedModelPrimitives placed = Placement(1, 17);
        NativeBattlePrimitiveReference packet = placed.ReadPacket(0);
        NativeBattleLogicalOrdering ordering = Insert(placed);
        NativeBattleLogicalCommandReference retained = ordering.TraverseReferences(Lengths)[0];
        using (NativeBattleLogicalOrdering.Builder writer = new(ordering))
        {
            writer.InvalidatePacketRange(packet with { ByteOffset = 4 }, 4);
            writer.ToImmutable();
        }

        NativeBattleCommandPages<NativeBattleLogicalCommandReference> overlay = ordering.TraverseReferences(Lengths, (_, size) =>
        {
            byte[] bytes = new byte[size];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x04FFFFFFu);
            if (size > 4)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x20000088u);
            }
            return bytes;
        });
        Assert.Equal(1, overlay.Length);
        Assert.False(overlay[0].HasModelPlacement);
        Assert.Null(overlay[0].PresentationActorId);
        Assert.Equal(0x20000088u, overlay[0].WordSpan[0]);
        Assert.Equal("extra-actor", retained.PresentationActorId);
        Assert.Equal(0x20000000u, retained.WordSpan[0]);

        NativeBattleLogicalOrdering mixed = Insert(placed);
        using (NativeBattleLogicalOrdering.Builder writer = new(mixed))
        {
            // Leave the old root and proxy reachable, but replace its physical packet owner.
            writer.Prepend(16, packet, [0x04000000u, 0x200000BBu, 1, 2, 3], 1, []);
            writer.ToImmutable();
        }
        NativeBattleCommandPages<NativeBattleLogicalCommandReference> replaced = mixed.TraverseReferences(Lengths);
        Assert.Equal(1, replaced.Length);
        Assert.False(replaced[0].HasModelPlacement);
        Assert.Equal(17, replaced[0].OrderingBucket);
        Assert.Equal((ushort)0, replaced[0].Flags20);
        Assert.Equal(0x200000BBu, replaced[0].WordSpan[0]);
    }

    [Fact]
    public void Whole_model_walk_allocates_reference_pages_without_per_row_commands()
    {
        const int count = 2048;
        NativeBattleLogicalOrdering ordering = Insert(Placement(count, 17));
        _ = ordering.TraverseReferences(Lengths);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleCommandPages<NativeBattleLogicalCommandReference> references = ordering.TraverseReferences(Lengths);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(count, references.Length);
        foreach (ref readonly NativeBattleLogicalCommandReference reference in references)
        {
            Assert.True(reference.HasModelPlacement);
        }
        long payload = count * Unsafe.SizeOf<NativeBattleLogicalCommandReference>();
        Assert.InRange(allocated, payload, payload + 4096);
    }

    private static NativeBattleLogicalOrdering Insert(NativeBattlePlacedModelPrimitives placed)
    {
        using NativeBattleLogicalOrdering.Builder writer = new(NativeBattleLogicalOrdering.Clear());
        for (int index = 0; index < placed.Batch.Count; index++)
        {
            writer.PrependPlacedModel(placed, index);
        }
        return writer.ToImmutable();
    }

    private static NativeBattlePlacedModelPrimitives Placement(int count, int bucket, bool compound = false)
    {
        NativeBattleModelPrimitiveBatch.Builder batch = new();
        ImmutableArray<NativeBattlePrimitiveReference>.Builder packets = ImmutableArray.CreateBuilder<NativeBattlePrimitiveReference>(count);
        int emission = batch.BeginEmission(new("extra-actor", null, null, default, false, false));
        for (int row = 0; row < count; row++)
        {
            uint command = 0x20000000u | (uint)row;
            if (compound)
            {
                batch.AddRaw(bucket, [0x08000000u, command, 1, 2, 3, 0x210000FFu, 1, 2, 3], [100, 110, 120, 0], [], [], default, emission);
            }
            else
            {
                batch.AddRaw(bucket, [0x04000000u, command, 1, 2, 3], [100, 110, 120, 0], [], [], default, emission);
            }
            packets.Add(new("off_B6D070", 0, row * 20));
        }
        return new(batch.Freeze(), packets.MoveToImmutable(), 19, "native-bank");
    }

    private static NativeBattlePrimitiveTables BuildTables()
    {
        ImmutableArray<uint> empty = new uint[256].ToImmutableArray();
        ImmutableArray<uint> polygon = empty.SetItem(0x20, 1);
        return new(Lengths, new byte[256].ToImmutableArray(), empty, polygon, polygon);
    }
}
