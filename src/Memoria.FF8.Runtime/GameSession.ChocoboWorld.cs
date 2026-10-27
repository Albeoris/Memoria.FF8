using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed record ChocoboImportItem(byte ItemId, int Class, int Count, bool AlreadyAcquired);

public sealed record ChocoboImportPreview(GameState Source, NativeChocoboWorldImport Import, NativeChocoboWorldTransferTable Table, GameState? Candidate, ImmutableArray<ChocoboImportItem> Items, string Message, bool RequiresAssociationReview = false)
{
    public bool CanImport => Candidate is not null;
    [System.Text.Json.Serialization.JsonIgnore]
    public ChocoboPresentationText? MessageText { get; init; }
}

public sealed partial class GameSession
{
    public ChocoboImportPreview PreviewChocoboWorldImport(NativeChocoboWorldImport import, NativeChocoboWorldTransferTable table, bool reviewedAssociation = false)
    {
        if (HasActiveBattle)
        {
            ChocoboPresentationText message = ChocoboText.Create("chocobo.import.battle-active");
            return new(State, import, table, null, [], message.Fallback) { MessageText = message };
        }
        return ChocoboWorldTransfers.Preview(State, import, table, reviewedAssociation);
    }

    public string CommitChocoboWorldImport(ChocoboImportPreview preview, string currentContentSha256)
    {
        if (HasActiveBattle || currentContentSha256 != preview.Import.ContentSha256 || !ChocoboWorldTransfers.SameOwners(State, preview.Source))
        {
            throw ChocoboText.Create("chocobo.import.source-changed").InvalidOperation();
        }
        ChocoboImportPreview verified = ChocoboWorldTransfers.Preview(State, preview.Import, preview.Table, preview.RequiresAssociationReview);
        if (verified.Candidate is null || preview.Candidate is null || !ChocoboWorldTransfers.SameOwners(verified.Candidate, preview.Candidate))
        {
            throw (verified.MessageText ?? ChocoboText.Create("chocobo.literal", verified.Message)).InvalidOperation();
        }
        State = verified.Candidate;
        return verified.Message;
    }
}

public static class ChocoboWorldTransfers
{
    public static bool SameOwners(GameState left, GameState right)
    {
        return JsonElement.DeepEquals(Owners(left), Owners(right));
    }

    private static JsonElement Owners(GameState state) => JsonSerializer.SerializeToElement(new { state.Economy, state.PocketStation, state.MenuRandom, state.ChocoboRewards, state.GameplayPreferences, state.PendingBattleEncounter, Battle = state.Battle is not null });

    public static ChocoboImportPreview Preview(GameState source, NativeChocoboWorldImport import, NativeChocoboWorldTransferTable table, bool reviewedAssociation = false)
    {
        ChocoboImportPreview Reject(ChocoboPresentationText message, bool review = false) => new(source, import, table, null, [], message.Fallback, review) { MessageText = message };
        table.Validate();
        if (import.RawState.IsDefault || import.RawState.Length != 64 || import.ClassCounts.IsDefault || import.ClassCounts.Length != 4 || import.ClassCounts.Any(count => count is < 0 or > 99))
        {
            throw ChocoboText.Create("chocobo.error.state-incomplete").InvalidData();
        }
        if (source.GameplayPreferences.ChocoboWorldMode != ChocoboWorldMode.NativeImport)
        {
            return Reject(ChocoboText.Create("chocobo.import.mode-required"));
        }
        if (source.Battle is not null || source.PendingBattleEncounter is not null)
        {
            return Reject(ChocoboText.Create("chocobo.import.battle-active"));
        }
        if (source.PocketStation is not { } pocket || (pocket.Flags & 1) == 0)
        {
            return Reject(ChocoboText.Create("chocobo.import.boko-locked"));
        }
        if (source.Economy is null || source.MenuRandom is null)
        {
            return Reject(ChocoboText.Create("chocobo.import.state-required"));
        }
        uint world = pocket.WorldId;
        bool foreign = world != 0 && import.WorldId != 0 && world != import.WorldId;
        if (foreign && !reviewedAssociation)
        {
            return Reject(ChocoboText.Create("chocobo.import.association-required"), true);
        }
        GameState state = ChocoboRewardTransactions.Reconcile(source);
        string sourceId = import.SourceId;
        state.ChocoboRewards.NativeImportSources.TryGetValue(sourceId, out ChocoboImportReceipt? receipt);
        int[] prior = receipt?.ConsumedClassCounters.Select(count => (int)count).ToArray() ?? [0, 0, 0, 0];
        if (receipt is not null && (import.Revision < receipt.Revision || Enumerable.Range(0, 4).Any(index => import.ClassCounts[index] < prior[index])))
        {
            return Reject(ChocoboText.Create("chocobo.import.counters-stale"));
        }
        if (receipt is not null && import.Revision == receipt.Revision && !import.ClassCounts.SequenceEqual(prior))
        {
            return Reject(ChocoboText.Create("chocobo.import.revision-stale"));
        }
        NativeMenuRandomState random = source.MenuRandom;
        List<ChocoboImportItem> rows = [];
        List<NativeItemStock> requested = [];
        HashSet<string> acquired = state.ChocoboRewards.AcquiredUniqueRewardIds.ToHashSet(StringComparer.Ordinal);
        void Resolve(byte item, int rewardClass)
        {
            ChocoboRewardDefinition? unique = ChocoboRewardCatalog.FindItem(item);
            bool already = unique is not null && !acquired.Add(unique.Id);
            rows.Add(new(item, rewardClass, 1, already));
            if (!already && item != 0)
            {
                requested.Add(new(item, 1));
            }
        }
        if (import.MiniMogFound && (pocket.Flags & 4) == 0)
        {
            Resolve(65, -1);
        }
        for (int rewardClass = 0; rewardClass < 4; rewardClass++)
        {
            for (int index = prior[rewardClass]; index < import.ClassCounts[rewardClass]; index++)
            {
                random = random.Advance(1);
                byte item = table.Select(rewardClass, (byte)random.Seed);
                if (item == 65 && !import.MiniMogFound && (pocket.Flags & 4) == 0)
                {
                    item = 110;
                }
                Resolve(item, rewardClass);
            }
        }
        ImmutableArray<ChocoboImportItem> grouped = rows.GroupBy(row => (row.ItemId, row.Class, row.AlreadyAcquired)).Select(group => new ChocoboImportItem(group.Key.ItemId, group.Key.Class, group.Count(), group.Key.AlreadyAcquired)).ToImmutableArray();
        if (!ChocoboRewardTransactions.TryGrantItems(state.Economy!, state.ChocoboRewards, requested, out NativeEconomy economy, out ChocoboRewardLedger ledger, out ImmutableArray<NativeItemStock> delivered))
        {
            ChocoboPresentationText message = ChocoboText.Create("chocobo.import.capacity");
            return new(source, import, table, null, grouped, message.Fallback, foreign) { MessageText = message };
        }
        byte[] raw = import.RawState.ToArray();
        raw[0] = (byte)((raw[0] | (pocket.Flags & 5)) & ~2);
        raw[1] = (byte)Math.Max(pocket.Level, import.Level);
        raw[2] = Math.Max((byte)1, raw[2]);
        raw[0x0D] |= 0x80;
        raw[0x2D] = Math.Max(pocket.AttackVariant, raw[0x2D]);
        raw[3] = Math.Max(pocket.Raw[3], raw[3]);
        raw.AsSpan(0x14, 4).Clear();
        if (import.WorldId == 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0x28, 4), world);
        }
        NativePocketStationState merged = pocket with { Raw = ImmutableArray.CreateRange(raw) };
        bool changedProgression = !pocket.Raw.SequenceEqual(merged.Raw);
        bool newCounts = Enumerable.Range(0, 4).Any(index => import.ClassCounts[index] != prior[index]);
        if (receipt is not null && !newCounts && !changedProgression)
        {
            ChocoboPresentationText message = ChocoboText.Create("chocobo.import.no-rewards");
            return new(source, import, table, null, grouped, message.Fallback, foreign) { MessageText = message };
        }
        ChocoboImportReceipt nextReceipt = new(sourceId, import.Revision, import.BackupRevision ?? import.Revision, receipt?.Cycle ?? 0, import.ClassCounts.Select(count => (byte)count).ToImmutableArray(), import.ContentSha256);
        ledger = ledger with { NativeImportSources = ledger.NativeImportSources.SetItem(sourceId, nextReceipt) };
        GameState candidate = state with { Economy = economy, PocketStation = merged, MenuRandom = random, ChocoboRewards = ledger };
        ChocoboPresentationText completed = delivered.Length == 0 ? ChocoboText.Create("chocobo.import.progression") : ChocoboText.Create("chocobo.import.items-added", delivered.Sum(stock => stock.Count));
        return new(source, import, table, candidate, grouped, completed.Fallback, foreign) { MessageText = completed };

    }
}
