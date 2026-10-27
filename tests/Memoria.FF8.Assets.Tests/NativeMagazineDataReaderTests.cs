using System.Collections.Immutable;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeMagazineDataReaderTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    public void SelectedPageCountsAndTextCountsUseTheirActualSelectorBounds(int pages)
    {
        byte[] metadata = Pages(pages);
        byte[] items = Items((byte)(pages - 1));
        MngrpStringTable text = Text();
        NativeMagazineDataReader.ValidateInventoryPages(metadata, items, text, Weapons(), 16, [], new byte[22]);
        Assert.Equal("Page sélectionnée", text[0]);
    }

    [Theory]
    [InlineData(24, 28)]
    [InlineData(26, 16)]
    [InlineData(27, 8)]
    [InlineData(55, 2)]
    [InlineData(39, 40)]
    public void SelectedPagesRejectUnownedLearningTextAndImageSelectors(int field, byte value)
    {
        byte[] metadata = Pages(1);
        metadata[field] = value;
        Assert.Throws<InvalidDataException>(() => NativeMagazineDataReader.ValidateInventoryPages(metadata, Items(0), Text(), Weapons(), 16, [], new byte[22]));
    }

    [Fact]
    public void MissingTextAndPageRangeDoNotAcquireImplicitFallbacks()
    {
        MngrpStringTable text = Text();
        text.Entries[0].Text = null;
        Assert.Throws<InvalidDataException>(() => NativeMagazineDataReader.ValidateInventoryPages(Pages(1), Items(0), text, Weapons(), 16, [], new byte[22]));
        Assert.Throws<InvalidDataException>(() => NativeMagazineDataReader.ValidateInventoryPages(Pages(1), Items(1), Text(), Weapons(), 16, [], new byte[22]));
        Assert.Throws<InvalidDataException>(() => NativeMagazineDataReader.ValidateInventoryPages(Pages(257), Items(0), Text(), Weapons(), 16, [], new byte[22]));
        byte[] differentTextBank = Pages(1);
        differentTextBank[21] = 1;
        Assert.Throws<NotSupportedException>(() => NativeMagazineDataReader.ValidateInventoryPages(differentTextBank, Items(0), Text(), Weapons(), 16, [], new byte[22]));
    }

    private static byte[] Pages(int count)
    {
        byte[] bytes = new byte[count * 68];
        for (int page = 0; page < count; page++)
        {
            int offset = page * 68;
            bytes[offset + 24] = 255;
            bytes[offset + 26] = 255;
            bytes[offset + 27] = 255;
            for (int slot = 0; slot < 4; slot++)
            {
                bytes[offset + 39 + slot * 4] = 255;
                bytes[offset + 55 + slot * 4] = 255;
            }
            bytes[offset + 55] = 0;
        }
        return bytes;
    }

    private static byte[] Items(byte page)
    {
        byte[] items = new byte[199 * 4];
        items[4] = 9;
        items[6] = page;
        items[7] = page;
        return items;
    }

    private static MngrpStringTable Text()
    {
        MngrpStringTable table = new();
        table.Entries.Add(new(new MngrpText("Page sélectionnée")));
        table.Entries.Add(new(new MngrpText("Second entry")));
        return table;
    }

    private static NativeWeaponShopDefinitions Weapons()
    {
        ImmutableArray<byte> words = ImmutableArray.CreateRange(new byte[12]);
        NativeWeaponRecipe recipe = new("Synthetic", 0, 0, Enumerable.Repeat(new NativeItemStock(0, 0), 4).ToImmutableArray(), words, words);
        string hash = new('A', 64);
        return new(NativeFieldRuntimeProfile.FF8EnExecutableHash, hash, hash, hash, Enumerable.Repeat(recipe, 33).ToImmutableArray(), ImmutableArray.CreateRange(new byte[68]), ImmutableArray.CreateRange(new byte[256]));
    }
}
