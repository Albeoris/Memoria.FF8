using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class NativeWeaponShopMemoryTests
{
    [Fact]
    public void WeaponShopUsesAuthenticatedRemasteredGuestData()
    {
        string directory = InstalledDirectory("MEMORIA_FF8_REMASTERED_DIR");
        using GameAssets assets = new(directory);
        NativeWeaponShopDefinitions definitions = assets.ReadNativeWeaponShopDefinitions();
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, definitions.ExecutableHash);
        Assert.Equal("6A9E9AA4B99B98AEC966D42537E9E119BF1BC1AF668CE8B791E7EAD00934397B", definitions.KernelHash);
        Assert.Equal(33, definitions.Recipes.Length);
        Assert.Equal("4684C6BA3D7618EE5D342550DBBEB22104CC14E8AD9E0E579D2CDBE4DCF65F3F", Convert.ToHexString(SHA256.HashData(definitions.ArgumentMap.AsSpan())));
        definitions.Validate();
        Assert.Throws<InvalidDataException>(() => (definitions with { ExecutableHash = new string('0', 64) }).Validate());
    }

    [Fact]
    public void ClassicByteArrayAdapterMatchesGuestMemoryReader()
    {
        string directory = InstalledDirectory("MEMORIA_FF8_GAME_DIR");
        byte[] executable = File.ReadAllBytes(Path.Combine(directory, "FF8_EN.exe"));
        using GameAssets assets = new(directory);
        byte[] kernel = assets.ReadResource(new ResourceKey("main/kernel.bin"));
        byte[] recipes = assets.ReadResource(new ResourceKey("menu/mwepon.bin"));
        byte[] messages = assets.ReadResource(new ResourceKey("menu/mwepon.msg"));
        NativeWeaponShopDefinitions adapter = NativeWeaponShopDefinitionReader.Read(executable, kernel, recipes, messages);
        NativeWeaponShopDefinitions memory = NativeWeaponShopDefinitionReader.Read(new NativeExecutableMemory(executable), kernel, recipes, messages, "en");
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, adapter.ExecutableHash);
        Assert.Equal(memory.ExecutableHash, adapter.ExecutableHash);
        Assert.Equal(executable.AsSpan(0x788918, 256).ToArray(), memory.ArgumentMap.ToArray());
        Assert.Equal(memory.ArgumentMap.ToArray(), adapter.ArgumentMap.ToArray());
        Assert.Equal(memory.Recipes.Select(recipe => recipe.Name), adapter.Recipes.Select(recipe => recipe.Name));
    }

    private static string InstalledDirectory(string variable)
    {
        string? directory = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), $"Set {variable} to run optional installed weapon shop checks.");
        return directory!;
    }
}
