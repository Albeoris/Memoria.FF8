namespace Memoria.FF8.Runtime;

public sealed record BattleResultOwners(NativeCharacterVitals CharacterVitals, NativeEconomy Economy);

/// <summary>Applies observed persistent battle effects and current reward rules without constructing or dispatching combat.</summary>
public static class BattleResultProjection
{
    public static BattleResultOwners Apply(ReplayBattleOutcome result, NativeCharacterVitals sourceVitals, NativeEconomy sourceEconomy, IReadOnlyDictionary<int, int> currentEnemyAbilityPoints)
    {
        result.Validate();
        sourceVitals.Validate();
        sourceEconomy.Validate();
        byte[] party = sourceVitals.CombatParty.Where(row => row != 255).ToArray();
        if (!party.SequenceEqual(result.Party.Select(member => member.Row)))
        {
            throw new InvalidDataException("Battle result party does not match the current canonical combat party.");
        }
        NativeCharacterVitals vitals = sourceVitals.DeepCopy();
        NativeEconomy economy = sourceEconomy.DeepCopy();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        foreach (ReplayBattlePartyResult member in result.Party)
        {
            NativeCharacterVital record = records[member.Row];
            if (record.CharacterId != member.CharacterId || record.HitPoints.Minimum != member.HitPointsBefore || record.HitPoints.Maximum != member.HitPointsBefore || record.Status != member.StatusBefore)
            {
                throw new InvalidDataException("Battle result HP/status effect operands disagree with their current canonical owner.");
            }
            NativeMagicStock[] stocks = record.MagicInventory.ToArray();
            foreach (ReplayBattleMagicChange change in member.MagicChanges)
            {
                int slot = Array.FindIndex(stocks, stock => stock.SpellId == change.SpellId);
                int before = slot < 0 ? 0 : stocks[slot].Count;
                if (before != change.CountBefore)
                {
                    throw new InvalidDataException("Battle magic effect requires its observed pre-action stock.");
                }
                if (slot < 0)
                {
                    slot = Array.FindIndex(stocks, stock => stock.SpellId == 0 || stock.Count == 0);
                    if (slot < 0)
                    {
                        throw new InvalidDataException("Battle magic effect exceeds the native thirty-two stock slots.");
                    }
                }
                stocks[slot] = new NativeMagicStock(change.SpellId, checked((byte)(before + change.Change)));
            }
            records[member.Row] = record with { HitPoints = new NativeHpWord(member.HitPointsAfter, member.HitPointsAfter), Status = member.StatusAfter, MagicInventory = stocks };
        }
        NativeItemStock[] items = economy.Items.ToArray();
        foreach (ReplayBattleItemConsumption consumption in result.ItemConsumptions)
        {
            int slot = Array.FindIndex(items, stock => stock.ItemId == consumption.ItemId);
            if (slot < 0 || items[slot].Count != consumption.CountBefore)
            {
                throw new InvalidDataException("Battle item consumption requires its observed pre-action stock.");
            }
            items[slot] = items[slot] with { Count = checked((byte)(consumption.CountBefore - consumption.Consumed)) };
        }
        vitals = vitals with { Records = records };
        int[] eligible = result.Outcome == BattleOutcome.Victory && !result.DebugVictory ? EligibleGuardianForces(vitals) : [];
        if (!eligible.Order().SequenceEqual(result.RewardEligibleGuardianForceIds.Order()))
        {
            throw new InvalidDataException("Observed GF reward eligibility disagrees with the current canonical junction/HP owners.");
        }
        if (eligible.Length > 0)
        {
            ushort points = 0;
            foreach (int slot in result.DefeatedEnemySlots)
            {
                if (!currentEnemyAbilityPoints.TryGetValue(slot, out int amount) || amount is < 0 or > 255)
                {
                    throw new InvalidDataException("Battle rewards require each defeated enemy's current original AP record.");
                }
                points = unchecked((ushort)(points + amount));
            }
            foreach (int id in eligible)
            {
                vitals = vitals.AwardGuardianForceAbilityPoints(id, points);
            }
        }
        return new BattleResultOwners(vitals.RebuildParty(), economy with { Items = items });
    }

    public static int[] EligibleGuardianForces(NativeCharacterVitals vitals)
    {
        // Native494D40 includes an active actor when KO is clear OR Petrify is clear. Each living junctioned GF gets full AP.
        return vitals.CombatParty.Where(row => row != 255 && ((vitals.Records[row].Status & 1) == 0 || (vitals.Records[row].Status & 4) == 0)).SelectMany(row => vitals.Records[row].Junction.GuardianForceAssignments).Distinct().Where(id => vitals.GuardianForces[id].HitPoints > 0).Order().ToArray();
    }
}
