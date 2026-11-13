using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

internal readonly record struct Ff7WorldEncounterModifiers(int EncounterRate, int InitiativeRate, int ChocoboLure, int SpecialRollShift)
{
    public static Ff7WorldEncounterModifiers Baseline { get; } = new(16, 16, 0, 0);

    public static Ff7WorldEncounterModifiers Read(Ff7Progress progress, ReadOnlySpan<byte> materiaData)
    {
        int enemyLure = 0;
        int enemyAway = 0;
        byte chocoboLure = 0;
        byte initiative = 16;
        bool halveSpecialChance = false;
        foreach (int id in progress.Party)
        {
            if (id == 255)
            {
                continue;
            }
            Ff7CharacterState character = progress.Characters.Single(candidate => candidate.Id == id);
            ReadOnlySpan<byte> equipment = character.NativeRecord.AsSpan(64, 16 * sizeof(uint));
            int characterEnemyLure = 0;
            int characterEnemyAway = 0;
            int characterChocoboLure = 0;
            int characterInitiative = 0;
            // The native refresh replaces this flag on each occupied party slot.
            halveSpecialChance = false;
            for (int offset = 0; offset < equipment.Length; offset += sizeof(uint))
            {
                uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(equipment[offset..]);
                int materiaId = (byte)encoded;
                if (materiaId == 255)
                {
                    continue;
                }
                if (materiaId >= materiaData.Length / 20)
                {
                    throw new InvalidDataException("Equipped FFVII materia references an absent original record.");
                }
                ReadOnlySpan<byte> materia = materiaData.Slice(materiaId * 20, 20);
                if (materia[13] is not (0x21 or 0x41) || materia[14] is not (1 or 2 or 3))
                {
                    continue;
                }
                int level = Ff7NativeMateriaInventory.ReadLevel(materia, encoded >> 8);
                int valueIndex = level;
                while (valueIndex > 0 && materia[14 + valueIndex] == 255)
                {
                    valueIndex--;
                }
                if (valueIndex == 0)
                {
                    throw new InvalidDataException("FFVII encounter materia has no defined bonus at its current level.");
                }
                int value = materia[14 + valueIndex];
                switch (materia[14])
                {
                    case 1 when materia[13] == 0x41 && materiaId == 7:
                        characterEnemyAway += value;
                        break;
                    case 1:
                        characterEnemyLure += value;
                        break;
                    case 2:
                        characterChocoboLure += value;
                        break;
                    case 3:
                        characterInitiative += value;
                        if (materia[13] == 0x21 && materiaId == 10)
                        {
                            characterInitiative = Math.Min(85, characterInitiative);
                            halveSpecialChance |= level == 5;
                        }
                        break;
                }
            }
            enemyLure += Math.Min(255, characterEnemyLure);
            enemyAway += Math.Min(255, characterEnemyAway);
            chocoboLure += (byte)Math.Min(32, characterChocoboLure);
            initiative = unchecked((byte)(initiative + characterInitiative));
        }
        int encounterRate = 16 + enemyLure - enemyAway;
        encounterRate = encounterRate < 0 ? 2 : Math.Min(32, (int)unchecked((byte)encounterRate));
        int initiativeRate = Math.Min(85, (int)initiative);
        return new(WorldRate(encounterRate), WorldRate(initiativeRate), Math.Min(32, (int)chocoboLure), halveSpecialChance ? 1 : 0);
    }

    private static int WorldRate(int rate) => rate > 16 ? rate * 2 : rate;
}
