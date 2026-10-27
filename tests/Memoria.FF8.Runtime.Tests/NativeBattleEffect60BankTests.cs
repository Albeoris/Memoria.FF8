using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect60BankTests
{
    [Fact]
    public void Each_addressed_bank_relocates_its_own_channels_and_publishes_without_overwriting_the_other_owner()
    {
        NativeBattleCuragaFamilyProfile primary = NativeBattleCuragaFamilyProfile.ForGenericBank(60, 0x1797CC0);
        NativeBattleCuragaFamilyProfile secondary = NativeBattleCuragaFamilyProfile.ForGenericBank(60, 0x179C998);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            Effect60PrimaryExecutable = new(0, null) { ResourceAddress = primary.ExecutableAddress },
            Effect60SecondaryExecutable = new(0, null) { ResourceAddress = secondary.ExecutableAddress }
        };
        NativeBattleCuragaExecutableSource firstSource = Source(primary, 0x1000);
        NativeBattleCuragaExecutableSource secondSource = Source(secondary, 0x1200);
        NativeBattleCuragaExecutableState first = primary.RequireExecutable(memory).Initialize87EA20(firstSource);
        memory = primary.WithExecutable(memory, first);
        Assert.Equal(0u, secondary.RequireExecutable(memory).InitializationFlag);
        Assert.Null(secondary.RequireExecutable(memory).Data);
        NativeBattleCuragaExecutableState second = secondary.RequireExecutable(memory).Initialize87EA20(secondSource);
        memory = secondary.WithExecutable(memory, second);
        Assert.Same(first, primary.RequireExecutable(memory));
        Assert.Equal(primary.ExecutableAddress + 0x1000u, Read(first, 4));
        Assert.Equal(secondary.ExecutableAddress + 0x1200u, Read(second, 4));
        Assert.Equal(primary.ExecutableAddress + 24u, Read(first, 0x10C8));
        Assert.Equal(secondary.ExecutableAddress + 24u, Read(second, 0x12C8));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(secondSource.Data.AsSpan()));
        NativeBattleEffectMemory restored = RoundTrip(memory);
        Assert.Equal(NativeBattleStorageSnapshots.Capture(first.Data!).Data.ToArray(), NativeBattleStorageSnapshots.Capture(primary.RequireExecutable(restored).Data!).Data.ToArray());
        Assert.Equal(NativeBattleStorageSnapshots.Capture(second.Data!).Data.ToArray(), NativeBattleStorageSnapshots.Capture(secondary.RequireExecutable(restored).Data!).Data.ToArray());
        Assert.Same(second, second.Initialize87EA20(secondSource));
    }

    [Fact]
    public void Missing_secondary_state_and_cross_bank_publication_fail_instead_of_using_the_primary()
    {
        NativeBattleCuragaFamilyProfile primary = NativeBattleCuragaFamilyProfile.ForGenericBank(60, 0x1797CC0);
        NativeBattleCuragaFamilyProfile secondary = NativeBattleCuragaFamilyProfile.ForGenericBank(60, 0x179C998);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Effect60PrimaryExecutable = new(0, null) { ResourceAddress = primary.ExecutableAddress } };
        Assert.Throws<NotSupportedException>(() => secondary.RequireExecutable(memory));
        Assert.Throws<InvalidDataException>(() => primary.WithExecutable(memory, new(0, null) { ResourceAddress = secondary.ExecutableAddress }));
        Assert.Throws<InvalidDataException>(() => NativeBattleCuragaFamilyProfile.ForGenericBank(60, 0x1797CC4));
        Assert.Equal(0x7EE0F0u, primary.Callback(0x87E1A0));
        Assert.Equal(0x7F1A30u, secondary.Callback(0x87E1A0));
    }

    private static NativeBattleCuragaExecutableSource Source(NativeBattleCuragaFamilyProfile profile, int node)
    {
        byte[] bytes = new byte[profile.ExecutableLength];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)node);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(node + 0xC8), 24);
        return new(bytes.ToImmutableArray()) { ResourceAddress = profile.ExecutableAddress };
    }

    private static uint Read(NativeBattleCuragaExecutableState bank, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bank.Read(bank.ResourceAddress + (uint)offset, 4));
    }
    private static string SnapshotJson<T>(T value) => JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(value));
    private static T RoundTrip<T>(T value) => NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<T>(SnapshotJson(value))!);

}
