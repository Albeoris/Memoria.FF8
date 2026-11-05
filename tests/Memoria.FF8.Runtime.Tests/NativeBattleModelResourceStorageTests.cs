using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelResourceStorageTests
{
    [Fact]
    public void Resource_binding_retains_the_actual_source_and_diagnostic_materialization_does_not_replace_it()
    {
        ImmutableArray<byte> source = Enumerable.Range(0, 128).Select(value => (byte)value).ToImmutableArray();
        NativeBattleModelCpuBank bank = NativeBattleModelCpuBank.Unknown().BindResource(8, source, 32, 24);
        NativeBattleModelResourceSegment segment = Assert.Single(bank.ResourceSegments);
        Assert.True(segment.Source.Equals(source));
        Assert.Equal(32, segment.SourceOffset);
        Assert.Equal(24, segment.Length);
        Assert.Equal(source.AsSpan(32, 24).ToArray(), bank.Read(8, 24));
        ImmutableArray<NativeBattleEffectMemoryRegion> first = bank.KnownRanges;
        ImmutableArray<NativeBattleEffectMemoryRegion> second = bank.KnownRanges;
        Assert.Equal(8, Assert.Single(first).Offset);
        Assert.False(first[0].Data.Equals(source));
        Assert.False(first[0].Data.Equals(second[0].Data));
        Assert.True(Assert.Single(bank.ResourceSegments).Source.Equals(source));
        Assert.Throws<NotSupportedException>(() => bank.Read(7, 2));
        Assert.Throws<NotSupportedException>(() => bank.Read(31, 2));
    }

    [Fact]
    public void Patches_copy_only_the_changed_bytes_and_preserve_prior_publications_and_source_neighbors()
    {
        ImmutableArray<byte> source = [0, 1, 2, 3, 4, 5, 6, 7];
        NativeBattleModelCpuBank original = NativeBattleModelCpuBank.Unknown().BindResource(0, source, 0, source.Length);
        NativeBattleModelCpuBank changed = original.Write(2, [90, 91, 92]);
        Assert.Equal(new byte[] { 0, 1, 90, 91, 92, 5, 6, 7 }, changed.Read(0, 8));
        Assert.Equal(source.ToArray(), original.Read(0, 8));
        Assert.Equal(3, changed.ResourceSegments.Length);
        Assert.True(changed.ResourceSegments[0].Source.Equals(source));
        Assert.Equal(3, changed.ResourceSegments[1].Source.Length);
        Assert.True(changed.ResourceSegments[2].Source.Equals(source));
        Assert.Equal(5, changed.ResourceSegments[2].SourceOffset);
        Assert.Same(changed, changed.Write(2, [90, 91, 92]));
        ImmutableArray<byte> replacement = [10, 11, 12, 13, 14, 15, 16, 17];
        NativeBattleModelCpuBank replaced = changed.BindResource(0, replacement, 0, replacement.Length);
        Assert.True(Assert.Single(replaced.ResourceSegments).Source.Equals(replacement));
        Assert.DoesNotContain(replaced.ResourceSegments, segment => segment.Source.Equals(source));
        Assert.Equal(new byte[] { 0, 1, 90, 91, 92, 5, 6, 7 }, changed.Read(0, 8));
    }

    [Fact]
    public void Complete_bank_copy_shares_resource_ownership_and_subsequent_writes_remain_independent()
    {
        ImmutableArray<byte> source = [4, 5, 6];
        NativeBattleModelCpuBanks banks = NativeBattleModelCpuBanks.Unknown();
        banks = banks with { Banks = banks.Banks.SetItem(0, banks.Banks[0].BindResource(8, source, 0, 3)).SetItem(2, banks.Banks[2].Write(200, [9])) };
        NativeBattleModelCpuBanks copied = banks.CopyBank50B2A0(0, 2);
        Assert.Same(copied.Banks[0], copied.Banks[2]);
        Assert.Throws<NotSupportedException>(() => copied.Banks[2].Read(200, 1));
        NativeBattleModelCpuBank changed = copied.Banks[2].Write(9, [99]);
        Assert.Equal(new byte[] { 4, 99, 6 }, changed.Read(8, 3));
        Assert.Equal(source.ToArray(), copied.Banks[0].Read(8, 3));
    }

    [Fact]
    public void Earlier_bank_json_adapts_to_resource_storage_and_new_diagnostics_restore_the_same_bytes_and_unknown_gaps()
    {
        LegacyBank legacy = new([new(4, [1, 2, 3]), new(12, [8, 9])], 0x10000);
        string oldJson = JsonSerializer.Serialize(legacy);
        NativeBattleModelCpuBank restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuBank>(oldJson);
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Read(4, 3));
        Assert.Equal(new byte[] { 8, 9 }, restored.Read(12, 2));
        Assert.Throws<NotSupportedException>(() => restored.Read(6, 7));
        NativeBattleModelCpuBank changed = restored.Write(6, [4, 5, 6, 7, 8, 9]);
        string diagnostic = NativeBattleTestSnapshots.Json(changed);
        Assert.DoesNotContain("ResourceSegments", diagnostic);
        Assert.DoesNotContain("SourceOffset", diagnostic);
        LegacyBank priorReader = JsonSerializer.Deserialize<LegacyBank>(diagnostic)!;
        Assert.Equal(changed.Read(4, 10), NativeKnownByteRanges.Read(priorReader.KnownRanges, 4, 10, priorReader.Capacity));
        NativeBattleModelCpuBank independent = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuBank>(diagnostic);
        Assert.Equal(diagnostic, NativeBattleTestSnapshots.Json(independent));
        Assert.Equal(new byte[] { 1, 2, 4, 5, 6, 7, 8, 9, 8, 9 }, independent.Read(4, 10));
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Read(4, 3));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_registration_observes_resource_slices_and_reconciles_stores_without_changing_the_prior_owner(bool remastered)
    {
        NativeBattlePartyCpuLayout layout = remastered ? NativeBattlePartyCpuLayout.Remastered : NativeBattlePartyCpuLayout.Classic;
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown() with { Cpu = NativeBattleModelCpuBanks.Unknown(layout) };
        ImmutableArray<byte> source = [1, 2, 3, 4, 5, 6, 7, 8];
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[0].BindResource(8, source, 1, 6).Write(10, [99]);
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, bank) } };
        uint address = layout.BaseAddress;
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleModelCpuResources reconciled;
        using (NativeFieldRegistrationMemory memory = new(staging, address, checked(address + (uint)(3 * layout.BankLength))))
        {
            memory.ObserveCpuBanks(cpu);
            Assert.Equal(new byte[] { 2, 3, 99, 5, 6, 7 }, memory.Read(address + 8, 6));
            Assert.Throws<NotSupportedException>(() => memory.Read(address + 7, 2));
            Assert.Same(cpu, memory.ReconcileCpuBanks(cpu));
            memory.Write8(address + 11, 77);
            reconciled = memory.ReconcileCpuBanks(cpu)!;
            memory.Finish();
        }
        Assert.Equal(new byte[] { 2, 3, 99, 77, 6, 7 }, reconciled.Cpu.Banks[0].Read(8, 6));
        Assert.Equal(new byte[] { 2, 3, 99, 5, 6, 7 }, bank.Read(8, 6));
        Assert.Throws<NotSupportedException>(() => reconciled.Cpu.Banks[0].Read(7, 2));
        NativeBattleModelCpuResources restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuResources>(NativeBattleTestSnapshots.Json(reconciled));
        Assert.Equal(reconciled.Cpu.Banks[0].Read(8, 6), restored.Cpu.Banks[0].Read(8, 6));
    }

    [Fact]
    public void Overlapping_source_bindings_and_runtime_patches_preserve_sparse_byte_and_snapshot_contracts()
    {
        NativeBattleModelCpuBank bank = NativeBattleModelCpuBank.Unknown(64);
        byte[] expected = new byte[64];
        bool[] known = new bool[64];
        Random random = new(73);
        for (int step = 0; step < 80; step++)
        {
            int length = random.Next(1, 10);
            int offset = random.Next(0, 65 - length);
            byte[] bytes = new byte[length];
            random.NextBytes(bytes);
            bytes.CopyTo(expected, offset);
            Array.Fill(known, true, offset, length);
            bank = step % 2 == 0 ? bank.BindResource(offset, bytes.ToImmutableArray(), 0, length) : bank.Write(offset, bytes);
            for (int index = 0; index < 64; index++)
            {
                if (known[index])
                {
                    Assert.Equal(expected[index], bank.Read(index, 1)[0]);
                }
                else
                {
                    Assert.Throws<NotSupportedException>(() => bank.Read(index, 1));
                }
            }
        }
        NativeBattleModelCpuBank restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelCpuBank>(NativeBattleTestSnapshots.Json(bank));
        Assert.Equal(expected, restored.Read(0, 64));
        Assert.Equal(bank.Read(0, 64), NativeKnownByteRanges.Read(bank.KnownRanges, 0, 64, bank.Capacity));
    }

    private sealed record LegacyBank(ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges, int Capacity);
}
