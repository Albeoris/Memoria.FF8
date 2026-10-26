using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum ChocoboWorldMode
{
    NativeImport,
    AngeloSearch,
    Memoria
}

public sealed record ChocoboRewardDefinition(string Id, byte ItemId, string DisplayName, string Description, int? TaughtAbilityId = null)
{
    [JsonIgnore]
    public ChocoboPresentationText NameText => ChocoboText.Create("chocobo.reward." + Id + ".name");
    [JsonIgnore]
    public ChocoboPresentationText DescriptionText => ChocoboText.Create("chocobo.reward." + Id + ".description");
}

/// <summary>Names are fallbacks; presentation resolves names, descriptions and icons from installed item metadata by ItemId.</summary>
public static class ChocoboRewardCatalog
{
    public static ImmutableArray<ChocoboRewardDefinition> Entries { get; } =
    [
        new("ribbon", 100, "Ribbon", "Teaches a Guardian Force Ribbon.", 77),
        new("mogs_amulet", 65, "Mog's Amulet", "Teaches a Guardian Force MiniMog.", 38),
        new("friendship", 32, "Friendship", "Calls a Moomba in battle."),
        new("chocobos_tag", 165, "Chocobo's Tag", "Renames Boko.")
    ];

    public static ChocoboRewardDefinition? FindItem(int itemId) => Entries.FirstOrDefault(entry => entry.ItemId == itemId);
}

public sealed record ChocoboImportReceipt(string SourceId, uint Revision, uint BackupRevision, int Cycle, ImmutableArray<byte> ConsumedClassCounters, string BatchId);

public sealed record ChocoboRewardLedger
{
    public int Version { get; init; } = 1;
    public ImmutableHashSet<string> AcquiredUniqueRewardIds { get; init; } = ImmutableHashSet<string>.Empty;
    public ImmutableDictionary<string, ChocoboImportReceipt> NativeImportSources { get; init; } = ImmutableDictionary<string, ChocoboImportReceipt>.Empty;
    [JsonIgnore]
    public bool HasState => AcquiredUniqueRewardIds.Count != 0 || NativeImportSources.Count != 0;
    [JsonIgnore]
    public bool IsEmpty => !HasState;

    public void Validate()
    {
        if (Version != 1 || AcquiredUniqueRewardIds is null || NativeImportSources is null || AcquiredUniqueRewardIds.Any(id => !ChocoboRewardCatalog.Entries.Any(entry => entry.Id == id)))
        {
            throw ChocoboText.Create("chocobo.error.ledger-unsupported").InvalidData();
        }
        foreach ((string key, ChocoboImportReceipt receipt) in NativeImportSources)
        {
            if (receipt is null || key != receipt.SourceId || string.IsNullOrWhiteSpace(key) || receipt.Cycle < 0 || receipt.ConsumedClassCounters.IsDefault || receipt.ConsumedClassCounters.Length != 4 || receipt.ConsumedClassCounters.Any(count => count > 99) || string.IsNullOrWhiteSpace(receipt.BatchId))
            {
                throw ChocoboText.Create("chocobo.error.receipt-invalid").InvalidData();
            }
        }
    }

    public ChocoboRewardLedger ObserveInventory(IEnumerable<NativeItemStock> items)
    {
        Validate();
        ImmutableHashSet<string> acquired = AcquiredUniqueRewardIds;
        foreach (NativeItemStock stock in items)
        {
            if (stock.Count > 0 && ChocoboRewardCatalog.FindItem(stock.ItemId) is { } entry)
            {
                acquired = acquired.Add(entry.Id);
            }
        }
        return ReferenceEquals(acquired, AcquiredUniqueRewardIds) ? this : this with { AcquiredUniqueRewardIds = acquired };
    }
}

public enum ChocoboRewardGrantStatus
{
    Granted,
    AlreadyAcquired,
    InventoryFull,
    WrongMode,
    UnsafeBoundary,
    Unavailable
}
public sealed record ChocoboRewardGrantResult(GameState State, ChocoboRewardGrantStatus Status, ImmutableArray<NativeItemStock> Delivered, string Message)
{
    [JsonIgnore]
    public ChocoboPresentationText? MessageText { get; init; }
}

/// <summary>Publishes inventory and history together only after every requested item fits.</summary>
public static class ChocoboRewardTransactions
{
    public static bool TryGrantItems(NativeEconomy sourceEconomy, ChocoboRewardLedger sourceLedger, IEnumerable<NativeItemStock> requested, out NativeEconomy economy, out ChocoboRewardLedger ledger, out ImmutableArray<NativeItemStock> delivered)
    {
        sourceEconomy.Validate();
        bool accepted = TryGrantInventory(sourceEconomy.Items, sourceLedger, requested, out NativeItemStock[] items, out ledger, out delivered);
        economy = accepted ? sourceEconomy with { Items = items } : sourceEconomy;
        return accepted;
    }

    public static bool TryGrantInventory(NativeItemStock[] sourceItems, ChocoboRewardLedger sourceLedger, IEnumerable<NativeItemStock> requested, out NativeItemStock[] items, out ChocoboRewardLedger ledger, out ImmutableArray<NativeItemStock> delivered)
    {
        sourceLedger.Validate();
        if (sourceItems.Length != NativeEconomy.ItemSlotCount)
        {
            throw ChocoboText.Create("chocobo.error.inventory-slots").InvalidData();
        }
        NativeItemStock[] candidate = sourceItems.ToArray();
        ChocoboRewardLedger candidateLedger = sourceLedger.ObserveInventory(sourceItems);
        ImmutableArray<NativeItemStock>.Builder accepted = ImmutableArray.CreateBuilder<NativeItemStock>();
        foreach (NativeItemStock item in requested)
        {
            if (item.ItemId == 0 || item.Count == 0 || item.Count > 100)
            {
                throw ChocoboText.Create("chocobo.error.grant-quantity").InvalidData();
            }
            ChocoboRewardDefinition? unique = ChocoboRewardCatalog.FindItem(item.ItemId);
            if (unique is not null && candidateLedger.AcquiredUniqueRewardIds.Contains(unique.Id))
            {
                continue;
            }
            int amount = unique is null ? item.Count : 1;
            int count = candidate.FirstOrDefault(stock => stock.ItemId == item.ItemId)?.Count ?? 0;
            if (count + amount > 100 || count == 0 && !candidate.Any(stock => stock.ItemId == item.ItemId || stock.ItemId == 0))
            {
                items = sourceItems;
                ledger = sourceLedger;
                delivered = [];
                return false;
            }
            int slot = Array.FindIndex(candidate, stock => stock.ItemId == item.ItemId);
            if (slot < 0)
            {
                slot = Array.FindIndex(candidate, stock => stock.ItemId == 0);
            }
            candidate[slot] = new(item.ItemId, (byte)(count + amount));
            accepted.Add(new(item.ItemId, (byte)amount));
            if (unique is not null)
            {
                candidateLedger = candidateLedger with { AcquiredUniqueRewardIds = candidateLedger.AcquiredUniqueRewardIds.Add(unique.Id) };
            }
        }
        items = candidate;
        ledger = candidateLedger;
        delivered = accepted.ToImmutable();
        return true;
    }

    public static GameState Reconcile(GameState state)
    {
        if (state.ChocoboRewards is null)
        {
            throw ChocoboText.Create("chocobo.error.ledger-invalid").InvalidData();
        }
        ChocoboRewardLedger ledger = state.ChocoboRewards.ObserveInventory(state.Economy?.Items ?? []);
        if (state.GameplayPreferences.ChocoboWorldMode is null && state.CharacterVitals is { } vitals)
        {
            foreach (ChocoboRewardDefinition reward in ChocoboRewardCatalog.Entries)
            {
                if (!ledger.AcquiredUniqueRewardIds.Contains(reward.Id) && reward.TaughtAbilityId is int ability && vitals.GuardianForces.Any(gf => gf.LearnedBank.Has(ability) || gf.Progression?.Abilities.Any(entry => entry.Ability == ability && entry.ItemTaught) == true))
                {
                    ledger = ledger with { AcquiredUniqueRewardIds = ledger.AcquiredUniqueRewardIds.Add(reward.Id) };
                }
            }
        }
        return ReferenceEquals(ledger, state.ChocoboRewards) ? state : state with { ChocoboRewards = ledger };
    }

    public static GameState MigrateLegacyBattle(GameState source, ChocoboWorldMode mode)
    {
        GameState state = Reconcile(source);
        if (state.Battle is not { Definition.ChocoboWorldMode: null } battle)
        {
            return state;
        }
        ChocoboRewardLedger initial = state.ChocoboRewards with
        {
            AcquiredUniqueRewardIds = state.ChocoboRewards.AcquiredUniqueRewardIds.Union(battle.Definition.ChocoboRewards.AcquiredUniqueRewardIds)
        };
        ChocoboRewardLedger current = battle.ChocoboRewards with
        {
            AcquiredUniqueRewardIds = battle.ChocoboRewards.AcquiredUniqueRewardIds.Union(initial.AcquiredUniqueRewardIds),
            NativeImportSources = battle.ChocoboRewards.NativeImportSources.SetItems(initial.NativeImportSources)
        };
        current = current.ObserveInventory(battle.NativeItemInventory ?? []);
        BattleSnapshot migrated = battle with
        {
            Definition = battle.Definition with { ChocoboWorldMode = mode, ChocoboRewards = initial },
            ChocoboRewards = current
        };
        return state with { ChocoboRewards = initial, Battle = migrated };
    }

    public static ChocoboRewardGrantResult ClaimRemaining(GameState source)
    {
        ChocoboRewardGrantResult Result(GameState resultState, ChocoboRewardGrantStatus status, ImmutableArray<NativeItemStock> delivered, ChocoboPresentationText message) => new(resultState, status, delivered, message.Fallback) { MessageText = message };
        GameState state = Reconcile(source);
        if (state.GameplayPreferences.ChocoboWorldMode != ChocoboWorldMode.Memoria)
        {
            return Result(source, ChocoboRewardGrantStatus.WrongMode, [], ChocoboText.Create("chocobo.claim.mode-required"));
        }
        if (state.Battle is not null || state.PendingBattleEncounter is not null)
        {
            return Result(source, ChocoboRewardGrantStatus.UnsafeBoundary, [], ChocoboText.Create("chocobo.claim.battle-active"));
        }
        NativeEconomy? economy = state.Economy;
        if (economy is null)
        {
            return Result(source, ChocoboRewardGrantStatus.Unavailable, [], ChocoboText.Create("chocobo.claim.inventory-required"));
        }
        ChocoboRewardDefinition[] remaining = ChocoboRewardCatalog.Entries.Where(entry => !state.ChocoboRewards.AcquiredUniqueRewardIds.Contains(entry.Id)).ToArray();
        if (remaining.Length == 0)
        {
            return Result(state, ChocoboRewardGrantStatus.AlreadyAcquired, [], ChocoboText.Create("chocobo.claim.complete"));
        }
        if (!TryGrantItems(economy, state.ChocoboRewards, remaining.Select(reward => new NativeItemStock(reward.ItemId, 1)), out NativeEconomy grantedEconomy, out ChocoboRewardLedger ledger, out ImmutableArray<NativeItemStock> delivered))
        {
            int available = economy.Items.Count(stock => stock.ItemId == 0);
            List<ChocoboPresentationText> blocked = [];
            foreach (ChocoboRewardDefinition reward in remaining)
            {
                NativeItemStock? existing = economy.Items.FirstOrDefault(stock => stock.ItemId == reward.ItemId);
                if (existing is null ? available-- <= 0 : existing.Count >= 100)
                {
                    blocked.Add(reward.NameText);
                }
            }
            return Result(source, ChocoboRewardGrantStatus.InventoryFull, [], ChocoboText.Create("chocobo.claim.capacity", new ChocoboPresentationList(blocked)));
        }
        return Result(state with { Economy = grantedEconomy, ChocoboRewards = ledger }, ChocoboRewardGrantStatus.Granted, delivered, ChocoboText.Create("chocobo.claim.received"));
    }
}
