using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

public sealed partial class ModPackageInstaller
{
    private sealed record NativeManifest(int SchemaVersion, string SourceId, string ComponentId, string RuntimePackageId, string Version, string EngineVersionRange, OfficialModTarget Target, IReadOnlyList<OfficialModFile> Files);

    internal string OfficialDownloadsDirectory => StatePath("official-downloads", "");

    public static string StateDirectoryFor(string modsRoot)
    {
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modsRoot));
        string parent = Path.GetDirectoryName(root) ?? throw new ArgumentException("A managed mod directory must be below the volume root.", nameof(modsRoot));
        string identity = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16].ToLowerInvariant();
        return Path.Combine(parent, ".memoria-mod-library-" + hash);
    }

    public static ModPackageInstallResult? ReadOfficialInstallation(string modsRoot, OfficialModComponent component)
    {
        return new ModPackageInstaller(StateDirectoryFor(modsRoot), modsRoot, "").GetStatus(component.ToEntry());
    }

    public static IReadOnlyDictionary<string, ModPackageInstallResult> ReadOfficialInventory(string modsRoot)
    {
        Dictionary<string, ModPackageInstallResult> result = new(StringComparer.OrdinalIgnoreCase);
        string state = Path.Combine(StateDirectoryFor(modsRoot), "receipts");
        EnsureNoLinks(state);
        if (!Directory.Exists(state))
        {
            return result;
        }
        ModPackageInstaller installer = new(StateDirectoryFor(modsRoot), modsRoot, "");
        foreach (string path in Directory.EnumerateFiles(state, "managed-*.json"))
        {
            Receipt? receipt = Read<Receipt>(path);
            if (receipt?.Official is not { } component || !ValidNativeReceipt(receipt) || !Path.GetFileName(path).Equals(ManagedName(component.ToEntry()) + ".json", StringComparison.Ordinal))
            {
                continue;
            }
            result[component.RuntimePackageId] = installer.NativeResult(receipt);
        }
        return result;
    }

    // Caller holds the exclusive startup resource lease for this mod root.
    public static bool RecordMigratedGirlNextDoor(string modsRoot)
    {
        OfficialModComponent component = OfficialModCatalog.LegacyGirlNextDoor;
        ModPackageInstaller installer = new(StateDirectoryFor(modsRoot), modsRoot, "");
        using FileStream installLock = installer.AcquireLock();
        string key = ManagedName(component.ToEntry());
        string path = installer.ReceiptPath(key);
        if (File.Exists(path))
        {
            return Read<Receipt>(path) is { } existing && ValidNativeReceipt(existing);
        }
        if (File.Exists(installer.JournalPath(key)))
        {
            return false;
        }
        string name = component.RuntimePackageId["Memoria/".Length..];
        string destination = installer.Destination(OfficialModCatalog.SourceId, name);
        string artwork = Path.Combine(destination, "GirlNextDoor.png");
        EnsureNoLinks(artwork);
        if (!File.Exists(artwork))
        {
            return false;
        }
        const string originalDigest = "9a29943f97a87df3a6f8cfb2f7f415ce71ff5039f1efea4086b64ee3e2e5d219";
        using FileStream input = File.OpenRead(artwork);
        string digest = Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant();
        bool known = digest == originalDigest && SafeFiles(destination).All(file => file == artwork || file == Path.Combine(destination, "mod.ini"));
        Receipt receipt = new(1, Guid.NewGuid(), OfficialModCatalog.SourceId, component.CatalogId, known ? "1.0.1" : "", "", name, Fingerprint(destination, false), true)
        {
            Official = component,
            EngineVersionRange = known ? "[0.8.10,)" : null,
            Files = [new("GirlNextDoor.png", input.Length, digest, false)],
            Migrated = true
        };
        WriteAtomic(path, receipt with { InternalWriteTimes = CaptureWriteTimes(destination, receipt.Files!) });
        return true;
    }

    private ModPackageInstallPlan InspectNative(string archivePath, RemoteModEntry entry, CancellationToken cancellationToken)
    {
        OfficialModComponent component = entry.Official ?? throw new InvalidDataException("Missing official component metadata.");
        OfficialModRelease release = component.Latest ?? throw new InvalidDataException("Missing official release metadata.");
        if (entry.Id != component.CatalogId || entry.Version != release.Version || !OfficialModCatalog.IsRuntimeId(component.RuntimePackageId))
        {
            throw new InvalidDataException("Official component identifiers do not match the pinned catalog release.");
        }
        string? unavailable = CompatibilityError(entry);
        if (unavailable is not null)
        {
            throw new NotSupportedException(unavailable);
        }
        using (FileStream file = File.OpenRead(archivePath))
        {
            Span<byte> signature = stackalloc byte[4];
            file.ReadExactly(signature);
            file.Position = 0;
            if (!signature.SequenceEqual("PK\x03\x04"u8) || file.Length != release.Artifact.Size || !Convert.ToHexString(SHA256.HashData(file)).Equals(release.Artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Official package format, size or SHA-256 does not match the catalog.");
            }
        }
        Guid token = Guid.NewGuid();
        string root = StatePath("prepared", token.ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            EnsureNoLinks(root);
            string extracted = Path.Combine(root, "outer");
            Directory.CreateDirectory(extracted);
            ExtractWrapper(archivePath, extracted, cancellationToken, release.Artifact.UnpackedSize);
            string payload = Path.Combine(extracted, "payload");
            if (!Directory.Exists(payload) || SafeFiles(extracted).Any(file => !IsWithin(file, payload) && file != Path.Combine(extracted, "mod.manifest.json") && file != Path.Combine(extracted, "NOTICE.md")))
            {
                throw new InvalidDataException("Unexpected files outside the official payload.");
            }
            string manifestPath = Path.Combine(extracted, "mod.manifest.json");
            if (new FileInfo(manifestPath).Length > 1024 * 1024)
            {
                throw new InvalidDataException("Official manifest exceeds its size limit.");
            }
            NativeManifest? manifest = System.Text.Json.JsonSerializer.Deserialize<NativeManifest>(File.ReadAllBytes(manifestPath), OfficialModCatalog.Json);
            if (manifest is null || manifest.SchemaVersion != 1 || manifest.SourceId != entry.SourceId || manifest.ComponentId != component.Id || manifest.RuntimePackageId != component.RuntimePackageId
                || manifest.Version != release.Version || manifest.EngineVersionRange != release.EngineVersionRange || manifest.Files is not { Count: > 0 })
            {
                throw new InvalidDataException("Native manifest does not match the catalog release.");
            }
            OfficialModCatalog.ValidateTarget(manifest.Target);
            if (manifest.Target.Game != component.Target.Game || !manifest.Target.Editions.ToHashSet(StringComparer.Ordinal).SetEquals(component.Target.Editions)
                || !manifest.Target.Languages.ToHashSet(StringComparer.Ordinal).SetEquals(component.Target.Languages))
            {
                throw new InvalidDataException("Native manifest game target does not match the catalog.");
            }
            ValidateNativeFiles(payload, manifest.Files, cancellationToken);
            ValidateNativeContent(payload, component, manifest.Files);
            string stable = Path.Combine(root, "content");
            Directory.Move(payload, stable);
            Directory.Delete(extracted, true);
            long bytes = PayloadBytes(stable);
            CheckSpace(bytes);
            string hash = Fingerprint(stable, false, cancellationToken);
            prepared.Add(token, new(root, stable, hash, bytes, entry) { NativeFiles = manifest.Files });
            string name = component.RuntimePackageId["Memoria/".Length..];
            Receipt? previous = Read<Receipt>(ReceiptPath(ManagedName(entry)));
            if (previous is not null && CanUseInstalledDestination(component, previous))
            {
                name = previous.DestinationName;
            }
            string destination = Destination(entry.SourceId, name);
            return new(this, token, entry, targetProfile, component.RuntimePackageId, release.Version, destination, bytes);
        }
        catch
        {
            Directory.Delete(root, true);
            throw;
        }
    }

    private void ValidateNativeFiles(string payload, IReadOnlyList<OfficialModFile> inventory, CancellationToken token, string? preservedSettingsHash = null)
    {
        HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase);
        foreach (OfficialModFile file in inventory)
        {
            token.ThrowIfCancellationRequested();
            if (file is null || file.Path is null || SafeRelative(file.Path) != file.Path || !expected.Add(file.Path) || file.Size < 0 || !OfficialModCatalog.IsDigest(file.Sha256)
                || file.Mutable || file.Path.Split('/').Contains("mod.ini", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Invalid native payload file inventory.");
            }
            string path = Path.Combine(payload, file.Path.Replace('/', Path.DirectorySeparatorChar));
            using FileStream content = File.OpenRead(path);
            if (content.Length != file.Size || !Convert.ToHexString(SHA256.HashData(content)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Official payload file does not match its inventory.");
            }
        }
        if (Directory.EnumerateDirectories(payload, "*", SearchOption.AllDirectories).Any(path => Path.GetFileName(path).Equals("mod.ini", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Official mod packages cannot contain mod.ini.");
        }
        foreach (string path in SafeFiles(payload))
        {
            string relative = Path.GetRelativePath(payload, path).Replace('\\', '/');
            if (!expected.Contains(relative) && !(relative == "mod.ini" && preservedSettingsHash is not null && SettingsHash(path) == preservedSettingsHash))
            {
                throw new InvalidDataException("The native payload contains an unlisted file or bundled settings.");
            }
        }
    }

    private static void ValidateNativeContent(string payload, OfficialModComponent component, IReadOnlyList<OfficialModFile> inventory)
    {
        if (!inventory.Any(file => file.Path == NativeModDescriptor.FileName))
        {
            throw new InvalidDataException("The native payload must contain a root mod.json.");
        }
        NativeModDescriptor descriptor = NativeModDescriptor.Read(Path.Combine(payload, NativeModDescriptor.FileName));
        if (descriptor.Id != component.Id || descriptor.RuntimePackageId != component.RuntimePackageId || descriptor.Id != component.SettingsKey
            || descriptor.Group != component.GroupId || descriptor.Name != component.Name || descriptor.DescriptionForLanguage("en") != component.Description)
        {
            throw new InvalidDataException("The native descriptor does not match the pinned catalog metadata.");
        }

        string packageId = descriptor.RuntimePackageId;
        ModFile FileFromInventory(OfficialModFile file)
        {
            string absolute = Path.GetFullPath(file.Path, payload);
            return new(packageId, ModFormat.Memoria, file.Path, 0, () => File.ReadAllBytes(absolute))
            {
                AbsolutePath = absolute,
                ContentStamp = (file.Size, 0)
            };
        }

        ModPackage package = new(packageId, descriptor.Name, ModFormat.Memoria, null, true, ModEffectiveStatus.Active, payload) { Descriptor = descriptor };
        ModCatalogSnapshot catalog = new(0, [package], inventory.Select(FileFromInventory));
        // Startup preparation checks IDs against the selected game's actual item table.
        if (!ItemOverrideMods.TryPrepare(catalog, byte.MaxValue + 1, out _, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw new InvalidDataException(diagnostics[0].Message);
        }
        if (!StatusMods.TryPrepare(catalog, out _, out diagnostics))
        {
            throw new InvalidDataException(diagnostics[0].Message);
        }
        if (!GuardianForceRules.TryPrepare(catalog, out _, out diagnostics))
        {
            throw new InvalidDataException($"{diagnostics[0].RelativePath}: {diagnostics[0].Message}");
        }
    }

    private static bool CanUseInstalledDestination(OfficialModComponent component, Receipt previous)
    {
        return ValidNativeReceipt(previous) && previous.Official is { } installed && component.CatalogId == installed.CatalogId
            && (component.RuntimePackageId == installed.RuntimePackageId || OfficialModCatalog.CanUpgradeIdentity(installed, component));
    }

    public string? CompatibilityError(RemoteModEntry entry)
    {
        if (entry.SourceId != OfficialModCatalog.SourceId)
        {
            return junctionSupported ? null : "Junction downloads require Classic English game data.";
        }
        if (entry.Official is not { } component)
        {
            return "Official component metadata is unavailable.";
        }
        if (component.Latest is null)
        {
            return "The download is unavailable. Refresh the official catalog when online.";
        }
        return OfficialModCatalog.CompatibilityError(component, EngineBuildMetadata.CompatibilityVersion, edition, language)
            ?? (OfficialModCatalog.HasLegacyGirlNextDoorIdentity(component) ? "This release uses a retired native mod format. Refresh the official catalog for an update." : null);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Size, long Modified, string Digest)> verifiedFiles = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private static bool ValidNativeReceipt(Receipt receipt)
    {
        return receipt.Official is { } component && receipt.Schema == 1 && receipt.SourceId == OfficialModCatalog.SourceId && receipt.ModId == component.CatalogId
            && receipt.Folder && OfficialModCatalog.IsRuntimeId(component.RuntimePackageId)
            && (receipt.DestinationName == component.RuntimePackageId["Memoria/".Length..]
                || OfficialModCatalog.SameComponent(component.Id, OfficialModCatalog.GirlNextDoor.Id)
                    && receipt.DestinationName is OfficialModCatalog.LegacyGirlNextDoorSettingsKey or OfficialModCatalog.PreviousGirlNextDoorId)
            && receipt.Files is { Count: > 0 } && (receipt.EngineVersionRange is not null || receipt.Migrated);
    }

    private ModPackageInstallResult NativeResult(Receipt receipt)
    {
        string destination = Destination(receipt.SourceId, receipt.DestinationName);
        foreach (OfficialModFile file in receipt.Files!.Where(file => !file.Mutable))
        {
            string path = Path.Combine(destination, SafeRelative(file.Path).Replace('/', Path.DirectorySeparatorChar));
            EnsureNoLinks(path);
            FileInfo info = new(path);
            if (!info.Exists || receipt.Version.Length > 0 && (info.Length != file.Size || receipt.InternalWriteTimes?.GetValueOrDefault(file.Path) != info.LastWriteTimeUtc.Ticks && !MatchesNativeFile(path, info, file.Sha256)))
            {
                return Result(receipt, false, false, $"The installed file {file.Path} is missing or damaged.");
            }
        }
        return Result(receipt, false, true);
    }

    private static IReadOnlyDictionary<string, long> CaptureWriteTimes(string destination, IEnumerable<OfficialModFile> files)
    {
        return files.Where(file => !file.Mutable).ToDictionary(file => file.Path, file => File.GetLastWriteTimeUtc(Path.Combine(destination, file.Path.Replace('/', Path.DirectorySeparatorChar))).Ticks, StringComparer.OrdinalIgnoreCase);
    }

    private static bool MatchesNativeFile(string path, FileInfo info, string expected)
    {
        long length = info.Length;
        long modified = info.LastWriteTimeUtc.Ticks;
        if (!verifiedFiles.TryGetValue(path, out var stamp) || stamp.Size != length || stamp.Modified != modified)
        {
            using FileStream input = File.OpenRead(path);
            stamp = (length, modified, Convert.ToHexString(SHA256.HashData(input)));
            verifiedFiles[path] = stamp;
        }
        return stamp.Digest.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private string CentralSettingsPath => Path.Combine(preferenceRoot, "Memoria", ModRuntime.MemoriaSettingsFile);

    private System.Text.Json.Nodes.JsonObject ReadCentralSettings()
    {
        EnsureNoLinks(CentralSettingsPath);
        return File.Exists(CentralSettingsPath)
            ? System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(CentralSettingsPath))?.AsObject() ?? throw new InvalidDataException("Invalid Memoria settings.")
            : new();
    }

    private bool? ReadCentralPreference(string key)
    {
        var components = ReadCentralSettings()["components"];
        foreach (string candidate in OfficialModCatalog.PreferenceKeys(key))
        {
            if (components?[candidate] is { } value)
            {
                return value.GetValue<bool>();
            }
        }
        return null;
    }

    private bool ShouldEnable(Journal journal)
    {
        return journal.Enable && journal.Next.Official is { } component && ReadCentralPreference(component.SettingsKey) == journal.PreviousPreference;
    }

    public void CancelPendingEnable(RemoteModEntry entry)
    {
        if (entry.Official is not { } component)
        {
            return;
        }
        using FileStream installLock = AcquireLock();
        string path = JournalPath(ManagedName(entry));
        if (Read<Journal>(path) is { Enable: true, Phase: "PendingRestart" } journal)
        {
            ValidateJournal(journal, path);
            WriteAtomic(path, journal with { Enable = false });
            using FileStream settingsLock = ModConfiguration.LockMemoriaSettings(CentralSettingsPath);
            var settings = ReadCentralSettings();
            settings["components"] ??= new System.Text.Json.Nodes.JsonObject();
            settings["components"]![component.SettingsKey] = false;
            WriteTextAtomic(CentralSettingsPath, settings.ToJsonString());
        }
    }

    internal static bool HasPendingEnable(string modsRoot, OfficialModComponent component, string? preferenceRoot = null)
    {
        ModPackageInstaller installer = new(StateDirectoryFor(modsRoot), modsRoot, "", preferenceRoot: preferenceRoot);
        Journal? journal = Read<Journal>(installer.JournalPath(ManagedName(component.ToEntry())));
        return journal is { Phase: "AwaitingValidation" } && installer.ShouldEnable(journal);
    }

    private void CommitEnable(Journal journal)
    {
        if (!journal.Enable)
        {
            return;
        }

        using FileStream settingsLock = ModConfiguration.LockMemoriaSettings(CentralSettingsPath);
        if (!ShouldEnable(journal))
        {
            return;
        }
        string key = journal.Next.Official!.SettingsKey;
        var settings = ReadCentralSettings();
        settings["components"] ??= new System.Text.Json.Nodes.JsonObject();
        settings["components"]![key] = true;
        WriteTextAtomic(CentralSettingsPath, settings.ToJsonString());
    }

    private void VerifyNativeCompatibility(Receipt receipt)
    {
        if (!ValidNativeReceipt(receipt) || receipt.EngineVersionRange is null || !OfficialModCatalog.TargetMatches(receipt.Official!.Target, edition, language))
        {
            throw new NotSupportedException("This native package is unavailable for the selected game edition or language.");
        }
        if (OfficialModCatalog.EngineError(receipt.EngineVersionRange, EngineBuildMetadata.CompatibilityVersion) is { } error)
        {
            throw new NotSupportedException(error);
        }
    }
}
