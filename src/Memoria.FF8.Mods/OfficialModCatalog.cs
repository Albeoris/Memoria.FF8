using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Memoria.FF8.Platform;
using NuGet.Versioning;

namespace Memoria.FF8.Mods;

public sealed record OfficialModTarget(string Game, IReadOnlyList<string> Editions, IReadOnlyList<string> Languages);
public sealed record OfficialModArtifact(string Url, long Size, long UnpackedSize, string Sha256);
public sealed record OfficialModRelease(string Version, string EngineVersionRange, string ReleaseTag, string ReleaseNotes, OfficialModArtifact Artifact)
{
    public IReadOnlyList<string>? RequiredCapabilities { get; init; }
}
public sealed record OfficialModFile(string Path, long Size, string Sha256, bool Mutable);
public sealed record OfficialModComponent(string Id, string GroupId, string RuntimePackageId, string SettingsKey, string Name, string Description, string Category, string Format, string Delivery, bool DefaultEnabled, OfficialModTarget Target, OfficialModRelease? Latest)
{
    public IReadOnlyList<string>? RequiredCapabilities { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public Guid CatalogId => OfficialModCatalog.CatalogId(Id);

    internal RemoteModEntry ToEntry() => new(OfficialModCatalog.SourceId, CatalogId, Name, Latest?.Version ?? "")
    {
        Official = this,
        Author = "Memoria",
        Category = Category,
        Description = Description,
        ReleaseNotes = Latest?.ReleaseNotes ?? "",
        PublishedSize = Latest?.Artifact.Size.ToString("N0", CultureInfo.InvariantCulture) + (Latest is null ? "" : " bytes"),
        Links = Latest is null ? [] : [Latest.Artifact.Url],
        Edition = string.Join(", ", Target.Editions),
        Language = string.Join(", ", Target.Languages)
    };
}

public sealed record OfficialCatalogSnapshot(long Revision, IReadOnlyList<OfficialModComponent> Components, DateTimeOffset? ValidatedAt, string? Error = null);

public static class OfficialModCatalog
{
    private sealed record Document(int SchemaVersion, string SourceId, long Revision, DateTimeOffset PublishedAt, IReadOnlyList<OfficialModComponent> Components, IReadOnlyList<string>? RequiredCapabilities = null);
    private const int MaximumBytes = 2 * 1024 * 1024;
    public const string SourceId = "memoria-official";
    private const string GirlNextDoorId = "memoria-girl-next-door";
    internal const string PreviousGirlNextDoorId = "girl-next-door";
    public const string LegacyGirlNextDoorRuntimePackageId = "Memoria/Girl Next Door";
    public const string LegacyGirlNextDoorSettingsKey = "Girl Next Door";
    public static Uri Feed { get; } = new("https://raw.githubusercontent.com/Albeoris/Memoria.Mods/catalog/catalog-v1.json");
    public static string DefaultCacheDirectory => Path.Combine(ApplicationPaths.Current.Cache, "mod-library", "official-catalog");
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);
    internal static IReadOnlyList<OfficialModComponent> SeedComponents { get; } = ReadSeed();
    public static OfficialModComponent GirlNextDoor { get; } = SeedComponents.Single(component => component.Id == GirlNextDoorId);
    internal static OfficialModComponent LegacyGirlNextDoor { get; } = GirlNextDoor with { Id = PreviousGirlNextDoorId, RuntimePackageId = LegacyGirlNextDoorRuntimePackageId, SettingsKey = LegacyGirlNextDoorSettingsKey };

    internal static bool HasDescriptorIdentity(OfficialModComponent component)
    {
        return NativeModDescriptor.IsId(component.Id) && component.RuntimePackageId == NativeModDescriptor.RuntimeId(component.Id) && component.SettingsKey == component.Id;
    }

    internal static bool HasLegacyGirlNextDoorIdentity(OfficialModComponent component)
    {
        return component.Id == PreviousGirlNextDoorId && component.RuntimePackageId == LegacyGirlNextDoorRuntimePackageId && component.SettingsKey == LegacyGirlNextDoorSettingsKey;
    }

    public static bool SameComponent(string? left, string? right)
    {
        return left is not null && right is not null && (left == PreviousGirlNextDoorId ? GirlNextDoorId : left) == (right == PreviousGirlNextDoorId ? GirlNextDoorId : right);
    }

    internal static bool CanUpgradeIdentity(OfficialModComponent previous, OfficialModComponent next)
    {
        return SameComponent(previous.Id, next.Id) && HasDescriptorIdentity(next)
            && (HasLegacyGirlNextDoorIdentity(previous)
                || previous.Id == PreviousGirlNextDoorId && HasDescriptorIdentity(previous) && next.Id == GirlNextDoor.Id);
    }

    internal static IEnumerable<string> PreferenceKeys(string key)
    {
        yield return key;
        if (key == GirlNextDoor.SettingsKey)
        {
            yield return PreviousGirlNextDoorId;
            yield return LegacyGirlNextDoorSettingsKey;
        }
        else if (key == PreviousGirlNextDoorId)
        {
            yield return LegacyGirlNextDoorSettingsKey;
        }
    }

    private static IReadOnlyList<OfficialModComponent> ReadSeed()
    {
        using Stream stream = typeof(OfficialModCatalog).Assembly.GetManifestResourceStream("Memoria.OfficialMods.Seed")!;
        return Array.AsReadOnly(JsonSerializer.Deserialize<OfficialModComponent[]>(stream, Json)!.Select(FreezeComponent).ToArray());
    }

    private static OfficialModComponent FreezeComponent(OfficialModComponent component) => component with
    {
        Target = component.Target with { Editions = Array.AsReadOnly(component.Target.Editions.ToArray()), Languages = Array.AsReadOnly(component.Target.Languages.ToArray()) }
    };

    public static Guid CatalogId(string componentId)
    {
        // Preserve the installed receipt and download identity across the official ID correction.
        string storageId = componentId == GirlNextDoorId ? PreviousGirlNextDoorId : componentId;
        return new(SHA256.HashData(Encoding.UTF8.GetBytes(SourceId + "\0" + storageId)).AsSpan(0, 16));
    }

    public static string? CompatibilityError(OfficialModComponent component, string? engineVersion, string edition, string language)
    {
        if (!TargetMatches(component.Target, edition, language))
        {
            return "This component is unavailable for this game edition or language.";
        }
        return component.Latest is null ? null : EngineError(component.Latest.EngineVersionRange, engineVersion);
    }

    public static bool TargetMatches(OfficialModTarget target, string edition, string language) =>
        target.Game == "FFVIII" && target.Editions.Contains(edition, StringComparer.OrdinalIgnoreCase) && (target.Languages.Contains("*") || target.Languages.Contains(language, StringComparer.OrdinalIgnoreCase));

    internal static string? EngineError(string required, string? engineVersion)
    {
        if (!NuGetVersion.TryParse(engineVersion, out NuGetVersion? engine))
        {
            return "The installed Memoria engine version is unknown.";
        }
        VersionRange range = ParseRange(required);
        return range.Satisfies(engine) ? null : $"Requires Memoria {range.MinVersion!.ToNormalizedString()} or later.";
    }

    internal static VersionRange ParseRange(string value)
    {
        if (!VersionRange.TryParse(value, out VersionRange? range) || range.MinVersion is null || !range.IsMinInclusive || range.HasUpperBound || range.Float is not null)
        {
            throw new InvalidDataException("An official component requires an inclusive minimum engine version.");
        }
        return range;
    }

    internal static bool IsDigest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    internal static bool IsRuntimeId(string? value)
    {
        if (value is null || !value.StartsWith("Memoria/", StringComparison.Ordinal) || value.Length > 136)
        {
            return false;
        }
        string name = value["Memoria/".Length..];
        return name.Length > 0 && name == name.Trim() && !name.EndsWith('.') && Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9 ._-]*$") && !Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(\.|$)", RegexOptions.IgnoreCase);
    }

    internal static void ValidateTarget(OfficialModTarget target)
    {
        if (target is null || target.Game != "FFVIII" || target.Editions is not { Count: > 0 } || target.Editions.Any(edition => edition is not ("Classic" or "Remastered"))
            || target.Languages is not { Count: > 0 } || target.Languages.Any(language => language is null || !Regex.IsMatch(language, @"^(\*|[a-z]{2,3}(-[a-zA-Z0-9]{2,8})*)$")))
        {
            throw new InvalidDataException("Invalid official mod target.");
        }
    }

    public static OfficialCatalogSnapshot Parse(byte[] data, DateTimeOffset? validatedAt = null)
    {
        if (data.Length > MaximumBytes)
        {
            throw new InvalidDataException("Official catalog exceeds its size limit.");
        }
        Document? document = JsonSerializer.Deserialize<Document>(data, Json);
        if (document is null || document.SchemaVersion != 1 || document.SourceId != SourceId || document.Revision < 1 || document.PublishedAt == default || document.Components is null || document.RequiredCapabilities is { Count: > 0 })
        {
            throw new InvalidDataException("The official catalog requires a supported schema and revision.");
        }
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<Guid> componentIds = [];
        HashSet<string> runtimeIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> settingsKeys = new(StringComparer.OrdinalIgnoreCase);
        List<OfficialModComponent> entries = [];
        foreach (OfficialModComponent component in document.Components)
        {
            if (component is null || !NativeModDescriptor.IsId(component.Id) || !ids.Add(component.Id) || !componentIds.Add(component.CatalogId)
                || !IsRuntimeId(component.RuntimePackageId) || !runtimeIds.Add(component.RuntimePackageId) || component.GroupId != "Memoria" || component.Format != "memoria-native" || component.Delivery != "download" || !component.DefaultEnabled
                || string.IsNullOrWhiteSpace(component.Name) || string.IsNullOrWhiteSpace(component.Description) || string.IsNullOrWhiteSpace(component.Category) || string.IsNullOrWhiteSpace(component.SettingsKey) || !settingsKeys.Add(component.SettingsKey))
            {
                throw new InvalidDataException("Invalid or duplicate official component identity or descriptor.");
            }
            if (component.RequiredCapabilities is { Count: > 0 } || component.Latest?.RequiredCapabilities is { Count: > 0 })
            {
                throw new InvalidDataException("An official component requires a newer catalog reader.");
            }
            ValidateTarget(component.Target);
            OfficialModRelease? release = component.Latest;
            if (release is null || !NuGetVersion.TryParse(release.Version, out NuGetVersion? version) || release.ReleaseTag != component.Id + "-v" + release.Version
                || release.Artifact is not { Size: > 0 and < 2147483648, UnpackedSize: > 0 } artifact || !IsDigest(artifact.Sha256)
                || !Uri.TryCreate(artifact.Url, UriKind.Absolute, out Uri? url) || url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length > 0)
            {
                throw new InvalidDataException("Invalid official component release or artifact.");
            }
            bool legacyIdentity = HasLegacyGirlNextDoorIdentity(component) && version <= new NuGetVersion(1, 0, 2);
            if ((!legacyIdentity && !HasDescriptorIdentity(component)) || ModRuntime.IsBundledMemoriaIdentity(component.RuntimePackageId, component.SettingsKey))
            {
                throw new InvalidDataException("Invalid official component runtime or settings identity.");
            }
            ParseRange(release.EngineVersionRange);
            entries.Add(FreezeComponent(component));
        }
        if (!componentIds.Contains(GirlNextDoor.CatalogId))
        {
            entries.Add(GirlNextDoor);
        }
        return new(document.Revision, entries.AsReadOnly(), validatedAt);
    }

    public static OfficialCatalogSnapshot ReadCached(string cacheRoot)
    {
        try
        {
            string path = Path.Combine(cacheRoot, "catalog-v1.json");
            using FileStream file = new(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (file.Length > MaximumBytes)
            {
                throw new InvalidDataException("Official catalog exceeds its size limit.");
            }
            byte[] raw = new byte[file.Length];
            file.ReadExactly(raw);
            return Parse(raw, File.GetLastWriteTimeUtc(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new(0, Array.AsReadOnly(new[] { GirlNextDoor }), null, exception is FileNotFoundException or DirectoryNotFoundException ? null : exception.Message);
        }
    }

    public static async Task<OfficialCatalogSnapshot> RefreshAsync(string cacheRoot, HttpClient? client = null, bool force = false, CancellationToken cancellationToken = default, bool bypassDebounce = false)
    {
        OfficialCatalogSnapshot saved = ReadCached(cacheRoot);
        if (!bypassDebounce && DateTimeOffset.UtcNow - saved.ValidatedAt < (force ? TimeSpan.FromSeconds(20) : TimeSpan.FromHours(6)))
        {
            return saved;
        }
        try
        {
            Directory.CreateDirectory(cacheRoot);
            string lockPath = Path.Combine(cacheRoot, "catalog.lock");
            using FileStream? cacheLock = await AcquireCacheLock(lockPath, cancellationToken).ConfigureAwait(false);
            if (cacheLock is null)
            {
                return ReadCached(cacheRoot) with { Error = "Another process is checking official mods." };
            }
            saved = ReadCached(cacheRoot);
            if (!bypassDebounce && DateTimeOffset.UtcNow - saved.ValidatedAt < (force ? TimeSpan.FromSeconds(20) : TimeSpan.FromHours(6)))
            {
                return saved;
            }
            using HttpClient? ownedClient = client is null ? ModDownloadTransport.CreateClient() : null;
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            using HttpResponseMessage response = await (client ?? ownedClient!).GetAsync(Feed, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using Stream input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using MemoryStream buffer = new();
            byte[] chunk = new byte[16384];
            int length;
            while ((length = await input.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + length > MaximumBytes)
                {
                    throw new InvalidDataException("Official catalog exceeds its size limit.");
                }
                buffer.Write(chunk, 0, length);
            }
            byte[] raw = buffer.ToArray();
            OfficialCatalogSnapshot next = Parse(raw, DateTimeOffset.UtcNow);
            string path = Path.Combine(cacheRoot, "catalog-v1.json");
            if (next.Revision < saved.Revision || next.Revision == saved.Revision && !raw.AsSpan().SequenceEqual(File.ReadAllBytes(path)))
            {
                throw new InvalidDataException("The official catalog revision conflicts with saved information.");
            }
            foreach (OfficialModComponent component in next.Components.Where(component => component.Latest is not null))
            {
                OfficialModComponent? prior = saved.Components.FirstOrDefault(existing => existing.CatalogId == component.CatalogId);
                if (prior is not null && (prior.RuntimePackageId != component.RuntimePackageId || prior.SettingsKey != component.SettingsKey)
                    && !(prior.Latest is null && SameComponent(prior.Id, component.Id))
                    && !CanUpgradeIdentity(prior, component))
                {
                    throw new InvalidDataException("An official component changed its runtime or settings identity.");
                }
            }
            string temporary = path + "." + Guid.NewGuid().ToString("N");
            try
            {
                await File.WriteAllBytesAsync(temporary, raw, cancellationToken).ConfigureAwait(false);
                File.Move(temporary, path, true);
            }
            finally
            {
                File.Delete(temporary);
            }
            return next;
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException or UnauthorizedAccessException or JsonException or InvalidDataException || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return saved with { Error = exception.Message };
        }
    }

    private static async Task<FileStream?> AcquireCacheLock(string path, CancellationToken token)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            try
            {
                return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }
        return null;
    }
}
