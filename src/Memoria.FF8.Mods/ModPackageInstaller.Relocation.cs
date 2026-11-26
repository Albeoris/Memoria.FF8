using System.Text.Json;

namespace Memoria.FF8.Mods;

public sealed partial class ModPackageInstaller
{
    private sealed record RelocationReservation(int Schema, Guid TransactionId, string SourceRoot, Receipt Receipt, string SettingsHash, bool Publishing = false);

    /// <summary>Resumes accepted updates while the caller holds exclusive startup resource leases for the source and every supplied installer.</summary>
    public IReadOnlyList<ModPackageInstallResult> RecoverRelocations(IReadOnlyList<ModPackageInstaller> writableInstallers)
    {
        string directory = StatePath("journals", "");
        if (!Directory.Exists(directory))
        {
            return [];
        }
        gate.Wait();
        try
        {
            using FileStream sourceLock = AcquireLock();
            EnsureNoLinks(directory);
            List<ModPackageInstallResult> results = [];
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                Journal? journal = null;
                bool relocated = false;
                try
                {
                    journal = Read<Journal>(path) ?? throw new InvalidDataException("Empty installation journal.");
                    if (!IsRelocationPhase(journal))
                    {
                        continue;
                    }
                    ValidateJournal(journal, path);
                    ModPackageInstaller? target = writableInstallers.FirstOrDefault(candidate => SameRoot(candidate.modsRoot, journal.RelocateTo!));
                    if (target is null)
                    {
                        results.Add(Result(journal.Next, true, false, "The update is waiting for access to its selected destination."));
                        continue;
                    }
                    target.gate.Wait();
                    try
                    {
                        using FileStream targetLock = target.AcquireLock();
                        Relocate(journal, path, target, ref relocated);
                        results.Add(Result(journal.Next, false, true) with { Relocated = true });
                    }
                    finally
                    {
                        target.gate.Release();
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or JsonException)
                {
                    string error = "Mod relocation needs attention: " + exception.Message;
                    results.Add(journal?.Next is { } next ? Result(next, true, relocated, error) with { Relocated = relocated } : new(Guid.Empty, "", "", true, false, error));
                }
            }
            return results.AsReadOnly();
        }
        finally
        {
            gate.Release();
        }
    }

    private void Relocate(Journal journal, string journalPath, ModPackageInstaller target, ref bool relocated)
    {
        Receipt next = journal.Next;
        RemoteModEntry entry = new(next.SourceId, next.ModId, "", next.Version);
        string key = ManagedName(entry);
        string source = Destination(next.SourceId, next.DestinationName);
        string destination = target.Destination(next.SourceId, next.DestinationName);
        string sourceSettings = Configuration(source, next.Folder);
        string destinationSettings = Configuration(destination, next.Folder);
        string retired = StatePath("transactions", Path.Combine(journal.TransactionId.ToString("N"), "relocated"));
        string retiredSettings = Configuration(retired, next.Folder);
        string stage = target.StatePath("incoming-payloads", journal.TransactionId.ToString("N"));
        string payload = Path.Combine(stage, journal.StageName);
        string payloadSettings = Configuration(payload, next.Folder);
        string reservationPath = target.IncomingRelocationPath(key);
        Receipt targetReceipt = next with { Target = next.Official is null ? target.targetProfile : "", InternalWriteTimes = null };
        EnsureNoLinks(sourceSettings);
        EnsureNoLinks(destinationSettings);
        EnsureNoLinks(retired);
        EnsureNoLinks(retiredSettings);
        EnsureNoLinks(payloadSettings);

        RelocationReservation? reservation = Read<RelocationReservation>(reservationPath);
        if (reservation is not null && (reservation.Schema != 1 || reservation.TransactionId != journal.TransactionId || string.IsNullOrWhiteSpace(reservation.SourceRoot) || !SameRoot(reservation.SourceRoot, modsRoot) || !SameReceipt(reservation.Receipt, targetReceipt) || reservation.SettingsHash != "missing" && !OfficialModCatalog.IsDigest(reservation.SettingsHash)))
        {
            throw new InvalidDataException("A different move owns the destination reservation.");
        }
        Receipt? published = Read<Receipt>(target.ReceiptPath(key));
        if (published is not null && (!SameReceipt(published, targetReceipt) || reservation is null && journal.Phase != "Relocated"))
        {
            throw new InvalidDataException("Another managed installation owns this destination.");
        }

        if (journal.Phase == "Relocating")
        {
            if (Exists(target.JournalPath(key)) || Exists(target.RemovalPath(key)))
            {
                throw new InvalidDataException("An installation change is pending at the destination.");
            }
            if (reservation is null)
            {
                if (!SameReceipt(Read<Receipt>(ReceiptPath(key)), next) || Exists(retired) || !next.Folder && Exists(retiredSettings) || Exists(stage))
                {
                    throw new InvalidDataException("The source installation or relocation staging has an unexpected owner.");
                }
                VerifyFinal(next, source);
                target.CheckOwnership(entry, destination, null);
                reservation = new(1, journal.TransactionId, Path.TrimEndingDirectorySeparator(modsRoot), targetReceipt, SettingsHash(sourceSettings));
                WriteAtomic(reservationPath, reservation);
            }
            else if (Exists(source))
            {
                if (!SameReceipt(Read<Receipt>(ReceiptPath(key)), next) || Exists(retired) || !next.Folder && Exists(retiredSettings))
                {
                    throw new InvalidDataException("The source installation changed after relocation was reserved.");
                }
                VerifyFinal(next, source);
                string latestSettings = SettingsHash(sourceSettings);
                if (latestSettings != reservation.SettingsHash)
                {
                    if (Exists(destination) || published is not null)
                    {
                        RequireRelocationSettings(sourceSettings, reservation.SettingsHash);
                    }
                    reservation = reservation with { SettingsHash = latestSettings, Publishing = false };
                    WriteAtomic(reservationPath, reservation);
                }
            }
            else
            {
                if (!Exists(retired) || Fingerprint(retired, false) != next.PayloadHash)
                {
                    throw new InvalidDataException("The retired source payload was changed. Its recovery data was preserved.");
                }
                RequireRelocationSettings(!next.Folder && File.Exists(sourceSettings) ? sourceSettings : retiredSettings, reservation.SettingsHash);
            }

            if (!Exists(destination))
            {
                if (!Exists(source))
                {
                    throw new InvalidDataException("The destination disappeared after the source was retired. The verified source copy was retained.");
                }
                target.CheckOwnership(entry, destination, null);
                if (!next.Folder && Directory.Exists(destinationSettings))
                {
                    throw new InvalidDataException("A directory occupies the destination settings path.");
                }
                if (!reservation.Publishing || !Exists(payload))
                {
                    DeleteTransactionDirectory(stage);
                    target.CheckSpace(PayloadBytes(source));
                    CopyRelocationPayload(source, payload, next.Folder);
                    if (!next.Folder && File.Exists(sourceSettings))
                    {
                        CopyRelocationFile(sourceSettings, payloadSettings);
                    }
                }
                target.VerifyFinal(targetReceipt, payload);
                RequireRelocationSettings(payloadSettings, reservation.SettingsHash);
                WriteAtomic(reservationPath, reservation with { Publishing = true });
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Move(payload, destination);
            }
            else if (!reservation.Publishing || Exists(payload))
            {
                throw new InvalidDataException("Unmanaged files appeared at the relocation destination. They were preserved.");
            }

            try
            {
                if (!next.Folder && File.Exists(payloadSettings) && !Exists(destinationSettings))
                {
                    RequireRelocationSettings(payloadSettings, reservation.SettingsHash);
                    File.Move(payloadSettings, destinationSettings);
                }
                RequireRelocationSettings(destinationSettings, reservation.SettingsHash);
                target.VerifyFinal(targetReceipt, destination);

                if (published is null)
                {
                    if (targetReceipt.Files is { } files)
                    {
                        targetReceipt = targetReceipt with { InternalWriteTimes = CaptureWriteTimes(destination, files) };
                    }
                    WriteAtomic(target.ReceiptPath(key), targetReceipt);
                }
                if (Exists(source))
                {
                    VerifyFinal(next, source);
                    RequireRelocationSettings(sourceSettings, reservation.SettingsHash);
                    Move(source, retired);
                }
                relocated = true;
                if (!next.Folder && File.Exists(sourceSettings))
                {
                    File.Move(sourceSettings, retiredSettings);
                }
                WriteAtomic(journalPath, journal with { Phase = "Relocated" });
            }
            catch (Exception exception) when (Exists(source) && !Exists(retired) && exception is (IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or JsonException))
            {
                // Keep exposing the original installation when the newly published copy can be safely withdrawn.
                if (!Exists(payload) && Exists(destination) && Fingerprint(destination, false) == next.PayloadHash)
                {
                    if (next.Folder || File.Exists(destinationSettings))
                    {
                        RequireRelocationSettings(destinationSettings, reservation.SettingsHash);
                    }
                    if (!next.Folder && File.Exists(destinationSettings))
                    {
                        File.Move(destinationSettings, payloadSettings);
                    }
                    Move(destination, payload);
                    if (SameReceipt(Read<Receipt>(target.ReceiptPath(key)), targetReceipt))
                    {
                        File.Delete(target.ReceiptPath(key));
                    }
                }
                throw;
            }
        }
        else
        {
            if (published is null || Exists(source) || !next.Folder && Exists(sourceSettings))
            {
                throw new InvalidDataException("The completed move no longer matches the installation directories.");
            }
            target.VerifyFinal(targetReceipt, destination);
            relocated = true;
        }

        string sourceReceipt = ReceiptPath(key);
        if (Read<Receipt>(sourceReceipt) is { } originalReceipt)
        {
            if (!SameReceipt(originalReceipt, next))
            {
                throw new InvalidDataException("The original receipt changed. It was preserved.");
            }
            File.Delete(sourceReceipt);
        }
        if (next.Folder && Directory.Exists(retired))
        {
            DeleteTransactionDirectory(retired);
        }
        else if (!next.Folder)
        {
            if (File.Exists(retired))
            {
                File.Delete(retired);
            }
            if (File.Exists(retiredSettings))
            {
                File.Delete(retiredSettings);
            }
        }
        if (Directory.Exists(stage))
        {
            Directory.Delete(stage);
        }
        if (reservation is not null)
        {
            File.Delete(reservationPath);
        }
        File.Delete(journalPath);
    }

    private static bool IsRelocationPhase(Journal journal) => journal.Phase is "Relocating" or "Relocated";

    private string IncomingRelocationPath(string key) => StatePath("incoming", key + ".json");

    private static bool SameRoot(string first, string second) => Path.TrimEndingDirectorySeparator(first).Equals(Path.TrimEndingDirectorySeparator(second), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private string ValidateRelocationRoot(string root)
    {
        if (!Path.IsPathFullyQualified(root))
        {
            throw new InvalidDataException("A mod relocation destination must be an absolute directory.");
        }
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (Path.GetDirectoryName(destination) is null || SameRoot(destination, modsRoot) || IsWithin(destination, modsRoot) || IsWithin(modsRoot, destination))
        {
            throw new InvalidDataException("Choose a separate mod relocation directory below the volume root.");
        }
        return destination;
    }

    private static bool SameReceipt(Receipt? actual, Receipt expected)
    {
        return actual is not null && JsonSerializer.Serialize(actual with { InternalWriteTimes = null }) == JsonSerializer.Serialize(expected with { InternalWriteTimes = null });
    }

    private static void RequireRelocationSettings(string path, string expected)
    {
        if (SettingsHash(path) != expected)
        {
            throw new InvalidDataException("Mod settings changed during relocation. Existing files were preserved.");
        }
    }

    private static void CopyRelocationPayload(string source, string destination, bool folder)
    {
        if (!folder)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            CopyRelocationFile(source, destination);
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (string file in SafeFiles(source))
        {
            string copy = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            CopyRelocationFile(file, copy);
        }
    }

    private static void CopyRelocationFile(string source, string destination)
    {
        EnsureNoLinks(source);
        using FileStream input = File.OpenRead(source);
        using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyTo(output);
        output.Flush(true);
    }

    private static void DeleteTransactionDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            _ = SafeFiles(directory).ToArray();
            Directory.Delete(directory, true);
        }
    }
}
