namespace Memoria.FF7.Formats;

public sealed record KernelCharacterData(byte Id, byte Level, ReadOnlyMemory<byte> EncodedName, byte Weapon, byte Armor, byte Accessory, ushort CurrentHp, ushort BaseHp, ushort CurrentMp, ushort BaseMp, ushort RecordedMaximumHp, ushort RecordedMaximumMp, uint Experience, ReadOnlyMemory<byte> Raw);
public sealed record KernelInventorySlot(ushort Encoded, ushort ItemId, byte Quantity);
public sealed record KernelMateriaSlot(uint Encoded, byte MateriaId, uint AbilityPoints);
public sealed record KernelInitialData(ReadOnlyMemory<byte> NativeData, IReadOnlyList<KernelCharacterData> Characters, IReadOnlyList<byte> Party, IReadOnlyList<KernelInventorySlot> Inventory, IReadOnlyList<KernelMateriaSlot> Materia, uint Gil)
{
    public static KernelInitialData Read(ReadOnlySpan<byte> source)
    {
        if (source.Length != 2876)
        {
            throw new InvalidDataException("Unsupported FF7 initial kernel payload size; expected 2876 bytes.");
        }
        byte[] data = source.ToArray();
        BinaryCursor cursor = new(source);
        KernelCharacterData[] characters = new KernelCharacterData[9];
        for (int i = 0; i < characters.Length; i++)
        {
            int start = i * 132;
            cursor.Seek(start);
            byte id = cursor.U8();
            byte level = cursor.U8();
            cursor.Skip(14);
            ReadOnlyMemory<byte> name = data.AsMemory(cursor.Position, 12);
            cursor.Skip(12);
            byte weapon = cursor.U8();
            byte armor = cursor.U8();
            byte accessory = cursor.U8();
            cursor.Seek(start + 44);
            ushort hp = cursor.U16();
            ushort baseHp = cursor.U16();
            ushort mp = cursor.U16();
            ushort baseMp = cursor.U16();
            cursor.Skip(4);
            ushort maximumHp = cursor.U16();
            ushort maximumMp = cursor.U16();
            uint experience = cursor.U32();
            characters[i] = new KernelCharacterData(id, level, name, weapon, armor, accessory, hp, baseHp, mp, baseMp, maximumHp, maximumMp, experience, data.AsMemory(start, 132));
        }
        cursor.Seek(0x4A4);
        byte[] party = cursor.Take(3).ToArray();
        if (party.Any(id => id != 255 && id >= 9))
        {
            throw new InvalidDataException("FF7 initial party contains an unsupported character ID.");
        }
        cursor.Seek(0x4A8);
        KernelInventorySlot[] inventory = new KernelInventorySlot[320];
        for (int i = 0; i < inventory.Length; i++)
        {
            ushort encoded = cursor.U16();
            inventory[i] = new KernelInventorySlot(encoded, (ushort)(encoded & 511), (byte)(encoded >> 9));
        }
        cursor.Seek(0x728);
        KernelMateriaSlot[] materia = new KernelMateriaSlot[200];
        for (int i = 0; i < materia.Length; i++)
        {
            uint encoded = cursor.U32();
            materia[i] = new KernelMateriaSlot(encoded, (byte)encoded, encoded >> 8);
        }
        cursor.Seek(0xB28);
        return new KernelInitialData(data, Array.AsReadOnly(characters), Array.AsReadOnly(party), Array.AsReadOnly(inventory), Array.AsReadOnly(materia), cursor.U32());
    }
}
