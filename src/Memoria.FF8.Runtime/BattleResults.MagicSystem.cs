using System.Text.Json;

namespace Memoria.FF8.Runtime;

/// <summary>One aggregate receipt for all logical banks and allowance histories, independent of active party membership.</summary>
public sealed record BattleMagicSystemReceipt(MagicSystemState Before, MagicSystemState After, NativeMagicStock[][] BanksBefore, NativeMagicStock[][] BanksAfter)
{
    public BattleMagicSystemReceipt DeepCopy() => new(Before.DeepCopy(), After.DeepCopy(), BanksBefore.Select(bank => bank.ToArray()).ToArray(), BanksAfter.Select(bank => bank.ToArray()).ToArray());
    public void Validate()
    {
        Before.Validate();
        After.Validate();
        if (Before.SharedMagicStock != After.SharedMagicStock || Before.InfiniteMagic != After.InfiniteMagic || Before.SmartAttack != After.SmartAttack || !Before.AttackModes.SequenceEqual(After.AttackModes) || Before.HistoriesInitialized != After.HistoriesInitialized || !Before.SharedFieldMasks.SequenceEqual(After.SharedFieldMasks) || !Before.PersonalFieldMasks.Zip(After.PersonalFieldMasks).All(pair => pair.First.SequenceEqual(pair.Second)) || BanksBefore is not { Length: 8 } || BanksAfter is not { Length: 8 })
        {
            throw new InvalidDataException("Battle magic receipt changes field histories or rules during an encounter.");
        }
        foreach (NativeMagicStock[] bank in BanksBefore.Concat(BanksAfter))
        {
            BattleCombatant.ValidateMagicInventory(bank);
        }
        if (Before.RecordZeroExtension.Any(extra => BanksBefore[0].Any(stock => stock.SpellId == extra.SpellId)) || After.RecordZeroExtension.Any(extra => BanksAfter[0].Any(stock => stock.SpellId == extra.SpellId)))
        {
            throw new InvalidDataException("Battle magic receipt duplicates extension stock in its native bank.");
        }
        if (Before.SharedMagicStock && Enumerable.Range(1, 7).Any(bank => !BanksBefore[bank].SequenceEqual(BanksAfter[bank])))
        {
            throw new InvalidDataException("Shared battle magic receipt modifies dormant personal stock.");
        }
    }
}

public sealed partial record BattleResult
{
    public BattleMagicSystemReceipt? MagicSystemReceipt { get; init; }
}

public static partial class BattleResultProjection
{
    private static NativeCharacterVitals ApplyMagicSystemReceipt(BattleResult result, NativeCharacterVitals vitals)
    {
        if (result.MagicSystemReceipt is not { } receipt)
        {
            if (vitals.MagicSystem.RequiresMemoriaFormat && result.RewardPolicy == BattleRewardPolicy.NativeCombat)
            {
                throw new InvalidDataException("Feature-bearing battle result requires its aggregate magic receipt.");
            }
            return vitals;
        }
        receipt.Validate();
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(receipt.Before), JsonSerializer.SerializeToElement(vitals.MagicSystem)) || Enumerable.Range(0, 8).Any(row => !vitals.Records[row].MagicInventory.SequenceEqual(receipt.BanksBefore[row])))
        {
            throw new InvalidDataException("Battle magic receipt disagrees with its canonical saved banks or histories.");
        }
        NativeCharacterVital[] records = vitals.Records.Select((record, row) => record with { MagicInventory = receipt.BanksAfter[row].ToArray() }).ToArray();
        return vitals with { Records = records, MagicSystem = receipt.After.DeepCopy() with { Reservations = [] } };
    }
}
