using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class RecreateGame
{
    private ChocoboImportPreview? chocoboTransferPreview;
    private string? chocoboTransferPath;
    private bool promptNewGameChocoboClaim;

    private void InitializeChocoboWorldIntegration()
    {
        gameMenus!.ConnectChocoboWorld(DiscoverChocoboWorldSaves, BrowseChocoboWorldSave, path => PreviewChocoboWorldFile(path, false), CommitChocoboWorldFile, PreviewChocoboWorldClaim, CommitChocoboWorldClaim, () => PreviewChocoboWorldFile(chocoboTransferPath!, true));
    }

    private IReadOnlyList<ChocoboImportSource> DiscoverChocoboWorldSaves()
    {
        List<ChocoboImportSource> sources = [];
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] folders = [Path.Combine(options.GameDirectory, "save"), Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII Steam"), Path.Combine(documents, "Square Enix", "FINAL FANTASY VIII")];
        foreach (string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                foreach (string file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 2, IgnoreInaccessible = true }))
                {
                    string name = Path.GetFileName(file);
                    if (name.Equals("chocorpg", StringComparison.OrdinalIgnoreCase) || name.Equals("chocorpg.ff8", StringComparison.OrdinalIgnoreCase))
                    {
                        sources.Add(new(file, $"Classic / PC · {name} · {Path.GetDirectoryName(file)}"));
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                // Discovery is best effort; Browse remains available for inaccessible roots.
            }
        }
        return sources.DistinctBy(source => source.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static byte[] ReadChocoboWorldFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > 0xE080)
        {
            throw new InvalidDataException("The selected file is too large for a supported standalone Chocobo World save.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private ChocoboImportReview PreviewChocoboWorldFile(string path, bool reviewed)
    {
        chocoboTransferPath = path;
        chocoboTransferPreview = null;
        try
        {
            NativeChocoboWorldImport imported = NativeChocoboWorldReader.Read(ReadChocoboWorldFile(path));
            using GameAssets assets = new(options.GameDirectory, resources: resourceHost?.Resources);
            ChocoboImportPreview preview = session.PreviewChocoboWorldImport(imported, assets.ReadNativeChocoboWorldTransferTable(), reviewed);
            chocoboTransferPreview = preview;
            EnsureLiveMenuCatalog();
            string[] attacks = ["ChocoFire", "ChocoFlare", "ChocoMeteor", "ChocoBocle"];
            string[] header =
            [
                Path.GetFileName(path) + " · " + imported.Format,
                $"Saved name: {NativePocketStationText.ChocoboName(session.State.PocketStation) ?? "Boko"} · ID {imported.BokoId:D4} · Level {imported.Level} · {attacks[imported.AttackTier]}",
                $"Selected bank {imported.SelectedBank + 1} · Revision {imported.Revision} · Backup {imported.BackupRevision?.ToString() ?? "none"}",
                $"Source world {imported.WorldId:X8} / Current world {session.State.PocketStation?.WorldId ?? 0:X8} · " + (preview.RequiresAssociationReview ? reviewed ? "Association reviewed" : "Association review required" : "Compatible / first association"),
                $"Level {session.State.PocketStation?.Level ?? 0} → {preview.Candidate?.PocketStation?.Level ?? session.State.PocketStation?.Level ?? 0}",
                $"Attack tier {session.State.PocketStation?.AttackVariant ?? 0} → {preview.Candidate?.PocketStation?.AttackVariant ?? session.State.PocketStation?.AttackVariant ?? 0}",
                "Transfers use the installed item table. Source files remain unchanged."
            ];
            ChocoboRewardRow[] rows = preview.Items.OrderByDescending(item => ChocoboRewardCatalog.FindItem(item.ItemId) is not null).Select(item => new ChocoboRewardRow(liveMenuCatalog?.ItemName(item.ItemId) ?? $"Item {item.ItemId}", (ChocoboRewardCatalog.FindItem(item.ItemId) is null ? "Ordinary" : "Exclusive") + $" · {(item.Class < 0 ? "MiniMog" : "Class " + (char)('A' + item.Class))} · {item.Count} · " + (item.AlreadyAcquired ? "Already acquired" : preview.CanImport ? "Incoming" : "Deferred"), liveMenuCatalog?.ItemIcon(item.ItemId), item.AlreadyAcquired, Exclusive: ChocoboRewardCatalog.FindItem(item.ItemId) is not null)).ToArray();
            Dictionary<byte, int> totals = preview.Items.Where(item => !item.AlreadyAcquired).GroupBy(item => item.ItemId).ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
            List<ChocoboRewardRow> detailedRows = [];
            foreach (ChocoboImportItem item in preview.Items.OrderByDescending(item => ChocoboRewardCatalog.FindItem(item.ItemId) is not null))
            {
                int current = session.State.Economy?.Items.FirstOrDefault(stock => stock.ItemId == item.ItemId)?.Count ?? 0;
                bool noSlot = current == 0 && session.State.Economy?.Items.Any(stock => stock.ItemId == item.ItemId || stock.ItemId == 0) != true;
                int overflow = item.AlreadyAcquired ? 0 : noSlot ? totals.GetValueOrDefault(item.ItemId) : Math.Max(0, current + totals.GetValueOrDefault(item.ItemId) - 100);
                ChocoboRewardRow row = rows[detailedRows.Count];
                detailedRows.Add(row with { Details = row.Details + $" · Grant {(preview.CanImport && !item.AlreadyAcquired ? item.Count : 0)} · Overflow {overflow}" });
            }
            session.State.ChocoboRewards.NativeImportSources.TryGetValue(imported.SourceId, out ChocoboImportReceipt? checkpoint);
            header = [.. header, string.Join(" · ", Enumerable.Range(0, 4).Select(index => $"{(char)('A' + index)} {imported.ClassCounts[index]} / previously imported {checkpoint?.ConsumedClassCounters[index] ?? 0}"))];
            string? boundary = ChocoboWorldBoundaryUnavailableReason;
            return new(header, detailedRows, preview.CanImport && boundary is null, boundary ?? preview.Message, preview.RequiresAssociationReview && !reviewed);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return new([Path.GetFileName(path)], [], false, exception.Message);
        }
    }

    private string CommitChocoboWorldFile()
    {
        if (ChocoboWorldBoundaryUnavailableReason is { } reason)
        {
            return reason;
        }
        try
        {
            ChocoboImportPreview preview = chocoboTransferPreview ?? throw new InvalidOperationException("Refresh the import preview first.");
            string hash = Convert.ToHexString(SHA256.HashData(ReadChocoboWorldFile(chocoboTransferPath!)));
            string result = session.CommitChocoboWorldImport(preview, hash);
            chocoboTransferPreview = null;
            gameMenus?.InvalidateLiveData();
            return result;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            return exception.Message;
        }
    }

    private ChocoboClaimReview PreviewChocoboWorldClaim()
    {
        EnsureLiveMenuCatalog();
        ChocoboRewardGrantResult preview = ChocoboRewardTransactions.ClaimRemaining(session.State);
        ChocoboRewardRow[] rows = ChocoboRewardCatalog.Entries.Select(entry => new ChocoboRewardRow(liveMenuCatalog?.ItemName(entry.ItemId) ?? entry.DisplayName, liveMenuCatalog?.ItemDescription(entry.ItemId) ?? entry.Description, liveMenuCatalog?.ItemIcon(entry.ItemId), session.State.ChocoboRewards.AcquiredUniqueRewardIds.Contains(entry.Id))).ToArray();
        string? boundary = ChocoboWorldBoundaryUnavailableReason;
        string? reason = preview.Status == ChocoboRewardGrantStatus.Granted ? null : preview.Message;
        if (preview.Status == ChocoboRewardGrantStatus.InventoryFull)
        {
            string[] remaining = rows.Where(row => !row.Obtained).Select(row => row.Name).ToArray();
            int slots = session.State.Economy?.Items.Count(item => item.ItemId == 0) ?? 0;
            reason = $"Claim blocked: the {remaining.Length} remaining items below need inventory space; {slots} slots are empty. Free space for the complete claim.";
        }
        return new(rows, preview.Status == ChocoboRewardGrantStatus.Granted && boundary is null, boundary ?? reason);
    }

    private string CommitChocoboWorldClaim()
    {
        if (ChocoboWorldBoundaryUnavailableReason is { } reason)
        {
            return reason;
        }
        ChocoboRewardGrantResult result = session.ClaimRemainingChocoboRewards();
        gameMenus?.InvalidateLiveData();
        return result.Message;
    }

    private void ShowPendingNewGameChocoboClaim()
    {
        if (promptNewGameChocoboClaim && session.State.Scene is SceneKind.Field or SceneKind.WorldMap && session.State.Economy is not null && ChocoboWorldBoundaryUnavailableReason is null && gameMenus?.IsOpen != true)
        {
            promptNewGameChocoboClaim = false;
            gameMenus?.ShowChocoboClaim();
        }
    }
}
