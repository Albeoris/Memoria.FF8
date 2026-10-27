using System.Buffers.Binary;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMenuDataReaderTests
{
    [Theory]
    [InlineData(0, 256)]
    [InlineData(1, 1)]
    [InlineData(2, 12)]
    [InlineData(3, 32)]
    [InlineData(4, 110)]
    public void RefinementUsesByteSelectorsAndTypedInventoryDomains(int kind, int count)
    {
        byte[] bytes = Recipes(kind, count);
        NativeMenuDataReader.ValidateRefinementTable(bytes, kind);
        bytes[7] = kind is 0 or 2 ? (byte)57 : (byte)199;
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ValidateRefinementTable(bytes, kind));
        bytes = Recipes(kind, count);
        bytes[5] = kind == 4 ? (byte)110 : kind == 2 ? (byte)57 : (byte)199;
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ValidateRefinementTable(bytes, kind));
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ValidateRefinementTable(Recipes(kind, kind == 4 ? 109 : 257), kind));
    }

    [Fact]
    public void CardModAcceptsSelectedRecipesAndChecksOrdinalsAndQuantities()
    {
        byte[] bytes = Recipes(4, 110);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 37);
        bytes[6] = 3;
        bytes[7] = 198;
        Assert.Equal(new Runtime.TripleTriadRefinement(0, 3, 198, 37), TripleTriadCatalog.ReadRefinements(bytes)[0]);
        bytes[13] = 0;
        Assert.Throws<InvalidDataException>(() => TripleTriadCatalog.ReadRefinements(bytes));
        bytes = Recipes(4, 110);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 101);
        Assert.Throws<InvalidDataException>(() => TripleTriadCatalog.ReadRefinements(bytes));
    }

    [Fact]
    public void CardCatalogLoadsSelectedMenuDataThroughResourceService()
    {
        string directory = TestResources.GameDirectory;
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        byte[] bytes = Recipes(4, 110);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 37);
        bytes[7] = 198;
        resources.Prepare(null, encodedResources: [new EncodedResource(new ResourceKey("menu/m004.bin"), "bin", 1, "fixture", bytes)]).Commit();
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory, resources);
        Assert.Equal(new Runtime.TripleTriadRefinement(0, 1, 198, 37), catalog.Refinements[0]);
    }

    [Fact]
    public void ShopAndWeaponRecordsRejectIdsOutsideTheItemDomain()
    {
        byte[] shops = new byte[640];
        shops[0] = 198;
        shops[1] = 255;
        NativeMenuDataReader.ValidateShopCatalogs(shops);
        NativeMenuDataReader.ValidateShopPrices(new byte[796]);
        shops[0] = 199;
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ValidateShopCatalogs(shops));
        byte[] recipes = new byte[396];
        recipes[4] = 198;
        NativeMenuDataReader.ValidateWeaponRecipes(recipes);
        recipes[4] = 199;
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ValidateWeaponRecipes(recipes));
        NativeMenuDataReader.ValidateWeaponMessages(Enumerable.Repeat((byte)123, 68).ToArray());
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ValidateWeaponMessages(new byte[67]));
    }

    [Fact]
    public void SolomonRingUsesSelectedIngredientsAndOneItemSelector()
    {
        byte[] ingredients = [1, 7, 0, 0, 2, 8, 0, 0, 198, 9, 0, 0, 0, 0, 0, 0];
        NativeMenuDataReader.ValidateSolomonRingIngredients(ingredients);
        byte[] items = new byte[796];
        items[198 * 4] = 15;
        Assert.Equal(198, NativeMenuDataReader.ReadSolomonRingItemId(items));
        items[4] = 15;
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ReadSolomonRingItemId(items));
        ingredients[8] = 1;
        Assert.Throws<InvalidDataException>(() => NativeMenuDataReader.ValidateSolomonRingIngredients(ingredients));
    }

    [Theory]
    [InlineData("menu-data-a")]
    [InlineData("menu-data-b")]
    [InlineData("menu-data-c")]
    public void OptionalSelectedMenuTablesReachTheirStructuralReaders(string fixture)
    {
        string catalog = TestResources.FixturePath(fixture);
        Dictionary<string, string> files = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(catalog))!;
        byte[] Read(string name) => File.ReadAllBytes(Path.GetFullPath(files[name], Path.GetDirectoryName(catalog)!));
        for (int kind = 0; kind < 5; kind++)
        {
            NativeMenuDataReader.ValidateRefinementTable(Read($"m00{kind}.bin"), kind);
        }
        Assert.Equal(110, TripleTriadCatalog.ReadRefinements(Read("m004.bin")).Length);
        NativeMenuDataReader.ValidateShopCatalogs(Read("shop.bin"));
        NativeMenuDataReader.ValidateShopPrices(Read("price.bin"));
        NativeMenuDataReader.ValidateWeaponRecipes(Read("mwepon.bin"));
        NativeMenuDataReader.ValidateWeaponMessages(Read("mwepon.msg"));
        NativeMenuDataReader.ValidateSolomonRingIngredients(Read("mthomas.bin"));
        Assert.NotEqual(0, NativeMenuDataReader.ReadSolomonRingItemId(Read("mitem.bin")));
    }

    private static byte[] Recipes(int kind, int count)
    {
        byte[] bytes = new byte[count * 8];
        for (int row = 0; row < count; row++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(row * 8 + 2), 1);
            bytes[row * 8 + 4] = 1;
            bytes[row * 8 + 5] = kind == 4 ? (byte)row : (byte)1;
            bytes[row * 8 + 6] = 1;
            bytes[row * 8 + 7] = 1;
        }
        return bytes;
    }
}
