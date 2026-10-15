using System.Buffers.Binary;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public int[] ReadEnemyDrawGuardianForces(int modelId, int level)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections is not (2 or 11))
        {
            throw new NotSupportedException("Enemy GF Draw metadata requires the original information section.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(sections == 11 ? 28 : 4));
        if (information < 0 || information > bytes.Length - 284)
        {
            throw new InvalidDataException("Enemy GF Draw metadata exceeds the original information section.");
        }
        int band = level < bytes[information + 244] ? 0 : level < bytes[information + 245] ? 1 : 2;
        return Enumerable.Range(0, 4).Select(slot => (int)bytes[information + 260 + band * 8 + 2 * slot]).Where(id => id >= 64).Select(id => id - 64).ToArray();
    }

    public NativeBattleAiDefinition ReadEnemyAi(int modelId, int level)
    {
        byte[] bytes = Read($"c0m{modelId:000}.dat");
        if (bytes.Length < 48 || BinaryPrimitives.ReadInt32LittleEndian(bytes) != 11)
        {
            throw new NotSupportedException("Enemy AI requires its original eleven-section monster directory.");
        }
        int information = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28));
        int section = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(32));
        int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(36));
        if (information < 0 || information > bytes.Length - 246 || section < 0 || end > bytes.Length || end - section < 16 || BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section)) != 3)
        {
            throw new InvalidDataException("Enemy AI requires its original bounded script/text directory.");
        }
        int script = checked(section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + 4)));
        int textIndices = checked(section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + 8)));
        int text = checked(section + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(section + 12)));
        if (script < section + 16 || script > textIndices - 20 || textIndices > text || text > end || (text - textIndices) % 2 != 0)
        {
            throw new InvalidDataException("Enemy AI script/text offsets are not ordered within their native section.");
        }
        byte[][] scripts = new byte[5][];
        for (int index = 0; index < scripts.Length; index++)
        {
            int start = checked(script + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(script + 4 * index)));
            int finish = index == 4 ? textIndices : checked(script + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(script + 4 * (index + 1))));
            if (start < script + 20 || finish < start || finish > textIndices)
            {
                throw new InvalidDataException("Enemy AI callback exceeds its original script bank.");
            }
            scripts[index] = bytes.AsSpan(start, finish - start).ToArray();
        }
        string[] texts = new string[(text - textIndices) / 2];
        for (int index = 0; index < texts.Length; index++)
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(textIndices + 2 * index));
            if (offset == ushort.MaxValue)
            {
                texts[index] = "";
                continue;
            }
            int start = checked(text + offset);
            if (start >= end)
            {
                throw new InvalidDataException("Enemy AI text exceeds its original text bank.");
            }
            int terminator = bytes.AsSpan(start, end - start).IndexOf((byte)0);
            if (terminator < 0)
            {
                throw new InvalidDataException("Enemy AI text has no original terminator.");
            }
            texts[index] = FF8Encoding.CreateEuropean().GetString(bytes.AsSpan(start, terminator));
        }
        NativeBattleAiAbility[] abilities = new NativeBattleAiAbility[48];
        for (int index = 0; index < abilities.Length; index++)
        {
            int offset = information + 52 + 4 * index;
            byte command = bytes[offset];
            byte sequence = bytes[offset + 1];
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2));
            if (command == 8)
            {
                if (id >= battleKernel.EnemyAttacks.Count)
                {
                    throw new InvalidDataException("Enemy AI ability references an absent original kernel attack.");
                }
                var attack = battleKernel.EnemyAttacks[id];
                abilities[index] = new(command, sequence, id, attack.AttackType, attack.AttackPower, (int)attack.Element, attack.Statuses0, attack.Statuses1);
            }
            else
            {
                abilities[index] = new(command, sequence, id);
            }
        }
        int band = level < bytes[information + 244] ? 0 : level < bytes[information + 245] ? 1 : 2;
        return new NativeBattleAiDefinition(scripts, abilities, band, texts);
    }
}
