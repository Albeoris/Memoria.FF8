using System.Text.Json;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledBattleEntryResourcesTests
{
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
