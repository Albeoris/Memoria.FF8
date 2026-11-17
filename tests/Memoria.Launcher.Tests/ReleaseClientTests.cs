using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Memoria.Launcher.Tests;

public sealed class ReleaseClientTests
{
    [Fact]
    public async Task StaticFeedAndDirectDownloadWorkWithoutApiAccess()
    {
        byte[] bytes = [1, 2, 3];
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        using HttpClient http = new(new Handler(request => request.RequestUri!.Host switch
        {
            "raw.githubusercontent.com" => JsonResponse(StaticFeed(bytes)),
            "github.com" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) },
            _ => throw new HttpRequestException("GitHub API unavailable")
        }));
        try
        {
            ReleaseClient client = new(http, "win-x64");
            UpdateRelease release = Assert.IsType<UpdateRelease>(await client.GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));
            Assert.Equal("0.8.10", release.EngineCompatibilityVersion);
            Assert.Equal("https://github.com/Albeoris/Memoria.FF8/releases/download/nightly/Memoria-FF8-win-x64.zip", release.DownloadUri.AbsoluteUri);
            await client.DownloadAsync(release, destination, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task StaticFeedRejectsMismatchedAssetAndDoesNotFallBackToApi()
    {
        using HttpClient http = new(new Handler(request =>
        {
            Assert.Equal("raw.githubusercontent.com", request.RequestUri!.Host);
            return JsonResponse(StaticFeed([1, 2, 3]).Replace("/nightly/Memoria-FF8-", "/v1.2.3/Memoria-FF8-", StringComparison.Ordinal));
        }));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0, "v1.2.3", "latest")]
    [InlineData(1, "nightly", "tags/nightly")]
    public async Task ChannelsResolvePinnedAssetAndIdentity(int channelValue, string tag, string endpoint)
    {
        UpdateChannel channel = (UpdateChannel)channelValue;
        string identity = new('a', 40);
        using HttpClient http = new(new LegacyHandler(request =>
        {
            Assert.Equal($"https://api.github.com/repos/Albeoris/Memoria.FF8/releases/{endpoint}", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Memoria-FF8-Launcher", request.Headers.UserAgent.ToString());
            return JsonResponse(Metadata(tag, label: identity));
        }));
        UpdateRelease release = Assert.IsType<UpdateRelease>(await new ReleaseClient(http, "win-x64").GetAsync(channel, TestContext.Current.CancellationToken));
        Assert.Equal(channel, release.Channel);
        Assert.Equal(tag, release.Tag);
        Assert.Equal(42, release.AssetId);
        Assert.Equal("https://api.github.com/repos/Albeoris/Memoria.FF8/releases/assets/42", release.DownloadUri.AbsoluteUri);
        Assert.Equal(identity, release.BuildId);
        Assert.Equal(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero), release.UpdatedAt);
    }

    [Theory]
    [InlineData("win-x64", "Windows_x64_", 40)]
    [InlineData("linux-x64", "Linux_x64_", 40)]
    [InlineData("osx-x64", "macOS_Intel_x64_", 40)]
    [InlineData("osx-arm64", "macOS_Apple_Silicon_arm64_", 40)]
    [InlineData("osx-arm64", "macOS_Apple_Silicon_arm64_", 64)]
    [InlineData("linux-x64", "Linux_x64_", 64)]
    [InlineData("win-x64", "build:", 40)]
    [InlineData("win-x64", "", 64)]
    public async Task PlatformAndLegacyLabelsPreserveBuildIdentity(string rid, string prefix, int length)
    {
        string identity = new('a', length);
        string metadata = Metadata("nightly", label: prefix + identity).Replace(ReleaseClient.AssetNameFor("win-x64"), ReleaseClient.AssetNameFor(rid), StringComparison.Ordinal);
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(metadata)));

        UpdateRelease release = Assert.IsType<UpdateRelease>(await new ReleaseClient(http, rid).GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));

        Assert.Equal(identity, release.BuildId);
        Assert.Equal(rid, release.RuntimeIdentifier);
    }

    [Theory]
    [InlineData("Linux_x64_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Windows_x64_short")]
    [InlineData("Windows_x64_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa_extra")]
    public async Task InvalidOrMismatchedLabelsCannotSupplyBuildIdentity(string label)
    {
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(Metadata("nightly", label: label))));

        UpdateRelease release = Assert.IsType<UpdateRelease>(await new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));

        Assert.Null(release.BuildId);
        Assert.Equal(Digest[7..], release.Sha256);
    }

    [Theory]
    [InlineData("osx-arm64", "osx-x64")]
    [InlineData("osx-x64", "osx-arm64")]
    public async Task MacReleaseSelectionNeverFallsBackToTheOtherArchitecture(string requested, string available)
    {
        string metadata = Metadata("nightly").Replace(ReleaseClient.AssetNameFor("win-x64"), ReleaseClient.AssetNameFor(available), StringComparison.Ordinal);
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(metadata)));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http, requested).GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MissingReleaseIsNotAnError()
    {
        using HttpClient http = new(new LegacyHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.Null(await new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("not-a-version", false)]
    [InlineData("v1.2.3", true)]
    public async Task LatestUsesGitHubSelectionWithoutVersionOrPrereleaseRestrictions(string tag, bool prerelease)
    {
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(Metadata(tag, prerelease: prerelease))));
        UpdateRelease release = Assert.IsType<UpdateRelease>(await new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
        Assert.Equal(tag, release.Tag);
    }

    [Theory]
    [InlineData("{\"draft\":true,\"tag_name\":\"v1.0\"}")]
    [InlineData("{\"draft\":false,\"prerelease\":false,\"tag_name\":\"v1.0\",\"assets\":[]}")]
    [InlineData("[]")]
    public async Task DraftOrMissingArchiveIsRejected(string json)
    {
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(json)));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("sha256:bad", 3, "uploaded")]
    [InlineData(null, 3, "uploaded")]
    [InlineData("valid", 0, "uploaded")]
    [InlineData("valid", 3, "new")]
    public async Task IncompleteAssetOrInvalidDigestIsRejected(string? digest, int size, string state)
    {
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(Metadata("nightly", digest: digest == "valid" ? Digest : digest, size: size, state: state))));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnrecognizedAssetLabelDoesNotBecomeBuildIdentity()
    {
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(Metadata("nightly", label: "Windows archive"))));
        Assert.Null((await new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken))!.BuildId);
    }

    [Fact]
    public async Task DuplicateArchiveNamesAreRejected()
    {
        using JsonDocument document = JsonDocument.Parse(Metadata("nightly"));
        string asset = document.RootElement.GetProperty("assets")[0].GetRawText();
        string json = $"{{\"tag_name\":\"nightly\",\"draft\":false,\"assets\":[{asset},{asset}]}}";
        using HttpClient http = new(new LegacyHandler(_ => JsonResponse(json)));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Nightly, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task RateLimitExplainsRetry(HttpStatusCode status)
    {
        using HttpClient http = new(new LegacyHandler(_ => new HttpResponseMessage(status)));
        HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
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
            await new ReleaseClient(http, "win-x64").DownloadAsync(Release(bytes), destination, cancellationToken: TestContext.Current.CancellationToken);
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
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http, "win-x64").DownloadAsync(Release(expected), destination, cancellationToken: TestContext.Current.CancellationToken));
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
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ReleaseClient(http, "win-x64").DownloadAsync(Release(bytes), destination, progress, cancellation.Token));
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
            await Assert.ThrowsAsync<IOException>(() => new ReleaseClient(http, "win-x64").DownloadAsync(Release(bytes), destination, cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("existing", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(destination);
        }
    }

    [Fact]
    public async Task LatestBypassesStaticFeedAndUsesGitHubLatest()
    {
        using HttpClient http = new(new Handler(request =>
        {
            Assert.Equal("https://api.github.com/repos/Albeoris/Memoria.FF8/releases/latest", request.RequestUri!.AbsoluteUri);
            return JsonResponse(Metadata("preview-preview", prerelease: true));
        }));
        UpdateRelease release = Assert.IsType<UpdateRelease>(await new ReleaseClient(http, "win-x64").GetAsync(UpdateChannel.Release, TestContext.Current.CancellationToken));
        Assert.Equal("preview-preview", release.Tag);
        Assert.Equal(Digest[7..], release.Sha256);
    }

    [Fact]
    public async Task CatalogPaginatesIncludesEveryTagAndSortsByUpdateTime()
    {
        object[] firstPage = Enumerable.Range(0, 100).Select(index => (object)new
        {
            tag_name = index == 0 ? "custom-tag" : "draft-" + index,
            name = "Release " + index,
            draft = index != 0,
            prerelease = false,
            updated_at = "2026-11-01T00:00:00Z"
        }).ToArray();
        object[] secondPage =
        [
            new { tag_name = "v2.0-preview", name = "Preview", draft = false, prerelease = true, updated_at = "2026-11-15T00:00:00Z" },
            new { tag_name = "nightly", name = "Nightly", draft = false, prerelease = true, updated_at = "2026-11-17T00:00:00Z" }
        ];
        List<string> requests = [];
        using HttpClient http = new(new Handler(request =>
        {
            string query = request.RequestUri!.Query;
            requests.Add(query);
            Assert.Equal("/repos/Albeoris/Memoria.FF8/releases", request.RequestUri.AbsolutePath);
            return query switch
            {
                "?per_page=100&page=1" => JsonResponse(JsonSerializer.Serialize(firstPage)),
                "?per_page=100&page=2" => JsonResponse(JsonSerializer.Serialize(secondPage)),
                _ => throw new InvalidOperationException("Unexpected catalog page")
            };
        }));
        IReadOnlyList<ReleaseCatalogEntry> releases = await new ReleaseClient(http, "win-x64").GetCatalogAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, requests.Count);
        Assert.Equal(["nightly", "v2.0-preview", "custom-tag"], releases.Select(release => release.Tag));
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero), releases[0].UpdatedAt);
        Assert.Contains(releases[0].UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm zzz"), releases[0].ToString());
    }

    [Fact]
    public async Task SelectedTagUsesEscapedEndpointAndVerifiedAssetMetadata()
    {
        using HttpClient http = new(new Handler(request =>
        {
            Assert.Equal("https://api.github.com/repos/Albeoris/Memoria.FF8/releases/tags/preview%2Ftest", request.RequestUri!.AbsoluteUri);
            return JsonResponse(Metadata("preview/test", prerelease: true));
        }));
        UpdateRelease release = Assert.IsType<UpdateRelease>(await new ReleaseClient(http, "win-x64").GetByTagAsync("preview/test", TestContext.Current.CancellationToken));
        Assert.Equal("preview/test", release.Tag);
        Assert.True(release.HasArtifact);
        Assert.Equal(Digest[7..], release.Sha256);
    }

    [Fact]
    public async Task SelectedTagCannotResolveToAnotherTag()
    {
        using HttpClient http = new(new Handler(_ => JsonResponse(Metadata("another-tag"))));
        await Assert.ThrowsAsync<InvalidDataException>(() => new ReleaseClient(http, "win-x64").GetByTagAsync("selected-tag", TestContext.Current.CancellationToken));
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

    internal static string StaticFeed(byte[] bytes)
    {
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            channels = new
            {
                nightly = new
                {
                    tag = "nightly",
                    name = "New nightly",
                    buildId = new string('b', 40),
                    engineCompatibilityVersion = "0.8.10",
                    publishedAt = "2026-11-16T12:00:00Z",
                    artifacts = new Dictionary<string, object>
                    {
                        ["win-x64"] = new
                        {
                            url = "https://github.com/Albeoris/Memoria.FF8/releases/download/nightly/Memoria-FF8-win-x64.zip",
                            size = bytes.Length,
                            sha256 = Convert.ToHexString(SHA256.HashData(bytes))
                        }
                    }
                }
            }
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

    private sealed class LegacyHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(request.RequestUri!.Host == "raw.githubusercontent.com" ? new HttpResponseMessage(HttpStatusCode.NotFound) : response(request));
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
