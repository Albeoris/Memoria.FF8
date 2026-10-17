using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public byte ReadEnemyNativeInformation246(int model)
    {
        byte[] bytes = Read($"c0m{model:000}.dat");
        int sections = bytes.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : 0;
        if (sections is not (2 or 11))
        {
            throw new InvalidDataException("Native model initialization requires the original DAT information directory.");
        }
        if (sections == 11 && bytes.Length < 32)
        {
            throw new InvalidDataException("Native model initialization requires the complete DAT information directory.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 247)
        {
            throw new InvalidDataException("Native model initialization BYTE246 exceeds the original information section.");
        }
        return bytes[information + 246];
    }

    public byte ReadEnemyNativeEntryFlags(int model)
    {
        byte[] bytes = Read($"c0m{model:000}.dat");
        int sections = bytes.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : 0;
        if (sections is not (2 or 11))
        {
            throw new InvalidDataException("Native encounter advantage requires the original DAT information directory.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 255)
        {
            throw new InvalidDataException("Native encounter advantage flags exceed the original information section.");
        }
        return bytes[information + 254];
    }

    public uint ReadEnemyNativeCombatantFlags(int model)
    {
        byte[] bytes = Read($"c0m{model:000}.dat");
        int sections = bytes.Length >= 8 ? BinaryPrimitives.ReadInt32LittleEndian(bytes) : 0;
        if (sections is not (2 or 11))
        {
            throw new InvalidDataException("Native combatant flags require the original DAT information directory.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 255)
        {
            throw new InvalidDataException("Native combatant flags exceed the original information section.");
        }
        //48BBD0: static properties, independent of allocation/visibility/status flags.
        uint flags = 0x10 | ((bytes[information + 247] & 8) != 0 ? 0x8000u : 0) | ((bytes[information + 247] & 16) != 0 ? 0x2000u : 0) | ((bytes[information + 254] & 64) != 0 ? 0x10000u : 0);
        //48BBD0 marks allocations whose original death callback begins with a nonzero opcode.
        if (ReadEnemyAi(model, 1).Scripts[3][0] != 0)
        {
            flags |= 0x20;
        }
        return flags;
    }
}
