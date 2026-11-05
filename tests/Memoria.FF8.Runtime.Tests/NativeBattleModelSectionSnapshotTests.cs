using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelSectionSnapshotTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Selected_skeleton_padding_keeps_domain_knownness_and_pinned_publication_after_bank_replacement(bool remastered, bool logical)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        int first = logical ? layout.BankLength + 64 : 64;
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[1].BindPartyResource(first + 18, [0x42], 0, 1).BindPartyResource(first + 31, [0x99], 0, 1);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(1, bank) } };
        NativeBattleResourcePointer pointer = new(new(1, first) { Layout = layout, ResourceOnly = logical }, null, 32);
        pointer = pointer.Pin(cpu);
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelCpuResources replaced = cpu with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        NativeBattleResourcePointer restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleResourcePointer>(NativeBattleTestSnapshots.Json(pointer));
        (byte[] data, bool[] known) = NativeBattleModelSectionSnapshot.Read(replaced, staging, restored, 18, 2, (_, _) => throw new InvalidOperationException("CPU source must not become a shared numeric read."));
        Assert.Equal(new byte[] { 0x42, 0 }, data);
        Assert.Equal(new[] { true, false }, known);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelSectionSnapshot.Read(replaced, staging, restored, 31, 2, (_, _) => throw new InvalidOperationException()));
        if (logical)
        {
            Assert.Empty(bank.KnownRanges);
            NativeBattleResourcePointer guest = pointer with { Cpu = new(1, 64) { Layout = layout }, Length = 32 };
            Assert.All(NativeBattleModelSectionSnapshot.Read(cpu, staging, guest, 18, 2, (_, _) => throw new InvalidOperationException()).Known, value => Assert.False(value));
        }
        Assert.Equal(new byte[] { 0x42 }, cpu.Cpu.Banks[1].Read(first + 18, 1));
    }

    [Fact]
    public void Shared_skeleton_retains_actual_addressed_callback_and_explicit_unknown_bytes()
    {
        NativeBattleModelCpuReference reference = new(3, 16);
        NativeBattleResourcePointer pointer = new(reference, null, 32);
        int calls = 0;
        var result = NativeBattleModelSectionSnapshot.Read(null, NativeBattleFileStaging.Unknown(), pointer, 18, 2, (address, length) =>
        {
            calls++;
            Assert.Equal(reference.SharedAddress + 18, address);
            Assert.Equal(2, length);
            return ([7, 0], [true, false]);
        });
        Assert.Equal(1, calls);
        Assert.Equal(new byte[] { 7, 0 }, result.Data);
        Assert.Equal(new[] { true, false }, result.Known);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelSectionSnapshot.Read(null, NativeBattleFileStaging.Unknown(), pointer, -1, 2, (_, _) => throw new InvalidOperationException()));
    }
}
