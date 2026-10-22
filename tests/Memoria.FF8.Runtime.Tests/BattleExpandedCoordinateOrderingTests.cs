using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleExpandedCoordinateOrderingTests
{
    private static readonly NativeBattlePrimitiveReference Reference = new("off_B6D070", 0, 0);
    private static readonly ImmutableArray<BattleSceneScreenPoint> Coordinates = [new(-40000, 800), new(1600, 800), new(1600, 1600)];

    [Fact]
    public void Pending_and_executed_coordinates_round_trip_without_changing_packet_words_or_payloads()
    {
        NativeBattleLogicalOrdering source = Ordering();
        NativeBattleLogicalOrdering restored = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(source))!;
        Assert.Equal(Coordinates, restored.Packets[0].ProjectedCoordinates!.Value);
        NativeBattleLogicalCommand command = Assert.Single(restored.Traverse(Lengths()));
        Assert.Equal(Coordinates, command.ProjectedCoordinates!.Value);
        Assert.Equal(Packet().Skip(1), command.Words);
        Assert.Equal(new int[] { 1000, 1100, 1200, 0 }, command.VertexPayload4);
        NativeBattleLogicalCommand restoredCommand = JsonSerializer.Deserialize<NativeBattleLogicalCommand>(JsonSerializer.Serialize(command))!;
        Assert.Equal(Coordinates, restoredCommand.ProjectedCoordinates!.Value);
        Assert.Equal(command.Words, restoredCommand.Words);
    }

    [Fact]
    public void Physical_reread_retains_coordinates_only_when_current_packed_coordinates_match()
    {
        NativeBattleLogicalOrdering ordering = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(JsonSerializer.Serialize(Ordering().ResetAfterDraw45D080()))!;
        uint[] words = Packet().ToArray();
        words[0] |= 0xD8E098;
        NativeBattleLogicalCommand retained = Assert.Single(ordering.Traverse(Lengths(), Reader(words)));
        Assert.Equal(Coordinates, retained.ProjectedCoordinates!.Value);
        words[2] ^= 1;
        NativeBattleLogicalCommand overwritten = Assert.Single(ordering.Traverse(Lengths(), Reader(words)));
        Assert.Null(overwritten.ProjectedCoordinates);
    }

    [Fact]
    public void Alias_replacement_uses_current_packet_coordinates_instead_of_the_earlier_proxy()
    {
        ImmutableArray<BattleSceneScreenPoint> replacement = [new(25536, 800), new(1600, 800), new(1600, 1600)];
        NativeBattleLogicalOrdering ordering = Ordering().Prepend(16, Reference, Packet(), 0, [2000, 2100, 2200, 0], projectedCoordinates: replacement);
        NativeBattleLogicalCommand command = Assert.Single(ordering.Traverse(Lengths()));
        Assert.Equal(17, command.OrderingBucket);
        Assert.Equal(replacement, command.ProjectedCoordinates!.Value);
        NativeBattleLogicalCommand cleared = Assert.Single(ordering.Prepend(15, Reference, Packet(), 0, [3000, 3100, 3200, 0]).Traverse(Lengths()));
        Assert.Null(cleared.ProjectedCoordinates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Physical_overlap_invalidates_coordinates_even_after_a_retained_packet_reset(bool reset)
    {
        NativeBattleLogicalOrdering source = reset ? Ordering().ResetAfterDraw45D080() : Ordering();
        NativeBattleLogicalOrdering.Builder builder = new(source);
        builder.InvalidatePacketRange(new(Reference.Bank, Reference.Buffer, 8), 4);
        NativeBattleLogicalPacket packet = Assert.Single(builder.ToImmutable().Packets);
        Assert.True(packet.RequiresPhysicalRead);
        Assert.Null(packet.ProjectedCoordinates);
        Assert.Equal(Coordinates, source.Packets[0].ProjectedCoordinates!.Value);
    }

    private static NativeBattleLogicalOrdering Ordering() => NativeBattleLogicalOrdering.Clear().Prepend(17, Reference, Packet(), 0, [1000, 1100, 1200, 0], projectedCoordinates: Coordinates);

    private static ImmutableArray<uint> Packet() => [0x07000000, 0x24808080, Pack(Coordinates[0]), 0, Pack(Coordinates[1]), 0, Pack(Coordinates[2]), 0];

    private static uint Pack(BattleSceneScreenPoint point) => unchecked((ushort)point.X) | unchecked((uint)point.Y << 16);

    private static ImmutableArray<byte> Lengths()
    {
        byte[] lengths = new byte[256];
        lengths[0x24] = 7;
        return lengths.ToImmutableArray();
    }

    private static Func<NativeBattlePrimitiveReference, int, byte[]> Reader(uint[] words)
    {
        return (_, length) =>
        {
            byte[] bytes = new byte[length];
            for (int index = 0; index < length / 4; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
            }
            return bytes;
        };
    }
}
