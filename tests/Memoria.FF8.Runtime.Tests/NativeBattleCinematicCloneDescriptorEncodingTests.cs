using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicCloneDescriptorEncodingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Clone_resource_dwords_survive_raw_copy_and_restore_without_rebinding_logical_sources(bool remastered, bool enemy)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        ImmutableArray<byte> bytes = Enumerable.Range(0, 64).Select(value => (byte)(value + 1)).ToImmutableArray();
        int origin = layout.BankLength + 8;
        NativeBattleModelCpuBank publication;
        if (enemy)
        {
            publication = NativeBattleModelCpuBank.Unknown(72).BindResource(8, bytes, 0, bytes.Length);
            cpu = cpu with { EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, publication) };
        }
        else
        {
            publication = cpu.Cpu.Banks[1].BindPartyResource(origin, bytes, 0, bytes.Length);
            cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(1, publication) } };
        }
        NativeBattleModelCpuResourceRow row = NativeBattleModelCpuResourceRow.Unknown();
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModelCpuReference reference = enemy ? new(3, 0x400 + index * 4) { Layout = layout, EnemyResourceId = 7, ResourceOffset = 8 + index * 4 } : new(1, origin + index * 4) { Layout = layout, ResourceOnly = true };
            row = row.Set(12 + index * 4, reference) with { Lengths = row.Lengths.SetItem(12 + index * 4, 16) };
        }
        NativeBattleEffectArena arena = NativeBattleCinematicActorResourceDescriptors.Refresh(NativeBattleEffectArena.Clear(), cpu, row, NativeBattleEffectNodeMemory.ArenaAddress + 0x100);
        NativeBattleEffectArena copied = NativeBattleEffectArena.CreateUnknown(16).CopyResourceSnapshot(arena, 0x130, 0, 16);
        NativeBattleEffectArena restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectArena>(NativeBattleTestSnapshots.Json(copied));

        Assert.Equal(arena.Read(0x130, 16), restored.Read(0, 16));
        Assert.Throws<NotSupportedException>(() => arena.Read(0x110, 12));
        for (int index = 0; index < 4; index++)
        {
            NativeBattleModelCpuReference reference = row.Require(12 + index * 4);
            uint expectedAddress = enemy ? reference.NativePlacementAddress : checked(layout.BaseAddress + (uint)layout.BankLength + (uint)reference.Offset);
            NativeBattleResourcePointer retained = restored.ReadResourcePointer(index * 4)!;
            Assert.Equal(expectedAddress, restored.ReadUInt32(index * 4));
            Assert.Equal(reference, retained.Cpu);
            Assert.Null(retained.Cpu!.TryPhysicalAddress());
            Assert.True(retained.NativeAddressEncoding);
            Assert.Equal(16, retained.Length);
            Assert.True(NativeBattleModelCpuBank.SamePublication(publication, retained.CpuPublication));
            Assert.Equal(bytes.AsSpan(index * 4, 16).ToArray(), NativeBattleModelResourceView.Open(cpu, NativeBattleFileStaging.Unknown(), retained, 16).Read(0, 16));
        }
    }
}
