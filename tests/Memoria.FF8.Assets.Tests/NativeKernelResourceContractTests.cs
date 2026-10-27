using System.Buffers.Binary;
using System.Security.Cryptography;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeKernelResourceContractTests
{
    [Fact]
    public void RelocatedKernelKeepsSelectedShopNamesAndRefinementContracts()
    {
        RequireInstallation();
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        byte[] original = resources.Read(new ResourceKey("main/kernel.bin"));
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        KernelData kernel = KernelData.ReadFromArray(original, encoding);
        kernel.Weapons[0].Name = "Selected longer weapon name";
        kernel.BattleItems[1].Name = "Selected battle item";
        kernel.FieldItems[0].Name = "Selected field item";
        kernel.MenuAbilities[5].Name = "Selected refinement";
        byte[] selected = Relocate(kernel.WriteToArray(encoding));
        Assert.NotEqual(Section(original, 4), Section(selected, 4));
        ResourceService selectedResources = new(source);
        selectedResources.Prepare(null, encodedResources: [new EncodedResource(new ResourceKey("main/kernel.bin"), "bin", 1, "fixture", selected)]).Commit();
        using GameAssets assets = new(TestResources.GameDirectory, resources: selectedResources);
        NativeWeaponShopDefinitions weapons = assets.ReadNativeWeaponShopDefinitions();
        NativeShopDefinitions shops = assets.ReadNativeShopDefinitions();
        NativeRefinementDefinitions refinement = assets.ReadNativeRefinementDefinitions();
        Assert.Equal("Selected longer weapon name", weapons.Recipes[0].Name);
        Assert.Equal("Selected battle item", shops.Prices[1].Name);
        Assert.Equal("Selected field item", shops.Prices[33].Name);
        Assert.Equal("Selected refinement", refinement.Abilities[5].Name);
        string fingerprint = Convert.ToHexString(SHA256.HashData(selected));
        Assert.Equal(fingerprint, weapons.KernelHash);
        Assert.Equal(fingerprint, shops.KernelHash);
        Assert.Equal(fingerprint, refinement.KernelHash);
        Assert.Equal(selected.AsSpan(Section(selected, 4), 12).ToArray(), weapons.Recipes[0].KernelWords.ToArray());
        Assert.Equal(original.AsSpan(Section(original, 4) + 2, 10).ToArray(), weapons.Recipes[0].KernelWords.AsSpan(2, 10).ToArray());
        Assert.Throws<InvalidDataException>(() => (refinement with { KernelHash = new string('Z', 64) }).Validate());
        Assert.Throws<InvalidDataException>(() => (refinement with { ExecutableHash = new string('0', 64) }).Validate());
        Assert.Throws<InvalidDataException>(() => (refinement with { Abilities = refinement.Abilities.SetItem(5, refinement.Abilities[5] with { Kind = 255 }) }).Validate());
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("count")]
    [InlineData("directory-order")]
    [InlineData("directory-end")]
    public void ShopReadersRejectInvalidKernelDirectory(string defect)
    {
        RequireInstallation();
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] kernel = assets.ReadResource(new ResourceKey("main/kernel.bin"));
        switch (defect)
        {
            case "truncated":
                kernel = new byte[12];
                break;
            case "count":
                BinaryPrimitives.WriteInt32LittleEndian(kernel, 55);
                break;
            case "directory-order":
                WriteSection(kernel, 10, Section(kernel, 9) - 1);
                break;
            case "directory-end":
                WriteSection(kernel, 55, kernel.Length + 1);
                break;
        }
        Assert.Throws<InvalidDataException>(() => Weapons(assets, kernel));
        Assert.Throws<InvalidDataException>(() => Shops(assets, kernel));
    }

    [Theory]
    [InlineData("weapon-rows")]
    [InlineData("weapon-name")]
    [InlineData("weapon-terminator")]
    [InlineData("item-rows")]
    [InlineData("item-name")]
    [InlineData("item-terminator")]
    public void ShopReadersRejectInvalidReachedRowsAndNames(string defect)
    {
        RequireInstallation();
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] kernel = assets.ReadResource(new ResourceKey("main/kernel.bin"));
        switch (defect)
        {
            case "weapon-rows":
                WriteSection(kernel, 5, Section(kernel, 5) + 1);
                break;
            case "weapon-name":
                BinaryPrimitives.WriteUInt16LittleEndian(kernel.AsSpan(Section(kernel, 4)), 65534);
                break;
            case "weapon-terminator":
                kernel.AsSpan(Section(kernel, 35), Section(kernel, 36) - Section(kernel, 35)).Fill(0x20);
                break;
            case "item-rows":
                WriteSection(kernel, 8, Section(kernel, 8) + 1);
                break;
            case "item-name":
                BinaryPrimitives.WriteUInt16LittleEndian(kernel.AsSpan(Section(kernel, 7)), 65534);
                break;
            case "item-terminator":
                kernel.AsSpan(Section(kernel, 38), Section(kernel, 39) - Section(kernel, 38)).Fill(0x20);
                break;
        }
        if (defect.StartsWith("weapon", StringComparison.Ordinal))
        {
            Assert.Throws<InvalidDataException>(() => Weapons(assets, kernel));
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => Shops(assets, kernel));
        }
    }

    private static NativeWeaponShopDefinitions Weapons(GameAssets assets, byte[] kernel)
    {
        return NativeWeaponShopDefinitionReader.Read(GameDataResources.ReadNativeMemory(assets.Resources), kernel, assets.ReadResource(new ResourceKey("menu/mwepon.bin")), assets.ReadResource(new ResourceKey("menu/mwepon.msg")));
    }

    private static NativeShopDefinitions Shops(GameAssets assets, byte[] kernel)
    {
        return NativeShopDefinitionReader.Read(GameDataResources.ReadNativeMemory(assets.Resources), assets.ReadResource(new ResourceKey("menu/shop.bin")), assets.ReadResource(new ResourceKey("menu/price.bin")), kernel);
    }

    private static byte[] Relocate(byte[] kernel)
    {
        const int header = 228;
        const int padding = 32;
        byte[] relocated = new byte[kernel.Length + padding];
        kernel.AsSpan(0, header).CopyTo(relocated);
        kernel.AsSpan(header).CopyTo(relocated.AsSpan(header + padding));
        for (int section = 0; section < 56; section++)
        {
            WriteSection(relocated, section, Section(kernel, section) + padding);
        }
        return relocated;
    }

    private static int Section(byte[] kernel, int index) => BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + index * 4));
    private static void WriteSection(byte[] kernel, int index, int offset) => BinaryPrimitives.WriteInt32LittleEndian(kernel.AsSpan(4 + index * 4), offset);
    private static void RequireInstallation() => Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native kernel resources are required.");
}
