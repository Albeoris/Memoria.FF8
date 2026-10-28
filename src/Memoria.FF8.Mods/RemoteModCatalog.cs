using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Memoria.FF8.Mods;

public sealed record RemoteModEntry(string SourceId, Guid Id, string Name, string Version)
{
    public bool IsStale { get; init; }
    public string Author { get; init; } = "";
    public string MetaVersion { get; init; } = "";
    public string Category { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Links { get; init; } = Array.Empty<string>();
    public string Edition { get; init; } = "All";
    public string Language { get; init; } = "English (catalog default)";
    public string ReleaseNotes { get; init; } = "";
    public string PublishedSize { get; init; } = "";
    public string PreviewUrl { get; init; } = "";
    public string Requirements { get; init; } = "";
    public string UnsupportedMetadata { get; init; } = "";

    public static int? CompareRelease(string left, string right)
    {
        return decimal.TryParse(left, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal a) && decimal.TryParse(right, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal b) ? a.CompareTo(b) : null;
    }
}

public sealed record RemoteCatalogSnapshot(IReadOnlyList<RemoteModEntry> Entries, DateTimeOffset? FetchedAt, bool IsRefreshing, bool IsStale, string? Error)
{
    public DateTimeOffset? LastValidatedAt { get; init; }
}

/// <summary>Owns the Junction feed semantics; catalog estimates are never transfer byte totals.</summary>
public static class RemoteModCatalogAdapter
{
    public const string SourceId = "junction-viii";
    public static Uri DefaultFeed { get; } = new("https://codeload.github.com/tsunamods-codes/Junction-VIII-Catalogs/zip/refs/heads/master");
    public const int MaximumCatalogBytes = 16 * 1024 * 1024;
    public const int MaximumDocumentBytes = 2 * 1024 * 1024;

    public static IReadOnlyList<RemoteModEntry> Parse(byte[] data) => Parse(data, out _);

    public static IReadOnlyList<RemoteModEntry> Parse(byte[] data, out int rejectedCount, IReadOnlyList<RemoteModEntry>? previous = null)
    {
        if (data.Length > MaximumCatalogBytes)
        {
            throw new InvalidDataException("Catalog exceeds its size limit.");
        }
        List<RemoteModEntry> entries = [];
        int rejected = 0;
        if (data.Length >= 2 && data[0] == 'P' && data[1] == 'K')
        {
            using ZipArchive archive = new(new MemoryStream(data), ZipArchiveMode.Read);
            long expanded = 0;
            foreach (ZipArchiveEntry item in archive.Entries.Where(item => item.FullName.Contains("/mods/", StringComparison.Ordinal) && item.FullName.EndsWith("/mod.xml", StringComparison.Ordinal)).OrderBy(item => item.FullName, StringComparer.Ordinal))
            {
                expanded += item.Length;
                if (item.Length > MaximumDocumentBytes || expanded > MaximumCatalogBytes)
                {
                    throw new InvalidDataException("Catalog document expansion exceeds its limit.");
                }
                using Stream stream = item.Open();
                ReadDocument(stream, entries, ref rejected, previous);
            }
            if (entries.Count == 0)
            {
                throw new InvalidDataException("Catalog archive contains no valid mod documents.");
            }
        }
        else
        {
            using MemoryStream stream = new(data);
            ReadDocument(stream, entries, ref rejected, previous);
        }
        if (rejected > 0 && entries.All(entry => entry.IsStale))
        {
            throw new InvalidDataException("Every catalog entry was rejected.");
        }
        if (entries.Select(entry => entry.Id).Distinct().Count() != entries.Count)
        {
            throw new InvalidDataException("Catalog contains duplicate mod identities.");
        }
        rejectedCount = rejected;
        return Array.AsReadOnly(entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static void ReadDocument(Stream stream, List<RemoteModEntry> entries, ref int rejected, IReadOnlyList<RemoteModEntry>? previous)
    {
        using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumDocumentBytes });
        XElement root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Catalog has no root.");
        if (root.Name.Namespace != XNamespace.None || (root.Name.LocalName != "Mod" && root.Name.LocalName != "Catalog"))
        {
            throw new InvalidDataException("Catalog schema is unsupported.");
        }
        IEnumerable<XElement> mods = root.Name == "Mod" ? [root] : root.Element("Mods")?.Elements("Mod") ?? throw new InvalidDataException("Catalog is missing its Mods collection.");
        foreach (XElement mod in mods)
        {
            string Read(string name) => mod.Element(name)?.Value.Trim() ?? "";
            string ReleaseRead(string name) => mod.Element("LatestVersion")?.Element(name)?.Value.Trim() ?? Read(name);
            XElement? release = mod.Element("LatestVersion");
            string name = Read("Name");
            string version = release?.Element("Version")?.Value.Trim() ?? "";
            string[] links = release?.Elements("Link").Select(link => link.Value.Trim()).Where(link => link.Length > 0).ToArray() ?? [];
            bool validId = Guid.TryParse(Read("ID"), out Guid id);
            if (!validId || name.Length == 0 || version.Length == 0 || version.Length > 64 || links.Length == 0 || links.Length > 16 || links.Any(link => link.Length > 4096))
            {
                rejected++;
                if (validId && previous?.FirstOrDefault(entry => entry.Id == id && entry.SourceId == SourceId) is RemoteModEntry old)
                {
                    entries.Add(old with { IsStale = true });
                }
                continue;
            }
            entries.Add(new(SourceId, id, Limit(name, 256), version)
            {
                Author = Limit(Read("Author"), 256), MetaVersion = Limit(Read("MetaVersion"), 64), Category = Limit(Read("Category"), 128), Description = PlainText(Read("Description")), Links = Array.AsReadOnly(links),
                Edition = ReleaseRead("CompatibleGameVersions") is { Length: > 0 } edition ? Limit(edition, 128) : "All", Language = ReleaseRead("Language") is { Length: > 0 } language ? Limit(language, 128) : "English (catalog default)",
                ReleaseNotes = PlainText(release?.Element("ReleaseNotes")?.Value ?? ""), PublishedSize = Limit(release?.Element("DownloadSize")?.Value ?? "", 128), PreviewUrl = ReleaseRead("PreviewImage") is { Length: <= 4096 } preview ? preview : "", Requirements = Limit(mod.Element("Requirements")?.ToString(SaveOptions.DisableFormatting) ?? "", 32768),
                UnsupportedMetadata = Limit(string.Join(", ", mod.Descendants().Where(element => element.Name.LocalName is "Patch" or "Patches" or "ExtractInto" or "ExtractFiles" or "ApplyPatch" or "InstallCode" or "InstallScript" or "Execute" || element.Attributes().Any(attribute => attribute.Name.LocalName.Equals("critical", StringComparison.OrdinalIgnoreCase) && attribute.Value.Equals("true", StringComparison.OrdinalIgnoreCase))).Select(element => element.Name.LocalName).Distinct()), 4096)
            });
        }
    }
    private static string Limit(string value, int length) => value.Length > length ? value[..length] : value;

    private static string PlainText(string value)
    {
        string decoded = WebUtility.HtmlDecode(Limit(value, 32768));
        StringBuilder result = new(decoded.Length);
        bool tag = false;
        foreach (char character in decoded)
        {
            if (character == '<')
            {
                tag = true;
                continue;
            }
            if (character == '>' && tag)
            {
                tag = false;
                result.Append(' ');
                continue;
            }
            if (!tag)
            {
                result.Append(character);
            }
        }
        return result.ToString().Trim();
    }
}

public sealed class RemoteModCatalogCoordinator : IDisposable
{
    private sealed record CacheMetadata(int SchemaVersion, string RawFile, string RawSha256, DateTimeOffset? FetchedAt, DateTimeOffset LastValidatedAt, string? ETag, DateTimeOffset? LastModified, IReadOnlyList<RemoteModEntry> Entries);
    private const int MaximumMetadataBytes = 8 * 1024 * 1024;
    private readonly string cacheDirectory;
    private readonly HttpClient client;
    private readonly bool ownsClient;
    private readonly SemaphoreSlim gate = new(1);
    private CacheMetadata? metadata;
    private string? lastGoodRawFile;
    private RemoteCatalogSnapshot snapshot = new(Array.Empty<RemoteModEntry>(), null, false, true, null);
    public RemoteCatalogSnapshot Snapshot => Volatile.Read(ref snapshot);

    public RemoteModCatalogCoordinator(string cacheDirectory, HttpClient? client = null)
    {
        this.cacheDirectory = Path.GetFullPath(cacheDirectory);
        ownsClient = client == null;
        this.client = client ?? ModDownloadTransport.CreateClient();
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            string metadataPath = Path.Combine(cacheDirectory, "catalog.json");
            if (File.Exists(metadataPath))
            {
                try
                {
                    if (new FileInfo(metadataPath).Length > MaximumMetadataBytes)
                    {
                        throw new InvalidDataException("Catalog metadata exceeds its size limit.");
                    }
                    CacheMetadata saved = JsonSerializer.Deserialize<CacheMetadata>(await File.ReadAllBytesAsync(metadataPath, cancellationToken)) ?? throw new InvalidDataException("Catalog metadata is empty.");
                    if (saved.SchemaVersion != 1 || string.IsNullOrEmpty(saved.RawSha256) || saved.RawSha256.Length != 64 || !saved.RawSha256.All(Uri.IsHexDigit) || saved.RawFile != "catalog-" + saved.RawSha256 + ".raw" || saved.Entries == null || saved.Entries.Any(entry => entry is null) || saved.LastValidatedAt == default)
                    {
                        throw new InvalidDataException("Catalog metadata schema is unsupported.");
                    }
                    byte[] raw = await ReadRawAsync(saved.RawFile, cancellationToken);
                    if (!Digest(raw).Equals(saved.RawSha256, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Catalog cache identity does not match.");
                    }
                    IReadOnlyList<RemoteModEntry> entries = RemoteModCatalogAdapter.Parse(raw, out int rejected, saved.Entries);
                    metadata = saved;
                    lastGoodRawFile = saved.RawFile;
                    snapshot = new(entries, saved.FetchedAt, false, true, RejectionMessage(rejected, entries)) { LastValidatedAt = saved.LastValidatedAt };
                    return;
                }
                catch (Exception error) when (error is IOException or XmlException or JsonException or InvalidDataException or ArgumentException)
                {
                    // Raw generations remain independently reparsable if normalized metadata is incompatible.
                }
            }
            if (Directory.Exists(cacheDirectory))
            {
                foreach (string path in Directory.EnumerateFiles(cacheDirectory, "*.raw").Where(path => IsOwnedRaw(Path.GetFileName(path))).OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
                {
                    try
                    {
                        byte[] raw = await ReadRawAsync(Path.GetFileName(path), cancellationToken);
                        IReadOnlyList<RemoteModEntry> entries = RemoteModCatalogAdapter.Parse(raw);
                        lastGoodRawFile = Path.GetFileName(path);
                        snapshot = new(entries, null, false, true, "Cached catalog loaded; its last successful validation time is unknown.");
                        return;
                    }
                    catch (Exception error) when (error is IOException or XmlException or InvalidDataException)
                    {
                    }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            snapshot = snapshot with { Error = "The cached catalog could not be read. Refresh to try again." };
        }
        finally
        {
            gate.Release();
        }
    }, cancellationToken);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            return;
        }
        snapshot = snapshot with { IsRefreshing = true };
        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            bool conditional = metadata != null;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using HttpResponseMessage response = await ModDownloadTransport.OpenAsync(client, RemoteModCatalogAdapter.DefaultFeed, null, null, false, timeout.Token, conditional ? metadata?.ETag : null, conditional ? metadata?.LastModified : null);
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    bool valid = false;
                    if (conditional && metadata != null)
                    {
                        try
                        {
                            valid = Digest(await ReadRawAsync(metadata.RawFile, timeout.Token)) == metadata.RawSha256;
                        }
                        catch (IOException)
                        {
                        }
                    }
                    if (!valid)
                    {
                        conditional = false;
                        if (attempt == 0)
                        {
                            continue;
                        }
                        throw new InvalidDataException("The server returned an unchanged catalog without a valid local cache.");
                    }
                    CacheMetadata updated = metadata! with { LastValidatedAt = DateTimeOffset.UtcNow, ETag = response.Headers.ETag?.ToString() ?? metadata!.ETag, LastModified = response.Content.Headers.LastModified ?? metadata!.LastModified };
                    await SaveMetadataAsync(updated, timeout.Token);
                    metadata = updated;
                    snapshot = snapshot with { IsRefreshing = false, IsStale = false, LastValidatedAt = updated.LastValidatedAt, Error = snapshot.Entries.Any(entry => entry.IsStale) ? "Some catalog entries retain previous metadata as stale." : null };
                    return;
                }
                response.EnsureSuccessStatusCode();
                if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentEncoding.Count > 0 || response.Content.Headers.ContentLength > RemoteModCatalogAdapter.MaximumCatalogBytes)
                {
                    throw new InvalidDataException("Catalog response metadata is unsupported.");
                }
                using Stream body = await response.Content.ReadAsStreamAsync(timeout.Token);
                using MemoryStream buffer = new();
                byte[] chunk = new byte[65536];
                int count;
                while ((count = await body.ReadAsync(chunk, timeout.Token)) > 0)
                {
                    if (buffer.Length + count > RemoteModCatalogAdapter.MaximumCatalogBytes)
                    {
                        throw new InvalidDataException("Catalog exceeds its size limit.");
                    }
                    buffer.Write(chunk, 0, count);
                }
                byte[] raw = buffer.ToArray();
                int rejected = 0;
                IReadOnlyList<RemoteModEntry> candidate = await Task.Run(() => RemoteModCatalogAdapter.Parse(raw, out rejected, snapshot.Entries), timeout.Token);
                string digest = Digest(raw);
                string rawFile = "catalog-" + digest + ".raw";
                Directory.CreateDirectory(cacheDirectory);
                foreach (string old in Directory.EnumerateFiles(cacheDirectory, "*.raw").Where(path => IsOwnedRaw(Path.GetFileName(path)) && Path.GetFileName(path) != lastGoodRawFile && Path.GetFileName(path) != rawFile))
                {
                    File.Delete(old);
                }
                // Content-addressed raw generations are written before the atomic metadata pointer changes.
                // An interruption leaves the preceding metadata and raw generation usable.
                await WriteAtomicAsync(Path.Combine(cacheDirectory, rawFile), raw, timeout.Token);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                CacheMetadata next = new(1, rawFile, digest, now, now, response.Headers.ETag?.ToString(), response.Content.Headers.LastModified, candidate);
                await SaveMetadataAsync(next, timeout.Token);
                metadata = next;
                lastGoodRawFile = rawFile;
                snapshot = new(candidate, now, false, false, RejectionMessage(rejected, candidate)) { LastValidatedAt = now };
                return;
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or UnauthorizedAccessException or XmlException or JsonException or InvalidDataException or OperationCanceledException)
        {
            snapshot = snapshot with { IsRefreshing = false, IsStale = true, Error = "Catalog unavailable. The last good catalog remains available; retry Refresh." };
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<byte[]> ReadRawAsync(string file, CancellationToken token)
    {
        string path = Path.Combine(cacheDirectory, file);
        if (new FileInfo(path).Length > RemoteModCatalogAdapter.MaximumCatalogBytes)
        {
            throw new InvalidDataException("Cached catalog exceeds its size limit.");
        }
        return await File.ReadAllBytesAsync(path, token);
    }

    private async Task SaveMetadataAsync(CacheMetadata value, CancellationToken token)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length > MaximumMetadataBytes)
        {
            throw new InvalidDataException("Catalog metadata exceeds its size limit.");
        }
        await WriteAtomicAsync(Path.Combine(cacheDirectory, "catalog.json"), bytes, token);
    }

    private static async Task WriteAtomicAsync(string path, byte[] data, CancellationToken token)
    {
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, "catalog-write.tmp");
        using (FileStream output = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await output.WriteAsync(data, token);
            await output.FlushAsync(token);
            output.Flush(true);
        }
        File.Move(temporary, path, true);
    }

    private static string Digest(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    private static bool IsOwnedRaw(string name) => name == "catalog.raw" || name.Length == 76 && name.StartsWith("catalog-", StringComparison.Ordinal) && name.EndsWith(".raw", StringComparison.Ordinal) && name.AsSpan(8, 64).ToString().All(Uri.IsHexDigit);
    private static string? RejectionMessage(int rejected, IReadOnlyList<RemoteModEntry> entries) => rejected > 0 ? $"{rejected} invalid catalog entries; {entries.Count(entry => entry.IsStale)} previous records retained as stale." : null;

    public void Dispose()
    {
        if (ownsClient)
        {
            client.Dispose();
        }
    }
}
