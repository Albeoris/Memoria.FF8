using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledBattleEntryResourcesTests
{
    [Fact]
    public void RemasteredBodyCrossesClassicCapacityAndCopiesIntoItsActualPartyAllocation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(Directory.Exists(directory), "Select installed Remastered resources with MEMORIA_FF8_REMASTERED_DIR.");
        using BattleAssets assets = new(directory!);
        NativeBattleModelDatSource source = assets.ReadNativeModelDatSource(3, false, 0);
        int first = source.Section(0);
        int length = source.Section(5) - first;
        Assert.Equal(72904, length);
        NativeBattleModelResourceBind bind = new(new(3, 0, 0, 0, 0x5077B0, 2), source, new([]));
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.FromColdProcess(NativeFieldRuntimeProfile.RemasteredEnImageHash);
        Assert.Throws<InvalidDataException>(() => NativeBattleModelResourceCopies.Complete(bind, NativeBattleModelCpuBanks.Unknown(), cpu.Rows, 0, null));
        NativeBattleModelResourceCompletion completed = NativeBattleModelResourceCopies.Complete(bind, cpu.Cpu, cpu.Rows, 0, null);
        Assert.Equal(source.Bytes.AsSpan().Slice(first, length).ToArray(), completed.Cpu.Banks[0].Read(0, length));
        Assert.Equal(0xA100000u + (uint)(source.Section(1) - first), completed.Rows[0].Require(16).PhysicalAddress);
        Assert.Throws<NotSupportedException>(() => completed.Cpu.Banks[0].Read(length, 1));
        (cpu with { Cpu = completed.Cpu, Rows = completed.Rows }).Validate();
    }

    [Fact]
    public void BattleEntryReadersRejectUnknownImageBeforeReadingDataOrPermissions()
    {
        UnsupportedImage image = new();
        Assert.Throws<NotSupportedException>(() => NativeBattleRenzokukenResourceReader.Read(image));
        Assert.Throws<NotSupportedException>(() => NativeBattleModuleResourceReader.Read(image));
        Assert.Throws<NotSupportedException>(() => NativeBattleDefensiveResourceReader.Read(image));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectCatalogReader.Read(image));
    }

    [Fact]
    public void BattleEntryResourcesMatchAcrossAuthenticatedInstalledEditions()
    {
        string? remasteredDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        string? classicDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(Directory.Exists(remasteredDirectory) && Directory.Exists(classicDirectory), "Select both installed editions with MEMORIA_FF8_REMASTERED_DIR and MEMORIA_FF8_GAME_DIR.");
        using GameAssets classic = new(classicDirectory!);
        using GameAssets remastered = new(remasteredDirectory!);
        Equal(classic.ReadNativeRenzokukenResources(), remastered.ReadNativeRenzokukenResources());
        Equal(classic.ReadNativeBattleModuleResources(), remastered.ReadNativeBattleModuleResources());
        Equal(classic.ReadNativeBattleDefensiveResources(), remastered.ReadNativeBattleDefensiveResources());
        Equal(classic.ReadNativeEffectMeshes(), remastered.ReadNativeEffectMeshes());
        Equal(classic.ReadNativeEffectCatalog(), remastered.ReadNativeEffectCatalog());
    }

    private static void Equal<T>(T classic, T remastered)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(classic), JsonSerializer.SerializeToElement(remastered)));
    }

    private sealed class UnsupportedImage : IGameNativeImageSource
    {
        public string SourceHash => new string('0', 64);
        public byte[] Read(uint address, int count) => throw new InvalidOperationException("Unknown images must be rejected before reading data.");
        public byte[] ReadLoader(uint address, int count) => Read(address, count);
        public bool IsExecutableAddress(uint address) => throw new InvalidOperationException("Unknown images must be rejected before reading permissions.");
        public bool IsWritableRange(uint address, int count) => throw new InvalidOperationException("Unknown images must be rejected before reading permissions.");
    }
}
