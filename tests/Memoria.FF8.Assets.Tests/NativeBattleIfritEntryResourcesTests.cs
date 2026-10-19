using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleIfritEntryResourcesTests
{
    [Fact]
    public void Original_Ifrit_directories_supply_its_own_offsets_and_the_five_shared_scripted_core_owners()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeExecutableMemory original = new(File.ReadAllBytes(Path.Combine(game.GameDirectory, "FF8_EN.exe")));
        NativeBattleScriptedInitializationDirectories directories = new(
            new("FF8_EN:1874894", NativeBattleEffectStaticResourcesReader.Read(original, 0x1874894, 28)),
            new("FF8_EN:1874B58", NativeBattleEffectStaticResourcesReader.Read(original, 0x1874B58, 20)));
        directories.Validate();
        Assert.Equal(new uint[] { 0x20, 0xD8, 0x190, 0x1A8, 0x1C0, 0, 0x1DC }, Enumerable.Range(0, 7).Select(index => directories.InitializationOffset(index * 4)));
        Assert.Equal(new uint[] { 0x2797690, 0x803FF000, NativeBattleScriptedEffectMemory.Address, 0x27975D8, 0x2797628 }, Enumerable.Range(0, 5).Select(directories.OwnerAddress));
        NativeBattleScriptedInitializationDirectories loaded = game.ReadNativeIfritInitializationDirectories();
        Assert.Equal(directories.InitializationDirectory.Data, loaded.InitializationDirectory.Data);
        Assert.Equal(directories.OwnerDirectory.Data, loaded.OwnerDirectory.Data);
        Assert.Same(loaded, game.ReadNativeIfritInitializationDirectories());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([201])[200];
        Assert.Same(loaded, binding.ScriptedInitializationDirectories);
        Assert.Null(binding.Water222Resources);
        Assert.Equal(new[] { "mag200_b.00", "mag200_b.01" }, NativeBattleEffectInitializerPolicy.Assets(binding).Select(asset => asset.Name));
    }

    [Fact]
    public void Reader_rejects_an_unauthenticated_executable()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleIfritResourcesReader.Read(new byte[64]));
    }
}
