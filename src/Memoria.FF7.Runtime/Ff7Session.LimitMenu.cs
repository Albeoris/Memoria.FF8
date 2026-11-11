namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    public bool TrySetMenuLimitLevel(int characterId, int level)
    {
        if (level is < 1 or > 4 || !CanUsePlayerMenu || !Progress.Party.Contains(characterId) || GuestFieldVitals?.StoryCharacters.Contains(characterId) == true)
        {
            return false;
        }

        int index = Array.FindIndex(Progress.Characters, character => character.Id == characterId);
        if (index < 0)
        {
            return false;
        }

        Ff7CharacterState selected = Progress.Characters[index];
        if (Ff7NativeBattleLimits.ReadLearnedMask(selected, level) == 0)
        {
            return false;
        }

        byte[] record = (byte[])selected.NativeRecord.Clone();
        record[14] = (byte)level;
        record[15] = 0;
        Ff7CharacterState[] characters = (Ff7CharacterState[])Progress.Characters.Clone();
        characters[index] = selected with { NativeRecord = record };
        Progress = Progress with { Characters = characters };
        return true;
    }
}
