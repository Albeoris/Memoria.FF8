using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8;

internal static class ChocoboWorldPresentation
{
    internal static ChocoboPresentationText Literal(object? value) => ChocoboText.Create("chocobo.literal", value);

    private static ChocoboPresentationText ItemText(int itemId, Func<int, string?> resolve, ChocoboPresentationText fallback)
    {
        return Literal((Func<ChocoboPresentationText>)(() => resolve(itemId) is { } text ? Literal(text) : fallback));
    }

    internal static ChocoboImportReview Import(string path, GameState state, NativeChocoboWorldImport imported, ChocoboImportPreview preview, bool reviewed, ChocoboPresentationText? boundary, Func<int, string?> itemName, Func<int, UiImageSource?> itemIcon)
    {
        ChocoboPresentationText association = ChocoboText.Create(preview.RequiresAssociationReview ? reviewed ? "chocobo.association.reviewed" : "chocobo.association.required" : "chocobo.association.compatible");
        ChocoboPresentationText[] header =
        [
            ChocoboText.Create("chocobo.header.container", Path.GetFileName(path), imported.FormatText ?? Literal(imported.Format)),
            ChocoboText.Create("chocobo.header.name", NativePocketStationText.ChocoboName(state.PocketStation) is { } savedName ? Literal(savedName) : ChocoboText.Create("chocobo.name.default"), imported.BokoId, imported.Level, ChocoboText.Create("chocobo.attack." + imported.AttackTier)),
            ChocoboText.Create("chocobo.header.bank", imported.SelectedBank + 1, imported.Revision, imported.BackupRevision is uint backup ? Literal(backup) : ChocoboText.Create("chocobo.backup.none")),
            ChocoboText.Create("chocobo.header.world", imported.WorldId, state.PocketStation?.WorldId ?? 0, association),
            ChocoboText.Create("chocobo.header.level", state.PocketStation?.Level ?? 0, preview.Candidate?.PocketStation?.Level ?? state.PocketStation?.Level ?? 0),
            ChocoboText.Create("chocobo.header.attack", state.PocketStation?.AttackVariant ?? 0, preview.Candidate?.PocketStation?.AttackVariant ?? state.PocketStation?.AttackVariant ?? 0),
            ChocoboText.Create("chocobo.header.transfer-rule")
        ];
        Dictionary<byte, int> totals = preview.Items.Where(item => !item.AlreadyAcquired).GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
        List<ChocoboRewardRow> rows = [];
        foreach (ChocoboImportItem item in preview.Items.OrderByDescending(item => ChocoboRewardCatalog.FindItem(item.ItemId) is not null))
        {
            bool exclusive = ChocoboRewardCatalog.FindItem(item.ItemId) is not null;
            ChocoboPresentationText name = ItemText(item.ItemId, itemName, ChocoboText.Create("chocobo.item.fallback", item.ItemId));
            ChocoboPresentationText summary = ChocoboText.Create("chocobo.item.summary", ChocoboText.Create(exclusive ? "chocobo.item.exclusive" : "chocobo.item.ordinary"), item.Class < 0 ? ChocoboText.Create("chocobo.item.minimog") : ChocoboText.Create("chocobo.item.class", (char)('A' + item.Class)), item.Count, ChocoboText.Create(item.AlreadyAcquired ? "chocobo.item.acquired" : preview.CanImport ? "chocobo.item.incoming" : "chocobo.item.deferred"));
            int current = state.Economy?.Items.FirstOrDefault(stock => stock.ItemId == item.ItemId)?.Count ?? 0;
            bool noSlot = current == 0 && state.Economy?.Items.Any(stock => stock.ItemId == item.ItemId || stock.ItemId == 0) != true;
            int overflow = item.AlreadyAcquired ? 0 : noSlot ? totals.GetValueOrDefault(item.ItemId) : Math.Max(0, current + totals.GetValueOrDefault(item.ItemId) - 100);
            ChocoboPresentationText details = ChocoboText.Create("chocobo.item.capacity", summary, preview.CanImport && !item.AlreadyAcquired ? item.Count : 0, overflow);
            rows.Add(new(name.Fallback, details.Fallback, itemIcon(item.ItemId), item.AlreadyAcquired, exclusive) { NameText = name, DetailsText = details });
        }
        state.ChocoboRewards.NativeImportSources.TryGetValue(imported.SourceId, out ChocoboImportReceipt? checkpoint);
        ChocoboPresentationText[] counters = Enumerable.Range(0, 4).Select(index => ChocoboText.Create("chocobo.header.counter", (char)('A' + index), imported.ClassCounts[index], checkpoint?.ConsumedClassCounters[index] ?? 0)).ToArray();
        header = [.. header, ChocoboText.Create("chocobo.header.counters", counters.Cast<object?>().ToArray())];
        ChocoboPresentationText reason = boundary ?? preview.MessageText ?? Literal(preview.Message);
        return new(header.Select(text => text.Fallback).ToArray(), rows, preview.CanImport && boundary is null, reason.Fallback, preview.RequiresAssociationReview && !reviewed) { HeaderText = header, ReasonText = reason };
    }

    internal static ChocoboClaimReview Claim(GameState state, ChocoboRewardGrantResult preview, ChocoboPresentationText? boundary, Func<int, string?> itemName, Func<int, string?> itemDescription, Func<int, UiImageSource?> itemIcon)
    {
        ChocoboRewardRow[] rows = ChocoboRewardCatalog.Entries.Select(entry =>
        {
            ChocoboPresentationText name = ItemText(entry.ItemId, itemName, entry.NameText);
            ChocoboPresentationText description = ItemText(entry.ItemId, itemDescription, entry.DescriptionText);
            return new ChocoboRewardRow(name.Fallback, description.Fallback, itemIcon(entry.ItemId), state.ChocoboRewards.AcquiredUniqueRewardIds.Contains(entry.Id)) { NameText = name, DetailsText = description };
        }).ToArray();
        ChocoboPresentationText? reason = preview.Status == ChocoboRewardGrantStatus.Granted ? null : preview.MessageText ?? Literal(preview.Message);
        if (preview.Status == ChocoboRewardGrantStatus.InventoryFull)
        {
            int remaining = rows.Count(row => !row.Obtained);
            int slots = state.Economy?.Items.Count(item => item.ItemId == 0) ?? 0;
            reason = ChocoboText.Create("chocobo.claim.capacity-review", remaining, slots);
        }
        reason = boundary ?? reason;
        return new(rows, preview.Status == ChocoboRewardGrantStatus.Granted && boundary is null, reason?.Fallback) { ReasonText = reason };
    }
}
