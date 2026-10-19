using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.Launcher.Tests;

public sealed class ReleaseClientTests
{
    [Theory]
    [InlineData(0, "v1.2.3", "latest")]
    [InlineData(1, "nightly", "tags/nightly")]
    public async Task ChannelsResolvePinnedAssetAndIdentity(int channelValue, string tag, string endpoint)
    {
        UpdateChannel channel = (UpdateChannel)channelValue;
        string identity = new('a', 40);
        using HttpClient http = new(new Handler(request =>
        {
            Assert.Equal($"https://api.github.com/repos/Albeoris/Memoria.FF8/releases/{endpoint}", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Memoria-FF8-Launcher", request.Headers.UserAgent.ToString());
            return JsonResponse(Metadata(tag, label: identity));
        }));
        UpdateRelease release = Assert.IsType<UpdateRelease>(await new ReleaseClient(http).GetAsync(channel, TestContext.Current.CancellationToken));
        Assert.Equal(channel, release.Channel);
        Assert.Equal(tag, release.Tag);
        Assert.Equal(42, release.AssetId);
        Assert.Equal("https://api.github.com/repos/Albeoris/Memoria.FF8/releases/assets/42", release.DownloadUri.AbsoluteUri);
        Assert.Equal(identity, release.BuildId);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), release.UpdatedAt);
    }

    [Fact]
    public async Task MissingReleaseIsNotAnError()
    {
        using HttpClient http = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.Null(await new ReleaseClient(http).GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstalledNightlyManifestResolvesHistoricalVersionedRelease(bool prefixedLabel)
    {
        string identity = new('a', 40);
        string history = "[" + Metadata("v1.2.4", label: new string('b', 40)) + "," + Metadata("nightly", prerelease: true, label: identity) + "," + Metadata("v1.2.3", label: (prefixedLabel ? "build:" : "") + identity.ToUpperInvariant()) + "]";
        using HttpClient http = new(new Handler(request =>
        {
            Assert.Equal("https://api.github.com/repos/Albeoris/Memoria.FF8/releases?per_page=100&page=1", request.RequestUri!.AbsoluteUri);
            return JsonResponse(history);
        }));
        PackageManifest installed = new(1, identity, "nightly", DateTimeOffset.UtcNow, []);
        Assert.Equal("v1.2.3", await new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
        Assert.Equal("nightly", installed.Version);
    }

    [Fact]
    public async Task InstalledVersionSearchContinuesOntoNextPage()
    {
        string identity = new('a', 40);
        int requests = 0;
        using HttpClient http = new(new Handler(request =>
        {
            requests++;
            Assert.Equal($"?per_page=100&page={requests}", request.RequestUri!.Query);
            return JsonResponse(requests == 1
                ? "[" + string.Join(",", Enumerable.Repeat(Metadata("v1.2.4", label: new string('b', 40)), 100)) + "]"
                : "[" + Metadata("v1.2.3", label: identity) + "]");
        }));
        PackageManifest installed = new(1, identity, "nightly", DateTimeOffset.UtcNow, []);
        Assert.Equal("v1.2.3", await new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData("nightly", false, "uploaded", "Memoria-FF8-win-x64.zip")]
    [InlineData("v1.2.3", true, "uploaded", "Memoria-FF8-win-x64.zip")]
    [InlineData("invalid", false, "uploaded", "Memoria-FF8-win-x64.zip")]
    [InlineData("v1.2.3", false, "new", "Memoria-FF8-win-x64.zip")]
    [InlineData("v1.2.3", false, "uploaded", "Other.zip")]
    public async Task UnmatchedOrUnpublishedVersionDoesNotRelabelNightly(string tag, bool prerelease, string state, string assetName)
    {
        string identity = new('a', 40);
        string metadata = Metadata(tag, prerelease: prerelease, state: state, label: identity).Replace("Memoria-FF8-win-x64.zip", assetName, StringComparison.Ordinal);
        using HttpClient http = new(new Handler(_ => JsonResponse("[" + metadata + "]")));
        PackageManifest installed = new(1, identity, "nightly", DateTimeOffset.UtcNow, []);
        Assert.Equal("nightly", await new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExplicitInstalledReleaseVersionDoesNotNeedNetworkLookup()
    {
        using HttpClient http = new(new Handler(_ => throw new InvalidOperationException("Unexpected network request.")));
        PackageManifest installed = new(1, new string('a', 40), "v1.2.3", DateTimeOffset.UtcNow, []);
        Assert.Equal("v1.2.3", await new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[null,{},123]")]
    public async Task EmptyOrUnrecognizedHistoryPreservesManifestVersion(string history)
    {
        using HttpClient http = new(new Handler(_ => JsonResponse(history)));
        PackageManifest installed = new(1, new string('a', 40), "nightly", DateTimeOffset.UtcNow, []);
        Assert.Equal("nightly", await new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DraftVersionDoesNotRelabelInstalledBuild()
    {
        string identity = new('a', 40);
        string metadata = Metadata("v1.2.3", label: identity).Replace("\"draft\":false", "\"draft\":true", StringComparison.Ordinal);
        using HttpClient http = new(new Handler(_ => JsonResponse("[" + metadata + "]")));
        PackageManifest installed = new(1, identity, "nightly", DateTimeOffset.UtcNow, []);
        Assert.Equal("nightly", await new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task UnavailableHistoryIsReportedInsteadOfConfirmingNightly(HttpStatusCode status)
    {
        using HttpClient http = new(new Handler(_ => new HttpResponseMessage(status)));
        PackageManifest installed = new(1, new string('a', 40), "nightly", DateTimeOffset.UtcNow, []);
        await Assert.ThrowsAsync<HttpRequestException>(() => new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("invalid JSON")]
    public async Task InvalidReleaseHistoryIsRejected(string json)
    {
        using HttpClient http = new(new Handler(_ => JsonResponse(json)));
        PackageManifest installed = new(1, new string('a', 40), "nightly", DateTimeOffset.UtcNow, []);
        if (json == "{}")
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAnyAsync<JsonException>(() => new ReleaseClient(http).GetInstalledVersionAsync(installed, TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("not-a-version", false)]
    [InlineData("v1.2.3", true)]
    public async Task StableRejectsInvalidTagOrPrerelease(string tag, bool prerelease)
    {
        using HttpClient http = new(new Handler(_ => JsonResponse(Metadata(tag, prerelease: prerelease))));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http).GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("{\"draft\":true,\"tag_name\":\"v1.0\"}")]
    [InlineData("{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v1.0\",\"assets\":[]}")]
    [InlineData("[]")]
    public async Task DraftOrMissingArchiveIsRejected(string json)
    {
        using HttpClient http = new(new Handler(_ => JsonResponse(json)));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http).GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("sha256:bad", 3, "uploaded")]
    [InlineData(null, 3, "uploaded")]
    [InlineData("valid", 0, "uploaded")]
    [InlineData("valid", 3, "new")]
    public async Task IncompleteAssetOrInvalidDigestIsRejected(string? digest, int size, string state)
    {
        using HttpClient http = new(new Handler(_ => JsonResponse(Metadata("nightly", digest: digest == "valid" ? Digest : digest, size: size, state: state))));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http).GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnrecognizedAssetLabelDoesNotBecomeBuildIdentity()
    {
        using HttpClient http = new(new Handler(_ => JsonResponse(Metadata("nightly", label: "Windows archive"))));
        Assert.Null((await new ReleaseClient(http).GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken))!.BuildId);
    }

    [Fact]
    public async Task DuplicateArchiveNamesAreRejected()
    {
        using JsonDocument document = JsonDocument.Parse(Metadata("nightly"));
        string asset = document.RootElement.GetProperty("assets")[0].GetRawText();
        string json = $"{{\"tag_name\":\"nightly\",\"draft\":false,\"assets\":[{asset},{asset}]}}";
        using HttpClient http = new(new Handler(_ => JsonResponse(json)));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http).GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RateLimitExplainsRetry(HttpStatusCode status)
    {
        using HttpClient http = new(new Handler(_ => new HttpResponseMessage(status)));
        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => new ReleaseClient(http).GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
        Assert.Equal(status, error.StatusCode);
        Assert.Contains("rate limit", error.Message);
    }

    [Fact]
    public async Task DownloadUsesPinnedAssetAndVerifiesArchive()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("valid archive payload");
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpClient http = new(new Handler(request =>
        {
            Assert.Equal("https://api.github.com/repos/Albeoris/Memoria.FF8/releases/assets/42", request.RequestUri!.AbsoluteUri);
            Assert.Equal("application/octet-stream", Assert.Single(request.Headers.Accept).MediaType);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }));
        try
        {
            await new ReleaseClient(http).DownloadAsync(Release(bytes), destination, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CorruptOrTruncatedDownloadDeletesPartial(bool corrupt)
    {
        byte[] expected = [1, 2, 3];
        byte[] actual = corrupt ? [3, 2, 1] : [1, 2];
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpClient http = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekableMemoryStream(actual)) }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http).DownloadAsync(Release(expected), destination, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task CancellationDuringTransferDeletesPartial()
    {
        byte[] bytes = [1, 2, 3];
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpClient http = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        IProgress<double> progress = new InlineProgress(value =>
        {
            if (value > 0)
            {
                cancellation.Cancel();
            }
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ReleaseClient(http).DownloadAsync(Release(bytes), destination, progress, cancellation.Token));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ExistingDestinationIsPreserved()
    {
        byte[] bytes = [1, 2, 3];
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(destination, "existing", TestContext.Current.CancellationToken);
        using HttpClient http = new(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }));
        try
        {
            await Assert.ThrowsAsync<IOException>(() => new ReleaseClient(http).DownloadAsync(Release(bytes), destination, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("existing", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    private const string Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static UpdateRelease Release(byte[] bytes)
    {
        return new UpdateRelease(UpdateChannel.Nightly, "nightly", "Nightly", DateTimeOffset.UtcNow, 42, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), null);
    }

    private static string Metadata(string tag, bool prerelease = false, string? digest = Digest, int size = 3, string state = "uploaded", string? label = null)
    {
        return JsonSerializer.Serialize(new
        {
            tag_name = tag,
            name = "Available build",
            draft = false,
            prerelease,
            assets = new[] { new { id = 42, name = "Memoria-FF8-win-x64.zip", state, size, digest, updated_at = "2026-10-18T12:00:00Z", label } }
        });
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value)
        {
            report(value);
        }
    }

    private sealed class NonSeekableMemoryStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
