using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace Memoria.Launcher.Tests;

public sealed class UpdatePackageCacheTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-cache-tests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedNightlyRefreshesMetadataOnceAndUsesNewDigest(bool nextDownloadMatches)
    {
        byte[] replacement = [3, 2, 1];
        int downloads = 0;
        int refreshes = 0;
        int confirmations = 0;
        using HttpClient http = new(new Handler(request =>
        {
            if (request.RequestUri!.Host == "raw.githubusercontent.com")
            {
                refreshes++;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleaseClientTests.StaticFeed(replacement)) };
            }
            Assert.Equal("github.com", request.RequestUri.Host);
            downloads++;
            return Response(downloads == 1 || nextDownloadMatches ? replacement : [0, 0, 0]);
        }));
        ReleaseClient client = new(http, "win-x64");
        UpdateRelease initial = Release([1, 2, 3]) with { DirectDownloadUri = new("https://github.com/Albeoris/Memoria.FF8/releases/download/nightly/Memoria-FF8-win-x64.zip") };
        Task<(UpdateRelease Release, string Archive)?> download = UpdatePackageCache.GetArchiveWithNightlyRefreshAsync(root, client, initial, next =>
        {
            confirmations++;
            Assert.Equal("New nightly", next.Name);
            return Task.FromResult(true);
        }, cancellationToken: TestContext.Current.CancellationToken);
        if (nextDownloadMatches)
        {
            var result = await download;
            Assert.True(result.HasValue);
            Assert.Equal(replacement, await File.ReadAllBytesAsync(result.Value.Archive, TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await download);
        }
        Assert.Equal(1, refreshes);
        Assert.Equal(1, confirmations);
        Assert.Equal(2, downloads);
    }

    [Fact]
    public async Task VerifiedPackageSurvivesPreparationAndDoesNotDownloadAgain()
    {
        byte[] bytes = [1, 2, 3];
        int requests = 0;
        using HttpClient http = new(new Handler(_ =>
        {
            requests++;
            return Response(bytes);
        }));
        ReleaseClient client = new(http);
        UpdateRelease release = Release(bytes);
        string first = await UpdatePackageCache.GetArchiveAsync(root, client, release, cancellationToken: TestContext.Current.CancellationToken);
        UpdateInstaller.Prepare(root);
        string second = await UpdatePackageCache.GetArchiveAsync(root, client, release, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(first, second);
        Assert.Equal(1, requests);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(second, TestContext.Current.CancellationToken));
        Assert.Equal(Path.Combine(root, UpdatePackageCache.CacheDirectory), Path.GetDirectoryName(second));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangedAssetOrDigestDownloadsAndReplacesOldCache(bool changedAsset)
    {
        byte[] firstBytes = [1, 2, 3];
        byte[] secondBytes = changedAsset ? firstBytes : [3, 2, 1];
        int requests = 0;
        using HttpClient http = new(new Handler(request =>
        {
            requests++;
            Assert.EndsWith(changedAsset && requests == 2 ? "/43" : "/42", request.RequestUri!.AbsoluteUri);
            return Response(requests == 1 ? firstBytes : secondBytes);
        }));
        ReleaseClient client = new(http);
        string first = await UpdatePackageCache.GetArchiveAsync(root, client, Release(firstBytes), cancellationToken: TestContext.Current.CancellationToken);
        string second = await UpdatePackageCache.GetArchiveAsync(root, client, Release(secondBytes, changedAsset ? 43 : 42), cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotEqual(first, second);
        Assert.False(File.Exists(first));
        Assert.Equal(secondBytes, await File.ReadAllBytesAsync(second, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(Path.Combine(root, UpdatePackageCache.CacheDirectory)));
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CorruptOrTruncatedCacheIsDownloadedAgain(bool truncated)
    {
        byte[] bytes = [1, 2, 3];
        int requests = 0;
        using HttpClient http = new(new Handler(_ =>
        {
            requests++;
            return Response(bytes);
        }));
        ReleaseClient client = new(http);
        UpdateRelease release = Release(bytes);
        string archive = await UpdatePackageCache.GetArchiveAsync(root, client, release, cancellationToken: TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(archive, truncated ? [1] : [3, 2, 1], TestContext.Current.CancellationToken);
        Assert.Equal(archive, await UpdatePackageCache.GetArchiveAsync(root, client, release, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(archive, TestContext.Current.CancellationToken));
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task FailedNewDownloadPreservesPreviousVerifiedPackage()
    {
        byte[] bytes = [1, 2, 3];
        int requests = 0;
        using HttpClient http = new(new Handler(_ =>
        {
            requests++;
            return Response(bytes);
        }));
        ReleaseClient client = new(http);
        UpdateRelease original = Release(bytes);
        string archive = await UpdatePackageCache.GetArchiveAsync(root, client, original, cancellationToken: TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidDataException>(() => UpdatePackageCache.GetArchiveAsync(root, client, Release([3, 2, 1], 43), cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(archive, TestContext.Current.CancellationToken));
        Assert.Equal(archive, await UpdatePackageCache.GetArchiveAsync(root, client, original, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, requests);
        Assert.Single(Directory.GetFiles(Path.Combine(root, UpdatePackageCache.CacheDirectory)));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private static UpdateRelease Release(byte[] bytes, long assetId = 42)
    {
        return new UpdateRelease(UpdateChannel.Nightly, "nightly", "Nightly", DateTimeOffset.UtcNow, assetId, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)), null);
    }

    private static HttpResponseMessage Response(byte[] bytes)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(response(request));
        }
    }
}
