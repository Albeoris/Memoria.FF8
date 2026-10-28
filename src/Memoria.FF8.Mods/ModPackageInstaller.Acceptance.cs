namespace Memoria.FF8.Mods;

public sealed partial class ModPackageInstaller
{
    /// <summary>Accepts verified startup packages; restores rejected updates before consumers may retain backing paths.</summary>
    public IReadOnlyList<ModPackageInstallResult> CompleteStartupApplications(IReadOnlyCollection<string> acceptedPackageIds)
    {
        if (!Directory.Exists(StatePath("journals", "")))
        {
            return Array.Empty<ModPackageInstallResult>();
        }
        gate.Wait();
        try
        {
            using FileStream installLock = AcquireLock();
            string directory = StatePath("journals", "");
            List<ModPackageInstallResult> results = [];
            if (!Directory.Exists(directory))
            {
                return results.AsReadOnly();
            }
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                Journal? journal = null;
                try
                {
                    journal = Read<Journal>(path)!;
                    ValidateJournal(journal, path);
                    if (journal.Phase != "AwaitingValidation")
                    {
                        continue;
                    }
                    Receipt next = journal.Next;
                    string packageId = "Junction/" + next.DestinationName;
                    string destination = Destination(next.DestinationName);
                    VerifyFinal(next, destination);
                    if (acceptedPackageIds.Contains(packageId, StringComparer.OrdinalIgnoreCase))
                    {
                        WriteAtomic(path, journal with { Phase = "Committed" });
                        File.Delete(path);
                        results.Add(Result(next, false, true));
                        continue;
                    }
                    string transaction = StatePath("transactions", journal.TransactionId.ToString("N"));
                    string backup = Path.Combine(transaction, "previous");
                    if (journal.Previous is not null && (!Exists(backup) || Fingerprint(backup, false) != journal.Previous.PayloadHash))
                    {
                        throw new InvalidDataException("The verified previous backup is unavailable; recovery data has been retained.");
                    }
                    WriteAtomic(path, journal with { Phase = "RollingBack" });
                    Rollback(journal, path);
                    string recovery = journal.Previous is not null ? "The previous installation was restored." : journal.MissingPrevious is not null ? "The previous missing-installation state was restored." : "The rejected installation was removed.";
                    results.Add(Result(next, false, false, "The package failed startup resource validation. " + recovery));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or System.Text.Json.JsonException)
                {
                    results.Add(journal?.Next is null ? new(Guid.Empty, "", "", false, false, "Recovery needed: " + exception.Message) : Result(journal.Next, false, false, "Recovery needed: " + exception.Message));
                }
            }
            return results.AsReadOnly();
        }
        finally
        {
            gate.Release();
        }
    }

    private void Rollback(Journal journal, string path)
    {
        string destination = Destination(journal.Next.DestinationName);
        string transaction = StatePath("transactions", journal.TransactionId.ToString("N"));
        string rejected = Path.Combine(transaction, "rejected");
        string backup = Path.Combine(transaction, "previous");
        if (Exists(destination) && Fingerprint(destination, false) == journal.Next.PayloadHash)
        {
            if (Exists(rejected))
            {
                throw new InvalidDataException("Rollback destination is occupied; recovery data has been retained.");
            }
            Move(destination, rejected);
        }
        if (journal.Previous is not null)
        {
            if (!Exists(destination))
            {
                if (!Exists(backup) || Fingerprint(backup, false) != journal.Previous.PayloadHash)
                {
                    throw new InvalidDataException("Previous backup changed during rollback.");
                }
                Move(backup, destination);
            }
            if (Fingerprint(destination, false) != journal.Previous.PayloadHash)
            {
                throw new InvalidDataException("Restored installation failed verification.");
            }
            WriteAtomic(ReceiptPath(ManagedName(new(journal.Previous.SourceId, journal.Previous.ModId, "", journal.Previous.Version))), journal.Previous);
        }
        else
        {
            if (Exists(destination))
            {
                throw new InvalidDataException("A different payload occupies the rejected installation destination.");
            }
            string settings = Configuration(destination, journal.Next.Folder);
            if (!journal.Next.Folder && journal.ExpectedSettingsHash == "missing" && File.Exists(settings))
            {
                if (SettingsHash(settings) != TextHash(journal.Settings))
                {
                    throw new InvalidDataException("Settings changed during rollback; recovery data has been retained.");
                }
                File.Delete(settings);
            }
            string receiptPath = ReceiptPath(ManagedName(new(journal.Next.SourceId, journal.Next.ModId, "", journal.Next.Version)));
            if (journal.MissingPrevious is { } missing)
            {
                WriteAtomic(receiptPath, missing);
            }
            else
            {
                File.Delete(receiptPath);
            }
        }
        File.Delete(path);
    }
}
