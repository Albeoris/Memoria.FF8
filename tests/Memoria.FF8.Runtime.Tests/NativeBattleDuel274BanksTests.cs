using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274BanksTests
{
    [Fact]
    public void NativeUnschedulingReturnsPrivateRowsWithoutChangingActiveHeadersOrUnknownBytes()
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1);
        (root, _) = root.Append(0x689990);
        NativeBattleEffectTaskPool target = NativeBattleEffectTaskPool.Initialize(0x25084A8, 0x20DFAB8, 116, 1);
        (target, _) = target.Append(0x689AC0);
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(0x2508478, 0x20E3AB8, 44, 2);
        (particles, _) = particles.Append(0x68D3B0);
        NativeBattleModulePoolTask task = particles.Slots[0].WriteWord(12, 17).WriteResourcePointer(16, new(new(0, 0x123), null, 20)).MarkPayloadUnknown(26, 2);
        particles = NativeBattleTaskFixture.EditPool(particles, data => data with { Slots = particles.Slots.SetItem(0, task) });
        ImmutableArray<NativeBattleEffectTaskPool> pools = [root, target, particles];
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pools);
        NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), root.DescriptorAddress) { Pools = pools };
        NativeBattleDuel274Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), new(1));

        NativeBattleModulePoolTask preservedTask = NativeBattleStorageSnapshots.Capture(task);
        frame = NativeBattleDuel274Banks.DetachUnscheduledPrivateBanks(frame);
        frame = NativeBattleTestSnapshots.RestoreJson<NativeBattleDuel274Frame>(NativeBattleTestSnapshots.Json(frame))!;

        Assert.Equal(new uint[] { 0x25084B8, 0x25084A8 }, frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(NativeBattleTestSnapshots.Json(root), NativeBattleTestSnapshots.Json(frame.Effect.Pools[0]));
        Assert.Equal(NativeBattleTestSnapshots.Json(target), NativeBattleTestSnapshots.Json(frame.Effect.Pools[1]));
        Assert.Equal(root.DescriptorAddress, frame.Effect.ReturnedDescriptor);
        Assert.Equal(preservedTask.Flags, BitConverter.ToUInt16(frame.Memory.Arena!.Read(0x4000, 2)));
        Assert.Equal(preservedTask.Callback, BitConverter.ToUInt32(frame.Memory.Arena.Read(0x4008, 4)));
        Assert.Equal((ushort)17, BitConverter.ToUInt16(frame.Memory.Arena.Read(0x400C, 2)));
        Assert.Equal(new NativeBattleModelCpuReference(0, 0x123), frame.Memory.Arena.ReadResourcePointer(0x4010)!.Cpu);
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena.Read(0x401A, 2));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect));
    }

    [Fact]
    public void ReassignedPrivateBankOverwritesItsReturnedPhysicalBytesAndPointerTags()
    {
        NativeBattleEffectTaskPool particles = NativeBattleEffectTaskPool.Initialize(0x2508498, 0x20E3AB8, 36, 1);
        (particles, _) = particles.Append(0x68B140);
        particles = NativeBattleTaskFixture.EditPool(particles, data => data with { Slots = particles.Slots.SetItem(0, particles.Slots[0].WriteResourcePointer(16, new(new(0, 0x123), null, 20)).MarkPayloadUnknown(30, 2)) });
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [particles]);
        NativeBattleOriginalEffectState effect = new(274, 273, new(274, 0x6897E0, 0x6897F0), new(0, 0, 8, 0, 0, 0), 0x25084B8) { Pools = [particles] };
        NativeBattleDuel274Frame frame = NativeBattleDuel274Banks.DetachUnscheduledPrivateBanks(new(memory, effect, NativeBattleFileStaging.Unknown(), new(1)));

        frame = NativeBattleDuel274Banks.Initialize508300(frame, 0x2508498, particles.BankAddress, 36, 1);

        NativeBattleEffectTaskPool replacement = Assert.Single(frame.Effect.Pools);
        Assert.Equal(0, replacement.Slots[0].Flags);
        Assert.Equal(0u, replacement.Slots[0].Callback);
        Assert.Equal(0u, replacement.Slots[0].ReadDword(16));
        Assert.Null(NativeBattleStorageSnapshots.Capture(replacement.Slots[0]).ResourcePointers);
        Assert.Null(NativeBattleStorageSnapshots.Capture(replacement.Slots[0]).UnknownPayloadRanges);
        Assert.Equal(replacement.Slots[0].ReadBytes(16, 4), frame.Memory.Arena!.Read(0x4010, 4));
    }
}
