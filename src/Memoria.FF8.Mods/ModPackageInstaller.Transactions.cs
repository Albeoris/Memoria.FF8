using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed partial class ModPackageInstaller
{
    private sealed record Receipt(int Schema, Guid InstallationId, string SourceId, Guid ModId, string Version, string Target, string DestinationName, string PayloadHash, bool Folder);
    private sealed record Journal(int Schema, Guid TransactionId, Receipt Next, Receipt? Previous, string StageName, string ExpectedSettingsHash, string Settings, string Phase)
    {
        public Receipt? MissingPrevious { get; init; }
    }

    public Task<ModPackageInstallResult> RequestInstallAsync(ModPackageInstallPlan plan, bool autoEnableNewMods = true, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using FileStream installLock = AcquireLock();
                if (plan.TargetProfile != targetProfile || !prepared.TryGetValue(plan.Token, out Prepared item) || item.Entry != plan.Entry)
                {
                    throw new InvalidOperationException("The install plan expired or belongs to another target; inspect again.");
                }
                if (Fingerprint(item.Payload, false, cancellationToken) != item.Hash)
                {
                    throw new InvalidDataException("The prepared payload changed; inspect again.");
                }
                string name = Path.GetFileName(plan.Destination);
                string receiptPath = ReceiptPath(ManagedName(plan.Entry));
                Receipt? previous = Read<Receipt>(receiptPath);
                string destination = Destination(name);
                if (File.Exists(JournalPath(ManagedName(plan.Entry))))
                {
                    throw new InvalidOperationException("This installation already has a pending transaction. Restart to apply it first.");
                }
                CheckOwnership(plan.Entry, destination, previous, allowMissing: true);
                bool previousPayloadExists = Exists(destination);
                if (previousPayloadExists && previous?.PayloadHash == item.Hash)
                {
                    prepared.Remove(plan.Token);
                    Directory.Delete(item.Root, true);
                    return Result(previous, false, true);
                }
                string settingsPath = Configuration(destination, Directory.Exists(item.Payload));
                string settings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "[Mod]\nenabled=" + (autoEnableNewMods ? "1" : "0") + "\n";
                if (previous is not null)
                {
                    ValidatePackage(item.Payload, plan.Entry, ModConfiguration.ReadJunctionOptions(settingsPath));
                }
                Receipt next = new(1, previous?.InstallationId ?? Guid.NewGuid(), plan.Entry.SourceId, plan.Entry.Id, plan.Version, targetProfile, name, item.Hash, Directory.Exists(item.Payload));
                string stage = StatePath("transactions", plan.Token.ToString("N"));
                Directory.CreateDirectory(Path.GetDirectoryName(stage)!);
                string payloadName = Path.GetRelativePath(item.Root, item.Payload);
                Directory.Move(item.Root, stage);
                prepared[plan.Token] = item with { Root = stage, Payload = Path.Combine(stage, payloadName) };
                Journal journal = new(1, plan.Token, next, previousPayloadExists ? previous : null, payloadName, SettingsHash(settingsPath), settings, "PendingRestart")
                {
                    MissingPrevious = previousPayloadExists ? null : previous
                };
                WriteAtomic(JournalPath(ManagedName(plan.Entry)), journal);
                prepared.Remove(plan.Token);
                return Result(next, true, false);
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
    }

    /// <summary>Call before constructing the runtime or allowing any discovery/resource generation.</summary>
    public IReadOnlyList<ModPackageInstallResult> RecoverAndApply()
    {
        if (!Directory.Exists(StatePath("journals", "")))
        {
            return Array.Empty<ModPackageInstallResult>();
        }
        gate.Wait();
        try
        {
            using FileStream installLock = AcquireLock();
            List<ModPackageInstallResult> results = [];
            string journals = StatePath("journals", "");
            if (!Directory.Exists(journals))
            {
                return results.AsReadOnly();
            }
            foreach (string path in Directory.GetFiles(journals, "*.json"))
            {
                Journal? journal = null;
                try
                {
                    EnsureNoLinks(path);
                    journal = Read<Journal>(path) ?? throw new InvalidDataException("Empty installation journal.");
                    ValidateJournal(journal, path);
                    if (journal.Phase == "RollingBack")
                    {
                        Rollback(journal, path);
                        results.Add(Result(journal.Next, false, false, "Previous installation restored during recovery."));
                        continue;
                    }
                    Apply(journal, path);
                    results.Add(Result(journal.Next, false, true));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or JsonException)
                {
                    bool restored = false;
                    if (journal?.Previous is not null)
                    {
                        try
                        {
                            ValidateJournal(journal, path);
                            string backup = StatePath("transactions", Path.Combine(journal.TransactionId.ToString("N"), "previous"));
                            if (Exists(backup) && Fingerprint(backup, false) == journal.Previous.PayloadHash)
                            {
                                WriteAtomic(path, journal with { Phase = "RollingBack" });
                                Rollback(journal, path);
                                restored = true;
                            }
                        }
                        catch (Exception recoveryException) when (recoveryException is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or JsonException)
                        {
                            // Retain all journal and backup data when recovery cannot safely finish.
                        }
                    }
                    string error = restored ? "The update failed. The previous installation was restored: " + exception.Message : "Recovery needed: " + exception.Message;
                    results.Add(journal?.Next is null ? new(Guid.Empty, "", "", false, false, error) : Result(journal.Next, false, false, error));
                }
            }
            return results.AsReadOnly();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Reads durable installation state. Call on a worker, never while drawing or handling input.</summary>
    public ModPackageInstallResult? GetStatus(RemoteModEntry entry)
    {
        string key = ManagedName(entry);
        Journal? journal = Read<Journal>(JournalPath(key));
        if (journal is not null)
        {
            ValidateJournal(journal, JournalPath(key));
            return Result(journal.Next, true, false);
        }
        Receipt? receipt = Read<Receipt>(ReceiptPath(key));
        if (receipt is null)
        {
            return null;
        }
        if (receipt.Schema != 1 || receipt.SourceId != entry.SourceId || receipt.ModId != entry.Id || receipt.Target != targetProfile || receipt.DestinationName != key + (receipt.Folder ? "" : ".iroj"))
        {
            return Result(receipt, false, false, "The installation receipt does not match this target.");
        }
        string destination = Destination(receipt.DestinationName);
        EnsureNoLinks(destination);
        if (!Exists(destination))
        {
            return Result(receipt, false, false, "The managed installation was removed outside the mod manager.");
        }
        return Result(receipt, false, true);
    }

    private void Apply(Journal journal, string journalPath)
    {
        Receipt next = journal.Next;
        string destination = Destination(next.DestinationName);
        string transaction = StatePath("transactions", journal.TransactionId.ToString("N"));
        string payload = Path.Combine(transaction, journal.StageName);
        string backup = Path.Combine(transaction, "previous");
        string settingsPath = Configuration(destination, next.Folder);
        EnsureNoLinks(transaction);
        EnsureNoLinks(destination);
        if (journal.Phase is "Committed" or "AwaitingValidation")
        {
            VerifyFinal(next, destination);
            if (journal.Phase == "Committed")
            {
                File.Delete(journalPath);
            }
            return;
        }
        RemoteModEntry pinned = new(next.SourceId, next.ModId, "", next.Version);
        if (journal.Phase == "PendingRestart")
        {
            bool oldMoveInterrupted = journal.Previous is not null && !Exists(destination) && Exists(backup) && Fingerprint(backup, false) == journal.Previous.PayloadHash;
            string settingsToCheck = oldMoveInterrupted && next.Folder ? Configuration(backup, true) : settingsPath;
            if (SettingsHash(settingsToCheck) != journal.ExpectedSettingsHash)
            {
                throw new InvalidDataException("Installation settings changed after the plan was accepted; replan before updating.");
            }
            if (journal.Previous is null && Exists(destination))
            {
                throw new InvalidDataException("A payload appeared at the destination after the plan was accepted; no files were replaced.");
            }
            if (!oldMoveInterrupted)
            {
                CheckOwnership(pinned, destination, journal.Previous ?? journal.MissingPrevious, allowMissing: journal.MissingPrevious is not null);
            }
        }
        bool finalIsNew = Exists(destination) && Fingerprint(destination, false) == next.PayloadHash;
        if (!finalIsNew)
        {
            if (!Exists(payload) || Fingerprint(payload, false) != next.PayloadHash)
            {
                throw new InvalidDataException("The staged payload is missing or changed. Recovery data has been retained.");
            }
            RemoteModEntry entry = new(next.SourceId, next.ModId, "", next.Version);
            ValidatePackage(payload, entry);
            if (journal.Previous is null)
            {
                CheckOwnership(entry, destination, journal.MissingPrevious, allowMissing: journal.MissingPrevious is not null);
            }
            if (Exists(destination))
            {
                CheckOwnership(entry, destination, journal.Previous);
                if (SettingsHash(settingsPath) != journal.ExpectedSettingsHash)
                {
                    throw new InvalidDataException("Installation settings changed after the plan was accepted; replan before updating.");
                }
                if (Exists(backup))
                {
                    throw new InvalidDataException("Both the destination and recovery backup exist with different payloads. Recovery data has been retained.");
                }
                Move(destination, backup);
            }
            else if (journal.Previous is not null && (!Exists(backup) || Fingerprint(backup, false) != journal.Previous.PayloadHash))
            {
                throw new InvalidDataException("The previous installation or its verified backup is missing.");
            }
            if (next.Folder)
            {
                File.WriteAllText(Path.Combine(payload, "mod.ini"), journal.Settings);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            WriteAtomic(journalPath, journal with { Phase = "OldMoved" });
            Move(payload, destination);
            WriteAtomic(journalPath, journal with { Phase = "NewMoved" });
        }
        if (!next.Folder)
        {
            string currentSettings = SettingsHash(settingsPath);
            string intendedHash = TextHash(journal.Settings);
            if (currentSettings != journal.ExpectedSettingsHash && currentSettings != intendedHash)
            {
                throw new InvalidDataException("Installation settings changed during recovery. Recovery data has been retained.");
            }
            WriteTextAtomic(settingsPath, journal.Settings);
        }
        VerifyFinal(next, destination);
        WriteAtomic(ReceiptPath(ManagedName(new(next.SourceId, next.ModId, "", next.Version))), next);
        WriteAtomic(journalPath, journal with { Phase = "AwaitingValidation" });
        // Retain transaction backups for explicit recovery; cache eviction must never delete them.
    }

    private void CheckOwnership(RemoteModEntry entry, string destination, Receipt? previous, bool allowMissing = false)
    {
        EnsureNoLinks(modsRoot);
        EnsureNoLinks(destination);
        if (previous is not null)
        {
            if (previous.Schema != 1 || previous.SourceId != entry.SourceId || previous.ModId != entry.Id || previous.Target != targetProfile || previous.DestinationName != Path.GetFileName(destination))
            {
                throw new InvalidDataException("Receipt ownership or target differs from the accepted plan.");
            }
            if (!Exists(destination) ? !allowMissing : Fingerprint(destination, false) != previous.PayloadHash)
            {
                throw new InvalidDataException("This managed installation is missing or contains local changes.");
            }
        }
        else if (Exists(destination) || File.Exists(destination + ".ini"))
        {
            throw new InvalidDataException("The destination is not owned by the downloader. Manual installations cannot be overwritten.");
        }
        string junction = Path.Combine(modsRoot, "Junction");
        if (!Directory.Exists(junction))
        {
            return;
        }
        foreach (string root in Directory.EnumerateFileSystemEntries(junction))
        {
            if (root.Equals(destination, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(root) && !root.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            IEnumerable<string> candidates = Directory.Exists(root) && !Directory.GetFiles(root).Any(path => Path.GetFileName(path).Equals("mod.xml", StringComparison.OrdinalIgnoreCase)) ? Directory.GetFiles(root).Where(path => path.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase)) : [root];
            foreach (string candidate in candidates)
            {
                try
                {
                    if (JunctionModImporter.Open(candidate).Manifest.Id == entry.Id)
                    {
                        throw new InvalidDataException("A separate manual or duplicate installation has this GUID. Resolve it before installing.");
                    }
                }
                catch (NotSupportedException)
                {
                    throw new InvalidDataException("An existing package cannot be safely identified; resolve its import diagnostic before installing.");
                }
            }
        }
    }

    private void ValidateJournal(Journal journal, string path)
    {
        if (journal is null || journal.Next is null || string.IsNullOrWhiteSpace(journal.Next.SourceId) || journal.Next.ModId == Guid.Empty || !DecimalVersion(journal.Next.Version, out _))
        {
            throw new InvalidDataException("Installation journal identity is missing or invalid.");
        }
        RemoteModEntry entry = new(journal.Next.SourceId, journal.Next.ModId, "", journal.Next.Version);
        string name = ManagedName(entry);
        if (journal.Schema != 1 || journal.Next.Schema != 1 || journal.Next.Target != targetProfile || journal.Next.DestinationName != name + (journal.Next.Folder ? "" : ".iroj") || journal.StageName != (journal.Next.Folder ? "content" : "content.iroj") || !Path.GetFullPath(path).Equals(JournalPath(name), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Unsupported journal schema, target or destination mapping.");
        }
        if (journal.MissingPrevious is { } missing && (journal.Previous is not null || missing.Schema != 1 || missing.SourceId != journal.Next.SourceId || missing.ModId != journal.Next.ModId || missing.Target != journal.Next.Target || missing.DestinationName != journal.Next.DestinationName || missing.Folder != journal.Next.Folder || missing.InstallationId != journal.Next.InstallationId))
        {
            throw new InvalidDataException("The missing-installation receipt does not match the journal ownership and target.");
        }
        if (journal.Phase is not ("PendingRestart" or "OldMoved" or "NewMoved" or "Committed" or "AwaitingValidation" or "RollingBack"))
        {
            throw new InvalidDataException("Unknown installation journal phase.");
        }
    }

    private void VerifyFinal(Receipt receipt, string destination)
    {
        if (!Exists(destination) || Fingerprint(destination, false) != receipt.PayloadHash)
        {
            throw new InvalidDataException("The installed payload failed verification. Recovery data has been retained.");
        }
        ValidatePackage(destination, new(receipt.SourceId, receipt.ModId, "", receipt.Version), ModConfiguration.ReadJunctionOptions(Configuration(destination, receipt.Folder)));
    }

    private FileStream AcquireLock()
    {
        Directory.CreateDirectory(stateRoot);
        EnsureNoLinks(stateRoot);
        return new FileStream(StatePath("", "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private string Destination(string name)
    {
        if (Path.GetFileName(name) != name)
        {
            throw new InvalidDataException("Invalid installation destination.");
        }
        return Path.Combine(modsRoot, "Junction", name);
    }

    private string ReceiptPath(string name) => StatePath("receipts", name + ".json");
    private string JournalPath(string name) => StatePath("journals", name + ".json");
    private static string Configuration(string destination, bool folder) => folder ? Path.Combine(destination, "mod.ini") : destination + ".ini";
    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static string SettingsHash(string path) => File.Exists(path) ? TextHash(File.ReadAllText(path)) : "missing";
    private static string TextHash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    private static ModPackageInstallResult Result(Receipt receipt, bool restart, bool installed, string? error = null) => new(receipt.InstallationId, "Junction/" + receipt.DestinationName, receipt.Version, restart, installed, error);

    private static void Move(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    private static T? Read<T>(string path) where T : class
    {
        EnsureNoLinks(path);
        if (!File.Exists(path))
        {
            return null;
        }
        if (new FileInfo(path).Length > 1024 * 1024)
        {
            throw new InvalidDataException("Installation metadata exceeds its size limit.");
        }
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid installation metadata.");
    }

    private static void WriteAtomic<T>(string path, T value) => WriteTextAtomic(path, JsonSerializer.Serialize(value));

    private static void WriteTextAtomic(string path, string value)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(value) > 1024 * 1024)
        {
            throw new InvalidDataException("Installation metadata exceeds its size limit.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        EnsureNoLinks(path);
        string temporary = path + ".writing-" + Guid.NewGuid().ToString("N");
        using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            stream.Write(bytes);
            stream.Flush(true);
        }
        File.Move(temporary, path, true);
    }
}
