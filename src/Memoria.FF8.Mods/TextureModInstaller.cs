using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public sealed record TextureModReplacement(TextureTarget Target, byte[] Content, string Extension = ".png", TextureTargetContract? Contract = null)
{
    public TextureNativeCoverage? NativeCoverage { get; init; }
}
public sealed record TextureModOutput(string RelativePath, byte[] Content);
public sealed record TextureModInstallPlan(IReadOnlyList<TextureModOutput> Files)
{
    public IReadOnlyDictionary<string, string?> ExpectedDestinationHashes { get; init; } = new Dictionary<string, string?>();
}

/// <summary>Selective native texture updates with recoverable disk staging and publication rollback.</summary>
public static class TextureModInstaller
{
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> ActiveTransactions = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    internal const int MaximumRecoveryJournalBytes = 64 * 1024 * 1024;
    public static TextureModInstallPlan Plan(IEnumerable<TextureModReplacement> replacements)
    {
        List<TextureModOutput> files = [];
        HashSet<TextureTarget> targets = [];
        foreach (TextureModReplacement replacement in replacements)
        {
            replacement.Target.Validate();
            if (!targets.Add(replacement.Target))
            {
                throw new InvalidDataException("Multiple selected images replace the same texture target.");
            }
            string path = replacement.Target.CanonicalPath(replacement.Extension);
            byte[] content = replacement.Content.ToArray();
            _ = TextureImageDecoder.Decode(content, path);
            files.Add(new(path, content));
            if (replacement.Contract is not null || replacement.NativeCoverage is not null)
            {
                TextureModMapping mapping = new(TextureTarget.SchemaVersion, replacement.Target, Path.GetFileName(path), replacement.Contract)
                {
                    Extensions = replacement.NativeCoverage is null ? null : new() { ["extensions"] = JsonSerializer.SerializeToElement(new { nativeCoverage = new { width = replacement.NativeCoverage.Width, height = replacement.NativeCoverage.Height, alpha = replacement.NativeCoverage.Alpha } }) }
                };
                files.Add(new(path + TextureModMapping.Extension, mapping.ToBytes()));
            }
        }
        if (files.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
        {
            throw new InvalidDataException("Texture target paths collide on a case-insensitive filesystem.");
        }
        return new(Array.AsReadOnly(files.ToArray()));
    }

    public static TextureModInstallPlan WithNewDescriptor(TextureModInstallPlan plan, string id, string name, string package = "My mods", bool enable = true)
    {
        if (!NativeModDescriptor.IsId(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(package))
        {
            throw new InvalidDataException("A new texture package needs a stable lowercase slug, name and display group.");
        }
        byte[] descriptor = JsonSerializer.SerializeToUtf8Bytes(new { id, name, package, description = new { en = "Custom texture replacements." } }, new JsonSerializerOptions { WriteIndented = true });
        return new(Array.AsReadOnly(plan.Files.Concat(new[] { new TextureModOutput(NativeModDescriptor.FileName, descriptor), new TextureModOutput("mod.ini", System.Text.Encoding.UTF8.GetBytes("enabled=" + (enable ? "1" : "0") + "\n")) }).ToArray())) { ExpectedDestinationHashes = plan.ExpectedDestinationHashes };
    }

    public static TextureModInstallTransaction PrepareInstall(string allowedRoot, string destination, TextureModInstallPlan plan) => new(allowedRoot, destination, plan, CancellationToken.None);
    public static TextureModInstallTransaction PrepareInstall(string allowedRoot, string destination, TextureModInstallPlan plan, CancellationToken cancellationToken) => new(allowedRoot, destination, plan, cancellationToken);

    public static TextureModInstallTransaction BeginInstall(string allowedRoot, string destination, TextureModInstallPlan plan)
    {
        TextureModInstallTransaction transaction = PrepareInstall(allowedRoot, destination, plan);
        try
        {
            transaction.Apply();
            return transaction;
        }
        catch
        {
            transaction.Dispose();
            throw;
        }
    }

    /// <summary>Restores the previous files after interruption before publication was finalized.</summary>
    public static void Recover(string allowedRoot, string destination)
    {
        string root = ValidateDestination(allowedRoot, destination);
        if (!Directory.Exists(root))
        {
            return;
        }
        foreach (string stage in Directory.EnumerateDirectories(root, ".texture-stage-*"))
        {
            RejectReparsePoints(stage);
            string leasePath = Path.Combine(stage, "lease");
            RejectReparsePoints(leasePath);
            FileStream? recoveryLease = null;
            if (File.Exists(leasePath))
            {
                try
                {
                    recoveryLease = new(leasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException exception)
                {
                    throw new IOException("A texture installation still owns this package; retry recovery after that process finishes.", exception);
                }
            }
            try
            {
                string journal = Path.Combine(stage, "journal.json");
                if (!File.Exists(journal))
                {
                    throw new InvalidDataException("An incomplete texture staging directory requires manual recovery; no journal is present.");
                }
                if (new FileInfo(journal).Length > MaximumRecoveryJournalBytes)
                {
                    throw new InvalidDataException("Texture recovery journal exceeds its size limit.");
                }
                TextureInstallJournal state = JsonSerializer.Deserialize<TextureInstallJournal>(File.ReadAllBytes(journal), new JsonSerializerOptions { AllowDuplicateProperties = false }) ?? throw new InvalidDataException("Invalid texture recovery journal.");
                if (!state.Committed)
                {
                    Restore(root, stage, state.Files);
                }
            }
            finally
            {
                recoveryLease?.Dispose();
            }
            Directory.Delete(stage, true);
            ActiveTransactions.TryRemove(root, out _);
        }
    }

    internal static string ValidateDestination(string allowedRoot, string destination)
    {
        string allowed = Path.TrimEndingDirectorySeparator(Path.GetFullPath(allowedRoot));
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        if (!root.StartsWith(allowed + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidDataException("The texture package destination must be inside the selected mod root.");
        }
        RejectReparsePoints(root);
        return root;
    }

    internal static string OutputPath(string root, string relative)
    {
        TextureModPaths.ValidateRelative(relative);
        if (relative is not (NativeModDescriptor.FileName or "mod.ini") && (!relative.StartsWith("textures/", StringComparison.Ordinal) || !TextureModPaths.IsImage(relative) && !relative.EndsWith(TextureModMapping.Extension, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("A texture installation may only write textures/ contributions and a new descriptor.");
        }
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Texture output escapes the selected package.");
        }
        RejectReparsePoints(path);
        return path;
    }

    internal static void RejectReparsePoints(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Texture installation cannot traverse a symbolic link or junction.");
            }
        }
    }

    internal static void Restore(string root, string stage, IReadOnlyList<TextureInstallJournalFile> files)
    {
        if (files.Count > 100000 || files.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
        {
            throw new InvalidDataException("Invalid or excessive texture recovery entries.");
        }
        foreach (TextureInstallJournalFile file in files)
        {
            _ = OutputPath(root, file.Path);
        }
        for (int index = files.Count - 1; index >= 0; index--)
        {
            TextureInstallJournalFile file = files[index];
            string target = OutputPath(root, file.Path);
            if (file.Existed)
            {
                string backup = Path.Combine(stage, index + ".original");
                RejectReparsePoints(backup);
                if (!File.Exists(backup))
                {
                    throw new InvalidDataException("Texture recovery is missing a required original file; retain the journal for repair.");
                }
                using (FileStream stream = File.OpenRead(backup))
                {
                    if (Convert.ToHexString(SHA256.HashData(stream)) != file.Hash)
                    {
                        throw new InvalidDataException("A texture recovery backup is corrupt; retain the journal for repair.");
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(backup, target, true);
            }
            else
            {
                File.Delete(target);
            }
        }
    }
}

public sealed record TextureInstallJournalFile(string Path, bool Existed, string? Hash);
public sealed record TextureInstallJournal(IReadOnlyList<TextureInstallJournalFile> Files, bool Committed);

public sealed class TextureModInstallTransaction : IDisposable
{
    private sealed record DestinationRevision(string Path, FileStream? Stream, long Length, DateTime Modified, string? Hash);
    private readonly List<DestinationRevision> destinationRevisions = [];
    private readonly Dictionary<string, DestinationRevision> destinationByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<TextureModOutput> frozenFiles;
    private readonly IReadOnlyDictionary<string, string?> reviewedHashes;
    private readonly string allowedRoot;
    private readonly string root;
    private readonly string stage;
    private readonly TextureInstallJournal journal;
    private readonly bool rootExisted;
    private readonly List<string> createdDirectories = [];
    private FileStream? lease;
    private bool applying;
    private bool committed;
    private bool disposed;

    internal TextureModInstallTransaction(string allowedRoot, string destination, TextureModInstallPlan plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.allowedRoot = allowedRoot;
        root = TextureModInstaller.ValidateDestination(allowedRoot, destination);
        rootExisted = Directory.Exists(root);
        reviewedHashes = new Dictionary<string, string?>(plan.ExpectedDestinationHashes, StringComparer.OrdinalIgnoreCase);
        foreach ((string path, string? expected) in reviewedHashes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(Hash(TextureModInstaller.OutputPath(root, path), cancellationToken), expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("The destination changed after review; refresh the import before installing.");
            }
        }
        if (Directory.Exists(root) && Directory.EnumerateDirectories(root, ".texture-stage-*").Any())
        {
            throw new InvalidDataException("Recover the interrupted texture installation before installing again.");
        }
        List<TextureModOutput> ownedFiles = [];
        foreach (TextureModOutput file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ownedFiles.Add(new(file.RelativePath, file.Content.ToArray()));
        }
        frozenFiles = Array.AsReadOnly(ownedFiles.ToArray());
        if (frozenFiles.Any(file => file.RelativePath is NativeModDescriptor.FileName or "mod.ini") && (rootExisted && Directory.EnumerateFileSystemEntries(root).Any() || !frozenFiles.Any(file => file.RelativePath == NativeModDescriptor.FileName)))
        {
            throw new InvalidDataException("Descriptor and enablement generation is only supported for a new empty package; preserve existing configuration.");
        }
        if (frozenFiles.Count > 100000 || frozenFiles.Sum(file => (long)file.Content.Length) > 512L * 1024 * 1024 || frozenFiles.Select(file => file.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != frozenFiles.Count)
        {
            throw new InvalidDataException("Texture installation exceeds its bounded budget or contains duplicate paths.");
        }
        foreach (TextureModOutput file in frozenFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = TextureModInstaller.OutputPath(root, file.RelativePath);
            if (file.RelativePath == NativeModDescriptor.FileName && File.Exists(path))
            {
                throw new InvalidDataException("An existing mod descriptor must be preserved; omit it from the update plan.");
            }
            if (TextureModPaths.IsImage(file.RelativePath))
            {
                _ = TextureImageDecoder.Decode(file.Content, file.RelativePath);
            }
            else if (file.RelativePath.EndsWith(TextureModMapping.Extension, StringComparison.Ordinal))
            {
                _ = TextureModMapping.Parse(file.Content);
            }
        }
        long originalBytes = frozenFiles.Sum(file =>
        {
            string path = TextureModInstaller.OutputPath(root, file.RelativePath);
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        });
        if (originalBytes > 512L * 1024 * 1024)
        {
            throw new InvalidDataException("Texture rollback backups exceed their bounded storage budget.");
        }
        stage = Path.Combine(root, ".texture-stage-" + Guid.NewGuid().ToString("N"));
        if (!TextureModInstaller.ActiveTransactions.TryAdd(root, 0))
        {
            throw new IOException("Another texture installation is preparing this package; retry after it finishes.");
        }
        List<TextureInstallJournalFile> originals = [];
        try
        {
            foreach (string relative in frozenFiles.Select(file => file.RelativePath).Concat(reviewedHashes.Keys).Concat(new[] { "mod.json", "mod.ini" }).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = TextureModInstaller.OutputPath(root, relative);
                FileStream? stream = File.Exists(path) ? new(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
                FileInfo info = new(path);
                try
                {
                    string? hash = stream is null ? null : HashStream(stream, cancellationToken);
                    DestinationRevision revision = new(path, stream, stream?.Length ?? 0, info.Exists ? info.LastWriteTimeUtc : default, hash);
                    destinationRevisions.Add(revision);
                    destinationByPath.Add(path, revision);
                    if (reviewedHashes.TryGetValue(relative, out string? expected) && !string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("The destination changed while acquiring its installation lease; review the import again.");
                    }
                }
                catch
                {
                    stream?.Dispose();
                    throw;
                }
            }
            Directory.CreateDirectory(stage);
            lease = new(Path.Combine(stage, "lease"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            for (int index = 0; index < frozenFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TextureModOutput file = frozenFiles[index];
                string target = TextureModInstaller.OutputPath(root, file.RelativePath);
                bool exists = File.Exists(target);
                if (exists)
                {
                    File.Copy(target, Path.Combine(stage, index + ".original"));
                }
                string? originalHash = exists ? Hash(Path.Combine(stage, index + ".original"), cancellationToken) : null;
                if (originalHash != destinationByPath[target].Hash)
                {
                    throw new IOException("The destination changed while its rollback backup was captured; review the import again.");
                }
                originals.Add(new(file.RelativePath, exists, originalHash));
                File.WriteAllBytes(Path.Combine(stage, index + ".new"), file.Content);
                if (file.RelativePath == NativeModDescriptor.FileName)
                {
                    _ = NativeModDescriptor.Read(Path.Combine(stage, index + ".new"));
                }
            }
            journal = new(Array.AsReadOnly(originals.ToArray()), false);
            foreach (TextureInstallJournalFile original in journal.Files)
            {
                if (reviewedHashes.TryGetValue(original.Path, out string? expected) && !string.Equals(original.Hash, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("The destination changed while the reviewed inputs were staged; review the import again.");
                }
            }
            WriteJournal(journal);
            cancellationToken.ThrowIfCancellationRequested();
            WriteJournal(journal with { Committed = true }, "journal.commit");
            WriteJournal(journal, "journal.rollback");
            cancellationToken.ThrowIfCancellationRequested();
            ValidateDestinationRevisions();
        }
        catch
        {
            ReleaseDestinationLeases();
            lease?.Dispose();
            TextureModInstaller.ActiveTransactions.TryRemove(root, out _);
            if (Directory.Exists(stage))
            {
                Directory.Delete(stage, true);
            }
            throw;
        }
    }

    public IReadOnlyList<TextureModOutput> Files => Array.AsReadOnly(frozenFiles.Select(file => new TextureModOutput(file.RelativePath, file.Content.ToArray())).ToArray());
    internal IReadOnlyList<TextureModOutput> FrozenFiles => frozenFiles;
    public string Destination => root;

    public void Apply()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (applying || committed)
        {
            throw new InvalidOperationException("Texture transaction has already been applied.");
        }
        TextureModInstaller.ValidateDestination(allowedRoot, root);
        ValidateDestinationRevisions();
        ReleaseDestinationLeases();
        applying = true;
        for (int index = 0; index < frozenFiles.Count; index++)
        {
            string path = TextureModInstaller.OutputPath(root, frozenFiles[index].RelativePath);
            string? directory = Path.GetDirectoryName(path);
            List<string> missing = [];
            while (directory is not null && !Directory.Exists(directory))
            {
                missing.Add(directory);
                directory = Path.GetDirectoryName(directory);
            }
            foreach (string missingDirectory in missing.AsEnumerable().Reverse())
            {
                TextureModInstaller.RejectReparsePoints(missingDirectory);
                Directory.CreateDirectory(missingDirectory);
                createdDirectories.Add(missingDirectory);
            }
            TextureModInstaller.RejectReparsePoints(path);
            File.Move(Path.Combine(stage, index + ".new"), path, true);
        }
    }

    /// <summary>Finalizes the recoverable disk journal and publishes the already prepared generation.</summary>
    public void Commit(Action? publish = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!applying)
        {
            throw new InvalidOperationException("Apply the staged texture files before committing publication.");
        }
        File.Move(Path.Combine(stage, "journal.commit"), Path.Combine(stage, "journal.json"), true);
        try
        {
            publish?.Invoke();
            committed = true;
        }
        catch
        {
            File.Copy(Path.Combine(stage, "journal.rollback"), Path.Combine(stage, "journal.json"), true);
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        try
        {
            if (applying && !committed)
            {
                TextureModInstaller.Restore(root, stage, journal.Files);
            }
        }
        finally
        {
            ReleaseDestinationLeases();
            lease?.Dispose();
            TextureModInstaller.ActiveTransactions.TryRemove(root, out _);
        }
        TextureModInstaller.RejectReparsePoints(stage);
        Directory.Delete(stage, true);
        if (!committed)
        {
            foreach (string directory in createdDirectories.AsEnumerable().Reverse())
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            if (!rootExisted && Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any())
            {
                Directory.Delete(root);
            }
        }
        disposed = true;
        TextureModInstaller.ActiveTransactions.TryRemove(root, out _);
    }

    private void WriteJournal(TextureInstallJournal value, string name = "journal.json")
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length > TextureModInstaller.MaximumRecoveryJournalBytes)
        {
            throw new InvalidDataException("The texture recovery journal exceeds its bounded size limit.");
        }
        string temporary = Path.Combine(stage, "journal.new");
        using (FileStream stream = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        File.Move(temporary, Path.Combine(stage, name), true);
    }

    private void ValidateDestinationRevisions()
    {
        foreach (DestinationRevision revision in destinationRevisions)
        {
            TextureModInstaller.RejectReparsePoints(revision.Path);
            FileInfo current = new(revision.Path);
            if (revision.Stream is null ? current.Exists : !current.Exists || current.Length != revision.Length || current.LastWriteTimeUtc != revision.Modified || revision.Stream.Length != revision.Length)
            {
                throw new IOException("The destination package changed after preparation; review and retry the import.");
            }
        }
    }

    private void ReleaseDestinationLeases()
    {
        foreach (DestinationRevision revision in destinationRevisions)
        {
            revision.Stream?.Dispose();
        }
    }

    private static string HashStream(Stream stream, CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[65536];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(buffer, 0, count);
        }
        stream.Position = 0;
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string? Hash(string path, CancellationToken cancellationToken)
    {
        TextureModInstaller.RejectReparsePoints(path);
        if (!File.Exists(path))
        {
            return null;
        }
        using FileStream stream = File.OpenRead(path);
        return HashStream(stream, cancellationToken);
    }
}


