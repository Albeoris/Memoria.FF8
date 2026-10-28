using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SharpCompress.Archives;

namespace Memoria.FF8.Mods;

public sealed record ModPackageInstallLimits(long ExpandedBytes = 256L * 1024 * 1024 * 1024, int FileCount = 250000, int PathDepth = 32, int ExpansionRatio = 1000);

public sealed class ModPackageInstallPlan
{
    internal ModPackageInstallPlan(Guid token, RemoteModEntry entry, string target, string packageId, string version, string destination, long bytes)
    {
        Token = token;
        Entry = entry;
        TargetProfile = target;
        PackageId = packageId;
        Version = version;
        Destination = destination;
        RequiredBytes = bytes;
    }

    internal Guid Token { get; }
    public RemoteModEntry Entry { get; }
    public string TargetProfile { get; }
    public string PackageId { get; }
    public string Version { get; }
    public string Destination { get; }
    public long RequiredBytes { get; }
    public bool RestartRequired => true;
}

public sealed record ModPackageInstallResult(Guid InstallationId, string PackageId, string Version, bool RestartRequired, bool Installed, string? Error = null);

/// <summary>Prepares immutable payloads off-thread. Applies queued changes only before runtime discovery at startup.</summary>
public sealed partial class ModPackageInstaller
{
    private readonly string stateRoot;
    private readonly string modsRoot;
    private readonly string targetProfile;
    private readonly bool junctionSupported;
    private readonly ModPackageInstallLimits limits;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<Guid, Prepared> prepared = [];
    private readonly record struct Prepared(string Root, string Payload, string Hash, long Bytes, RemoteModEntry Entry);

    public ModPackageInstaller(string stateRoot, string modsRoot, string targetProfile, bool junctionSupported = true, ModPackageInstallLimits? limits = null)
    {
        this.stateRoot = Path.GetFullPath(stateRoot);
        this.modsRoot = Path.GetFullPath(modsRoot);
        this.targetProfile = targetProfile;
        this.junctionSupported = junctionSupported;
        this.limits = limits ?? new();
        if (IsWithin(this.stateRoot, this.modsRoot) || IsWithin(this.modsRoot, this.stateRoot))
        {
            throw new ArgumentException("Installation state must be outside mod discovery roots.");
        }
        if (!Path.GetPathRoot(this.stateRoot)!.Equals(Path.GetPathRoot(this.modsRoot), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Installation staging must be on the destination volume.");
        }
    }

    public Task<ModPackageInstallPlan> InspectAsync(string archivePath, RemoteModEntry entry, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return Inspect(archivePath, entry, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
    }

    /// <summary>Removes only an uncommitted inspection owned by this installer; never touches transactions or backups.</summary>
    public Task DiscardAsync(ModPackageInstallPlan plan, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (plan.TargetProfile != targetProfile || !prepared.TryGetValue(plan.Token, out Prepared item))
                {
                    return;
                }
                string expected = StatePath("prepared", plan.Token.ToString("N"));
                string failedStage = StatePath("transactions", plan.Token.ToString("N"));
                bool uncommittedStage = item.Root.Equals(failedStage, StringComparison.OrdinalIgnoreCase) && !File.Exists(JournalPath(ManagedName(plan.Entry)));
                if (!item.Root.Equals(expected, StringComparison.OrdinalIgnoreCase) && !uncommittedStage)
                {
                    throw new InvalidDataException("The prepared plan does not belong to this installation state.");
                }
                EnsureNoLinks(item.Root);
                if (Directory.Exists(item.Root))
                {
                    Directory.Delete(item.Root, true);
                }
                prepared.Remove(plan.Token);
            }
            finally
            {
                gate.Release();
            }
        }, cancellationToken);
    }

    private ModPackageInstallPlan Inspect(string archivePath, RemoteModEntry entry, CancellationToken cancellationToken)
    {
        if (!junctionSupported)
        {
            throw new NotSupportedException("Junction packages are unsupported for this target edition.");
        }
        if (!string.IsNullOrWhiteSpace(entry.UnsupportedMetadata) || !string.IsNullOrWhiteSpace(entry.Requirements))
        {
            throw new NotSupportedException("This release requires unsupported catalog directives or dependency planning.");
        }
        Guid token = Guid.NewGuid();
        string root = StatePath("prepared", token.ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            EnsureNoLinks(root);
            string payload;
            using (FileStream input = File.OpenRead(archivePath))
            {
                Span<byte> header = stackalloc byte[4];
                input.ReadExactly(header);
                if (header.SequenceEqual("IROS"u8))
                {
                    payload = Path.Combine(root, "package.iroj");
                    CopyBounded(input, payload, cancellationToken, rewind: true);
                }
                else
                {
                    string extracted = Path.Combine(root, "outer");
                    Directory.CreateDirectory(extracted);
                    ExtractWrapper(archivePath, extracted, cancellationToken);
                    string[] archives = Directory.GetFiles(extracted, "*", SearchOption.AllDirectories).Where(path => path.EndsWith(".iroj", StringComparison.OrdinalIgnoreCase)).ToArray();
                    string[] manifests = Directory.GetFiles(extracted, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path).Equals("mod.xml", StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (archives.Length + manifests.Length != 1)
                    {
                        throw new NotSupportedException("The wrapper must contain exactly one Junction archive or unpacked mod.xml root. Multiple components require explicit mapping.");
                    }
                    payload = archives.Length == 1 ? archives[0] : Path.GetDirectoryName(manifests[0])!;
                    foreach (string extra in SafeFiles(extracted))
                    {
                        if (extra.Equals(payload, StringComparison.OrdinalIgnoreCase) || Directory.Exists(payload) && IsWithin(extra, payload))
                        {
                            continue;
                        }
                        string extension = Path.GetExtension(extra).ToLowerInvariant();
                        if (extension is not (".txt" or ".md" or ".png" or ".jpg" or ".jpeg" or ".gif"))
                        {
                            throw new NotSupportedException("The wrapper contains additional payloads outside its Junction package; explicit component mapping is required.");
                        }
                    }
                }
            }
            string stable = Path.Combine(root, Directory.Exists(payload) ? "content" : "content.iroj");
            if (Directory.Exists(payload))
            {
                Directory.Move(payload, stable);
            }
            else
            {
                File.Move(payload, stable);
            }
            JunctionModPackage package = ValidatePackage(stable, entry);
            ValidateInnerPayload(stable, cancellationToken);
            long bytes = PayloadBytes(stable);
            CheckSpace(bytes);
            string hash = Fingerprint(stable, false, cancellationToken);
            prepared.Add(token, new(root, stable, hash, bytes, entry));
            string name = ManagedName(entry);
            string destination = Path.Combine(modsRoot, "Junction", name + (Directory.Exists(stable) ? "" : ".iroj"));
            return new(token, entry, targetProfile, "Junction/" + Path.GetFileName(destination), package.Manifest.Version!, destination, bytes);
        }
        catch
        {
            Directory.Delete(root, true);
            throw;
        }
    }

    private JunctionModPackage ValidatePackage(string path, RemoteModEntry entry, IReadOnlyDictionary<string, int>? options = null)
    {
        if (!junctionSupported)
        {
            throw new NotSupportedException("Junction packages are unsupported for this target edition.");
        }
        JunctionModPackage package = JunctionModImporter.Open(path, options);
        if (package.Manifest.Id != entry.Id || !DecimalVersion(package.Manifest.Version, out decimal actual) || !DecimalVersion(entry.Version, out decimal expected) || actual != expected)
        {
            throw new InvalidDataException("Package GUID or release version does not match the pinned catalog release.");
        }
        if (package.Manifest.RequiredMods.Count > 0 || package.Manifest.ForbiddenMods.Count > 0 || package.Manifest.SettingConstraints.Count > 0)
        {
            throw new NotSupportedException("This package requires dependency or option relationship planning before installation.");
        }
        if (package.Manifest.ExternalMusicExtension is "psf" or "minipsf" || package.Files.Any(file => Path.GetExtension(file.RelativePath).Equals(".psf", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file.RelativePath).Equals(".minipsf", StringComparison.OrdinalIgnoreCase)))
        {
            JunctionReplacement? bios = package.Files.FirstOrDefault(file => file.RelativePath.Equals(package.Manifest.PsfBiosPath, StringComparison.OrdinalIgnoreCase));
            if (bios is null)
            {
                throw new NotSupportedException("PSF music requires an explicitly selected BIOS resource inside this package.");
            }
            using Stream stream = bios.OpenRead();
            byte[] bytes = new byte[524289];
            int count = stream.ReadAtLeast(bytes, bytes.Length, false);
            if (count != 524288 || !bytes.AsSpan(0x80, 19).SequenceEqual("Highly Experimental"u8))
            {
                throw new NotSupportedException("PSF music requires a validated Highly Experimental BIOS resource.");
            }
        }
        return package;
    }

    private static bool DecimalVersion(string? value, out decimal version)
    {
        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out version);
    }

    private void ExtractWrapper(string path, string destination, CancellationToken cancellationToken)
    {
        using IArchive archive = ArchiveFactory.OpenArchive(path);
        if (archive.Type is not (SharpCompress.Common.ArchiveType.Zip or SharpCompress.Common.ArchiveType.SevenZip))
        {
            throw new NotSupportedException("Only ZIP and 7z outer wrappers are supported.");
        }
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> casing = new(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        int count = 0;
        foreach (IArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = SafeRelative(entry.Key ?? "");
            if (!paths.Add(relative) || ++count > limits.FileCount || entry.IsEncrypted || entry.LinkTarget is not null || ((entry.Attrib ?? 0) & 0x400) != 0 || (((entry.Attrib ?? 0) >> 16) & 0xf000) == 0xa000)
            {
                throw new InvalidDataException("Archive contains duplicate paths, links, encrypted entries or too many files.");
            }
            string[] segments = relative.Split('/');
            for (int depth = 1; depth <= segments.Length; depth++)
            {
                string ancestor = string.Join('/', segments.Take(depth));
                if (casing.TryGetValue(ancestor, out string? known) && known != ancestor)
                {
                    throw new InvalidDataException("Archive contains case-colliding destination paths.");
                }
                casing[ancestor] = ancestor;
            }
            string target = Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(target);
                continue;
            }
            expanded = checked(expanded + entry.Size);
            if (expanded > limits.ExpandedBytes || expanded > Math.Max(new FileInfo(path).Length, 1) * (double)limits.ExpansionRatio)
            {
                throw new InvalidDataException("Archive exceeds the configured expansion limits.");
            }
            CheckSpace(entry.Size);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using Stream stream = entry.OpenEntryStream();
            long actual = CopyBounded(stream, target, cancellationToken, maximum: entry.Size, expectedCrc: entry.Crc is long crc ? unchecked((uint)crc) : null);
            if (actual != entry.Size)
            {
                throw new InvalidDataException("Archive entry size does not match its contents.");
            }
        }
    }

    private void ValidateInnerPayload(string path, CancellationToken cancellationToken)
    {
        if (Directory.Exists(path))
        {
            foreach (string file in SafeFiles(path))
            {
                SafeRelative(Path.GetRelativePath(path, file));
                EnsureNoLinks(file);
            }
            return;
        }
        JunctionArchive archive = new(path);
        if (archive.Entries.Count > limits.FileCount)
        {
            throw new InvalidDataException("Archive exceeds the configured file count limit.");
        }
        long total = 0;
        byte[] buffer = new byte[128 * 1024];
        foreach (JunctionArchiveEntry entry in archive.Entries)
        {
            SafeRelative(entry.RelativePath);
            using Stream stream = entry.OpenRead();
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                total = checked(total + read);
                if (total > limits.ExpandedBytes || total > Math.Max(new FileInfo(path).Length, 1) * (double)limits.ExpansionRatio)
                {
                    throw new InvalidDataException("IROJ exceeds the configured expansion limits.");
                }
            }
        }
    }

    private string SafeRelative(string path)
    {
        path = path.Replace('\\', '/').TrimEnd('/');
        string[] parts = path.Split('/');
        if (parts.Length > limits.PathDepth || parts.Any(part => part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || IsDevice(part)))
        {
            throw new InvalidDataException("Archive contains an unsafe destination path.");
        }
        return path;
    }

    private static bool IsDevice(string part)
    {
        string stem = part.Split('.')[0].ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" || stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] is >= '0' and <= '9';
    }

    private long CopyBounded(Stream input, string path, CancellationToken cancellationToken, bool rewind = false, long? maximum = null, uint? expectedCrc = null)
    {
        if (rewind)
        {
            input.Position = 0;
        }
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] buffer = new byte[128 * 1024];
        long total = 0;
        uint crc = uint.MaxValue;
        int read;
        while ((read = input.Read(buffer)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total = checked(total + read);
            if (total > limits.ExpandedBytes || maximum is long bound && total > bound)
            {
                throw new InvalidDataException("Payload exceeds the configured size limit.");
            }
            if (expectedCrc is not null)
            {
                for (int index = 0; index < read; index++)
                {
                    crc = CrcTable[(crc ^ buffer[index]) & 255] ^ (crc >> 8);
                }
            }
            output.Write(buffer, 0, read);
        }
        if (expectedCrc is uint expected && ~crc != expected)
        {
            throw new InvalidDataException("Archive entry CRC does not match its contents.");
        }
        output.Flush(true);
        return total;
    }

    private static readonly uint[] CrcTable = CreateCrcTable();

    private static uint[] CreateCrcTable()
    {
        uint[] result = new uint[256];
        for (uint index = 0; index < result.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
            {
                value = (value >> 1) ^ ((value & 1) == 0 ? 0 : 0xedb88320);
            }
            result[index] = value;
        }
        return result;
    }

    private void CheckSpace(long bytes)
    {
        DriveInfo drive = new(Path.GetPathRoot(stateRoot)!);
        if (drive.AvailableFreeSpace < bytes + 64L * 1024 * 1024)
        {
            throw new IOException($"Not enough free space to prepare installation. Required: {bytes + 64L * 1024 * 1024} bytes; available: {drive.AvailableFreeSpace} bytes.");
        }
    }

    private static IEnumerable<string> SafeFiles(string root)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.TryPop(out string? directory))
        {
            EnsureNoLinks(directory);
            foreach (string path in Directory.EnumerateFileSystemEntries(directory))
            {
                EnsureNoLinks(path);
                if (Directory.Exists(path))
                {
                    pending.Push(path);
                }
                else
                {
                    yield return path;
                }
            }
        }
    }

    private static long PayloadBytes(string path) => Directory.Exists(path) ? SafeFiles(path).Sum(file => new FileInfo(file).Length) : new FileInfo(path).Length;
    private static bool IsWithin(string path, string parent) => path.Equals(parent, StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private string StatePath(string category, string name) => Path.Combine(stateRoot, category, name);
    private static string ManagedName(RemoteModEntry entry) => "managed-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.SourceId)))[..16].ToLowerInvariant() + "-" + entry.Id.ToString("N");

    private static void EnsureNoLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Installation paths must not contain links or reparse points.");
            }
        }
    }

    private static string Fingerprint(string path, bool includeSettings, CancellationToken cancellationToken = default)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[128 * 1024];
        IEnumerable<string> files = Directory.Exists(path) ? SafeFiles(path).Order(StringComparer.OrdinalIgnoreCase) : [path];
        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureNoLinks(file);
            string relative = Directory.Exists(path) ? Path.GetRelativePath(path, file).Replace('\\', '/') : "payload";
            if (!includeSettings && relative.Equals("mod.ini", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            hash.AppendData(Encoding.UTF8.GetBytes(relative + "\0"));
            using FileStream input = File.OpenRead(file);
            using IncrementalHash fileHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                fileHash.AppendData(buffer, 0, read);
            }
            hash.AppendData(fileHash.GetHashAndReset());
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
