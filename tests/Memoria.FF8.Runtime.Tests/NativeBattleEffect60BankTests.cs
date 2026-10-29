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

    [Fact]
    public void Generic_presentation_task_owners_keep_both_native_source_banks_and_callbacks_distinct()
    {
        NativeBattleCureFamilyPool descriptor = NativeBattleEffect60Profile.Pools[3];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(descriptor.Descriptor, descriptor.Bank, descriptor.Stride, descriptor.Capacity);
        (pool, int firstSlot) = pool.Append(NativeBattleEffect60Profile.GenericPrimaryCallback);
        (pool, int secondSlot) = pool.Append(NativeBattleEffect60Profile.GenericSecondaryCallback);
        pool = pool.WriteSlot(firstSlot, pool.Slots[firstSlot].WriteDword(0x30, 0x1797CC0));
        pool = pool.WriteSlot(secondSlot, pool.Slots[secondSlot].WriteDword(0x30, 0x179C998));
        NativeBattleOriginalEffectState effect = new(60, 59, new(60, NativeBattleEffect60Profile.Entry, NativeBattleEffect60Profile.Initializer), null, null) { Pools = [pool] };
        NativeBattleCuragaFrame frame = new(NativeBattleEffectMemory.FromColdProcess(), effect, new(0, 0, 0, 0, null));
        NativeBattleEffectHierarchy hierarchy = new(effect.Pools);
        NativeBattleCuragaFamilyProfile primary = NativeBattleCuragaFamilyProfile.ForGenericTask(frame, hierarchy.Address(0, firstSlot));
        NativeBattleCuragaFamilyProfile secondary = NativeBattleCuragaFamilyProfile.ForGenericTask(frame, hierarchy.Address(0, secondSlot));
        Assert.Equal(0x1797CC0u, primary.ExecutableAddress);
        Assert.Equal(0x179C998u, secondary.ExecutableAddress);
        Assert.Equal(pool.Slots[firstSlot].Callback, primary.Callback(0x87E1A0));
        Assert.Equal(pool.Slots[secondSlot].Callback, secondary.Callback(0x87E1A0));
        NativeBattlePilotVisualIdentity first = NativeBattlePilotVisualLifetimes.Identity(60, pool.Slots[firstSlot]);
        NativeBattlePilotVisualIdentity second = NativeBattlePilotVisualLifetimes.Identity(60, pool.Slots[secondSlot]);
        Assert.Equal(first.Bank, second.Bank);
        Assert.NotEqual(first.Slot, second.Slot);
        Assert.NotEqual(first.Generation, second.Generation);
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
