using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>Inventory magazine page selectors and their selected text, sprite and learning dependencies.</summary>
public static class NativeMagazineDataReader
{
    public static void ValidateInventoryPages(byte[] metadata, byte[] items, MngrpStringTable text, NativeWeaponShopDefinitions weapons, int duelMoveCount, byte[] imageIndex, byte[] itemIconKinds)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(imageIndex);
        ArgumentNullException.ThrowIfNull(itemIconKinds);
        if (metadata.Length == 0 || metadata.Length % 68 != 0 || metadata.Length / 68 > 256 || items.Length != 199 * 4 || itemIconKinds.Length != 22)
        {
            throw new InvalidDataException("Native magazines require byte-addressable complete 68-byte pages, item records and item-type icons.");
        }
        weapons.Validate();
        for (int item = 1; item < 199; item++)
        {
            if (items[item * 4] != 9)
            {
                continue;
            }
            int first = items[item * 4 + 2];
            int last = items[item * 4 + 3];
            if (first > last || last >= metadata.Length / 68)
            {
                throw new InvalidDataException("Magazine item range exceeds its selected page records.");
            }
            for (int page = first; page <= last; page++)
            {
                ReadOnlySpan<byte> row = metadata.AsSpan(page * 68, 68);
                if (row[21] != 0)
                {
                    throw new NotSupportedException("Inventory magazine pages require their selected section 87 text bank.");
                }
                if (row[24] != 255 && (row[24] >= 28 || row[24] >= weapons.Recipes.Length) || row[26] != 255 && (row[26] >= 16 || row[26] >= duelMoveCount) || row[27] != 255 && row[27] >= 8)
                {
                    throw new InvalidDataException("Magazine page refers to an unavailable weapon, Duel move or Pet Pals learning bit.");
                }
                for (int slot = 0; slot < 4; slot++)
                {
                    byte textId = row[55 + slot * 4];
                    if (textId != 255 && (textId >= text.Entries.Count || text[textId] is null))
                    {
                        throw new InvalidDataException("Magazine page refers to an absent selected text entry.");
                    }
                    byte imageId = row[39 + slot * 4];
                    if (imageId != 255)
                    {
                        if (imageId >= 40)
                        {
                            throw new InvalidDataException("Magazine page illustration lies outside the supported native atlas selectors.");
                        }
                        _ = NativeMagazineArtwork.GetDimensions(imageIndex, imageId);
                    }
                }
                if (row[24] != 255)
                {
                    foreach (NativeItemStock material in weapons.Recipes[row[24]].Materials)
                    {
                        if (material.ItemId != 0 && items[material.ItemId * 4] >= itemIconKinds.Length)
                        {
                            throw new InvalidDataException("Magazine weapon material has an unsupported native item-type icon selector.");
                        }
                    }
                }
            }
        }
    }
}
