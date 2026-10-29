using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.FF8.Mods.Localization;

internal static class LocalizationAuthoringTransaction
{
    private const string Journal = ".localization-authoring.transaction.json";
    internal sealed record FileChange(string Destination, string Backup, string Candidate, bool Existed = true, string? CandidateSha256 = null);
    internal static FileStream Acquire(string root)
    {
        string locks = Path.Combine(Path.GetTempPath(), "Memoria.Localization.Authoring.Locks");
        Directory.CreateDirectory(locks);
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(OperatingSystem.IsWindows() ? Path.GetFullPath(root).ToUpperInvariant() : Path.GetFullPath(root))));
        return new FileStream(Path.Combine(locks, key + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    internal static void Recover(string root)
    {
        string path = Path.Combine(root, Journal);
        if (!File.Exists(path))
        {
            return;
        }
        FileChange[] changes = JsonSerializer.Deserialize<FileChange[]>(File.ReadAllBytes(path)) ?? throw new InvalidDataException("Invalid authoring recovery journal.");
        foreach (FileChange change in changes)
        {
            string backup = LocalizationProjectIO.Resolve(root, change.Backup);
            string destination = LocalizationProjectIO.Resolve(root, change.Destination);
            if (!change.Existed)
            {
                string candidate = LocalizationProjectIO.Resolve(root, change.Candidate);
                if (!File.Exists(candidate) && File.Exists(destination))
                {
                    if (change.CandidateSha256 is null || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(destination))) != change.CandidateSha256)
                    {
                        throw new InvalidDataException("A newly inserted payload changed after an interrupted save; retain the recovery files and review the external change.");
                    }
                    File.Delete(destination);
                }
                continue;
            }
            if (!File.Exists(backup))
            {
                throw new InvalidDataException("An interrupted authoring transaction lacks its recovery backup.");
            }
            File.Copy(backup, destination, true);
        }
        File.Delete(path);
        Cleanup(root, changes);
    }
    internal static void Publish(string root, IReadOnlyDictionary<string, byte[]> candidates, CancellationToken cancellation, IReadOnlyDictionary<string, byte[]>? expectedOriginals = null, IReadOnlySet<string>? expectedAbsent = null)
    {
        string token = Guid.NewGuid().ToString("N");
        List<FileChange> changes = [];
        List<FileStream> originals = [];
        bool journalWritten = false;
        try
        {
            foreach (string relative in candidates.Keys.Concat(expectedOriginals?.Keys.AsEnumerable() ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (expectedAbsent?.Contains(relative) == true)
                {
                    if (File.Exists(LocalizationProjectIO.Resolve(root, relative)))
                    {
                        throw new IOException("A new localization payload appeared during final write coordination; review and retry.");
                    }
                    continue;
                }
                FileStream original = new(LocalizationProjectIO.Resolve(root, relative), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                originals.Add(original);
                if (expectedOriginals?.TryGetValue(relative, out byte[]? expected) == true)
                {
                    using MemoryStream copy = new();
                    original.CopyTo(copy);
                    if (!copy.ToArray().AsSpan().SequenceEqual(expected))
                    {
                        throw new IOException("The localization files changed during final write coordination; review and retry.");
                    }
                }
            }
            foreach ((string relative, byte[] bytes) in candidates)
            {
                string backup = relative + ".authoring-" + token + ".bak";
                string candidate = relative + ".authoring-" + token + ".tmp";
                bool existed = expectedAbsent?.Contains(relative) != true;
                changes.Add(new(relative, backup, candidate, existed, Convert.ToHexString(SHA256.HashData(bytes))));
                Directory.CreateDirectory(Path.GetDirectoryName(LocalizationProjectIO.Resolve(root, relative))!);
                if (existed)
                {
                    File.Copy(LocalizationProjectIO.Resolve(root, relative), LocalizationProjectIO.Resolve(root, backup), false);
                    using FileStream retained = new(LocalizationProjectIO.Resolve(root, backup), FileMode.Open, FileAccess.Write, FileShare.Read);
                    retained.Flush(true);
                }
                using (FileStream staged = new(LocalizationProjectIO.Resolve(root, candidate), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    staged.Write(bytes);
                    staged.Flush(true);
                }
            }
            cancellation.ThrowIfCancellationRequested();
            using (FileStream journal = new(Path.Combine(root, Journal), FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                journal.Write(JsonSerializer.SerializeToUtf8Bytes(changes));
                journal.Flush(true);
            }
            journalWritten = true;
            foreach (FileChange change in changes)
            {
                if (change.Existed)
                {
                    File.Replace(LocalizationProjectIO.Resolve(root, change.Candidate), LocalizationProjectIO.Resolve(root, change.Destination), null);
                }
                else
                {
                    File.Move(LocalizationProjectIO.Resolve(root, change.Candidate), LocalizationProjectIO.Resolve(root, change.Destination));
                }
            }
            File.Delete(Path.Combine(root, Journal));
            journalWritten = false;
        }
        catch
        {
            foreach (FileStream original in originals)
            {
                original.Dispose();
            }
            originals.Clear();
            if (journalWritten)
            {
                Recover(root);
            }
            throw;
        }
        finally
        {
            foreach (FileStream original in originals)
            {
                original.Dispose();
            }
            if (!journalWritten)
            {
                Cleanup(root, changes);
            }
        }
    }
    private static void Cleanup(string root, IEnumerable<FileChange> changes)
    {
        foreach (FileChange change in changes)
        {
            File.Delete(LocalizationProjectIO.Resolve(root, change.Backup));
            File.Delete(LocalizationProjectIO.Resolve(root, change.Candidate));
        }
    }
}
