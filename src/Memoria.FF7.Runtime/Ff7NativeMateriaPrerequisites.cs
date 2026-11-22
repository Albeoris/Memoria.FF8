using System.Buffers.Binary;

namespace Memoria.FF7.Runtime;

public static class Ff7NativeMateriaPrerequisites
{
    public static bool Evaluate(Ff7Progress progress, int parameter, IReadOnlyList<IReadOnlyList<byte>> groups)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(groups);
        if ((uint)parameter > byte.MaxValue)
        {
            throw new InvalidDataException("The native materia prerequisite selector must be a byte.");
        }
        if (parameter > 3)
        {
            return true;
        }
        if (groups.Count != 4)
        {
            throw new InvalidDataException("The native materia prerequisite operation requires its original groups.");
        }
        bool requireMastery = parameter != 3;
        foreach (byte required in groups[parameter])
        {
            bool found = false;
            for (int id = 0; id < 9 && !found; id++)
            {
                if ((progress.AvailableCharacters & (1 << id)) == 0)
                {
                    continue;
                }
                Ff7CharacterState character = progress.Characters.SingleOrDefault(character => character.Id == id) ?? throw new InvalidDataException("An available native character has no materia record.");
                if (character.NativeRecord.Length != 132)
                {
                    throw new InvalidDataException("Native equipped materia requires its complete character record.");
                }
                for (int offset = 64; offset < 128; offset += 4)
                {
                    uint value = BinaryPrimitives.ReadUInt32LittleEndian(character.NativeRecord.AsSpan(offset));
                    if ((byte)value == required && (!requireMastery || value >> 8 == 0xFFFFFF))
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found && !progress.Materia.Any(materia => materia.MateriaId == required && (!requireMastery || materia.AbilityPoints == 0xFFFFFF)))
            {
                return false;
            }
        }
        return true;
    }
}
