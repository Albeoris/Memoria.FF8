namespace Memoria.FF7.Events;

/// <summary>Encoded FFVII field selectors. Word selectors alias the corresponding byte storage.</summary>
public enum FieldBank : byte
{
    Immediate = 0,
    Persistent0Byte = 1,
    Persistent0Word = 2,
    Persistent1Byte = 3,
    Persistent1Word = 4,
    MapByte = 5,
    MapWord = 6,
    Persistent4Word = 7,
    Persistent2Byte = 11,
    Persistent2Word = 12,
    Persistent3Byte = 13,
    Persistent3Word = 14,
    Persistent4Byte = 15
}

public sealed class FieldVariables
{
    private readonly byte[] persistent;
    private readonly byte[] map;

    public FieldVariables() : this(new byte[1280], new byte[256])
    {
    }

    public FieldVariables(byte[] persistent, byte[] map)
    {
        if (persistent.Length != 1280 || map.Length != 256)
        {
            throw new ArgumentException("FFVII variable storage must contain 1280 persistent and 256 map bytes.");
        }
        this.persistent = (byte[])persistent.Clone();
        this.map = (byte[])map.Clone();
    }

    public byte[] CopyPersistent() => (byte[])persistent.Clone();
    public byte[] CopyMap() => (byte[])map.Clone();
    internal void ClearMap() => Array.Clear(map);

    public byte ReadByte(FieldBank bank, byte address)
    {
        if (bank == FieldBank.Immediate)
        {
            return address;
        }
        var (storage, index, _) = Locate(bank, address);
        return storage[index];
    }

    public short ReadWord(FieldBank bank, ushort operand)
    {
        if (bank == FieldBank.Immediate)
        {
            return unchecked((short)operand);
        }
        var (storage, index, word) = Locate(bank, (byte)operand);
        CheckWord(storage, index, word);
        return unchecked((short)(storage[index] | (word ? storage[index + 1] << 8 : 0)));
    }

    public void WriteByte(FieldBank bank, byte address, byte value)
    {
        var (storage, index, _) = Locate(bank, address);
        storage[index] = value;
    }

    public void WriteWord(FieldBank bank, byte address, short value)
    {
        var (storage, index, word) = Locate(bank, address);
        CheckWord(storage, index, word);
        storage[index] = unchecked((byte)value);
        if (word)
        {
            storage[index + 1] = unchecked((byte)(value >> 8));
        }
    }

    public ushort ReadWindowParameter(FieldBank bank, ushort address)
    {
        if (bank == FieldBank.Immediate)
        {
            return address;
        }
        if ((byte)bank is 8 or 9 or 10)
        {
            return 0;
        }
        var (storage, index, word) = Locate(bank, address);
        if ((uint)index >= storage.Length || word && index + 1 >= storage.Length)
        {
            throw new NotSupportedException("FFVII numeric message parameter extends beyond its admitted native variable storage.");
        }
        return (ushort)(storage[index] | (word ? storage[index + 1] << 8 : 0));
    }

    private (byte[] Storage, int Index, bool Word) Locate(FieldBank bank, int address)
    {
        return bank switch
        {
            FieldBank.Persistent0Byte => (persistent, address, false),
            FieldBank.Persistent0Word => (persistent, address, true),
            FieldBank.Persistent1Byte => (persistent, 256 + address, false),
            FieldBank.Persistent1Word => (persistent, 256 + address, true),
            FieldBank.Persistent2Byte => (persistent, 512 + address, false),
            FieldBank.Persistent2Word => (persistent, 512 + address, true),
            FieldBank.Persistent3Byte => (persistent, 768 + address, false),
            FieldBank.Persistent3Word => (persistent, 768 + address, true),
            FieldBank.Persistent4Byte => (persistent, 1024 + address, false),
            FieldBank.Persistent4Word => (persistent, 1024 + address, true),
            FieldBank.MapByte => (map, address, false),
            FieldBank.MapWord => (map, address, true),
            _ => throw new NotSupportedException($"Unsupported FFVII variable selector {(byte)bank:X2}.")
        };
    }

    private static void CheckWord(byte[] storage, int index, bool word)
    {
        if (word && index + 1 >= storage.Length)
        {
            throw new InvalidDataException("FFVII word variable extends outside its storage.");
        }
    }
}
