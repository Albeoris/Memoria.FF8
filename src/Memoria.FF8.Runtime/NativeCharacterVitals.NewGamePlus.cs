using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeCharacterVitals
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<byte, uint>? NewGamePlusDreamExperienceBackup { get; init; }

    private NativeJunctionResult ReconcileNewGamePlusDreamExperience(NativeJunctionResult result)
    {
        if (NewGamePlusPermanentGuests is null || Records.Length != ExtendedRecordCount || DreamEnabled == result.Vitals.DreamEnabled)
        {
            return result;
        }
        NativeCharacterVital[] records = result.Vitals.Records.ToArray();
        if (result.Vitals.DreamEnabled)
        {
            ImmutableSortedDictionary<byte, uint>.Builder backup = ImmutableSortedDictionary.CreateBuilder<byte, uint>();
            for (byte row = 0; row < NativeRecordCount; row++)
            {
                byte identity = records[row].CharacterId;
                if (identity < 8)
                {
                    continue;
                }
                backup.Add(row, Records[row].Experience);
                records[row] = records[row] with { Experience = Records[identity].Experience };
            }
            return result with { Vitals = (result.Vitals with { Records = records, NewGamePlusDreamExperienceBackup = backup.ToImmutable() }).RebuildParty() };
        }
        if (NewGamePlusDreamExperienceBackup is null)
        {
            throw new InvalidDataException("New Game+ dream progression requires its saved original character experience owners.");
        }
        foreach ((byte row, uint experience) in NewGamePlusDreamExperienceBackup)
        {
            byte identity = Records[row].CharacterId;
            if (identity is < 8 or > 10)
            {
                throw new InvalidDataException("New Game+ dream progression lost its current guest identity.");
            }
            records[identity] = records[identity] with { Experience = Records[row].Experience };
            records[row] = records[row] with { Experience = experience };
        }
        return result with { Vitals = (result.Vitals with { Records = records, NewGamePlusDreamExperienceBackup = null }).RebuildParty() };
    }
}
