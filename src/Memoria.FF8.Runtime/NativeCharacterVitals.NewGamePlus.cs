using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NewGamePlusDreamMagicBank(byte GuestIdentity, ImmutableArray<NativeMagicStock> Stock, ImmutableArray<byte> FieldMasks, ImmutableArray<byte> BattleMasks)
{
    public void Validate()
    {
        if (GuestIdentity is < 8 or > 10 || Stock.IsDefault || Stock.Length != 32 || Stock.Any(stock => stock is null || stock.SpellId > 56 || stock.Count > 100) || Stock.Where(stock => stock.SpellId != 0 && stock.Count != 0).Select(stock => stock.SpellId).Distinct().Count() != Stock.Count(stock => stock.SpellId != 0 && stock.Count != 0) || FieldMasks.IsDefault || FieldMasks.Length != 57 || BattleMasks.IsDefault || BattleMasks.Length != 57 || FieldMasks.Concat(BattleMasks).Any(mask => mask > 15))
        {
            throw new InvalidDataException("New Game+ dream magic backup requires its stable guest identity, native stock bank and use histories.");
        }
    }
}

public sealed partial record NativeCharacterVitals
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<byte, uint>? NewGamePlusDreamExperienceBackup { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableSortedDictionary<byte, NewGamePlusDreamMagicBank>? NewGamePlusDreamMagicBackup { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeMagicStock>? NewGamePlusDreamMagicExtensionBackup { get; init; }

    internal static void ValidateNewGamePlusDreamMagicBackup(ImmutableSortedDictionary<byte, NewGamePlusDreamMagicBank>? banks, ImmutableArray<NativeMagicStock>? extension)
    {
        if (banks is null && extension is not null || banks is not null && (extension is null || extension.Value.IsDefault || banks.Keys.Any(row => row >= NativeRecordCount) || banks.Values.Any(bank => bank is null) || banks.Values.Select(bank => bank.GuestIdentity).Distinct().Count() != banks.Count))
        {
            throw new InvalidDataException("New Game+ dream magic backup requires bounded distinct alias owners and its original inventory extension.");
        }
        foreach (NewGamePlusDreamMagicBank bank in banks?.Values ?? [])
        {
            bank.Validate();
        }
        if (extension is { } stock && (stock.Any(entry => entry is null || entry.SpellId is < 1 or > 56 || entry.Count is < 1 or > 100) || stock.Select(entry => entry.SpellId).Distinct().Count() != stock.Length))
        {
            throw new InvalidDataException("New Game+ dream magic backup contains an invalid inventory extension.");
        }
    }

    internal NativeCharacterVitals BindNewGamePlusDreamMagic()
    {
        if (NewGamePlusPermanentGuests is null || !DreamEnabled || MagicSystem.SharedMagicStock || NewGamePlusDreamMagicBackup is not null)
        {
            return this;
        }
        NativeCharacterVitals next = DeepCopy();
        ImmutableSortedDictionary<byte, NewGamePlusDreamMagicBank>.Builder backup = ImmutableSortedDictionary.CreateBuilder<byte, NewGamePlusDreamMagicBank>();
        for (byte row = 0; row < NativeRecordCount; row++)
        {
            byte identity = Records[row].CharacterId;
            if (identity < 8)
            {
                continue;
            }
            backup.Add(row, new(identity, Records[row].MagicInventory.ToImmutableArray(), MagicSystem.PersonalFieldMasks[row].ToImmutableArray(), MagicSystem.PersonalBattleMasks[row].ToImmutableArray()));
            next.Records[row] = next.Records[row] with { MagicInventory = Records[identity].MagicInventory.ToArray() };
            next.Records[identity] = next.Records[identity] with { MagicInventory = EmptyNativeMagicInventory() };
            next.MagicSystem.PersonalFieldMasks[row] = MagicSystem.PersonalFieldMasks[identity].ToArray();
            next.MagicSystem.PersonalBattleMasks[row] = MagicSystem.PersonalBattleMasks[identity].ToArray();
            next.MagicSystem.PersonalFieldMasks[identity] = new byte[57];
            next.MagicSystem.PersonalBattleMasks[identity] = new byte[57];
        }
        return next with
        {
            NewGamePlusDreamMagicBackup = backup.ToImmutable(),
            NewGamePlusDreamMagicExtensionBackup = MagicSystem.RecordZeroExtension.ToImmutableArray(),
            MagicSystem = next.MagicSystem with { RecordZeroExtension = [] }
        };
    }

    internal NativeCharacterVitals UnbindNewGamePlusDreamMagic()
    {
        if (NewGamePlusDreamMagicBackup is null)
        {
            return this;
        }
        NativeCharacterVitals next = DeepCopy();
        foreach ((byte row, NewGamePlusDreamMagicBank bank) in NewGamePlusDreamMagicBackup)
        {
            next.Records[bank.GuestIdentity] = next.Records[bank.GuestIdentity] with { MagicInventory = Records[row].MagicInventory.ToArray() };
            next.Records[row] = next.Records[row] with { MagicInventory = bank.Stock.ToArray() };
            next.MagicSystem.PersonalFieldMasks[bank.GuestIdentity] = MagicSystem.PersonalFieldMasks[row].ToArray();
            next.MagicSystem.PersonalBattleMasks[bank.GuestIdentity] = MagicSystem.PersonalBattleMasks[row].ToArray();
            next.MagicSystem.PersonalFieldMasks[row] = bank.FieldMasks.ToArray();
            next.MagicSystem.PersonalBattleMasks[row] = bank.BattleMasks.ToArray();
        }
        return next with
        {
            NewGamePlusDreamMagicBackup = null,
            NewGamePlusDreamMagicExtensionBackup = null,
            MagicSystem = next.MagicSystem with { RecordZeroExtension = NewGamePlusDreamMagicExtensionBackup?.ToArray() ?? [] }
        };
    }

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
            return result with { Vitals = (result.Vitals with { Records = records, NewGamePlusDreamExperienceBackup = backup.ToImmutable() }).BindNewGamePlusDreamMagic().RebuildParty() };
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
        return result with { Vitals = (result.Vitals with { Records = records, NewGamePlusDreamExperienceBackup = null }).UnbindNewGamePlusDreamMagic().RebuildParty() };
    }
}
