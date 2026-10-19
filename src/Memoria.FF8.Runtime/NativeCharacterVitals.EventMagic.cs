namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    /// <summary>Native5222D0 resolves the first combat-party identity, then repeats47EE00 until its first nonzero result.</summary>
    public NativeCharacterVitals AddEventMagic(int rawCharacter, int rawSpell, int quantity)
    {
        Validate();
        byte identity = unchecked((byte)rawCharacter);
        int slot = Array.FindIndex(CombatParty, row => row != 255 && Records[row].CharacterId == identity);
        if (slot < 0 || quantity <= 0 || rawSpell == 0)
        {
            return this;
        }
        int row = CombatParty[slot];
        NativeMagicStock[] inventory = Records[row].MagicInventory.ToArray();
        for (int remaining = quantity; remaining > 0; remaining--)
        {
            int index = Array.FindIndex(inventory, stock => stock.SpellId == rawSpell);
            if (index >= 0)
            {
                int count = inventory[index].Count;
                if (count >= 100)
                {
                    break;
                }
                inventory[index] = inventory[index] with { Count = (byte)Math.Min(100, (long)count + remaining) };
                break;
            }
            index = Array.FindIndex(inventory, stock => stock.SpellId == 0);
            if (index < 0)
            {
                break;
            }
            byte spell = unchecked((byte)rawSpell);
            //47EE00 compares the full DWORD but stores only its low BYTE. A zero low BYTE leaves this same empty slot available.
            int additions = spell == 0 ? remaining : 1;
            inventory[index] = new NativeMagicStock(spell, unchecked((byte)(inventory[index].Count + additions)));
            if (spell == 0)
            {
                break;
            }
        }
        NativeCharacterVital[] records = Records.ToArray();
        records[row] = records[row] with { MagicInventory = inventory };
        return this with { Records = records };
    }
}
