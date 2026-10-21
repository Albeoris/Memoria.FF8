namespace Memoria.FF8.Runtime;

public sealed record BattleCharacterRewardPresentation(byte Row, uint PreviousExperience, uint FinalExperience, byte PreviousLevel, byte FinalLevel, uint PreviousExperienceToNextLevel, uint ExperienceToNextLevel);
public sealed record BattleGuardianForceRewardPresentation(int Id, byte PreviousLevel, byte FinalLevel, int[] LearnedAbilityIds);
public sealed record BattleRewardSummary(IReadOnlyDictionary<byte, ushort> CharacterExperience, IReadOnlyDictionary<byte, ushort> GuardianForceExperience, IReadOnlyDictionary<int, ushort> GuardianForceAbilityPoints, BattleStock[] Items, byte[] Cards)
{
    public byte? AbilityPointsNotice { get; init; }
    public BattleRewardOffer[] LootOffers { get; init; } = [];
    public BattleCharacterRewardPresentation[] CharacterGrowth { get; init; } = [];
    public BattleGuardianForceRewardPresentation[] GuardianForceGrowth { get; init; } = [];
}
public sealed record BattleResultOwners(NativeCharacterVitals CharacterVitals, NativeEconomy Economy, NativeCardInventory? Cards, BattleRewardSummary Rewards);

/// <summary>Applies observed persistent battle effects and current reward rules without constructing or dispatching combat.</summary>
public static partial class BattleResultProjection
{
    public static BattleResultOwners Apply(BattleResult result, NativeCharacterVitals sourceVitals, NativeEconomy sourceEconomy, IReadOnlyDictionary<int, int> currentEnemyAbilityPoints, IReadOnlyDictionary<int, BattleEnemyRewardDefinition> currentEnemyRewards, NativeBattleExperienceDefinition experienceDefinition, NativeCardInventory? sourceCards, bool noExperience)
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
        NativeCharacterVitals vitals = ApplyMagicSystemReceipt(result, sourceVitals.DeepCopy());
        NativeEconomy economy = sourceEconomy.DeepCopy();
        NativeCharacterVital[] records = vitals.Records.ToArray();
        foreach (BattlePartyResult member in result.Party)
        {
            NativeCharacterVital record = records[member.Row];
            if (record.CharacterId != member.CharacterId || record.HitPoints.Minimum != member.HitPointsBefore || record.HitPoints.Maximum != member.HitPointsBefore || record.Status != member.StatusBefore)
            {
                throw new InvalidDataException("Battle result HP/status effect operands disagree with their current canonical owner.");
            }
            NativeMagicStock[] stocks = record.MagicInventory.ToArray();
            foreach (BattleMagicChange change in result.MagicSystemReceipt is null ? member.MagicChanges : Array.Empty<BattleMagicChange>())
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
                int after = before + change.Change;
                stocks[slot] = new NativeMagicStock(after == 0 ? (byte)0 : change.SpellId, checked((byte)after));
            }
            if (result.MagicSystemReceipt is null && member.MagicInventoryAfter is { } finalInventory)
            {
                BattleCombatant.ValidateMagicInventory(finalInventory);
                for (int spell = 1; spell < 64; spell++)
                {
                    if ((stocks.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0) != (finalInventory.FirstOrDefault(stock => stock.SpellId == spell)?.Count ?? 0))
                    {
                        throw new InvalidDataException("Battle physical magic inventory contradicts its observed stock changes.");
                    }
                }
                stocks = finalInventory.ToArray();
            }
            //486CD0 retains physical inventory positions and clears any of the twenty junctions whose spell is exhausted.
            byte[] junctions = record.Junction.MagicJunctions.ToArray();
            for (int slot = 0; slot < junctions.Length; slot++)
            {
                bool present = result.MagicSystemReceipt is null ? stocks.Any(stock => stock.SpellId == junctions[slot] && stock.Count > 0) : vitals.GetMagicCount(member.Row, junctions[slot]) > 0;
                if (!present && (slot != 19 || !vitals.MagicSystem.RequiresMemoriaFormat))
                {
                    junctions[slot] = 0;
                }
            }
            records[member.Row] = record with { HitPoints = new NativeHpWord(member.HitPointsAfter, member.HitPointsAfter), Status = member.StatusAfter, MagicInventory = stocks, Junction = record.Junction with { MagicJunctions = junctions } };
        }
        foreach (BattleEnemyDefeat defeat in result.EnemyDefeats.Where(defeat => defeat.Credit == BattleDefeatCredit.Character))
        {
            byte row = defeat.KillerRow!.Value;
            records[row] = records[row] with { KillCount = unchecked((ushort)(records[row].KillCount + 1)) };
        }
        NativeItemStock[] items = economy.Items.ToArray();
        foreach (BattleItemConsumption consumption in result.ItemConsumptions)
        {
            int slot = Array.FindIndex(items, stock => stock.ItemId == consumption.ItemId);
            if (slot < 0 || items[slot].Count != consumption.CountBefore)
            {
                throw new InvalidDataException("Battle item consumption requires its observed pre-action stock.");
            }
            byte remaining = checked((byte)(consumption.CountBefore - consumption.Consumed));
            items[slot] = remaining == 0 ? new NativeItemStock(0, 0) : items[slot] with { Count = remaining };
        }
        if (result.NativeItemInventoryAfter is { } finalItems)
        {
            BattleSession.ValidateNativeItemInventory(finalItems);
            foreach (NativeItemStock before in economy.Items.Where(stock => stock.ItemId != 0))
            {
                int after = finalItems.FirstOrDefault(stock => stock.ItemId == before.ItemId)?.Count ?? 0;
                int consumed = result.ItemConsumptions.FirstOrDefault(consumption => consumption.ItemId == before.ItemId)?.Consumed ?? 0;
                if (after < before.Count ? before.Count - after != consumed : consumed != 0)
                {
                    throw new InvalidDataException("Battle physical item inventory contradicts its observed net consumptions.");
                }
            }
            items = finalItems.ToArray();
        }
        foreach (BattleGuardianForceCompatibilityChange change in result.GuardianForceCompatibilityChanges)
        {
            if (records[change.Row].GuardianForceCompatibility is not { } before || !before.SequenceEqual(change.Before))
            {
                throw new InvalidDataException("Battle GF compatibility change disagrees with its canonical saved owner.");
            }
            records[change.Row] = records[change.Row] with { GuardianForceCompatibility = change.After.ToArray() };
        }
        vitals = vitals with { Records = ApplyNativeGrowthChanges(result, ApplyNativeDeathCounts(result, records)) };
        foreach (byte id in result.NativeScriptEffects.AcquiredGuardianForces)
        {
            vitals = vitals.AcquireGuardianForce(id);
        }
        NativeGuardianForceVital[] guardianForces = vitals.GuardianForces.ToArray();
        foreach (BattleGuardianForceChange change in result.GuardianForceChanges)
        {
            NativeGuardianForceVital gf = guardianForces[change.Id];
            ushort maximum = vitals.Definitions.GuardianForceMaximum(change.Id, gf);
            if (gf.HitPoints != change.HitPointsBefore || change.HitPointsAfter > Math.Max(gf.HitPoints, maximum) || (gf.Flags & 1) == 0)
            {
                throw new InvalidDataException("Battle GF HP change disagrees with its canonical saved owner.");
            }
            guardianForces[change.Id] = gf with { HitPoints = change.HitPointsAfter };
        }
        ApplyNativeGuardianForceCounters(result, guardianForces);
        vitals = vitals with { GuardianForces = guardianForces };
        NativeCharacterVitals rewardSourceVitals = vitals;
        BattleExperienceAwards experience = new(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>());
        BattleLootOwners loot = new(economy with { Items = items }, ApplyNativeCardChange(result, sourceCards), [], []);
        if (result.Outcome == BattleOutcome.Victory && result.RewardPolicy == BattleRewardPolicy.NativeCombat)
        {
            experience = NativeBattleRewardDistribution.Experience(result.EnemyDefeats, vitals, currentEnemyRewards, noExperience);
            vitals = vitals.AwardBattleExperience(experience.Characters, experience.GuardianForces, experienceDefinition);
            loot = NativeBattleRewardDistribution.Loot(result.EnemyDefeats, currentEnemyRewards, loot.Economy, loot.Cards, result.NativeRewardItems);
            List<byte> grantedCards = [.. loot.CardsGranted];
            foreach (byte id in result.NativeScriptEffects.Cards)
            {
                NativeCardInventory cards = loot.Cards ?? throw new InvalidDataException("Native scripted card acquisition requires its saved inventory owner.");
                NativeCardInventory next = cards.Acquire(id, out int accepted);
                if (accepted == 0)
                {
                    grantedCards.Add(id);
                }
                loot = loot with { Cards = next, Offers = [.. loot.Offers, new BattleRewardOffer(true, id, 1, accepted == 0)] };
            }
            loot = loot with { CardsGranted = grantedCards.ToArray() };
        }
        int[] eligible = result.Outcome == BattleOutcome.Victory && !result.DebugVictory ? EligibleGuardianForces(vitals) : [];
        if (!eligible.Order().SequenceEqual(result.RewardEligibleGuardianForceIds.Order()))
        {
            throw new InvalidDataException("Observed GF reward eligibility disagrees with the current canonical junction/HP owners.");
        }
        Dictionary<int, ushort> abilityPoints = [];
        byte abilityPointsNotice = 0;
        if (eligible.Length > 0)
        {
            ushort points = 0;
            foreach (int slot in result.RewardPolicy == BattleRewardPolicy.NativeCombat ? result.EnemyDefeats.Select(defeat => defeat.Slot) : result.DefeatedEnemySlots)
            {
                if (!currentEnemyAbilityPoints.TryGetValue(slot, out int amount) || amount is < 0 or > 255)
                {
                    throw new InvalidDataException("Battle rewards require each defeated enemy's current original AP record.");
                }
                points = unchecked((ushort)(points + amount));
            }
            foreach (int id in eligible)
            {
                if (vitals.GuardianForceAbilities(id).Any(ability => !ability.Learned && ability.Ability == vitals.GuardianForces[id].Learning))
                {
                    abilityPointsNotice = unchecked((byte)points);
                }
                vitals = vitals.AwardGuardianForceAbilityPoints(id, points);
                abilityPoints[id] = points;
            }
        }
        if (vitals.MagicSystem.RequiresMemoriaFormat)
        {
            vitals = MagicJunctionPolicy.Reconcile(vitals, out _);
        }
        if (result.Outcome == BattleOutcome.Victory && !result.DebugVictory)
        {
            vitals = MagicCastingService.RecoverVictory(vitals, result.Encounter.OccurrenceId);
        }
        return new BattleResultOwners(vitals.RebuildParty(), loot.Economy, loot.Cards, new BattleRewardSummary(experience.Characters, experience.GuardianForces, abilityPoints, loot.ItemsGranted, loot.CardsGranted) { AbilityPointsNotice = abilityPointsNotice, LootOffers = NativeBattleResultPresentation.GroupLootOffers(loot.Offers), CharacterGrowth = NativeBattleResultPresentation.BuildCharacters(rewardSourceVitals, vitals), GuardianForceGrowth = NativeBattleResultPresentation.BuildGuardianForces(rewardSourceVitals, vitals) });
    }

    public static int[] EligibleGuardianForces(NativeCharacterVitals vitals)
    {
        // Native494D40 includes an active actor when KO is clear OR Petrify is clear. Each living junctioned GF gets full AP.
        return vitals.CombatParty.Where(row => row != 255 && ((vitals.Records[row].Status & 1) == 0 || (vitals.Records[row].Status & 4) == 0)).SelectMany(row => vitals.Records[row].Junction.GuardianForceAssignments).Distinct().Where(id => vitals.GuardianForces[id].HitPoints > 0).Order().ToArray();
    }
}
