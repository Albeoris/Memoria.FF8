using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffectTaskPointerTests
{
    private const uint Bank = 0x20DFAB8 + 0x4000;

    [Fact]
    public void PointerMetadataDescribesTheSinglePhysicalPayloadAndOverlappingStoresInvalidateIt()
    {
        NativeBattleModulePoolTask task = Pool().Slots[0].WriteResourcePointer(16, new(new(0, 0x123), null, 20));
        Assert.Equal(0x196B13Bu, task.ReadDword(16));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), task.ReadResourcePointer(16).Cpu);
        Assert.NotNull(task.WriteByte(24, 9).ResourcePointers);
        Assert.Null(task.WriteByte(19, 0).ResourcePointers);
        Assert.Null(task.WriteWord(18, 0).ResourcePointers);
        Assert.Null(task.WriteDword(16, 0).ResourcePointers);
        Assert.Equal(0u, task.WriteDword(16, 0).ReadResourcePointer(16).StagingAddress);
        Assert.Equal(JsonSerializer.Serialize(task), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleModulePoolTask>(JsonSerializer.Serialize(task))));
        byte[] changed = task.Payload.ToArray();
        changed[4] ^= 1;
        Assert.Throws<InvalidDataException>(() => (task with { Payload = changed.ToImmutableArray() }).ReadResourcePointer(16));
        Assert.Throws<InvalidDataException>(() => task.WriteResourcePointer(45, new(null, 0x1000)));
        Assert.Throws<NotSupportedException>(() => task.WriteResourcePointer(16, null));
        Assert.DoesNotContain("ResourcePointers", JsonSerializer.Serialize(Pool().Slots[0]));
    }

    [Fact]
    public void ClosedBanksTransferActualHeadersPayloadsAndPointerTagsThenArenaOverwritesAreAuthoritative()
    {
        NativeBattleEffectTaskPool pool = Pool();
        (pool, _) = pool.Append(0x6CD750);
        pool = pool with { Slots = pool.Slots.SetItem(0, pool.Slots[0].WriteResourcePointer(16, new(new(0, 0x123), null, 20)).WriteByte(28, 0xA5)) };
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [pool]);
        Assert.Throws<NotSupportedException>(() => memory.Arena!.Read(0x4010, 4));
        Assert.True(NativeBattleEffectTaskMemory.TryReadPointer([pool], Bank + 16, out NativeBattleResourcePointer? pointer));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), pointer!.Cpu);
        NativeBattleOriginalEffectState effect = new(112, 111, new(112, 0x6CBBE0, 0x6CBBF0), new(0, 0, 8, 0, 0, 0), 0x2521950) { Pools = [pool] };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(memory, effect));
        pool = pool.Advance((current, _) => new(current, 2)).Pool;
        effect = effect with { Pools = [pool], ReturnedDescriptor = 0 };
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(memory, effect);
        transfer.Effect.Validate();
        Assert.Empty(transfer.Effect.Pools);
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), transfer.Memory.Arena!.ReadResourcePointer(0x4010)!.Cpu);
        Assert.Equal((byte)0xA5, transfer.Memory.Arena.Read(0x401C, 1)[0]);
        Assert.Equal(Bank + 48, BitConverter.ToUInt32(transfer.Memory.Arena.Read(0x4004, 4)));
        Assert.Equal(0x6CD5C0u, BitConverter.ToUInt32(transfer.Memory.Arena.Read(0x4008, 4)));
        Assert.Equal(new byte[] { 0, 0 }, transfer.Memory.Arena.Read(0x4000, 2));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(transfer.Memory))!;
        Assert.Equal(pointer, restored.Arena!.ReadResourcePointer(0x4010));
        NativeBattleEffectArena overwritten = restored.Arena.Write(0x4010, new byte[4]);
        Assert.Equal(0u, overwritten.ReadResourcePointer(0x4010)!.StagingAddress);
    }

    [Fact]
    public void DescriptorReassignmentReturnsActivePhysicalBytesWithoutCompletingTasks()
    {
        NativeBattleEffectTaskPool pool = Pool();
        NativeBattleModulePoolTask task = pool.Slots[0].WriteResourcePointer(16, new(new(0, 0x123), null, 20)).MarkPayloadUnknown(30, 2);
        pool = pool with { Slots = pool.Slots.SetItem(0, task) };
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [pool]);
        NativeBattleOriginalEffectState effect = new(274, 111, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0x25084B8) { Pools = [pool] };
        NativeBattleEffectTaskTransfer transfer = NativeBattleEffectTaskMemory.DetachArenaBank(memory, effect, pool.DescriptorAddress);
        Assert.Empty(transfer.Effect.Pools);
        Assert.Equal(effect.ReturnedDescriptor, transfer.Effect.ReturnedDescriptor);
        Assert.Equal(task.Flags, BitConverter.ToUInt16(transfer.Memory.Arena!.Read(0x4000, 2)));
        Assert.Equal(task.Callback, BitConverter.ToUInt32(transfer.Memory.Arena.Read(0x4008, 4)));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), transfer.Memory.Arena.ReadResourcePointer(0x4010)!.Cpu);
        Assert.Throws<NotSupportedException>(() => transfer.Memory.Arena.Read(0x401E, 1));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(transfer.Memory))!;
        Assert.Throws<NotSupportedException>(() => restored.Arena!.Read(0x401E, 1));
        NativeBattleEffectTaskPool replacement = NativeBattleEffectTaskPool.Initialize(pool.DescriptorAddress, Bank + 24, 48, 2);
        NativeBattleEffectMemory replaced = NativeBattleEffectTaskMemory.RevokeArenaOwnership(restored, [replacement]);
        Assert.Equal(task.Flags, BitConverter.ToUInt16(replaced.Arena!.Read(0x4000, 2)));
        Assert.Throws<NotSupportedException>(() => replaced.Arena.Read(0x4018, 1));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.DetachArenaBank(memory, effect, 0xBAD));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.DetachArenaBank(memory, effect with { Pools = [pool, pool] }, pool.DescriptorAddress));
    }

    private static NativeBattleEffectTaskPool Pool()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2521950, Bank, 48, 2);
        (pool, _) = pool.Append(0x6CD5C0);
        return pool;
    }
}
