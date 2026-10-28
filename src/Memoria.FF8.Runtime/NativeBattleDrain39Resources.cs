using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrain39Resources(NativeBattleEffectHeapAllocation Animation, uint Sound, ImmutableArray<byte> NodeBank, ImmutableArray<uint> FirstFlash, ImmutableArray<uint> SecondFlash)
{
    public ushort Effect { get; init; } = 39;
    [JsonIgnore]
    public NativeBattleDrainFamilyProfile Profile => NativeBattleDrainFamilyProfile.ForEffect(Effect);

    public const uint ChannelsAddress = 0x269E764;
    public const int ChannelsLength = 0x1E60;
    public ImmutableArray<byte>? Channels { get; init; }

    public byte[] ReadChannels(uint address, int count)
    {
        ImmutableArray<byte> bytes = Channels ?? throw new NotSupportedException("Native Drain requires its authenticated absolute channel tables.");
        if (bytes.IsDefault || bytes.Length != ChannelsLength || address < Profile.ChannelsAddress || count < 0 || address - Profile.ChannelsAddress > ChannelsLength || count > ChannelsLength - (address - Profile.ChannelsAddress))
        {
            throw new InvalidDataException("Native Drain channel lookup leaves its actual addressed zero-table source.");
        }
        return bytes.AsSpan().Slice(checked((int)(address - Profile.ChannelsAddress)), count).ToArray();
    }
}
public sealed record NativeBattleDrain39State([property: JsonRequired] int SourceArenaOffset, [property: JsonRequired] int? PrimaryDrawCursor, [property: JsonRequired] int? SecondaryDrawCursor, [property: JsonRequired] int AuxiliaryCursor, [property: JsonRequired] NativeBattleActionMatrix? CopiedCamera)
{
    public int? Emitters { get; init; }
    public int? Geometry { get; init; }
    public ushort? EmitterCursor { get; init; }
    public ushort? GeometryCursor { get; init; }
    public ushort? UnusedGeometryCounter { get; init; }
    public ushort? UnusedEmitterCounter { get; init; }
    public ushort? PassEmitterCount { get; init; }
    public ushort? PassGeometryCount { get; init; }
}
public sealed record NativeBattleDrain39Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleDrain39State State);
public sealed record NativeBattleDrain39BankState([property: JsonRequired, JsonConverter(typeof(NativeBattleNodeSource.Converter))] NativeBattleNodeSource? Data)
{
    public static NativeBattleDrain39BankState FromColdProcess() => new(Data: null);

    public void Validate()
    {
        if (Data is { } bytes && (bytes.Length != NativeBattleDrain39Nodes.BankLength || bytes.ReadUInt32((int)(NativeBattleDrain39Nodes.DirectoryAddress - NativeBattleDrain39Nodes.BankAddress)) != 1))
        {
            throw new InvalidDataException("Native Drain retained bank requires its complete absolute-pointer source and initialized directory DWORD.");
        }
    }
}

public sealed partial record NativeBattleOriginalEffectState
{
    public NativeBattleDrain39State? Drain39 { get; init; }
}

public sealed partial record NativeBattleEffectMemory
{
    public NativeBattleDrain39BankState? Drain39Bank
    {
        get => effectState.Drain39Bank;
        init => effectState = effectState with { Drain39Bank = value };
    }
}
