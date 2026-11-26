using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed partial class ModPackageInstaller
{
    private sealed record Receipt(int Schema, Guid InstallationId, string SourceId, Guid ModId, string Version, string Target, string DestinationName, string PayloadHash, bool Folder)
    {
        public OfficialModComponent? Official { get; init; }
        public IReadOnlyList<OfficialModFile>? Files { get; init; }
        public string? EngineVersionRange { get; init; }
        public bool Migrated { get; init; }
        public IReadOnlyDictionary<string, long>? InternalWriteTimes { get; init; }
    }
    private sealed record Journal(int Schema, Guid TransactionId, Receipt Next, Receipt? Previous, string StageName, string ExpectedSettingsHash, string Settings, string Phase)
    {
        public Receipt? MissingPrevious { get; init; }
        public bool Enable { get; init; }
        public bool? PreviousPreference { get; init; }
        public string? RepairSourceHash { get; init; }
        public string? RelocateTo { get; init; }
        public bool LiveAddition { get; init; }
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsAddition => Previous is null && MissingPrevious is null && RelocateTo is null && ExpectedSettingsHash == "missing";
        [System.Text.Json.Serialization.JsonIgnore]
        public string? PreviousPayloadHash => RepairSourceHash ?? Previous?.PayloadHash;
    }

    public Task<ModPackageInstallResult> RequestInstallAsync(ModPackageInstallPlan plan, bool autoEnableNewMods = true, CancellationToken cancellationToken = default, bool enableOnAcceptance = false, bool repair = false, string? relocateTo = null, bool allowVersionMismatch = false)
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
                if (plan.VersionWarning is not null && !allowVersionMismatch)
                {
                    throw new InvalidOperationException(plan.VersionWarning);
                }
                RemoteModEntry accepted = plan.Entry with { Version = plan.Version };
                if (Fingerprint(item.Payload, false, cancellationToken) != item.Hash)
                {
                    throw new InvalidDataException("The prepared payload changed; inspect again.");
                }
                string name = Path.GetFileName(plan.Destination);
                string key = ManagedName(plan.Entry);
                string receiptPath = ReceiptPath(key);
                ForgetMissingInstallation(key);
                if (Read<RelocationReservation>(IncomingRelocationPath(key)) is not null)
                {
                    throw new InvalidOperationException("An update is moving to this directory. Restart to complete the move first.");
                }
                if (File.Exists(RemovalPath(key)))
                {
                    throw new InvalidOperationException("This mod is waiting to be uninstalled. Restart before installing it again.");
                }
                Receipt? previous = Read<Receipt>(receiptPath);
                string destination = Destination(plan.Entry.SourceId, name);
                if (File.Exists(JournalPath(key)))
                {
                    throw new InvalidOperationException("This installation already has a pending transaction. Restart to apply it first.");
                }
                string? repairSourceHash = repair && previous is not null && plan.Entry.Official is not null && Directory.Exists(destination) ? Fingerprint(destination, false, cancellationToken) : null;
                CheckOwnership(plan.Entry, destination, previous, allowMissing: true, repairSourceHash);
                bool previousPayloadExists = Exists(destination);
                if (previousPayloadExists && previous?.PayloadHash == item.Hash && (repairSourceHash is null || repairSourceHash == item.Hash) && accepted.CompareVersion(previous.Version) == 0)
                {
                    prepared.Remove(plan.Token);
                    Directory.Delete(item.Root, true);
                    return Result(previous, false, true);
                }
                string settingsPath = Configuration(destination, Directory.Exists(item.Payload));
                string settings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : plan.Entry.Official is null ? "[Mod]\nenabled=" + (autoEnableNewMods ? "1" : "0") + "\n" : "";
                if (previous is not null && plan.Entry.Official is null)
                {
                    ValidatePackage(item.Payload, accepted, ModConfiguration.ReadJunctionOptions(settingsPath));
                }
                if (CompatibilityError(plan.Entry) is { } unavailable)
                {
                    throw new NotSupportedException(unavailable);
                }
                string? relocationRoot = relocateTo is not null && previous is { Version.Length: > 0 } && accepted.CompareVersion(previous.Version) is > 0
                    ? ValidateRelocationRoot(relocateTo)
                    : null;
                Receipt next = new(1, previous?.InstallationId ?? Guid.NewGuid(), plan.Entry.SourceId, plan.Entry.Id, plan.Version, plan.Entry.Official is null ? targetProfile : "", name, item.Hash, Directory.Exists(item.Payload))
                {
                    Official = plan.Entry.Official,
                    EngineVersionRange = plan.Entry.Official?.Latest?.EngineVersionRange,
                    Files = item.NativeFiles
                };
                string stage = StatePath("transactions", plan.Token.ToString("N"));
                Directory.CreateDirectory(Path.GetDirectoryName(stage)!);
                string payloadName = Path.GetRelativePath(item.Root, item.Payload);
                Directory.Move(item.Root, stage);
                prepared[plan.Token] = item with { Root = stage, Payload = Path.Combine(stage, payloadName) };
                Journal journal = new(1, plan.Token, next, previousPayloadExists ? previous : null, payloadName, SettingsHash(settingsPath), settings, "PendingRestart")
                {
                    MissingPrevious = previousPayloadExists ? null : previous,
                    Enable = enableOnAcceptance && next.Official is not null,
                    PreviousPreference = enableOnAcceptance && next.Official is not null ? ReadCentralPreference(next.Official.SettingsKey) : null,
                    RepairSourceHash = repairSourceHash,
                    RelocateTo = relocationRoot
                };
                WriteAtomic(JournalPath(key), journal);
                prepared.Remove(plan.Token);
                return Result(next, true, false) with { PendingEnable = journal.Enable };
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
    }

    /// <summary>Call before constructing the runtime or allowing any discovery/resource generation.</summary>
    public IReadOnlyList<ModPackageInstallResult> RecoverAndApply() => RecoverAndApply(additionsOnly: false);

    /// <summary>Publishes new packages while holding the startup lock. Existing readers may keep using their current files.</summary>
    public bool ApplyNewInstallations()
    {
        IReadOnlyList<ModPackageInstallResult> results = RecoverAndApply(additionsOnly: true);
        if (results.Any(result => result.Installed))
        {
            return true;
        }
        if (results.FirstOrDefault(result => result.Error is not null) is { Error: { } error })
        {
            throw new IOException(error);
        }
        return false;
    }

    internal ModPackageInstallResult? ApplyLanguageInstallation(RemoteModEntry entry)
    {
        ModPackageInstallResult? status = GetStatus(entry);
        if (status is not { RestartRequired: true })
        {
            return status;
        }
        using ModDirectoryResourceLease lease = ModDirectoryResourceLease.Acquire(modsRoot, requestWriter: true, retainStartupForReader: true);
        return RecoverAndApply(additionsOnly: !lease.IsWriter, languagePackage: entry).SingleOrDefault() ?? GetStatus(entry);
    }

    private IReadOnlyList<ModPackageInstallResult> RecoverAndApply(bool additionsOnly, RemoteModEntry? languagePackage = null)
    {
        IReadOnlyList<ModPackageInstallResult> removals = additionsOnly || languagePackage is not null ? [] : RecoverRemovals();
        string journals = StatePath("journals", "");
        string receipts = StatePath("receipts", "");
        bool startup = !additionsOnly && languagePackage is null;
        if (!Directory.Exists(journals) && (!startup || !Directory.Exists(receipts)))
        {
            return removals;
        }
        gate.Wait();
        try
        {
            using FileStream installLock = AcquireLock();
            List<ModPackageInstallResult> results = new(removals);
            if (startup && Directory.Exists(receipts))
            {
                EnsureNoLinks(receipts);
                foreach (string receipt in Directory.EnumerateFiles(receipts, "managed-*.json"))
                {
                    string key = Path.GetFileNameWithoutExtension(receipt);
                    if (File.Exists(JournalPath(key)))
                    {
                        continue;
                    }
                    try
                    {
                        if (ForgetMissingInstallation(key) is { } removed)
                        {
                            results.Add(removed);
                        }
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or JsonException)
                    {
                        results.Add(new(Guid.Empty, "", "", false, false, "Mod removal reconciliation: " + exception.Message));
                    }
                }
            }
            if (!Directory.Exists(journals))
            {
                return results.AsReadOnly();
            }
            IEnumerable<string> paths = languagePackage is null ? Directory.GetFiles(journals, "*.json") : new[] { JournalPath(ManagedName(languagePackage)) }.Where(File.Exists);
            foreach (string path in paths)
            {
                Journal? journal = null;
                try
                {
                    EnsureNoLinks(path);
                    journal = Read<Journal>(path) ?? throw new InvalidDataException("Empty installation journal.");
                    ValidateJournal(journal, path);
                    if (WasPublishedAndDeleted(journal))
                    {
                        if (ForgetMissingInstallation(Path.GetFileNameWithoutExtension(path)) is { } removed)
                        {
                            results.Add(removed);
                        }
                        continue;
                    }
                    if (IsRelocationPhase(journal))
                    {
                        continue;
                    }
                    bool canPublishLive = journal.IsAddition && (journal.LiveAddition || journal.Phase == "PendingRestart");
                    if (additionsOnly && !canPublishLive)
                    {
                        if (languagePackage is not null)
                        {
                            results.Add(Result(journal.Next, true, false, "The language package is staged. Close any game using this mod directory and retry the installation."));
                        }
                        continue;
                    }
                    if (journal.Phase == "RollingBack")
                    {
                        Rollback(journal, path);
                        results.Add(Result(journal.Next, false, false, "Previous installation restored during recovery."));
                        continue;
                    }
                    // Language acquisitions publish fresh additions immediately and retain recovery data for repairs and updates.
                    Apply(journal, path, additionsOnly || languagePackage is not null && canPublishLive);
                    results.Add(Result(journal.Next, false, true));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or JsonException)
                {
                    bool restored = false;
                    if (journal?.Previous is not null && !IsRelocationPhase(journal))
                    {
                        try
                        {
                            ValidateJournal(journal, path);
                            string backup = StatePath("transactions", Path.Combine(journal.TransactionId.ToString("N"), "previous"));
                            if (Exists(backup) && Fingerprint(backup, false) == journal.PreviousPayloadHash)
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
        if (Read<Removal>(RemovalPath(key)) is { } removal)
        {
            ValidateRemoval(removal, RemovalPath(key));
            return Result(removal.Receipt, true, false) with { PendingRemoval = true };
        }
        Journal? journal = Read<Journal>(JournalPath(key));
        if (journal is not null)
        {
            ValidateJournal(journal, JournalPath(key));
            if (WasPublishedAndDeleted(journal))
            {
                return null;
            }
            if (journal.Phase == "Relocated")
            {
                return null;
            }
            if (journal.Phase == "Relocating")
            {
                return Result(journal.Next, true, false);
            }
            // Applied files can await game resource validation without requiring another file application.
            bool applied = journal.Phase is "AwaitingValidation" or "Committed";
            return Result(journal.Next, !applied || journal.RelocateTo is not null, applied) with { PendingEnable = ShouldEnable(journal) };
        }
        Receipt? receipt = Read<Receipt>(ReceiptPath(key));
        if (receipt is null)
        {
            return null;
        }
        if (entry.SourceId == OfficialModCatalog.SourceId)
        {
            if (entry.Official is not { } component || !CanUseInstalledDestination(component, receipt))
            {
                return Result(receipt, false, false, "The native installation receipt does not match this component.");
            }
            if (IsPayloadMissing(receipt))
            {
                return null;
            }
            ModPackageInstallResult status = NativeResult(receipt);
            return status with { CanUninstall = status.Installed && !receipt.Migrated && receipt.Version.Length > 0 };
        }
        if (receipt.Schema != 1 || receipt.SourceId != entry.SourceId || receipt.ModId != entry.Id || receipt.Target != targetProfile || receipt.DestinationName != key + (receipt.Folder ? "" : ".iroj"))
        {
            return Result(receipt, false, false, "The installation receipt does not match this target.");
        }
        if (IsPayloadMissing(receipt))
        {
            return null;
        }
        return Result(receipt, false, true) with { CanUninstall = true };
    }

    private void Apply(Journal journal, string journalPath, bool additionsOnly)
    {
        Receipt next = journal.Next;
        string destination = Destination(next.SourceId, next.DestinationName);
        string transaction = StatePath("transactions", journal.TransactionId.ToString("N"));
        string payload = Path.Combine(transaction, journal.StageName);
        string backup = Path.Combine(transaction, "previous");
        string settingsPath = Configuration(destination, next.Folder);
        EnsureNoLinks(transaction);
        EnsureNoLinks(destination);
        if (next.Official is not null)
        {
            VerifyNativeCompatibility(next);
        }
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
            bool oldMoveInterrupted = journal.Previous is not null && !Exists(destination) && Exists(backup) && Fingerprint(backup, false) == journal.PreviousPayloadHash;
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
                CheckOwnership(pinned, destination, journal.Previous ?? journal.MissingPrevious, allowMissing: journal.MissingPrevious is not null, journal.RepairSourceHash);
            }
        }
        bool liveAddition = additionsOnly || journal.LiveAddition;
        bool finalIsNew = journal.Phase != "PendingRestart" && Exists(destination) && (!liveAddition || !Exists(payload)) && Fingerprint(destination, false) == next.PayloadHash;
        if (!finalIsNew)
        {
            if (!Exists(payload) || Fingerprint(payload, false) != next.PayloadHash)
            {
                throw new InvalidDataException("The staged payload is missing or changed. Recovery data has been retained.");
            }
            RemoteModEntry entry = new(next.SourceId, next.ModId, "", next.Version);
            if (next.Official is null)
            {
                ValidatePackage(payload, entry);
            }
            else
            {
                ValidateNativeFiles(payload, next.Files!, CancellationToken.None, journal.ExpectedSettingsHash);
            }
            if (journal.Previous is null)
            {
                bool stagedSettings = journal.LiveAddition && !next.Folder && SettingsHash(settingsPath) == TextHash(journal.Settings);
                CheckOwnership(entry, destination, journal.MissingPrevious, allowMissing: journal.MissingPrevious is not null, journal.RepairSourceHash, stagedSettings);
            }
            if (Exists(destination))
            {
                CheckOwnership(entry, destination, journal.Previous, repairSourceHash: journal.RepairSourceHash);
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
            else if (journal.Previous is not null && (!Exists(backup) || Fingerprint(backup, false) != journal.PreviousPayloadHash))
            {
                throw new InvalidDataException("The previous installation or its verified backup is missing.");
            }
            if (next.Folder && (next.Official is null || journal.ExpectedSettingsHash != "missing"))
            {
                File.WriteAllText(Path.Combine(payload, "mod.ini"), journal.Settings);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            journal = journal with { Phase = "OldMoved", LiveAddition = liveAddition };
            WriteAtomic(journalPath, journal);
            if (liveAddition)
            {
                // Publish the metadata before making the complete package visible to a running discovery.
                if (next.Official is not null)
                {
                    next = next with { InternalWriteTimes = CaptureWriteTimes(payload, next.Files!) };
                    CommitEnable(journal);
                }
                if (!next.Folder)
                {
                    WriteTextAtomic(settingsPath, journal.Settings);
                }
                WriteAtomic(ReceiptPath(ManagedName(entry)), next);
            }
            Move(payload, destination);
            WriteAtomic(journalPath, journal with { Phase = "NewMoved" });
        }
        if (liveAddition)
        {
            // The package was verified in staging. Runtime refresh still decides whether it can become active.
            File.Delete(journalPath);
            return;
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
        if (next.Official is not null)
        {
            next = next with { InternalWriteTimes = CaptureWriteTimes(destination, next.Files!) };
        }
        WriteAtomic(ReceiptPath(ManagedName(new(next.SourceId, next.ModId, "", next.Version))), next);
        WriteAtomic(journalPath, journal with { Next = next, Phase = "AwaitingValidation" });
        // Retain transaction backups for explicit recovery; cache eviction must never delete them.
    }

    private void CheckOwnership(RemoteModEntry entry, string destination, Receipt? previous, bool allowMissing = false, string? repairSourceHash = null, bool stagedSettings = false)
    {
        EnsureNoLinks(modsRoot);
        EnsureNoLinks(destination);
        if (previous is not null)
        {
            if (previous.Schema != 1 || previous.SourceId != entry.SourceId || previous.ModId != entry.Id || (entry.SourceId != OfficialModCatalog.SourceId && previous.Target != targetProfile) || previous.DestinationName != Path.GetFileName(destination))
            {
                throw new InvalidDataException("Receipt ownership or target differs from the accepted plan.");
            }
            if (entry.SourceId == OfficialModCatalog.SourceId && (!ValidNativeReceipt(previous) || previous.Version.Length == 0 && repairSourceHash is null))
            {
                throw new InvalidDataException("This legacy installation has local changes. Keep or move its files before replacing it.");
            }
            if (repairSourceHash is not null && Directory.Exists(destination))
            {
                HashSet<string> owned = previous.Files!.Select(file => file.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
                owned.Add("mod.ini");
                if (SafeFiles(destination).Any(file => !owned.Contains(Path.GetRelativePath(destination, file).Replace('\\', '/'))))
                {
                    throw new InvalidDataException("The folder contains unowned files. Move those files somewhere safe before repairing the component.");
                }
            }
            if (!Exists(destination) ? !allowMissing : Fingerprint(destination, false) != (repairSourceHash ?? previous.PayloadHash))
            {
                throw new InvalidDataException("This managed installation is missing or contains local changes.");
            }
        }
        else if (Exists(destination) || !stagedSettings && File.Exists(destination + ".ini"))
        {
            throw new InvalidDataException("The destination is not owned by the downloader. Manual installations cannot be overwritten.");
        }
        if (entry.SourceId == OfficialModCatalog.SourceId)
        {
            return;
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
        if (journal is null || journal.Next is null || string.IsNullOrWhiteSpace(journal.Next.SourceId) || journal.Next.ModId == Guid.Empty || (journal.Next.SourceId == OfficialModCatalog.SourceId ? !NuGet.Versioning.NuGetVersion.TryParse(journal.Next.Version, out _) : !DecimalVersion(journal.Next.Version, out _)))
        {
            throw new InvalidDataException("Installation journal identity is missing or invalid.");
        }
        RemoteModEntry entry = new(journal.Next.SourceId, journal.Next.ModId, "", journal.Next.Version);
        string name = ManagedName(entry);
        bool validDestination = journal.Next.SourceId == OfficialModCatalog.SourceId ? ValidNativeReceipt(journal.Next) : journal.Next.Target == targetProfile && journal.Next.DestinationName == name + (journal.Next.Folder ? "" : ".iroj");
        if (journal.Schema != 1 || journal.Next.Schema != 1 || !validDestination || journal.StageName != (journal.Next.Folder ? "content" : "content.iroj") || !Path.GetFullPath(path).Equals(JournalPath(name), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Unsupported journal schema, target or destination mapping.");
        }
        if (journal.MissingPrevious is { } missing && (journal.Previous is not null || missing.Schema != 1 || missing.SourceId != journal.Next.SourceId || missing.ModId != journal.Next.ModId || missing.Target != journal.Next.Target || missing.DestinationName != journal.Next.DestinationName || missing.Folder != journal.Next.Folder || missing.InstallationId != journal.Next.InstallationId))
        {
            throw new InvalidDataException("The missing-installation receipt does not match the journal ownership and target.");
        }
        if (journal.Previous is { } previous && journal.Next.Official is not null && (!ValidNativeReceipt(previous) || previous.ModId != journal.Next.ModId || previous.InstallationId != journal.Next.InstallationId || previous.DestinationName != journal.Next.DestinationName))
        {
            throw new InvalidDataException("The previous native receipt does not match the transaction.");
        }
        if (journal.RepairSourceHash is not null && (journal.Previous?.Official is null || !OfficialModCatalog.IsDigest(journal.RepairSourceHash)))
        {
            throw new InvalidDataException("Invalid repair ownership in the installation journal.");
        }
        if (journal.RelocateTo is { } relocation)
        {
            Receipt? previousOwner = journal.Previous ?? journal.MissingPrevious;
            if (journal.TransactionId == Guid.Empty || relocation != ValidateRelocationRoot(relocation) || previousOwner is null || previousOwner.SourceId != journal.Next.SourceId || previousOwner.ModId != journal.Next.ModId || previousOwner.InstallationId != journal.Next.InstallationId)
            {
                throw new InvalidDataException("The update does not have a valid relocation destination or previous owner.");
            }
        }
        if (journal.LiveAddition && (!journal.IsAddition || journal.Phase is not ("OldMoved" or "NewMoved")))
        {
            throw new InvalidDataException("Invalid live installation journal.");
        }
        if (journal.Phase is not ("PendingRestart" or "OldMoved" or "NewMoved" or "Committed" or "AwaitingValidation" or "RollingBack") && (!IsRelocationPhase(journal) || journal.RelocateTo is null))
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
        if (receipt.Official is null)
        {
            ValidatePackage(destination, new(receipt.SourceId, receipt.ModId, "", receipt.Version), ModConfiguration.ReadJunctionOptions(Configuration(destination, receipt.Folder)));
        }
        else
        {
            VerifyNativeCompatibility(receipt);
        }
    }

    private FileStream AcquireLock()
    {
        Directory.CreateDirectory(stateRoot);
        EnsureNoLinks(stateRoot);
        return new FileStream(StatePath("", "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private string Destination(string source, string name)
    {
        if (Path.GetFileName(name) != name)
        {
            throw new InvalidDataException("Invalid installation destination.");
        }
        return Path.Combine(modsRoot, source == OfficialModCatalog.SourceId ? "Memoria" : "Junction", name);
    }

    private string ReceiptPath(string name) => StatePath("receipts", name + ".json");
    private string JournalPath(string name) => StatePath("journals", name + ".json");
    private static string Configuration(string destination, bool folder) => folder ? Path.Combine(destination, "mod.ini") : destination + ".ini";
    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
    private static string SettingsHash(string path) => File.Exists(path) ? TextHash(File.ReadAllText(path)) : "missing";
    private static string TextHash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
    private static string PackageId(Receipt receipt) => receipt.Official?.RuntimePackageId ?? "Junction/" + receipt.DestinationName;

    private static ModPackageInstallResult Result(Receipt receipt, bool restart, bool installed, string? error = null) => new(receipt.InstallationId, PackageId(receipt), receipt.Version, restart, installed, error)
    {
        Official = receipt.Official,
        NativeDestinationName = receipt.Official is null ? null : receipt.DestinationName,
        EngineVersionRange = receipt.EngineVersionRange
    };

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
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > 1024 * 1024)
            {
                throw new InvalidDataException("Installation metadata exceeds its size limit.");
            }
            return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidDataException("Invalid installation metadata.");
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
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
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
