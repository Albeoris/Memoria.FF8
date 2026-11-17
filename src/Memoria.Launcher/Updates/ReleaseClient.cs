using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using NuGet.Versioning;

namespace Memoria.Launcher;

internal enum UpdateChannel
{
    Release,
    Nightly
}

internal sealed record ReleaseCatalogEntry(string Tag, string Name, DateTimeOffset UpdatedAt)
{
    public override string ToString() => $"{Tag} | {UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm zzz}";
}

internal sealed record UpdateRelease(UpdateChannel Channel, string Tag, string Name, DateTimeOffset UpdatedAt, long AssetId, long Size, string Sha256, string? BuildId)
{
    public string RuntimeIdentifier { get; init; } = "win-x64";
    public Uri? DirectDownloadUri { get; init; }
    public string? EngineCompatibilityVersion { get; init; }
    public Uri DownloadUri => DirectDownloadUri ?? new($"https://api.github.com/repos/Albeoris/Memoria.FF8/releases/assets/{AssetId}");
    public bool HasArtifact => (DirectDownloadUri is not null || AssetId > 0) && Size > 0 && Sha256.Length == 64 && Sha256.All(Uri.IsHexDigit);
}

internal sealed class ReleaseClient(HttpClient client, string? runtimeIdentifier = null)
{
    private const string RepositoryApi = "https://api.github.com/repos/Albeoris/Memoria.FF8";
    private const string ReleaseDownloadRoot = "https://github.com/Albeoris/Memoria.FF8/releases/download/";
    internal const string StaticFeed = "https://raw.githubusercontent.com/Albeoris/Memoria.FF8/updates/updates-v1.json";
    internal static string CurrentRuntimeIdentifier => Memoria.FF8.Platform.HostPlatforms.RuntimeIdentifier(Memoria.FF8.Platform.HostPlatforms.Current);
    private readonly string rid = runtimeIdentifier ?? CurrentRuntimeIdentifier;
    internal static string AssetNameFor(string rid) => rid switch
    {
        "win-x64" => "Memoria-FF8-win-x64.zip",
        "linux-x64" => "Memoria-FF8-linux-x64.tar.gz",
        "osx-x64" => "Memoria-FF8-osx-x64.tar.gz",
        "osx-arm64" => "Memoria-FF8-osx-arm64.tar.gz",
        _ => throw new ArgumentOutOfRangeException(nameof(rid))
    };

    private static string AssetLabelPrefixFor(string rid) => rid switch
    {
        "win-x64" => "Windows_x64_",
        "linux-x64" => "Linux_x64_",
        "osx-x64" => "macOS_Intel_x64_",
        "osx-arm64" => "macOS_Apple_Silicon_arm64_",
        _ => throw new ArgumentOutOfRangeException(nameof(rid))
    };

    public async Task<UpdateRelease?> GetAsync(UpdateChannel channel, CancellationToken cancellationToken = default)
    {
        if (channel == UpdateChannel.Release)
        {
            return await GetGitHubAsync(channel, cancellationToken).ConfigureAwait(false);
        }
        using HttpRequestMessage request = CreateRequest(new Uri(StaticFeed), "application/json");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return await GetGitHubAsync(channel, cancellationToken).ConfigureAwait(false);
        }
        EnsureSuccess(response);
        await response.Content.LoadIntoBufferAsync(128 * 1024, cancellationToken).ConfigureAwait(false);
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        return ReadStaticRelease(document.RootElement, channel) ?? await GetGitHubAsync(channel, cancellationToken).ConfigureAwait(false);
    }

    private UpdateRelease? ReadStaticRelease(JsonElement root, UpdateChannel channel)
    {
        string key = channel switch
        {
            UpdateChannel.Release => "release",
            UpdateChannel.Nightly => "nightly",
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out JsonElement schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out int version) || version != 1 || !root.TryGetProperty("channels", out JsonElement channels) || channels.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The update feed is invalid or requires a newer launcher.");
        }
        if (!channels.TryGetProperty(key, out JsonElement release))
        {
            return null;
        }
        if (release.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The update feed contains invalid channel metadata.");
        }
        string tag = ReadString(release, "tag");
        string name = ReadString(release, "name");
        string buildId = ReadString(release, "buildId");
        string compatibilityVersion = ReadString(release, "engineCompatibilityVersion");
        if ((channel == UpdateChannel.Nightly ? tag != "nightly" : !IsStableTag(tag)) || (!IsHex(buildId, 40) && !IsHex(buildId, 64)) || !NuGetVersion.TryParse(compatibilityVersion, out _) || !release.TryGetProperty("publishedAt", out JsonElement date) || date.ValueKind != JsonValueKind.String || !date.TryGetDateTimeOffset(out DateTimeOffset publishedAt))
        {
            throw new InvalidDataException("The update feed contains invalid release metadata.");
        }
        if (!release.TryGetProperty("artifacts", out JsonElement artifacts) || artifacts.ValueKind != JsonValueKind.Object || !artifacts.TryGetProperty(rid, out JsonElement package) || package.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The update feed does not contain the selected platform archive.");
        }
        string url = ReadString(package, "url");
        string digest = ReadString(package, "sha256");
        if (!string.Equals(url, ReleaseDownloadRoot + tag + "/" + AssetNameFor(rid), StringComparison.Ordinal) || !IsHex(digest, 64) || !package.TryGetProperty("size", out JsonElement length) || length.ValueKind != JsonValueKind.Number || !length.TryGetInt64(out long size) || size <= 0)
        {
            throw new InvalidDataException("The update feed contains invalid platform archive metadata.");
        }
        return new UpdateRelease(channel, tag, name, publishedAt, 0, size, digest.ToLowerInvariant(), buildId.ToLowerInvariant())
        {
            RuntimeIdentifier = rid,
            DirectDownloadUri = new Uri(url),
            EngineCompatibilityVersion = compatibilityVersion
        };
    }

    private static bool IsStableTag(string tag) => tag.Length >= 2 && tag[0] == 'v' && Version.TryParse(tag.AsSpan(1), out _);

    public async Task<IReadOnlyList<ReleaseCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<string, ReleaseCatalogEntry> releases = new(StringComparer.Ordinal);
        for (int page = 1; ; page++)
        {
            using HttpRequestMessage request = CreateRequest(new Uri($"{RepositoryApi}/releases?per_page=100&page={page}"), "application/vnd.github+json");
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("The update server returned an invalid release catalog.");
            }
            foreach (JsonElement release in document.RootElement.EnumerateArray())
            {
                if (release.ValueKind != JsonValueKind.Object || !release.TryGetProperty("draft", out JsonElement draft) || draft.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new InvalidDataException("The update server returned invalid release metadata.");
                }
                if (draft.ValueKind == JsonValueKind.True)
                {
                    continue;
                }
                string tag = ReadString(release, "tag_name");
                if (!release.TryGetProperty("updated_at", out JsonElement updated) || updated.ValueKind != JsonValueKind.String || !updated.TryGetDateTimeOffset(out DateTimeOffset updatedAt))
                {
                    throw new InvalidDataException("The release has no valid update time.");
                }
                string name = release.TryGetProperty("name", out JsonElement title) && title.ValueKind == JsonValueKind.String ? title.GetString() ?? tag : tag;
                ReleaseCatalogEntry entry = new(tag, name, updatedAt);
                if (!releases.TryGetValue(tag, out ReleaseCatalogEntry? existing) || updatedAt > existing.UpdatedAt)
                {
                    releases[tag] = entry;
                }
            }
            if (document.RootElement.GetArrayLength() < 100)
            {
                break;
            }
        }
        return releases.Values.OrderByDescending(release => release.UpdatedAt).ThenBy(release => release.Tag, StringComparer.Ordinal).ToArray();
    }

    public Task<UpdateRelease?> GetByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        UpdateChannel channel = tag == "nightly" ? UpdateChannel.Nightly : UpdateChannel.Release;
        return GetReleaseAsync("tags/" + Uri.EscapeDataString(tag), channel, tag, cancellationToken);
    }

    private Task<UpdateRelease?> GetGitHubAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        return channel switch
        {
            UpdateChannel.Release => GetReleaseAsync("latest", channel, null, cancellationToken),
            UpdateChannel.Nightly => GetReleaseAsync("tags/nightly", channel, "nightly", cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };
    }

    private async Task<UpdateRelease?> GetReleaseAsync(string endpoint, UpdateChannel channel, string? expectedTag, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(new Uri($"{RepositoryApi}/releases/{endpoint}"), "application/vnd.github+json");
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        EnsureSuccess(response);
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement release = document.RootElement;
        if (release.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The update server returned invalid release metadata.");
        }
        string tag = ReadString(release, "tag_name");
        if (!release.TryGetProperty("draft", out JsonElement draft) || draft.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException("The update server returned a draft or invalid release.");
        }
        if (expectedTag is not null && tag != expectedTag)
        {
            throw new InvalidDataException("The update server did not return the selected release tag.");
        }
        if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The release does not contain an update archive. Try checking again after publication finishes.");
        }
        JsonElement? selected = null;
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object || !asset.TryGetProperty("name", out JsonElement name) || name.ValueKind != JsonValueKind.String || name.GetString() != AssetNameFor(rid))
            {
                continue;
            }
            if (selected.HasValue)
            {
                throw new InvalidDataException("The release contains duplicate update archives.");
            }
            selected = asset;
        }
        if (selected is not JsonElement package)
        {
            throw new InvalidDataException("The release does not contain the selected platform update archive. Try checking again after publication finishes.");
        }
        if (ReadString(package, "state") != "uploaded" || !package.TryGetProperty("id", out JsonElement id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out long assetId) || assetId <= 0 || !package.TryGetProperty("size", out JsonElement length) || length.ValueKind != JsonValueKind.Number || !length.TryGetInt64(out long size) || size <= 0)
        {
            throw new InvalidDataException("The update archive is incomplete. Try checking again after publication finishes.");
        }
        string digest = ReadString(package, "digest");
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || !IsHex(digest.AsSpan(7), 64))
        {
            throw new InvalidDataException("GitHub did not provide a valid SHA-256 digest for the update archive. Try checking again later.");
        }
        if (!package.TryGetProperty("updated_at", out JsonElement updated) || updated.ValueKind != JsonValueKind.String || !updated.TryGetDateTimeOffset(out DateTimeOffset updatedAt))
        {
            throw new InvalidDataException("The update archive has no valid publication time.");
        }
        string? buildId = package.TryGetProperty("label", out JsonElement label) && label.ValueKind == JsonValueKind.String ? label.GetString() : null;
        string platformPrefix = AssetLabelPrefixFor(rid);
        if (buildId?.StartsWith(platformPrefix, StringComparison.Ordinal) == true)
        {
            buildId = buildId[platformPrefix.Length..];
        }
        else if (buildId?.StartsWith("build:", StringComparison.Ordinal) == true)
        {
            buildId = buildId[6..];
        }
        if (buildId is not null && !IsHex(buildId.AsSpan(), 40) && !IsHex(buildId.AsSpan(), 64))
        {
            buildId = null;
        }
        string releaseName = release.TryGetProperty("name", out JsonElement title) && title.ValueKind == JsonValueKind.String ? title.GetString()! : tag;
        return new UpdateRelease(channel, tag, releaseName, updatedAt, assetId, size, digest[7..].ToLowerInvariant(), buildId?.ToLowerInvariant()) { RuntimeIdentifier = rid };
    }

    public async Task DownloadAsync(UpdateRelease release, string destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (!release.HasArtifact)
        {
            throw new InvalidDataException("The update archive metadata is invalid. Check for updates again.");
        }
        bool created = false;
        try
        {
            using HttpRequestMessage request = CreateRequest(release.DownloadUri, "application/octet-stream");
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(response);
            if (response.Content.Headers.ContentLength is long contentLength && contentLength != release.Size)
            {
                throw new InvalidDataException("The update download size changed. Check for updates again.");
            }
            await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            created = true;
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[81920];
            long total = 0;
            progress?.Report(0);
            while (true)
            {
                int count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }
                if (count > release.Size - total)
                {
                    throw new InvalidDataException("The downloaded archive is larger than the verified release metadata.");
                }
                await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, count);
                total += count;
                progress?.Report((double)total / release.Size);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (total != release.Size || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), Convert.FromHexString(release.Sha256)))
            {
                throw new InvalidDataException("The downloaded update failed its size or SHA-256 integrity check. Check for updates and download it again.");
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (created)
            {
                File.Delete(destination);
            }
            throw;
        }
    }

    private static HttpRequestMessage CreateRequest(Uri uri, string accept)
    {
        HttpRequestMessage request = new(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("Memoria-FF8-Launcher");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        if (uri.Host == "api.github.com")
        {
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        }
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }
        string message = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            ? "GitHub temporarily refused the update request or its API rate limit was reached. Wait and try again later."
            : $"GitHub update request failed (HTTP {(int)response.StatusCode}). Check your connection and try again later.";
        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(property.GetString()))
        {
            throw new InvalidDataException($"The update server returned invalid '{name}' metadata.");
        }
        return property.GetString()!;
    }

    private static bool IsHex(ReadOnlySpan<char> value, int length)
    {
        if (value.Length != length)
        {
            return false;
        }
        foreach (char character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }
        return true;
    }
}
