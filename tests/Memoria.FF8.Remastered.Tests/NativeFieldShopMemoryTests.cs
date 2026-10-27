using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class NativeFieldShopMemoryTests
{
    [Fact]
    public void RemasteredEncounterDrawAndShopResourcesRetainTheirSourceIdentity()
    {
        using GameAssets assets = new(InstalledDirectory("MEMORIA_FF8_REMASTERED_DIR"));
        NativeEncounterTables encounters = assets.ReadNativeEncounterTables();
        Assert.Equal("FA4AB9025D0922FDAFA6993E1559F96CEB82C5C7DC59C53CAFFD501B56A13B00", Hash(encounters.FieldRandom.AsSpan()));
        Assert.Equal(encounters.FieldRandom.ToArray(), encounters.WorldRandom.ToArray());
        Assert.Equal("F850B3EE6C8CB9DCABB5C202123D1F6826B287AD3E2028AD691539988DFD0CBE", Hash(encounters.WorldWeights.AsSpan()));

        NativeDrawPointCatalog draw = assets.ReadNativeDrawPointCatalog();
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, draw.ExecutableHash);
        Assert.Equal("5AA42DDD6B0C891EFEEA895E127BE4FFB8EFC54FC3981A70D2AD8EC2368805C2", Hash(draw.PointBytes.AsSpan()));
        Assert.Equal("Found a draw point!{Line}Blizzaga found.", draw.FormatMessage(2, 6));
        draw.ValidateInteractionResources();
        Assert.Throws<InvalidDataException>(() => (draw with { ExecutableHash = new string('0', 64) }).Validate());

        NativeShopDefinitions shop = assets.ReadNativeShopDefinitions();
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, shop.ExecutableHash);
        Assert.Equal("6A9E9AA4B99B98AEC966D42537E9E119BF1BC1AF668CE8B791E7EAD00934397B", shop.KernelHash);
        Assert.Equal("4684C6BA3D7618EE5D342550DBBEB22104CC14E8AD9E0E579D2CDBE4DCF65F3F", Hash(shop.ArgumentMap.AsSpan()));
        Assert.Equal("Potion", shop.ResolvePrice(1).Name);
        Assert.Equal("Pet Pals Vol.3", shop.ResolvePrice(191).Name);
        Assert.Equal("Pet Pals Vol.4", shop.ResolvePrice(192).Name);
        Assert.Equal(20, shop.Catalogs.Length);
        Assert.Equal(199, shop.Prices.Length);
        shop.Validate();
        Assert.Throws<InvalidDataException>(() => (shop with { ExecutableHash = new string('0', 64) }).Validate());
    }

    [Fact]
    public void ClassicByteAdaptersAndMemoryReadersReturnEquivalentResources()
    {
        string directory = InstalledDirectory("MEMORIA_FF8_GAME_DIR");
        using GameAssets assets = new(directory);
        byte[] executable = File.ReadAllBytes(Path.Combine(directory, "FF8_EN.exe"));
        NativeExecutableMemory memory = new(executable);
        byte[] kernel = assets.ReadResource(new ResourceKey("main/kernel.bin"));
        NativeEncounterTables encounters = NativeEncounterTables.ReadExecutable(executable);
        NativeEncounterTables memoryEncounters = NativeEncounterTables.Read(memory);
        Assert.Equal(encounters.FieldRandom.ToArray(), memoryEncounters.FieldRandom.ToArray());
        Assert.Equal(encounters.WorldRandom.ToArray(), memoryEncounters.WorldRandom.ToArray());
        Assert.Equal(encounters.WorldWeights.ToArray(), memoryEncounters.WorldWeights.ToArray());

        NativeDrawPointCatalog draw = NativeDrawPointCatalogReader.Read(executable, kernel);
        NativeDrawPointCatalog memoryDraw = NativeDrawPointCatalogReader.Read(memory, kernel, assets.ReadResource(new ResourceKey("text/draw-points")), language: "en");
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, memoryDraw.ExecutableHash);
        Assert.Equal(draw.PointBytes.ToArray(), memoryDraw.PointBytes.ToArray());
        Assert.Equal(draw.CommonMessages.ToArray(), memoryDraw.CommonMessages.ToArray());
        Assert.Equal(draw.MagicNames.ToArray(), memoryDraw.MagicNames.ToArray());
        Assert.Throws<InvalidDataException>(() => NativeDrawPointCatalogReader.Read(memory, kernel, new byte[320], language: "en"));

        byte[] shops = assets.ReadResource(new ResourceKey("menu/shop.bin"));
        byte[] prices = assets.ReadResource(new ResourceKey("menu/price.bin"));
        NativeShopDefinitions shop = NativeShopDefinitionReader.Read(executable, shops, prices, kernel);
        NativeShopDefinitions memoryShop = NativeShopDefinitionReader.Read(memory, shops, prices, kernel, "en");
        Assert.Equal(NativeFieldRuntimeProfile.FF8EnExecutableHash, memoryShop.ExecutableHash);
        Assert.Equal(shop.ArgumentMap.ToArray(), memoryShop.ArgumentMap.ToArray());
        Assert.Equal(shop.Prices.ToArray(), memoryShop.Prices.ToArray());
        Assert.Equal(shop.Prices.ToArray(), assets.ReadNativeShopDefinitions().Prices.ToArray());
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string InstalledDirectory(string variable)
    {
        string? directory = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), $"Set {variable} to run optional installed field/shop resource checks.");
        return directory!;
    }
}
