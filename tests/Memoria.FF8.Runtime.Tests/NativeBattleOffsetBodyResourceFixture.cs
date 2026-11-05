using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

internal sealed record NativeBattleOffsetBodyResourceFixture(NativeBattleModelGeometrySource Source, NativeBattleModelCpuResources Current, NativeBattleResourcePointer Pointer)
{
    internal static NativeBattleOffsetBodyResourceFixture Create(byte[] bytes, int domain)
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        if (domain == 0)
        {
            NativeBattleResourcePointer shared = new(null, 0x1000, bytes.Length);
            return new(NativeBattleModelGeometrySource.OpenShared(shared, (offset, length) => bytes.AsSpan(offset, length).ToArray()), cpu, shared);
        }
        NativeBattlePartyCpuLayout layout = domain is 3 or 4 or 6 ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        if (domain is 5 or 6)
        {
            NativeBattleModelCpuBank enemy = NativeBattleModelCpuBank.Unknown(bytes.Length + 4).BindResource(4, bytes.ToImmutableArray(), 0, bytes.Length);
            cpu = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout), EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty.Add(7, enemy) };
            NativeBattleResourcePointer owned = new NativeBattleResourcePointer(new(3, 1024) { Layout = layout, EnemyResourceId = 7, ResourceOffset = 4 }, null, bytes.Length).Pin(cpu);
            cpu = cpu with { EnemyResources = ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty };
            return new(NativeBattleModelGeometrySource.Open(cpu, NativeBattleFileStaging.Unknown(), owned, (_, _) => throw new InvalidOperationException("Pinned enemy geometry must retain its owned publication.")), cpu, owned);
        }
        bool logical = domain is 2 or 4;
        int origin = logical ? layout.BankLength + 4 : 4;
        NativeBattleModelCpuBank bank = NativeBattleModelCpuBanks.Unknown(layout).Banks[0].BindPartyResource(origin, bytes.ToImmutableArray(), 0, bytes.Length);
        cpu = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) with { Banks = NativeBattleModelCpuBanks.Unknown(layout).Banks.SetItem(0, bank) } };
        NativeBattleResourcePointer pointer = new NativeBattleResourcePointer(new(0, origin) { Layout = layout, ResourceOnly = logical }, null, bytes.Length).Pin(cpu);
        cpu = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleModelGeometrySource source = NativeBattleModelGeometrySource.Open(cpu, NativeBattleFileStaging.Unknown(), pointer, (_, _) => throw new InvalidOperationException("Pinned body geometry must not read the replacement bank or shared memory."));
        return new(source, cpu, pointer);
    }

    internal void AssertCursor(NativeBattleFileStaging staging, uint field, int offset, byte[] bytes)
    {
        NativeBattleFileStaging restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleFileStaging>(NativeBattleTestSnapshots.Json(staging));
        NativeBattleResourcePointer held = restored.ReadResourcePointer(field)!;
        if (Pointer.Cpu is { } cpu)
        {
            Assert.Equal(cpu.Offset + offset, held.Cpu!.Offset);
            Assert.Equal(cpu.ResourceOnly, held.Cpu.ResourceOnly);
            Assert.Equal(cpu.EnemyResourceId, held.Cpu.EnemyResourceId);
            if (cpu.EnemyResourceId is not null)
            {
                Assert.Equal(cpu.ResourceOffset + offset, held.Cpu.ResourceOffset);
            }
            Assert.Equal(cpu.EffectiveLayout, held.Cpu.EffectiveLayout);
            Assert.Equal(bytes.AsSpan(offset).ToArray(), NativeBattleModelResourceView.Open(Current, restored, held, bytes.Length - offset).Read(0, bytes.Length - offset));
            if (cpu.ResourceOnly || cpu.EnemyResourceId is not null)
            {
                Assert.All(restored.ReadSnapshot(field, 4).Known, known => Assert.False(known));
            }
            else
            {
                Assert.Equal(cpu.PhysicalAddress + (uint)offset, restored.ReadUInt32(field));
            }
        }
        else
        {
            Assert.Equal(Pointer.StagingAddress + (uint)offset, held.StagingAddress);
        }
    }
}
