namespace Memoria.FF8.Runtime;

/// <summary>Explicit initialization policy for world saves without a retained event process.</summary>
public static class NativeWorldSaveCompatibility
{
    public static byte[] InitialParty(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] party;
        if (state.OriginalSave is { } original)
        {
            original.Validate();
            party = original.FieldParty;
        }
        else
        {
            NativeCharacterVitals vitals = state.CharacterVitals ?? throw new NotSupportedException("World save initialization requires saved character identities and combat slots.");
            vitals.Validate();
            if (vitals.CombatParty.Where(row => row != 255).Distinct().Count() != vitals.CombatParty.Count(row => row != 255))
            {
                throw new InvalidDataException("World save initialization requires distinct occupied combat rows.");
            }
            party = vitals.CombatParty.Select(row => row == 255 ? (byte)255 : vitals.Records[row].CharacterId).ToArray();
        }
        if (party.Any(identity => identity != 255 && identity > 10) || party.Where(identity => identity != 255).Distinct().Count() != party.Count(identity => identity != 255))
        {
            throw new InvalidDataException("World save initialization requires distinct saved character identities.");
        }
        return party;
    }
}
