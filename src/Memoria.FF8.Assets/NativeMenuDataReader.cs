using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

/// <summary>Record and selector bounds for native menu resources.</summary>
public static class NativeMenuDataReader
{
    public static void ValidateRefinementTable(byte[] bytes, int kind)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(kind);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(kind, 4);
        if (bytes.Length == 0 || bytes.Length % 8 != 0 || bytes.Length / 8 > 256 || kind == 4 && bytes.Length != 110 * 8)
        {
            throw new InvalidDataException("Native refinement requires complete eight-byte records in its selector domain.");
        }
        for (int offset = 0; offset < bytes.Length; offset += 8)
        {
            int ingredient = bytes[offset + 5];
            int output = bytes[offset + 7];
            bool validIngredient = kind == 4 ? ingredient == offset / 8 : ingredient > 0 && ingredient < (kind == 2 ? 57 : 199);
            ushort outputCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2));
            if (!validIngredient || output == 0 || output >= (kind is 0 or 2 ? 57 : 199) || bytes[offset + 6] == 0 || outputCount == 0 || kind == 4 && (bytes[offset + 4] != 1 || bytes[offset + 6] > 100 || outputCount > 100))
            {
                throw new InvalidDataException("Native refinement record has an invalid ingredient, output or quantity.");
            }
        }
    }

    public static void ValidateShopCatalogs(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 20 * 16 * 2)
        {
            throw new InvalidDataException("Native shops require twenty catalogs of sixteen two-byte slots.");
        }
        for (int offset = 0; offset < bytes.Length; offset += 2)
        {
            if (bytes[offset] >= 199)
            {
                throw new InvalidDataException("Native shop item lies outside its price and inventory domain.");
            }
        }
    }

    public static void ValidateShopPrices(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 199 * 4)
        {
            throw new InvalidDataException("Native shop prices require one four-byte record for every item ID.");
        }
    }

    public static void ValidateWeaponRecipes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 33 * 12)
        {
            throw new InvalidDataException("Native weapon recipes require thirty-three twelve-byte records.");
        }
        for (int row = 0; row < 33; row++)
        {
            for (int slot = 0; slot < 4; slot++)
            {
                if (bytes[row * 12 + 4 + slot * 2] >= 199)
                {
                    throw new InvalidDataException("Native weapon material lies outside the item domain.");
                }
            }
        }
    }

    public static void ValidateWeaponMessages(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 68)
        {
            throw new InvalidDataException("Native weapon message words require their complete fixed record domain.");
        }
    }

    public static void ValidateSolomonRingIngredients(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 16 || bytes[12] != 0)
        {
            throw new InvalidDataException("Solomon Ring requires three ingredient records followed by its zero item sentinel.");
        }
        HashSet<byte> ingredients = [];
        for (int offset = 0; offset < 12; offset += 4)
        {
            if (bytes[offset] is 0 or >= 199 || bytes[offset + 1] == 0 || !ingredients.Add(bytes[offset]))
            {
                throw new InvalidDataException("Solomon Ring requires three distinct valid item ingredients with nonzero quantities.");
            }
        }
    }

    public static byte ReadSolomonRingItemId(byte[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Length != 199 * 4)
        {
            throw new InvalidDataException("Solomon Ring item identity requires a complete native item metadata table.");
        }
        int[] ringIds = Enumerable.Range(1, items.Length / 4 - 1).Where(id => items[id * 4] == 15).ToArray();
        if (ringIds.Length != 1)
        {
            throw new InvalidDataException("Native item metadata requires one unambiguous Solomon Ring selector.");
        }
        return checked((byte)ringIds[0]);
    }
}
