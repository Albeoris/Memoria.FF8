namespace Memoria.FF7.Runtime;

/// <summary>Resolves logical party names without changing their physical progress records.</summary>
public static class Ff7CharacterDisplayName
{
    public static string Resolve(IReadOnlyList<Ff7CharacterState> characters, int identity)
    {
        int record = Ff7NativeCharacterIdentity.RecordId(identity);
        if (identity == 9)
        {
            return characters.Single(character => character.Id == 0).Name;
        }
        Ff7CharacterState character = characters.Single(character => character.Id == record);
        if (identity != 10)
        {
            return character.Name;
        }
        if (character.NativeRecord is not { Length: 132 })
        {
            throw new InvalidDataException("FFVII character name requires its native character record.");
        }
        return Ff7NativeSaveSlot.ReadText(character.NativeRecord.AsSpan(16, 12));
    }
}
