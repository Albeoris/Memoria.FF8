using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePrimitiveOrderingTests
{
    [Theory]
    [InlineData(0, 0x1D8E058u)]
    [InlineData(1, 0x1D924E0u)]
    public void Battle_table_has_the_original4386_reverse_links(byte buffer, uint address)
    {
        NativeBattlePrimitiveOrdering state = NativeBattlePrimitiveOrdering.InitializeBattle(buffer);
        Assert.Equal(address, state.TableAddress);
        Assert.Equal(4386, state.Table.Length);
        Assert.Equal(uint.MaxValue, state.Table[0]);
        Assert.Equal(address, state.Table[1]);
        Assert.Equal(address + 17536, state.Table[^1]);
        Assert.Empty(state.Traverse(Lengths()));
    }

    [Fact]
    public void Buckets_are_descending_and_each_bucket_is_lifo_with_preserved_pointer_high_byte()
    {
        NativeBattlePrimitiveOrdering state = NativeBattlePrimitiveOrdering.InitializeBattle(0);
        state = state.Prepend(17, Packet(0x24CBD6C, 0x62010203, 0, 0));
        state = state.Prepend(19, Packet(0x24CBD80, 0x62040506, 0, 0));
        state = state.Prepend(17, Packet(0x24CBD94, 0x62070809, 0, 0), 0);

        Assert.Equal(1, state.Proxies[2].PreviousHighByte);
        Assert.Equal(NativeBattlePrimitiveOrdering.ProxyBankAddress & 0xFFFFFF, state.Packets[2].Words[0] & 0xFFFFFF);
        ImmutableArray<NativeBattlePrimitiveCommand> commands = state.Traverse(Lengths());
        Assert.Equal(new uint[] { 0x24CBD80, 0x24CBD94, 0x24CBD6C }, commands.Select(command => command.PacketAddress));
        Assert.Equal(new ushort[] { 1, 0, 1 }, commands.Select(command => command.ProxyFlags20));
    }

    [Fact]
    public void Combined_packet_processes_E1_before_the_following_flat_primitive_and_keeps_frame_state_unknown_until_owned()
    {
        NativeBattlePrimitiveOrdering state = NativeBattlePrimitiveOrdering.InitializeBattle(0).Prepend(17, Packet(0x24CBD6C, 0xE1000060, 0x62010203, 0, 0));
        ImmutableArray<NativeBattlePrimitiveCommand> commands = state.Traverse(Lengths());
        Assert.Equal(new byte[] { 0xE1, 0x62 }, commands.Select(command => command.Opcode));
        Assert.Equal(new int[] { 1, 2 }, commands.Select(command => command.WordOffset));
        NativeBattlePrimitiveDrawMode mode = new(null);
        Assert.Null(mode.BlendMode);
        Assert.Null(mode.Execute(commands[1]).BlendMode);
        mode = mode.Execute(commands[0]);
        Assert.Equal((ushort)0x60, mode.TexturePage);
        Assert.Equal(3, mode.Execute(commands[1]).BlendMode);
        Assert.Equal(3, new NativeBattlePrimitiveDrawMode(mode.TexturePage).BlendMode);
    }

    [Theory]
    [InlineData(0x24, 7, 4)]
    [InlineData(0x2C, 9, 4)]
    [InlineData(0x34, 9, 5)]
    [InlineData(0x3C, 12, 5)]
    public void Textured_polygon_tpage_overrides_E1_before_inherited_flat_draws(byte opcode, int words, int pageWord)
    {
        uint[] body = new uint[1 + words + 3];
        body[0] = 0xE1000220;
        body[1] = (uint)opcode << 24;
        body[1 + pageWord] = 0x00401234;
        body[1 + words] = 0x62010203;
        NativeBattlePrimitiveOrdering state = NativeBattlePrimitiveOrdering.InitializeBattle(0).Prepend(17, Packet(0x24CBD6C, body));
        ImmutableArray<NativeBattlePrimitiveCommand> commands = state.Traverse(Lengths());
        NativeBattlePrimitiveDrawMode mode = new(null);
        mode = mode.Execute(commands[0]);
        Assert.Equal(1, mode.BlendMode);
        mode = mode.Execute(commands[1]);
        Assert.Equal((ushort)0x40, mode.TexturePage);
        Assert.Equal(2, mode.Execute(commands[2]).BlendMode);
    }

    [Fact]
    public void Dispatch_selection_is_explicit_so_skipped_textured_handlers_cannot_change_transparent_pass_state()
    {
        NativeBattlePrimitiveCommand opaque = new(0x24CBD6C, 1, 0x24, [0x24000000, 0, 0, 0, 0x00600000, 0, 0], 1);
        NativeBattlePrimitiveCommand flat = new(0x24CBD80, 1, 0x62, [0x62000000, 0, 0], 1);
        uint[] opaqueHandlers = new uint[256];
        opaqueHandlers[0x24] = 0x4617F0;
        uint[] transparentHandlers = new uint[256];
        transparentHandlers[0x62] = 0x462720;
        ImmutableArray<NativeBattlePrimitiveCommand> commands = [flat, opaque];
        NativeBattlePrimitivePassResult first = new NativeBattlePrimitiveDrawMode(null).ExecutePass(commands, opaqueHandlers.ToImmutableArray());
        NativeBattlePrimitivePassResult second = first.DrawMode.ExecutePass(commands, transparentHandlers.ToImmutableArray());
        Assert.Equal(0x24, Assert.Single(first.Commands).Command.Opcode);
        NativeBattlePrimitiveResolvedCommand emitted = Assert.Single(second.Commands);
        Assert.Equal(0x62, emitted.Command.Opcode);
        Assert.Equal(3, emitted.DrawMode.BlendMode);
        Assert.Equal(first.DrawMode, second.DrawMode);
    }

    [Fact]
    public void Snapshot_keeps_physical_aliases_and_missing_or_cyclic_links_fail_instead_of_being_sorted()
    {
        NativeBattlePrimitiveOrdering state = NativeBattlePrimitiveOrdering.InitializeBattle(1).Prepend(17, Packet(0x24CBD6C, 0xE1000020));
        string json = JsonSerializer.Serialize(state);
        NativeBattlePrimitiveOrdering restored = JsonSerializer.Deserialize<NativeBattlePrimitiveOrdering>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(state.Traverse(Lengths()).Select(command => command.PacketAddress), restored.Traverse(Lengths()).Select(command => command.PacketAddress));

        NativeBattlePrimitiveOrdering missing = restored with { Packets = [] };
        Assert.Throws<NotSupportedException>(() => missing.Traverse(Lengths()));
        NativeBattlePrimitiveOrdering cyclic = restored.Prepend(17, Packet(0x24CBD6C, 0xE1000020));
        Assert.Throws<InvalidDataException>(() => cyclic.Traverse(Lengths()));
        NativeBattlePrimitiveOrdering broken = restored with { Table = restored.Table.SetItem(17, 0x2300000) };
        Assert.Throws<NotSupportedException>(() => broken.Traverse(Lengths()));
    }

    [Fact]
    public void Truncated_command_and_missing_length_table_are_explicit_boundaries()
    {
        NativeBattlePrimitiveOrdering state = NativeBattlePrimitiveOrdering.InitializeBattle(0).Prepend(17, Packet(0x24CBD6C, 0x62010203));
        Assert.Throws<InvalidDataException>(() => state.Traverse(Lengths()));
        Assert.Throws<InvalidDataException>(() => state.Traverse([]));
    }

    private static NativeBattlePrimitivePacket Packet(uint address, params uint[] body)
    {
        return new(address, ImmutableArray.Create((uint)body.Length << 24).AddRange(body));
    }

    private static ImmutableArray<byte> Lengths()
    {
        byte[] lengths = new byte[256];
        lengths[0xE1] = 1;
        lengths[0x62] = 3;
        lengths[0x24] = 7;
        lengths[0x2C] = 9;
        lengths[0x34] = 9;
        lengths[0x3C] = 12;
        return lengths.ToImmutableArray();
    }
}
