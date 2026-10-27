using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFieldRegistrationTests
{
    [Fact]
    public void Registration_snapshot_copies_loader_zero_knownness_and_publishes_only_changed_holes()
    {
        const uint origin = 0x1900000;
        NativeBattleFileStaging source = new([]) { BaseAddress = origin, LoaderZeroRanges = [new(origin, 4096)] };
        source = source.Write(origin + 4, [0xA7]).MarkUnknown(origin + 16, 2);
        using NativeFieldRegistrationMemory memory = new(source, origin, origin + 64);
        Assert.Equal(new byte[4], memory.Read(origin, 4));
        Assert.Equal(new byte[] { 0xA7 }, memory.Read(origin + 4, 1));
        memory.Forget(origin + 8, 2);
        memory.Write32(origin + 12, 0x12345678);
        memory.Copy(origin + 16, origin + 24, 2);
        NativeBattleFileStaging original = NativeBattleStorageSnapshots.Capture(source);
        NativeBattleFileStaging result = memory.Finish();
        Assert.Throws<NotSupportedException>(() => result.Read(origin + 8, 2));
        Assert.Throws<NotSupportedException>(() => result.Read(origin + 16, 2));
        Assert.Throws<NotSupportedException>(() => result.Read(origin + 24, 2));
        Assert.Equal(0x12345678u, BitConverter.ToUInt32(result.Read(origin + 12, 4)));
        Assert.Equal(new byte[2], result.Read(origin + 4094, 2));
        Assert.Equal(new byte[2], original.Read(origin + 8, 2));
        Assert.Equal(5, NativeBattleStorageSnapshots.Capture(result).KnownRanges.Sum(range => range.Data.Length));
        Assert.Throws<InvalidDataException>(() => memory.Read(origin + 64, 1));
    }

    [Fact]
    public void Registration_cpu_import_publishes_its_known_bytes_and_unknown_masks_over_loader_zero()
    {
        const uint origin = 0x196B018;
        const int length = 3 * NativeBattleModelCpuBank.Length;
        NativeBattleFileStaging source = new([]) { BaseAddress = origin, LoaderZeroRanges = [new(origin, length + 64)] };
        NativeBattleResourcePointer pointer = new(null, 0x12345678, 64);
        source = source.WriteResourcePointer(origin + 4, pointer);
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, cpu.Cpu.Banks[0].Write(4, BitConverter.GetBytes(0x12345678u))).SetItem(1, cpu.Cpu.Banks[1].Write(7, [0xC9])) } };
        using NativeFieldRegistrationMemory memory = new(source, origin, origin + length + 16);
        memory.ObserveCpuBanks(cpu);
        Assert.Equal(0x12345678u, BitConverter.ToUInt32(memory.Read(origin + 4, 4)));
        Assert.Throws<NotSupportedException>(() => memory.Read(origin, 1));
        NativeBattleFileStaging original = NativeBattleStorageSnapshots.Capture(source);
        NativeBattleFileStaging result = memory.Finish();
        Assert.Equal(0x12345678u, BitConverter.ToUInt32(result.Read(origin + 4, 4)));
        Assert.Equal(pointer, result.ReadResourcePointer(origin + 4));
        Assert.Equal(new byte[] { 0xC9 }, result.Read(origin + NativeBattleModelCpuBank.Length + 7, 1));
        Assert.Throws<NotSupportedException>(() => result.Read(origin, 1));
        Assert.Throws<NotSupportedException>(() => result.Read(origin + 2 * NativeBattleModelCpuBank.Length, 1));
        Assert.Equal(new byte[2], result.Read(origin + length + 32, 2));
        Assert.Equal(new byte[1], original.Read(origin, 1));
        Assert.Equal(5, NativeBattleStorageSnapshots.Capture(result).KnownRanges.Sum(range => range.Data.Length));
    }

    [Theory]
    [InlineData(0x1DCA328u, 0xC00)]
    [InlineData(0x1DCB0F0u, 0x100)]
    [InlineData(0x1DCB340u, 0x80)]
    public void Actual5310F0_clears_only_its_three_complete_native_ranges(uint address, int length)
    {
        NativeFieldModelCacheResetState reset = NativeFieldModelCacheResetState.Execute5310F0(NativeFieldRuntimeProfile.FF8En);
        Assert.Equal(length, reset.Read(address, length).Length);
        Assert.All(reset.Read(address, length), value => Assert.Equal((byte)0, value));
        Assert.Throws<NotSupportedException>(() => reset.Read(address - 1, 1));
        Assert.Throws<NotSupportedException>(() => reset.Read(address + (uint)length, 1));
    }

    [Fact]
    public void Adjacent_mapping_and_shared_staging_are_not_cleared_or_fabricated_by_model_cache_reset()
    {
        NativeFieldModelCacheResetState reset = NativeFieldModelCacheResetState.Execute5310F0(NativeFieldRuntimeProfile.FF8En);
        Assert.Throws<NotSupportedException>(() => reset.Read(0x1DCAF28, 64));
        Assert.Throws<NotSupportedException>(() => reset.Read(0x199B018, 163916));
        Assert.Throws<NotSupportedException>(() => reset.Read(0x1DCB0F0, 0x101));
        Assert.Throws<NotSupportedException>(() => reset.Read(uint.MaxValue, 4));
        Assert.Throws<NotSupportedException>(() => reset.Read(0x1DCB340, -1));
        NativeFieldModelCacheResetState restored = JsonSerializer.Deserialize<NativeFieldModelCacheResetState>(JsonSerializer.Serialize(reset))!;
        Assert.Equal(reset, restored);
        Assert.Equal(reset.Read(0x1DCB340, 0x80).ToArray(), restored.Read(0x1DCB340, 0x80).ToArray());
    }

    [Fact]
    public void Original5318D0_part_and_skin_initialization_preserves_unselected_copied_words()
    {
        NativeModelPartsDefinition source = Definition();
        NativeModelPartResourceState state = NativeModelPartResourceState.Create(source);
        state.Validate(source);
        Assert.Empty(state.Translations);
        Assert.Equal(new ushort[] { 0x1111, 0, 0x3333 }, Enumerable.Range(0, source.SkinBindings.Count).Select(source.ReadInitializedSkinFlags));
        Assert.Equal((ushort)0, source.ReadInitializedSkinFlags(1));
        Assert.Equal((short)0x1234, source.Parts[0].Words[7]);
        Assert.Equal((ushort)0x2222, source.SkinBindings[1].OriginalFlags);
    }

    [Fact]
    public void Fresh_dynamic_state_round_trips_without_resource_skin_words()
    {
        NativeModelPartsDefinition source = Definition();
        NativeModelPartResourceState current = NativeModelPartResourceState.Create(source);
        NativeModelPartResourceState restored = JsonSerializer.Deserialize<NativeModelPartResourceState>(JsonSerializer.Serialize(current))!;
        restored.Validate(source);
        Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(restored));
        Assert.Empty(restored.Translations);
        Assert.DoesNotContain("SkinFlags", JsonSerializer.Serialize(restored));
        Assert.Equal((ushort)0, source.ReadInitializedSkinFlags(1));
    }

    private static NativeModelPartsDefinition Definition()
    {
        short[] row = [1, 1, -7, 0, 20, 30, 40, 0x1234, 0, 0, 0, 0, 50, 60, 70, 90];
        return new(new string('A', 64), 100, [new(row)], [new(0, 1, 0, 0x1111), new(1, 1, 0, 0x2222), new(2, 1, 0, 0x3333)]);
    }
}
