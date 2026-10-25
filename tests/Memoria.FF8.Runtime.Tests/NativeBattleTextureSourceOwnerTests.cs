using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTextureSourceOwnerTests
{
    private const string StageOwner = "battle/a0stg008.x";

    [Fact]
    public void Source_owner_follows_physical_packet_commands_and_survives_serialization()
    {
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        ordering = ordering.Prepend(18, new("off_B6D070", 0, 0), Tile(), 1, [], textureSourceOwner: StageOwner);
        ordering = ordering.Prepend(17, new("off_B6D070", 0, 16), Tile(), 1, []);
        NativeBattleLogicalOrdering restored = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(ordering))!;
        ImmutableArray<NativeBattleLogicalCommand> commands = restored.Traverse(Lengths());
        Assert.Equal(StageOwner, commands[0].TextureSourceOwner);
        Assert.Null(commands[1].TextureSourceOwner);
        Assert.Equal(StageOwner, restored.Packets[0].TextureSourceOwner);
    }

    [Fact]
    public void Overlapping_writes_clear_retained_source_ownership_without_affecting_previous_snapshots()
    {
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear().Prepend(17, reference, Tile(), 1, [], textureSourceOwner: StageOwner).ResetAfterDraw45D080();
        Assert.True(source.Packets[0].RequiresPhysicalRead);
        Assert.Null(source.Packets[0].ProjectedCoordinates);
        NativeBattleLogicalOrdering.Builder builder = new(source);
        builder.InvalidatePacketRange(new("off_B6D070", 0, 16), 4);
        Assert.Equal(StageOwner, builder.ToImmutable().Packets[0].TextureSourceOwner);
        builder.InvalidatePacketRange(new("off_B6D070", 0, 12), 4);
        Assert.Null(builder.ToImmutable().Packets[0].TextureSourceOwner);
        Assert.Equal(StageOwner, source.Packets[0].TextureSourceOwner);
    }

    [Fact]
    public void Replacing_stage_packet_with_actor_packet_drops_source_owner()
    {
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering source = NativeBattleLogicalOrdering.Clear().Prepend(17, reference, Tile(), 1, [], textureSourceOwner: StageOwner);
        NativeBattleLogicalOrdering.Builder builder = new(source with { ProxyCursor = 0 });
        builder.Prepend(18, reference, Tile(), 1, []);
        Assert.Null(Assert.Single(builder.ToImmutable().Packets).TextureSourceOwner);
        Assert.Equal(StageOwner, source.Packets[0].TextureSourceOwner);
    }

    [Fact]
    public void Shared_transaction_preserves_owner_and_rolls_back_invalidations()
    {
        using NativeBattleLogicalOrderingTransaction transaction = new(NativeBattleLogicalOrdering.Clear());
        NativeBattleLogicalOrdering.Builder producer = new(transaction.View);
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        producer.Prepend(17, reference, Tile(), 1, [], textureSourceOwner: StageOwner);
        Assert.Equal(StageOwner, Assert.Single(transaction.Freeze().Traverse(Lengths())).TextureSourceOwner);
        transaction.BeginCheckpoint();
        producer.InvalidatePacketRange(reference, 4);
        Assert.Null(transaction.Freeze().Packets[0].TextureSourceOwner);
        transaction.RollbackCheckpoint();
        Assert.Equal(StageOwner, Assert.Single(transaction.Freeze().Traverse(Lengths())).TextureSourceOwner);
    }

    private static ImmutableArray<uint> Tile() => [0x03000000, 0x62010203, 0, 0x00100010];

    private static ImmutableArray<byte> Lengths()
    {
        byte[] lengths = new byte[256];
        lengths[0x62] = 3;
        return lengths.ToImmutableArray();
    }
}
