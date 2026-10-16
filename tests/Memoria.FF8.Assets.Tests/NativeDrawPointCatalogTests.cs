using System.Security.Cryptography;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeDrawPointCatalogTests
{
    private static GameAssets Assets() => new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII");

    [Fact]
    public void OriginalCommonResourcesRetainTemplatesHashesAndMagicNames()
    {
        using GameAssets assets = Assets();
        NativeDrawPointCatalog catalog = assets.ReadNativeDrawPointCatalog();
        catalog.ValidateInteractionResources();
        Assert.Equal("Found a draw point!{Line}{Name 32} found.", catalog.Message(2));
        Assert.Equal("Found a draw point!{Line}Nothing there...", catalog.Message(3));
        Assert.Equal("Who will draw?{Line}Don’t draw{Line}{Color Disabled}{Char Squall}      {Line}{Color Disabled}{Char Squall}      {Line}{Color Disabled}{Char Squall}      ", catalog.Message(4));
        Assert.Equal("{Char Squall} stocked {Var 55} {Name 32}s.", catalog.Message(5));
        Assert.Equal("{Char Squall} is unable to stock {Name 32}.", catalog.Message(6));
        Assert.Equal(catalog.NoEligibleMessage, catalog.Message(7));
        Assert.Equal(catalog.CommonMessageHash, catalog.MessageHash(7));
        Assert.Equal("Found a draw point!{Line}Blizzaga found.", catalog.FormatMessage(2, 6));
        Assert.Equal("{Char Quistis} stocked 4 Blizzagas.", catalog.FormatMessage(5, 6, 3, 4));
        Assert.Equal("{Char Ward} is unable to stock Fire.", catalog.FormatMessage(6, 1, 10));
        FF8Encoding encoding = FF8Encoding.CreateEuropean();
        for (int id = 0; id < 8; id++)
        {
            byte[] bytes = catalog.CommonMessageBytes[id].ToArray();
            Assert.Equal(0, bytes[^1]);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), catalog.MessageHash(id));
            Assert.Equal(encoding.GetString(bytes.AsSpan(0, bytes.Length - 1)), catalog.Message(id));
        }
        Assert.Throws<InvalidDataException>(() => (catalog with { CommonMessageBytes = default }).Validate());
        Assert.Throws<NotSupportedException>(() => catalog.FormatMessage(2, 63));
    }

    [Fact]
    public void NativeStockInsertionPreservesFirstMatchOrderRetainedCountAndFailureCodes()
    {
        using GameAssets assets = Assets();
        NativeCharacterVitals original = assets.ReadNewGameCharacterVitals();
        NativeCharacterVitals WithStocks(params NativeMagicStock[] values)
        {
            NativeMagicStock[] inventory = Enumerable.Range(0, 32).Select(_ => new NativeMagicStock(0, 0)).ToArray();
            values.CopyTo(inventory, 0);
            NativeCharacterVital[] records = original.Records.ToArray();
            records[0] = records[0] with { MagicInventory = inventory };
            return original with { Records = records };
        }
        NativeCharacterVitals state = WithStocks(new NativeMagicStock(4, 9), new NativeMagicStock(6, 98), new NativeMagicStock(6, 1));
        NativeCharacterVitals first = state.AddMagicUnit(0, 6, out int result);
        Assert.Equal(0, result);
        Assert.Equal(new NativeMagicStock(6, 99), first.Records[0].MagicInventory[1]);
        Assert.Equal(new NativeMagicStock(6, 1), first.Records[0].MagicInventory[2]);
        Assert.Equal(new NativeMagicStock(6, 98), state.Records[0].MagicInventory[1]);
        NativeCharacterVitals second = first.AddMagicUnit(0, 6, out result);
        Assert.Equal(0, result);
        Assert.Same(second, second.AddMagicUnit(0, 6, out result));
        Assert.Equal(1, result);
        Assert.Equal(1, NativeCharacterVitals.GetMagicCapacity(second.Records[0], 6));

        state = WithStocks(new NativeMagicStock(4, 9), new NativeMagicStock(0, 255), new NativeMagicStock(0, 7));
        first = state.AddMagicUnit(0, 6, out result);
        Assert.Equal(0, result);
        Assert.Equal(new NativeMagicStock(6, 0), first.Records[0].MagicInventory[1]);
        Assert.Equal(new NativeMagicStock(0, 7), first.Records[0].MagicInventory[2]);
        first = first.AddMagicUnit(0, 7, out result);
        Assert.Equal(0, result);
        Assert.Equal(new NativeMagicStock(7, 8), first.Records[0].MagicInventory[2]);

        state = WithStocks(Enumerable.Range(1, 32).Select(id => new NativeMagicStock((byte)id, 1)).ToArray());
        Assert.Same(state, state.AddMagicUnit(0, 40, out result));
        Assert.Equal(2, result);
        Assert.Same(state, state.AddMagicUnit(0, 0, out result));
        Assert.Equal(0, result);
        Assert.Equal(0, NativeCharacterVitals.GetMagicCapacity(state.Records[0], 0));
        Assert.Throws<NotSupportedException>(() => state.AddMagicUnit(8, 6, out _));
    }

    [Fact]
    public void SelectionPreservesFieldOrderOriginalStylingAndFirstFfBoundary()
    {
        using GameAssets assets = Assets();
        NativeDrawPointCatalog catalog = assets.ReadNativeDrawPointCatalog();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        NativeMagicStock[] inventory = records[0].MagicInventory.ToArray();
        inventory[0] = new NativeMagicStock(6, 100);
        records[0] = records[0] with { MagicInventory = inventory };
        byte[] junctions = records[3].Junction.MagicJunctions.ToArray();
        junctions[19] = 6;
        records[3] = records[3] with { Junction = records[3].Junction with { MagicJunctions = junctions } };
        vitals = vitals with { Records = records, CombatParty = [0, 1, 3] };
        string selection = catalog.FormatSelection([3, 0, 1], vitals, 6, 4);
        Assert.Equal("Who will draw?{Line}Don’t draw{Line}{Color White}{Char Quistis} {Key 65}   {Line}{Color Disabled}{Char Squall}      {Line}{Color Disabled}{Char Zell}      ", selection);
        Assert.Equal("Who will draw?{Line}Don’t draw{Line}{Color White}{Char Quistis} {Key 65}   ", catalog.FormatSelection([3, 255, 0], vitals, 6, 4));
        Assert.Equal("Who will draw?{Line}Don’t draw", catalog.FormatSelection([255, 3, 0], vitals, 6, 4));
        Assert.Throws<NotSupportedException>(() => catalog.FormatSelection([2, 255, 255], vitals, 6, 7));
        Assert.Equal(new NativeMagicStock(6, 100), vitals.Records[0].MagicInventory[0]);
    }
    [Fact]
    public void JunctionLookupInspectsAllTwentyBytesAndExcludesMagicZero()
    {
        using GameAssets assets = Assets();
        NativeCharacterVital character = assets.ReadNewGameCharacterVitals().Records[0];
        byte[] junctions = new byte[20];
        junctions[19] = 6;
        character = character with { Junction = character.Junction with { MagicJunctions = junctions } };
        Assert.True(NativeCharacterVitals.IsMagicJunctioned(character, 6));
        Assert.False(NativeCharacterVitals.IsMagicJunctioned(character, 5));
        Assert.False(NativeCharacterVitals.IsMagicJunctioned(character, 0));
    }
}
