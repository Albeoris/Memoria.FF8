using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltingBubble43State([property: JsonRequired] NativeBattleCuragaState? Nodes, [property: JsonRequired] uint Counter2656220, [property: JsonRequired] uint Counter265587C, [property: JsonRequired] NativeBattleEffectArena Channels)
{
    [JsonIgnore]
    public int SourceArenaOffset => Nodes?.SourceArenaOffset ?? throw new NotSupportedException("Melting Bubble requires its executed source pointer.");
    [JsonIgnore]
    public int? PrimaryDrawCursor => Nodes?.PrimaryDrawCursor;
    [JsonIgnore]
    public int? SecondaryDrawCursor => Nodes?.SecondaryDrawCursor;

    public static NativeBattleMeltingBubble43State FromColdProcess(uint counter2656220, uint counter265587C, ImmutableArray<byte> concatenatedChannels)
    {
        if (concatenatedChannels.IsDefault || concatenatedChannels.Length != NativeBattleMeltingBubble43Profile.ChannelByteCount)
        {
            throw new InvalidDataException("Melting Bubble cold channels require all original addressed channel intervals.");
        }
        NativeBattleEffectArena channels = NativeBattleEffectArena.Clear();
        int source = 0;
        foreach (NativeBattleMeltingBubble43ChannelRegion region in NativeBattleMeltingBubble43Profile.ChannelRegions)
        {
            channels = channels.Write(checked((int)(region.Address - NativeBattleMeltingBubble43Profile.ChannelBase)), concatenatedChannels.AsSpan().Slice(source, region.Length));
            source = checked(source + region.Length);
        }
        NativeBattleMeltingBubble43State state = new(null, counter2656220, counter265587C, channels);
        state.Validate();
        return state;
    }

    public byte[] ReadChannel(uint address, int length)
    {
        Validate();
        RequireChannelRange(address, length);
        return Channels.Read(checked((int)(address - NativeBattleMeltingBubble43Profile.ChannelBase)), length);
    }

    public void Validate()
    {
        Channels.Validate();
        foreach (NativeBattleEffectStorageRange region in Channels.EnumerateDataRanges())
        {
            RequireRelativeRange(region.Offset, region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Channels.EnumerateUnknownRanges())
        {
            RequireRelativeRange(region.Offset, region.Length);
        }
        if (Nodes is { } nodes && (nodes.SourceArenaOffset < 0 || nodes.SourceArenaOffset > NativeBattleEffectArena.Length - 2 * NativeBattleMeltingBubble43Profile.DrawStep || nodes.PrimaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.SecondaryDrawCursor is < 0 or > NativeBattleEffectArena.Length || nodes.AuxiliaryCursor < 0 || nodes.AuxiliaryCursor > NativeBattleEffectArena.Length || nodes.CopiedCamera is not null))
        {
            throw new InvalidDataException("Melting Bubble requires bounded cursors and its canonical shared camera owner.");
        }
    }

    private static void RequireRelativeRange(int offset, int length)
    {
        if (offset < 0 || (ulong)NativeBattleMeltingBubble43Profile.ChannelBase + (uint)offset > uint.MaxValue)
        {
            throw new InvalidDataException("Melting Bubble channel storage leaves its original physical intervals.");
        }
        RequireChannelRange(checked(NativeBattleMeltingBubble43Profile.ChannelBase + (uint)offset), length);
    }

    private static void RequireChannelRange(uint address, int length)
    {
        if (length < 0 || !NativeBattleMeltingBubble43Profile.ChannelRegions.Any(region => address >= region.Address && (ulong)address + (uint)length <= (ulong)region.Address + (uint)region.Length))
        {
            throw new InvalidDataException("Melting Bubble channel read leaves its original typed intervals.");
        }
    }
}
