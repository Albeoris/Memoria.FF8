namespace Memoria.FF8.Mods;

public sealed partial class ModPackageInstaller
{
    private sealed record Removal(int Schema, Guid TransactionId, Receipt Receipt, string SettingsHash, string Phase);

    /// <summary>Queues removal of a verified managed payload. Resource paths stay intact until startup.</summary>
    public void RequestUninstall(RemoteModEntry entry)
    {
        gate.Wait();
        try
        {
            using FileStream installLock = AcquireLock();
            string key = ManagedName(entry);
            if (File.Exists(JournalPath(key)) || File.Exists(RemovalPath(key)))
            {
                throw new InvalidOperationException("This mod already has a pending change. Restart before uninstalling it.");
            }
            Receipt receipt = Read<Receipt>(ReceiptPath(key)) ?? throw new InvalidOperationException("Only installations owned by the mod manager can be uninstalled.");
            Removal removal = new(1, Guid.NewGuid(), receipt, SettingsHash(Configuration(Destination(receipt.SourceId, receipt.DestinationName), receipt.Folder)), "Pending");
            ValidateRemoval(removal, RemovalPath(key));
            if (receipt.SourceId != entry.SourceId || receipt.ModId != entry.Id)
            {
                throw new InvalidDataException("Installation ownership differs from the selected mod.");
            }
            CheckOwnership(entry, Destination(receipt.SourceId, receipt.DestinationName), receipt);
            WriteAtomic(RemovalPath(key), removal);
        }
        finally
        {
            gate.Release();
        }
    }

    private string RemovalPath(string key) => StatePath("removals", key + ".json");

    private void ValidateRemoval(Removal removal, string path)
    {
        Receipt receipt = removal.Receipt;
        if (receipt is null || string.IsNullOrWhiteSpace(receipt.SourceId))
        {
            throw new InvalidDataException("Removal ownership is missing.");
        }
        string key = ManagedName(new(receipt.SourceId, receipt.ModId, "", receipt.Version));
        bool native = receipt.SourceId == OfficialModCatalog.SourceId;
        bool destination = native ? ValidNativeReceipt(receipt) : receipt.Target == targetProfile && receipt.DestinationName == key + (receipt.Folder ? "" : ".iroj");
        if (removal.Schema != 1 || receipt.Schema != 1 || receipt.ModId == Guid.Empty || removal.TransactionId == Guid.Empty || receipt.Migrated || receipt.Version.Length == 0 || !destination || !Path.GetFullPath(path).Equals(RemovalPath(key), StringComparison.OrdinalIgnoreCase) || removal.Phase is not ("Pending" or "Removed"))
        {
            throw new InvalidDataException("This removal does not identify an owned installation. Manual and migrated originals are protected.");
        }
    }

    private IReadOnlyList<ModPackageInstallResult> RecoverRemovals()
    {
        string directory = StatePath("removals", "");
        if (!Directory.Exists(directory))
        {
            return Array.Empty<ModPackageInstallResult>();
        }
        gate.Wait();
        try
        {
            using FileStream installLock = AcquireLock();
            EnsureNoLinks(directory);
            List<ModPackageInstallResult> results = [];
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                Removal? removal = null;
                try
                {
                    removal = Read<Removal>(path) ?? throw new InvalidDataException("Empty removal journal.");
                    ValidateRemoval(removal, path);
                    Receipt receipt = removal.Receipt;
                    string key = ManagedName(new(receipt.SourceId, receipt.ModId, "", receipt.Version));
                    string destination = Destination(receipt.SourceId, receipt.DestinationName);
                    string discarded = StatePath("removed", removal.TransactionId.ToString("N"));
                    EnsureNoLinks(destination);
                    EnsureNoLinks(discarded);
                    string settings = Configuration(destination, receipt.Folder);
                    if (removal.Phase == "Pending")
                    {
                        if (Exists(destination))
                        {
                            if (Exists(discarded))
                            {
                                throw new InvalidDataException("The removal destination is occupied. Recovery data has been retained.");
                            }
                            CheckOwnership(new(receipt.SourceId, receipt.ModId, "", receipt.Version), destination, receipt);
                            if (SettingsHash(settings) != removal.SettingsHash)
                            {
                                throw new InvalidDataException("Mod settings changed after uninstall was requested. No files were removed.");
                            }
                            Directory.CreateDirectory(Path.GetDirectoryName(discarded)!);
                            Move(destination, discarded);
                        }
                        if (!Exists(discarded) || Fingerprint(discarded, false) != receipt.PayloadHash || SettingsHash(receipt.Folder ? Configuration(discarded, true) : settings) != removal.SettingsHash)
                        {
                            throw new InvalidDataException("The removal payload or settings changed. Recovery data has been retained.");
                        }
                        WriteAtomic(path, removal with { Phase = "Removed" });
                    }
                    if (Exists(destination))
                    {
                        throw new InvalidDataException("A different installation appeared during removal. Its files were preserved.");
                    }
                    if (!receipt.Folder && File.Exists(settings))
                    {
                        if (SettingsHash(settings) != removal.SettingsHash)
                        {
                            throw new InvalidDataException("Mod settings changed during removal. They were preserved.");
                        }
                        File.Delete(settings);
                    }
                    File.Delete(ReceiptPath(key));
                    if (Directory.Exists(discarded))
                    {
                        // Enumerate every descendant first to reject directory links before recursive removal.
                        _ = SafeFiles(discarded).ToArray();
                        Directory.Delete(discarded, true);
                    }
                    else
                    {
                        File.Delete(discarded);
                    }
                    File.Delete(path);
                    results.Add(Result(receipt, false, false));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or System.Text.Json.JsonException)
                {
                    results.Add(removal?.Receipt is { } receipt ? Result(receipt, true, false, "Removal needs attention: " + exception.Message) with { PendingRemoval = true } : new(Guid.Empty, "", "", false, false, "Removal needs attention: " + exception.Message));
                }
            }
            return results.AsReadOnly();
        }
        finally
        {
            gate.Release();
        }
    }
}
