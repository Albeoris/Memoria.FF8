using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum BattleRewardPolicy
{
    NativeCombat,
    ResearchAutomaticApOnly,
    DebugNoRewards
}

public enum BattleDefeatCredit
{
    Unattributed,
    Character,
    GuardianForce
}

/// <summary>Observed death and selected reward branches, without action history or random samples.</summary>
public sealed record BattleEnemyDefeat([property: JsonRequired] int Slot, [property: JsonRequired] byte Level, [property: JsonRequired] BattleDefeatCredit Credit, [property: JsonRequired] byte? KillerRow, [property: JsonRequired] byte? GuardianForceId, [property: JsonRequired] bool CardSelected, [property: JsonRequired] sbyte DropSelection)
{
    public byte? NativeCommand { get; init; }
    public string? AllocationId { get; init; }
    public string AllocationKey => AllocationId ?? $"scene-{Slot}";
    public void Validate()
    {
        if (Slot is < 0 or >= 8 || AllocationId is not null && string.IsNullOrWhiteSpace(AllocationId) || Level is < 1 or > 100 || !Enum.IsDefined(Credit) || KillerRow is >= 8 || GuardianForceId is >= 16 || DropSelection is < -1 or > 3 || Credit == BattleDefeatCredit.Unattributed && (KillerRow is not null || GuardianForceId is not null) || Credit == BattleDefeatCredit.Character && (KillerRow is null || GuardianForceId is not null) || Credit == BattleDefeatCredit.GuardianForce && (KillerRow is null || GuardianForceId is null))
        {
            throw new InvalidDataException("Enemy defeat fact lacks its observed level, credit or selected reward branch.");
        }
    }
}

/// <summary>Current original monster information inputs consumed by494AF0,486650 and486750.</summary>
public sealed record BattleEnemyRewardDefinition(ushort KillExperience, ushort SharedExperience, byte MediumLevel, byte HighLevel, byte CardId, bool GuaranteedCard, byte DropRate, ImmutableArray<BattleStock> Drops)
{
    public void Validate()
    {
        if (CardId >= 110 && CardId != 255 || Drops.IsDefault || Drops.Length != 12 || Drops.Any(stock => stock is null || stock.Id is < 0 or > 255 || stock.Count is < 0 or > 255))
        {
            throw new InvalidDataException("Enemy rewards require the original three four-entry drop tables.");
        }
    }

    public int Tier(int level) => level < MediumLevel ? 0 : level < HighLevel ? 1 : 2;

    public BattleStock Drop(int level, int selection)
    {
        Validate();
        ArgumentOutOfRangeException.ThrowIfNegative(selection);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selection, 3);
        return Drops[4 * Tier(level) + selection];
    }

    public bool SelectCard(byte sample) => sample <= (GuaranteedCard ? 255 : 8) && CardId != 255;
    public bool SelectDrop(byte sample) => sample <= DropRate;

    public static sbyte SelectDropSlot(byte sample, bool rareItem)
    {
        // Native486650's rare threshold261 is intentionally unreachable for its unsigned byte sample.
        return rareItem ? sample < 128 ? (sbyte)0 : sample < 242 ? (sbyte)1 : (sbyte)2 : sample < 178 ? (sbyte)0 : sample < 229 ? (sbyte)1 : sample < 244 ? (sbyte)2 : (sbyte)3;
    }
}

public sealed record BattleExperienceAwards(IReadOnlyDictionary<byte, ushort> Characters, IReadOnlyDictionary<byte, ushort> GuardianForces);
public sealed record BattleLootOwners(NativeEconomy Economy, NativeCardInventory? Cards, BattleStock[] ItemsGranted, byte[] CardsGranted);

public static class NativeBattleRewardDistribution
{
    public static bool NoExperience(ushort activeFlags, byte formationFlags)
    {
        return (NativeBattleEntryRules.MergeFlags(activeFlags, formationFlags) & 8) != 0;
    }

    public static void ValidateItemBank(IReadOnlyList<BattleStock> items)
    {
        if (items.Count > 24 || items.Any(item => item is null || item.Id is < 0 or > 255 || item.Count is < 0 or > 255))
        {
            throw new InvalidDataException("Native item reward bank requires at most twenty-four ordered BYTE pairs.");
        }
    }

    public static BattleLootOwners Loot(IReadOnlyList<BattleEnemyDefeat> defeats, IReadOnlyDictionary<int, BattleEnemyRewardDefinition> definitions, NativeEconomy economy, NativeCardInventory? cards, IReadOnlyList<BattleStock>? orderedItems = null)
    {
        NativeEconomy nextEconomy = economy.DeepCopy();
        NativeCardInventory? nextCards = cards?.DeepCopy();
        List<BattleStock> gathered = [];
        IReadOnlyList<BattleStock> bank = orderedItems ?? defeats.Where(defeat => defeat.DropSelection >= 0).Take(24).Select(defeat => definitions[defeat.Slot].Drop(defeat.Level, defeat.DropSelection)).ToArray();
        ValidateItemBank(bank);
        foreach (BattleStock stock in bank)
        {
            // Original4A6680 stops at the first zero item in the native reward bank.
            if (stock.Id == 0)
            {
                break;
            }
            if (stock.Count == 0)
            {
                continue;
            }
            int index = gathered.FindIndex(item => item.Id == stock.Id);
            if (index < 0)
            {
                gathered.Add(stock);
            }
            else
            {
                gathered[index] = stock with { Count = unchecked((byte)(gathered[index].Count + stock.Count)) };
            }
        }
        List<BattleStock> granted = [];
        foreach (BattleStock item in gathered)
        {
            int before = nextEconomy.Items.FirstOrDefault(stock => stock.ItemId == item.Id)?.Count ?? 0;
            nextEconomy = nextEconomy.AddItem(item.Id, item.Count, out _);
            nextEconomy = nextEconomy.UpdateItemOrder(item.Id);
            int after = nextEconomy.Items.FirstOrDefault(stock => stock.ItemId == item.Id)?.Count ?? 0;
            if (after > before)
            {
                granted.Add(item with { Count = after - before });
            }
        }
        List<byte> grantedCards = [];
        foreach (BattleEnemyDefeat defeat in defeats.Where(defeat => defeat.CardSelected).Take(8))
        {
            byte id = definitions[defeat.Slot].CardId;
            if (id == 255)
            {
                break;
            }
            NativeCardInventory owner = nextCards ?? throw new InvalidDataException("Selected battle card rewards require their actual native card inventory owner.");
            nextCards = owner.Acquire(id, out int result);
            if (result == 0)
            {
                grantedCards.Add(id);
            }
        }
        return new BattleLootOwners(nextEconomy, nextCards, granted.ToArray(), grantedCards.ToArray());
    }

    public static BattleExperienceAwards Experience(IReadOnlyList<BattleEnemyDefeat> defeats, NativeCharacterVitals vitals, IReadOnlyDictionary<int, BattleEnemyRewardDefinition> definitions, bool noExperience)
    {
        vitals.Validate();
        byte[] party = vitals.CombatParty.Where(row => row != 255).ToArray();
        if (party.Length == 0)
        {
            throw new InvalidDataException("Battle experience requires its actual nonempty combat party.");
        }
        int averageLevel = party.Sum(row => NativeStatDefinitions.Level(vitals.Records[row].Experience, vitals.Definitions.Characters[vitals.Records[row].CharacterId])) / party.Length;
        Dictionary<byte, ushort> personal = [];
        Dictionary<byte, ushort> guardian = [];
        ushort shared = 0;
        foreach (BattleEnemyDefeat defeat in defeats)
        {
            defeat.Validate();
            BattleEnemyRewardDefinition definition = definitions[defeat.Slot];
            //494AF0's Card29 and Devour7 branches grant AP without accruing either EXP bank.
            if (defeat.NativeCommand is 7 or 29)
            {
                continue;
            }
            shared = Accumulate(shared, ScaledExperience(definition.SharedExperience, defeat.Level, averageLevel));
            if (defeat.KillerRow is byte killerRow && !party.Contains(killerRow))
            {
                throw new InvalidDataException("Enemy defeat credit belongs to a character outside the actual battle party.");
            }
            //494AF0 retains shared EXP but skips personal EXP for internal0, automatic GF245 and scripted death246.
            if (defeat.Credit == BattleDefeatCredit.Character && defeat.NativeCommand is not (0 or 245 or 246))
            {
                byte row = defeat.KillerRow!.Value;
                int level = NativeStatDefinitions.Level(vitals.Records[row].Experience, vitals.Definitions.Characters[vitals.Records[row].CharacterId]);
                personal[row] = Accumulate(personal.GetValueOrDefault(row), ScaledExperience(definition.KillExperience, defeat.Level, level));
            }
            else if (defeat.Credit == BattleDefeatCredit.GuardianForce)
            {
                byte id = defeat.GuardianForceId!.Value;
                if (!vitals.Records[defeat.KillerRow!.Value].Junction.GuardianForceAssignments.Contains(id))
                {
                    throw new InvalidDataException("GF defeat credit requires its actual junctioned summoner.");
                }
                int level = NativeStatDefinitions.Level(vitals.GuardianForces[id].Experience, vitals.Definitions.GuardianForces[id]);
                guardian[id] = Accumulate(guardian.GetValueOrDefault(id), ScaledExperience(definition.KillExperience, defeat.Level, level));
            }
        }
        Dictionary<byte, ushort> characters = [];
        foreach (byte row in party)
        {
            NativeCharacterVital character = vitals.Records[row];
            if ((character.Status & 5) == 0)
            {
                characters[row] = unchecked((ushort)(shared + personal.GetValueOrDefault(row)));
            }
            if ((character.Status & 1) != 0 && (character.Status & 4) != 0)
            {
                continue;
            }
            int[] assigned = character.Junction.GuardianForceAssignments.ToArray();
            foreach (int id in assigned)
            {
                byte guardianId = checked((byte)id);
                if (vitals.GuardianForces[id].HitPoints == 0)
                {
                    // The result distributor clears a defeated GF's finishing-blow bank as well as withholding its shared EXP.
                    guardian.Remove(guardianId);
                    continue;
                }
                guardian[guardianId] = unchecked((ushort)(guardian.GetValueOrDefault(guardianId) + shared / assigned.Length));
            }
        }
        return noExperience ? new BattleExperienceAwards(new Dictionary<byte, ushort>(), new Dictionary<byte, ushort>()) : new BattleExperienceAwards(characters, guardian);
    }

    private static ushort ScaledExperience(ushort basis, int enemyLevel, int recipientLevel)
    {
        if (basis == 0)
        {
            return 0;
        }
        int value = 5 * basis * enemyLevel / recipientLevel - basis;
        return value < 1 ? (ushort)1 : Math.Min(unchecked((ushort)value), (ushort)60000);
    }

    private static ushort Accumulate(ushort accumulated, ushort amount) => Math.Min(unchecked((ushort)(accumulated + amount)), (ushort)60000);
}
