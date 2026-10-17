using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRecoveryExecutableSource(ushort Effect, ImmutableArray<byte> Data);

/// <summary>Current complete addressed resource bank; cold flag1 and absolute pointers are authenticated, never inferred for an old save.</summary>
public sealed record NativeBattleRecoveryExecutableState([property: JsonRequired] ushort Effect, [property: JsonRequired] ImmutableArray<byte> Data)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<byte>? Channels { get; init; }

    public static NativeBattleRecoveryExecutableState FromAuthenticatedColdProcess(NativeBattleRecoveryExecutableSource source, ImmutableArray<byte> authenticatedLoaderChannels)
    {
        NativeBattleRecoveryExecutableState state = new(source.Effect, source.Data) { Channels = authenticatedLoaderChannels };
        state.Validate();
        if (state.InitializationFlag != 1 || authenticatedLoaderChannels.IsDefault || authenticatedLoaderChannels.Length != NativeBattleRecoveryProfile.ForEffect(source.Effect).ChannelLength || authenticatedLoaderChannels.Any(value => value != 0))
        {
            throw new InvalidDataException("Native recovery cold bank requires the authenticated original flag1.");
        }
        return state;
    }

    [JsonIgnore]
    public uint InitializationFlag => BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan().Slice(DirectoryOffset));
    private int DirectoryOffset => checked((int)(NativeBattleRecoveryProfile.ForEffect(Effect).DirectoryAddress - NativeBattleRecoveryProfile.ForEffect(Effect).ExecutableAddress));

    public void Validate()
    {
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(Effect);
        if (Data.IsDefault || Data.Length != profile.ExecutableLength || Channels is { } channels && (channels.IsDefault || channels.Length != profile.ChannelLength))
        {
            throw new InvalidDataException("Native recovery bank does not have its own original addressed extent.");
        }
    }

    public NativeBattleRecoveryExecutableState Initialize(NativeBattleRecoveryExecutableSource source)
    {
        Validate();
        if (source.Effect != Effect)
        {
            throw new InvalidDataException("Native recovery context cannot borrow another profile's executable bank.");
        }
        if (InitializationFlag != 0)
        {
            return this;
        }
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(Effect);
        byte[] written = NativeBattleEffectNodeBankFixups.Relocate(Data, profile.ExecutableAddress, DirectoryOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(written.AsSpan(DirectoryOffset), 1);
        return this with { Data = written.ToImmutableArray() };
    }

    public byte[] Read(uint address, int count)
    {
        Validate();
        uint start = NativeBattleRecoveryProfile.ForEffect(Effect).ExecutableAddress;
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        if (address >= start && address - start <= Data.Length && count <= Data.Length - (address - start))
        {
            return Data.AsSpan().Slice(checked((int)(address - start)), count).ToArray();
        }
        NativeBattleRecoveryProfile profile = NativeBattleRecoveryProfile.ForEffect(Effect);
        if (address < profile.ChannelAddress || address - profile.ChannelAddress > profile.ChannelLength || count > profile.ChannelLength - (address - profile.ChannelAddress))
        {
            throw new InvalidDataException("Native recovery read leaves its current executable and addressed channel owners.");
        }
        ImmutableArray<byte> channels = Channels ?? throw new NotSupportedException("Native recovery requires its actual retained zero-track channel bytes; a missing old owner is unknown.");
        return channels.AsSpan().Slice(checked((int)(address - profile.ChannelAddress)), count).ToArray();
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleRecoveryExecutableState>? RecoveryExecutables { get; init; }

    public NativeBattleRecoveryExecutableState? GetRecoveryExecutable(ushort effect)
    {
        _ = NativeBattleRecoveryProfile.ForEffect(effect);
        return RecoveryExecutables?.SingleOrDefault(bank => bank.Effect == effect);
    }

    public NativeBattleEffectMemory WithRecoveryExecutable(NativeBattleRecoveryExecutableState executable)
    {
        executable.Validate();
        ImmutableArray<NativeBattleRecoveryExecutableState> banks = RecoveryExecutables ?? [];
        int index = -1;
        for (int current = 0; current < banks.Length; current++)
        {
            if (banks[current].Effect == executable.Effect)
            {
                if (index != -1)
                {
                    throw new InvalidDataException("Native recovery executable owners must be unique by original effect.");
                }
                index = current;
            }
        }
        return this with { RecoveryExecutables = index < 0 ? banks.Add(executable) : banks.SetItem(index, executable) };
    }

    private void ValidateRecoveryExecutables()
    {
        if (RecoveryExecutables is not { } banks)
        {
            return;
        }
        if (banks.IsDefault || banks.Select(bank => bank.Effect).Distinct().Count() != banks.Length)
        {
            throw new InvalidDataException("Native recovery executable owners must be distinct current profile banks.");
        }
        foreach (NativeBattleRecoveryExecutableState bank in banks)
        {
            bank.Validate();
        }
    }
}
