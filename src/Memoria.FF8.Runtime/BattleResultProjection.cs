namespace Memoria.FF8.Runtime;

public sealed record BattleRewardSummary(IReadOnlyDictionary<byte, ushort> CharacterExperience, IReadOnlyDictionary<byte, ushort> GuardianForceExperience, IReadOnlyDictionary<int, ushort> GuardianForceAbilityPoints, BattleStock[] Items, byte[] Cards);
public sealed record BattleResultOwners(NativeCharacterVitals CharacterVitals, NativeEconomy Economy, NativeCardInventory? Cards, BattleRewardSummary Rewards);

/// <summary>Applies observed persistent battle effects and current reward rules without constructing or dispatching combat.</summary>
public static class BattleResultProjection
{
    public static BattleResultOwners Apply(ReplayBattleOutcome result, NativeCharacterVitals sourceVitals, NativeEconomy sourceEconomy, IReadOnlyDictionary<int, int> currentEnemyAbilityPoints, IReadOnlyDictionary<int, BattleEnemyRewardDefinition> currentEnemyRewards, NativeBattleExperienceDefinition experienceDefinition, NativeCardInventory? sourceCards, bool noExperience)
    {
        result.Validate();
        sourceVitals.Validate();
        sourceEconomy.Validate();
        byte[] party = sourceVitals.CombatParty.Where(row => row != 255).ToArray();
        if (!party.SequenceEqual(result.Party.Select(member => member.Row)))
        {
            throw new InvalidDataException($"Battle result party does not match the current canonical combat party: recorded [{string.Join(",", result.Party.Select(member => $"{member.Row}:{member.CharacterId}"))}], current [{string.Join(",", party.Select(row => $"{row}:{sourceVitals.Records[row].CharacterId}"))}].");
        }
        if (result.EnemyDefeats.Any(defeat => !currentEnemyRewards.ContainsKey(defeat.Slot) || defeat.KillerRow is byte row && !party.Contains(row)))
        {
            throw new InvalidDataException("Battle defeat reward facts require their current formation and actual party credit owners.");
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
        foreach (ReplayBattleEnemyDefeat defeat in result.EnemyDefeats.Where(defeat => defeat.Credit == BattleDefeatCredit.Character))
        {
            byte row = defeat.KillerRow!.Value;
            records[row] = records[row] with { KillCount = unchecked((ushort)(records[row].KillCount + 1)) };
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
        BattleExperienceAwards experience = new(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>());
        BattleLootOwners loot = new(economy with { Items = items }, sourceCards?.DeepCopy(), [], []);
        if (result.Outcome == BattleOutcome.Victory && result.RewardPolicy == BattleRewardPolicy.NativeCombat)
        {
            experience = NativeBattleRewardDistribution.Experience(result.EnemyDefeats, vitals, currentEnemyRewards, noExperience);
            vitals = vitals.AwardBattleExperience(experience.Characters, experience.GuardianForces, experienceDefinition);
            loot = NativeBattleRewardDistribution.Loot(result.EnemyDefeats, currentEnemyRewards, loot.Economy, loot.Cards);
        }
        int[] eligible = result.Outcome == BattleOutcome.Victory && !result.DebugVictory ? EligibleGuardianForces(vitals) : [];
        if (!eligible.Order().SequenceEqual(result.RewardEligibleGuardianForceIds.Order()))
        {
            throw new InvalidDataException("Observed GF reward eligibility disagrees with the current canonical junction/HP owners.");
        }
        Dictionary<int, ushort> abilityPoints = [];
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
                abilityPoints[id] = points;
            }
        }
        return new BattleResultOwners(vitals.RebuildParty(), loot.Economy, loot.Cards, new BattleRewardSummary(experience.Characters, experience.GuardianForces, abilityPoints, loot.ItemsGranted, loot.CardsGranted));
    }

    public static int[] EligibleGuardianForces(NativeCharacterVitals vitals)
    {
        // Native494D40 includes an active actor when KO is clear OR Petrify is clear. Each living junctioned GF gets full AP.
        return vitals.CombatParty.Where(row => row != 255 && ((vitals.Records[row].Status & 1) == 0 || (vitals.Records[row].Status & 4) == 0)).SelectMany(row => vitals.Records[row].Junction.GuardianForceAssignments).Distinct().Where(id => vitals.GuardianForces[id].HitPoints > 0).Order().ToArray();
    }
}
