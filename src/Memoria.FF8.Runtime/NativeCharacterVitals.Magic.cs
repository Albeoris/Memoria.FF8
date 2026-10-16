namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native47ED90: first matching stock at100 returns1; absent magic and all32 IDs occupied returns2.</summary>
    public static int GetMagicCapacity(NativeCharacterVital character, int magicId)
    {
        RequireMagicInventory(character);
        if (magicId == 0)
        {
            return 0;
        }
        foreach (NativeMagicStock stock in character.MagicInventory)
        {
            if (stock.SpellId == magicId)
            {
                return stock.Count >= 100 ? 1 : 0;
            }
        }
        return character.MagicInventory.Any(stock => stock.SpellId == 0) ? 0 : 2;
    }

    /// <summary>Native47EEB0 after the caller resolves its combat slot to the physical saved row.</summary>
    public static bool IsMagicJunctioned(NativeCharacterVital character, int magicId)
    {
        if (character.Junction.MagicJunctions is not { Length: 20 })
        {
            throw new InvalidDataException("Native magic junction lookup requires all twenty saved junction bytes.");
        }
        return magicId != 0 && character.Junction.MagicJunctions.Any(spell => spell == magicId);
    }

    /// <summary>Native47EE00 inserts exactly one unit without sorting or clearing a retained empty-slot count.</summary>
    public NativeCharacterVitals AddMagicUnit(int physicalRow, int magicId, out int result)
    {
        Validate();
        if ((uint)physicalRow >= Records.Length)
        {
            throw new NotSupportedException("Native magic insertion requires an owned physical saved row.");
        }
        NativeCharacterVital character = Records[physicalRow];
        result = GetMagicCapacity(character, magicId);
        if (result != 0 || magicId == 0)
        {
            return this;
        }
        int slot = Array.FindIndex(character.MagicInventory, stock => stock.SpellId == magicId);
        if (slot < 0)
        {
            slot = Array.FindIndex(character.MagicInventory, stock => stock.SpellId == 0);
        }
        NativeMagicStock[] inventory = character.MagicInventory.ToArray();
        inventory[slot] = new NativeMagicStock(unchecked((byte)magicId), unchecked((byte)(inventory[slot].Count + 1)));
        NativeCharacterVital[] records = Records.ToArray();
        records[physicalRow] = character with { MagicInventory = inventory };
        return (this with { Records = records }).DeepCopy();
    }

    private static void RequireMagicInventory(NativeCharacterVital character)
    {
        if (character.MagicInventory is not { Length: 32 } || character.MagicInventory.Any(stock => stock is null))
        {
            throw new InvalidDataException("Native magic stock lookup requires all thirty-two saved ID/count pairs.");
        }
    }
}
