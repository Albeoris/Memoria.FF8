using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMeltingBubble43StateTests
{
    [Fact]
    public void Cold_channel_bytes_populate_only_the_28_original_intervals_in_declared_order()
    {
        ImmutableArray<byte> bytes = Enumerable.Range(0, NativeBattleMeltingBubble43Profile.ChannelByteCount).Select(index => (byte)(index % 251 + 1)).ToImmutableArray();
        NativeBattleMeltingBubble43State state = NativeBattleMeltingBubble43State.FromColdProcess(123, 456, bytes);
        Assert.Null(state.Nodes);
        Assert.Equal(123u, state.Counter2656220);
        Assert.Equal(456u, state.Counter265587C);
        Assert.Equal(28, NativeBattleMeltingBubble43Profile.ChannelRegions.Length);
        Assert.Equal(948, NativeBattleMeltingBubble43Profile.ChannelRegions.Sum(region => region.Length));
        int offset = 0;
        foreach (NativeBattleMeltingBubble43ChannelRegion region in NativeBattleMeltingBubble43Profile.ChannelRegions)
        {
            Assert.Equal(bytes.AsSpan().Slice(offset, region.Length).ToArray(), state.ReadChannel(region.Address, region.Length));
            offset += region.Length;
        }
        Assert.Throws<InvalidDataException>(() => state.ReadChannel(NativeBattleMeltingBubble43Profile.ChannelBase, 2));
        NativeBattleMeltingBubble43ChannelRegion first = NativeBattleMeltingBubble43Profile.ChannelRegions[0];
        Assert.Throws<InvalidDataException>(() => state.ReadChannel(first.Address + (uint)first.Length - 1, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(947)]
    [InlineData(949)]
    public void Cold_reader_requires_exact_concatenated_physical_bytes(int length)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleMeltingBubble43State.FromColdProcess(0, 0, new byte[length].ToImmutableArray()));
    }

    [Fact]
    public void Current_channel_bytes_and_unknown_masks_survive_snapshot_without_zero_hydration()
    {
        NativeBattleMeltingBubble43State state = NativeBattleMeltingBubble43State.FromColdProcess(0, 0, new byte[948].ToImmutableArray());
        uint address = NativeBattleMeltingBubble43Profile.ChannelRegions[0].Address;
        int offset = checked((int)(address - NativeBattleMeltingBubble43Profile.ChannelBase));
        state = state with { Channels = state.Channels.Write(offset, new byte[] { 0xCD, 0xAB }).MarkUnknown(offset + 2, 2) };
        NativeBattleMeltingBubble43State restored = JsonSerializer.Deserialize<NativeBattleMeltingBubble43State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Equal(new byte[] { 0xCD, 0xAB }, restored.ReadChannel(address, 2));
        Assert.Throws<NotSupportedException>(() => restored.ReadChannel(address + 2, 2));
        Assert.Equal(new byte[] { 0, 0 }, restored.ReadChannel(address + 4, 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stored_or_unknown_bytes_in_unowned_gaps_are_rejected(bool unknown)
    {
        NativeBattleMeltingBubble43State state = NativeBattleMeltingBubble43State.FromColdProcess(0, 0, new byte[948].ToImmutableArray());
        NativeBattleEffectArena channels = unknown ? state.Channels.MarkUnknown(0, 2) : state.Channels.Write(0, new byte[] { 1, 2 });
        Assert.Throws<InvalidDataException>(() => (state with { Channels = channels }).Validate());
    }
}
