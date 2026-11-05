using System.Text.Json;

namespace Memoria.FF8.Runtime;

/// <summary>Explicit initialization policy for world saves without a retained event process.</summary>
public static class NativeWorldSaveCompatibility
{
    public static byte[] InitialParty(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        byte[] party;
        if (CurrentFieldParty(state) is { } current)
        {
            party = current;
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
        ValidateParty(party);
        return party;
    }

    internal static byte[]? CurrentFieldParty(GameState state)
    {
        JsonElement? snapshot = state.DepartingFieldState ?? state.EventState ?? state.WorldEventState;
        byte[]? party = null;
        if (snapshot is JsonElement captured && captured.TryGetProperty("Scene", out JsonElement scene) && scene.TryGetProperty("Party", out JsonElement members))
        {
            party = members.EnumerateArray().Select(value => value.GetByte()).ToArray();
        }
        else if (state.GameplaySceneFacts is { } facts)
        {
            facts.Validate();
            party = facts.FieldParty.ToArray();
        }
        else if (state.OriginalSave is { } original)
        {
            original.Validate();
            party = original.FieldParty;
        }
        if (party is not null)
        {
            ValidateParty(party);
        }
        return party;
    }

    private static void ValidateParty(byte[] party)
    {
        if (party.Length != 3 || party.Any(identity => identity != 255 && identity > 10) || party.Where(identity => identity != 255).Distinct().Count() != party.Count(identity => identity != 255))
        {
            throw new InvalidDataException("World field-party ownership requires three physical slots with distinct saved character identities.");
        }
    }
}
