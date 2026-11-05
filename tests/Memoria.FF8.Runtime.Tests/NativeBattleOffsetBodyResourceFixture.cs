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
        NativeBattlePartyCpuLayout layout = domain >= 3 ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
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
            Assert.Equal(cpu.EffectiveLayout, held.Cpu.EffectiveLayout);
            Assert.Equal(bytes.AsSpan(offset).ToArray(), NativeBattleModelResourceView.Open(Current, restored, held, bytes.Length - offset).Read(0, bytes.Length - offset));
            if (cpu.ResourceOnly)
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
