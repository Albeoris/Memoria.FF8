using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed record ChocoboImportItem(byte ItemId, int Class, int Count, bool AlreadyAcquired);

public sealed record ChocoboImportPreview(GameState Source, NativeChocoboWorldImport Import, NativeChocoboWorldTransferTable Table, GameState? Candidate, ImmutableArray<ChocoboImportItem> Items, string Message, bool RequiresAssociationReview = false)
{
    public bool CanImport => Candidate is not null;
}

public sealed partial class GameSession
{
    public ChocoboImportPreview PreviewChocoboWorldImport(NativeChocoboWorldImport import, NativeChocoboWorldTransferTable table, bool reviewedAssociation = false)
    {
        return ChocoboWorldTransfers.Preview(State, import, table, reviewedAssociation);
    }

    public string CommitChocoboWorldImport(ChocoboImportPreview preview, string currentContentSha256)
    {
        if (currentContentSha256 != preview.Import.ContentSha256 || !ChocoboWorldTransfers.SameOwners(State, preview.Source))
        {
            throw new InvalidOperationException("The source or game state changed. Refresh the import preview.");
        }
        ChocoboImportPreview verified = ChocoboWorldTransfers.Preview(State, preview.Import, preview.Table, preview.RequiresAssociationReview);
        if (verified.Candidate is null || preview.Candidate is null || !ChocoboWorldTransfers.SameOwners(verified.Candidate, preview.Candidate))
        {
            throw new InvalidOperationException(verified.Message);
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
        ChocoboImportPreview Reject(string message, bool review = false) => new(source, import, table, null, [], message, review);
        table.Validate();
        if (import.RawState.IsDefault || import.RawState.Length != 64 || import.ClassCounts.IsDefault || import.ClassCounts.Length != 4 || import.ClassCounts.Any(count => count is < 0 or > 99))
        {
            throw new InvalidDataException("The decoded Chocobo World state is incomplete.");
        }
        if (source.GameplayPreferences.ChocoboWorldMode != ChocoboWorldMode.NativeImport)
        {
            return Reject("Select Native Import mode before transferring rewards.");
        }
        if (source.Battle is not null || source.PendingBattleEncounter is not null)
        {
            return Reject("Import is unavailable until the active or pending battle ends.");
        }
        if (source.PocketStation is not { } pocket || (pocket.Flags & 1) == 0)
        {
            return Reject("Unlock Boko in a Chocobo Forest before transferring rewards.");
        }
        if (source.Economy is null || source.MenuRandom is null)
        {
            return Reject("The saved inventory and menu random state are required for a transfer.");
        }
        uint world = pocket.WorldId;
        bool foreign = world != 0 && import.WorldId != 0 && world != import.WorldId;
        if (foreign && !reviewedAssociation)
        {
            return Reject("This source belongs to another Chocobo World. Review the association before importing it.", true);
        }
        GameState state = ChocoboRewardTransactions.Reconcile(source);
        string sourceId = import.SourceId;
        state.ChocoboRewards.NativeImportSources.TryGetValue(sourceId, out ChocoboImportReceipt? receipt);
        int[] prior = receipt?.ConsumedClassCounters.Select(count => (int)count).ToArray() ?? [0, 0, 0, 0];
        if (receipt is not null && (import.Revision < receipt.Revision || Enumerable.Range(0, 4).Any(index => import.ClassCounts[index] < prior[index])))
        {
            return Reject("These counters precede an imported transfer or were reset. A new transfer cycle cannot be established safely; no repeatable loot was imported.");
        }
        if (receipt is not null && import.Revision == receipt.Revision && !import.ClassCounts.SequenceEqual(prior))
        {
            return Reject("The source counters changed without a new native save revision. Refresh with a newer save.");
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
            return new(source, import, table, null, grouped, "Deferred due to capacity. The complete transfer must fit; inventory, random state and import counters remain unchanged.", foreign);
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
            return new(source, import, table, null, grouped, "No new rewards.", foreign);
        }
        ChocoboImportReceipt nextReceipt = new(sourceId, import.Revision, import.BackupRevision ?? import.Revision, receipt?.Cycle ?? 0, import.ClassCounts.Select(count => (byte)count).ToImmutableArray(), import.ContentSha256);
        ledger = ledger with { NativeImportSources = ledger.NativeImportSources.SetItem(sourceId, nextReceipt) };
        GameState candidate = state with { Economy = economy, PocketStation = merged, MenuRandom = random, ChocoboRewards = ledger };
        return new(source, import, table, candidate, grouped, delivered.Length == 0 ? "Boko progression and transfer checkpoint updated. No new items." : $"Added {delivered.Sum(stock => stock.Count)} item(s). Boko progression and transfer checkpoint updated.", foreign);
    }
}
