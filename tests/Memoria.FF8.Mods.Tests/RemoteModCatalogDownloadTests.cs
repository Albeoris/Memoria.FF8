using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class RemoteModCatalogDownloadTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "catalog-download-" + Guid.NewGuid().ToString("N"));
    private ModDownloadCoordinator? coordinator;
    private static readonly byte[] Package = "IROSsynthetic-transfer-payload"u8.ToArray();

    [Fact]
    public void CatalogPreservesDecimalVersionsAndLegacySize()
    {
        RemoteModEntry entry = Assert.Single(RemoteModCatalogAdapter.Parse(Encoding.UTF8.GetBytes(Document())));
        Assert.Equal("1.10", entry.Version);
        Assert.Equal("15400", entry.PublishedSize);
        Assert.Equal(0, RemoteModEntry.CompareRelease("1.10", "1.1"));
        Assert.Contains("English", entry.Language);
        Assert.Null(RemoteModEntry.CompareRelease("release-a", "1"));
    }

    [Fact]
    public void CatalogDistinguishesEmptyFromRejectedAndRejectsDtd()
    {
        Assert.Empty(RemoteModCatalogAdapter.Parse("<Catalog><Mods /></Catalog>"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => RemoteModCatalogAdapter.Parse("<Catalog><Mods><Mod /></Mods></Catalog>"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => RemoteModCatalogAdapter.Parse("<Unknown />"u8.ToArray()));
        Assert.Throws<System.Xml.XmlException>(() => RemoteModCatalogAdapter.Parse("<!DOCTYPE Catalog [<!ENTITY x 'abc'>]><Catalog><Mods /></Catalog>"u8.ToArray()));
    }

    [Fact]
    public async Task CatalogKeepsLastGoodSnapshotWhenRefreshFails()
    {
        Queue<HttpResponseMessage> responses = new([Response(Encoding.UTF8.GetBytes(Document())), Response("<unknown/>"u8.ToArray())]);
        using HttpClient client = new(new Handler(_ => responses.Dequeue()));
        using RemoteModCatalogCoordinator catalog = new(root, client);
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Single(catalog.Snapshot.Entries);
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Single(catalog.Snapshot.Entries);
        Assert.True(catalog.Snapshot.IsStale);
        Assert.NotNull(catalog.Snapshot.Error);
    }

    [Fact]
    public async Task DownloadDeduplicatesAndComputesLocalDigest()
    {
        int requests = 0;
        using HttpClient client = new(new Handler(_ =>
        {
            Interlocked.Increment(ref requests);
            return Response(Package);
        }));
        coordinator = new(root, client);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        RemoteModEntry entry = Entry();
        Guid id = coordinator.Enqueue(entry, "test-profile");
        Assert.Equal(id, coordinator.Enqueue(entry, "test-profile"));
        DownloadJobSnapshot result = await Finish(id);
        Assert.Equal(ModDownloadPhase.ReadyToInstall, result.Phase);
        Assert.Equal(Package.Length, result.CompletedBytes);
        Assert.Equal(64, result.Sha256?.Length);
        Assert.Equal(Package, await File.ReadAllBytesAsync(result.ArchivePath!, TestContext.Current.CancellationToken));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task HtmlWithSuccessfulStatusNeverCompletes()
    {
        using HttpClient client = new(new Handler(_ => Response("<html>quota exceeded</html>"u8.ToArray())));
        coordinator = new(root, client);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        DownloadJobSnapshot result = await Finish(coordinator.Enqueue(Entry(), "test-profile"));
        Assert.Equal(ModDownloadPhase.Failed, result.Phase);
        Assert.Null(result.ArchivePath);
        Assert.Equal("invalid-response", result.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResumeOnlyAppendsValidatedSameObjectRange(bool changedIdentity)
    {
        Guid id = Guid.NewGuid();
        string directory = Path.Combine(root, id.ToString("N"));
        Directory.CreateDirectory(directory);
        RemoteModEntry entry = Entry();
        await File.WriteAllTextAsync(Path.Combine(directory, "job.json"), System.Text.Json.JsonSerializer.Serialize(new DownloadJobSnapshot(id, entry, "profile", false, ModDownloadPhase.Downloading)), TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "download.part"), Package[..8], TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "partial.json"), System.Text.Json.JsonSerializer.Serialize(new { Link = entry.Links[0], Validator = "\"original\"", Total = Package.Length }), TestContext.Current.CancellationToken);
        int requestCount = 0;
        using HttpClient client = new(new Handler(request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                Assert.Equal(8, request.Headers.Range?.Ranges.Single().From);
                Assert.Equal("\"original\"", request.Headers.IfRange?.EntityTag?.Tag);
                HttpResponseMessage partial = Response(Package[8..]);
                partial.StatusCode = HttpStatusCode.PartialContent;
                partial.Content.Headers.ContentRange = new ContentRangeHeaderValue(8, Package.Length - 1, Package.Length);
                partial.Headers.ETag = new(changedIdentity ? "\"changed\"" : "\"original\"");
                return partial;
            }
            Assert.Null(request.Headers.Range);
            return Response(Package);
        }));
        coordinator = new(root, client);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ModDownloadPhase.Paused, Assert.Single(coordinator.Snapshot).Phase);
        Assert.Equal(0, requestCount);
        coordinator.Resume(id);
        DownloadJobSnapshot result = await Finish(id);
        Assert.Equal(ModDownloadPhase.ReadyToInstall, result.Phase);
        Assert.Equal(Package, await File.ReadAllBytesAsync(result.ArchivePath!, TestContext.Current.CancellationToken));
        Assert.Equal(changedIdentity ? 2 : 1, requestCount);
    }

    [Fact]
    public async Task RecoveredCompleteDownloadValidatesLocallyWithoutNetwork()
    {
        Guid id = Guid.NewGuid();
        string directory = Path.Combine(root, id.ToString("N"));
        Directory.CreateDirectory(directory);
        string archive = Path.Combine(directory, "package.download");
        await File.WriteAllBytesAsync(archive, Package, TestContext.Current.CancellationToken);
        DownloadJobSnapshot saved = new(id, Entry(), "profile", false, ModDownloadPhase.ReadyToInstall)
        {
            ArchivePath = archive, CompletedBytes = Package.Length, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Package)).ToLowerInvariant()
        };
        await File.WriteAllTextAsync(Path.Combine(directory, "job.json"), System.Text.Json.JsonSerializer.Serialize(saved), TestContext.Current.CancellationToken);
        using HttpClient client = new(new Handler(_ => throw new InvalidOperationException("Recovery must not use the network.")));
        coordinator = new(root, client);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ModDownloadPhase.Paused, Assert.Single(coordinator.Snapshot).Phase);
        coordinator.Resume(id);
        Assert.Equal(ModDownloadPhase.ReadyToInstall, (await Finish(id)).Phase);
    }

    [Fact]
    public async Task PauseCancelsBlockedBodyAndRetryRemainsOneJob()
    {
        TaskCompletionSource reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int requests = 0;
        using HttpClient client = new(new Handler(_ =>
        {
            if (Interlocked.Increment(ref requests) == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BlockedBody(reading)) };
            }
            return Response(Package);
        }));
        coordinator = new(root, client);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        Guid id = coordinator.Enqueue(Entry(), "profile");
        await reading.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        coordinator.Pause(id);
        Assert.Equal(ModDownloadPhase.Paused, Assert.Single(coordinator.Snapshot).Phase);
        coordinator.Resume(id);
        Assert.Equal(ModDownloadPhase.ReadyToInstall, (await Finish(id)).Phase);
        Assert.Single(coordinator.Snapshot);
        Assert.Equal(2, requests);
    }

    private sealed class BlockedBody(TaskCompletionSource reading) : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            reading.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
    [Theory]
    [InlineData(200)]
    [InlineData(416)]
    [InlineData(206)]
    public async Task InconsistentOrIgnoredRangeSafelyRestarts(int status)
    {
        Guid id = Guid.NewGuid();
        string directory = Path.Combine(root, id.ToString("N"));
        Directory.CreateDirectory(directory);
        RemoteModEntry entry = Entry();
        await File.WriteAllTextAsync(Path.Combine(directory, "job.json"), System.Text.Json.JsonSerializer.Serialize(new DownloadJobSnapshot(id, entry, "profile", false, ModDownloadPhase.Downloading)), TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(Path.Combine(directory, "download.part"), Package[..8], TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "partial.json"), System.Text.Json.JsonSerializer.Serialize(new { Link = entry.Links[0], Validator = "\"original\"", Total = Package.Length }), TestContext.Current.CancellationToken);
        int requests = 0;
        using HttpClient client = new(new Handler(request =>
        {
            requests++;
            if (requests == 1)
            {
                Assert.NotNull(request.Headers.Range);
                HttpResponseMessage response = Response(Package);
                response.StatusCode = (HttpStatusCode)status;
                if (status == 206)
                {
                    response.Content.Headers.ContentRange = new ContentRangeHeaderValue(9, Package.Length - 1, Package.Length);
                }
                return response;
            }
            Assert.Null(request.Headers.Range);
            return Response(Package);
        }));
        coordinator = new(root, client);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        coordinator.Resume(id);
        DownloadJobSnapshot result = await Finish(id);
        Assert.Equal(ModDownloadPhase.ReadyToInstall, result.Phase);
        Assert.Equal(Package, await File.ReadAllBytesAsync(result.ArchivePath!, TestContext.Current.CancellationToken));
        Assert.Equal(status == 200 ? 1 : 2, requests);
    }
    [Fact]
    public async Task ConditionalCatalogValidationPersistsValidatorsAndHonestTimes()
    {
        DateTimeOffset modified = new(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
        int requests = 0;
        using HttpClient client = new(new Handler(request =>
        {
            requests++;
            if (requests == 1)
            {
                Assert.Empty(request.Headers.IfNoneMatch);
                HttpResponseMessage response = Response(Encoding.UTF8.GetBytes(Document()));
                response.Content.Headers.LastModified = modified;
                return response;
            }
            Assert.Equal("\"original\"", Assert.Single(request.Headers.IfNoneMatch).Tag);
            Assert.Equal(modified, request.Headers.IfModifiedSince);
            return requests == 2 ? new HttpResponseMessage(HttpStatusCode.NotModified) : Response("<unknown/>"u8.ToArray());
        }));
        using RemoteModCatalogCoordinator catalog = new(root, client);
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        DateTimeOffset? fetched = catalog.Snapshot.FetchedAt;
        Assert.NotNull(fetched);
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Equal(fetched, catalog.Snapshot.FetchedAt);
        DateTimeOffset? validated = catalog.Snapshot.LastValidatedAt;
        Assert.True(validated >= fetched);
        Assert.False(catalog.Snapshot.IsStale);
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Equal(fetched, catalog.Snapshot.FetchedAt);
        Assert.Equal(validated, catalog.Snapshot.LastValidatedAt);
        Assert.True(catalog.Snapshot.IsStale);
        foreach (string path in Directory.EnumerateFiles(root))
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(20));
        }
        using RemoteModCatalogCoordinator restored = new(root, client);
        await restored.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Single(restored.Snapshot.Entries);
        Assert.Equal(fetched, restored.Snapshot.FetchedAt);
        Assert.Equal(validated, restored.Snapshot.LastValidatedAt);
        Assert.Equal(3, requests);
    }

    [Fact]
    public async Task NotModifiedWithMissingRawRetriesWithoutConditions()
    {
        int requests = 0;
        using HttpClient client = new(new Handler(request =>
        {
            requests++;
            if (requests == 2)
            {
                Assert.NotEmpty(request.Headers.IfNoneMatch);
                return new HttpResponseMessage(HttpStatusCode.NotModified);
            }
            Assert.Empty(request.Headers.IfNoneMatch);
            Assert.Null(request.Headers.IfModifiedSince);
            return Response(Encoding.UTF8.GetBytes(Document()));
        }));
        using RemoteModCatalogCoordinator catalog = new(root, client);
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        foreach (string path in Directory.EnumerateFiles(root, "*.raw"))
        {
            File.Delete(path);
        }
        await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, requests);
        Assert.False(catalog.Snapshot.IsStale);
        Assert.Single(catalog.Snapshot.Entries);
    }

    [Fact]
    public async Task UnsupportedMetadataFallsBackToRawWithoutInventingFreshness()
    {
        using HttpClient client = new(new Handler(_ => Response(Encoding.UTF8.GetBytes(Document()))));
        using (RemoteModCatalogCoordinator catalog = new(root, client))
        {
            await catalog.RefreshAsync(TestContext.Current.CancellationToken);
        }
        string metadata = Path.Combine(root, "catalog.json");
        string json = await File.ReadAllTextAsync(metadata, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(metadata, json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":999"), TestContext.Current.CancellationToken);
        using RemoteModCatalogCoordinator restored = new(root, client);
        await restored.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Single(restored.Snapshot.Entries);
        Assert.Null(restored.Snapshot.FetchedAt);
        Assert.Null(restored.Snapshot.LastValidatedAt);
        Assert.True(restored.Snapshot.IsStale);
    }

    [Fact]
    public async Task InvalidIdentifiedEntryRetainsPreviousMetadataAcrossRestart()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        string changed = Document(second).Replace("</Mods>", $"<Mod><ID>{first}</ID><Name>Broken release</Name></Mod></Mods>");
        Queue<HttpResponseMessage> responses = new([Response(Encoding.UTF8.GetBytes(Document(first))), Response(Encoding.UTF8.GetBytes(changed))]);
        using HttpClient client = new(new Handler(_ => responses.Dequeue()));
        using (RemoteModCatalogCoordinator catalog = new(root, client))
        {
            await catalog.RefreshAsync(TestContext.Current.CancellationToken);
            await catalog.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, catalog.Snapshot.Entries.Count);
            Assert.True(catalog.Snapshot.Entries.Single(entry => entry.Id == first).IsStale);
            Assert.False(catalog.Snapshot.Entries.Single(entry => entry.Id == second).IsStale);
            Assert.Contains("retained as stale", catalog.Snapshot.Error);
        }
        using RemoteModCatalogCoordinator restored = new(root, client);
        await restored.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, restored.Snapshot.Entries.Count);
        Assert.True(restored.Snapshot.Entries.Single(entry => entry.Id == first).IsStale);
    }
    [Fact]
    public void CatalogBoundsDisplayTextAndSeparatesMetadataFromReleaseUpdates()
    {
        Guid id = Guid.NewGuid();
        string original = Document(id);
        string changed = original.Replace("<MetaVersion>2</MetaVersion>", "<MetaVersion>9</MetaVersion>");
        RemoteModEntry before = Assert.Single(RemoteModCatalogAdapter.Parse(Encoding.UTF8.GetBytes(original)));
        RemoteModEntry after = Assert.Single(RemoteModCatalogAdapter.Parse(Encoding.UTF8.GetBytes(changed)));
        Assert.NotEqual(before.MetaVersion, after.MetaVersion);
        Assert.Equal(0, RemoteModEntry.CompareRelease(after.Version, before.Version));
        string oversized = original.Replace("<Name>Fixture</Name>", "<Name>" + new string('N', 600) + "</Name>")
            .Replace("<Author>Test</Author>", "<Author>" + new string('A', 600) + "</Author><Category>" + new string('C', 600) + "</Category><Description><![CDATA[<b>Title</b> &amp; " + new string('d', 40000) + "]]></Description>")
            .Replace("</LatestVersion>", "<ReleaseNotes><![CDATA[&lt;i&gt;plain&lt;/i&gt;]]></ReleaseNotes><UnknownDirective critical=\"true\">value</UnknownDirective></LatestVersion>");
        RemoteModEntry bounded = Assert.Single(RemoteModCatalogAdapter.Parse(Encoding.UTF8.GetBytes(oversized)));
        Assert.Equal(256, bounded.Name.Length);
        Assert.Equal(256, bounded.Author.Length);
        Assert.Equal(128, bounded.Category.Length);
        Assert.True(bounded.Description.Length <= 32768);
        Assert.StartsWith("Title", bounded.Description);
        Assert.DoesNotContain("<", bounded.Description);
        Assert.Equal("plain", bounded.ReleaseNotes);
        Assert.Contains("UnknownDirective", bounded.UnsupportedMetadata);
        string tooManyLinks = original.Replace("</LatestVersion>", string.Concat(Enumerable.Repeat("<Link>iroj://Url/https$example.invalid/file</Link>", 16)) + "</LatestVersion>");
        Assert.Throws<InvalidDataException>(() => RemoteModCatalogAdapter.Parse(Encoding.UTF8.GetBytes(tooManyLinks)));
    }
    private async Task<DownloadJobSnapshot> Finish(Guid id)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(15));
        while (true)
        {
            DownloadJobSnapshot job = coordinator!.Snapshot.Single(job => job.JobId == id);
            if (job.Phase is ModDownloadPhase.ReadyToInstall or ModDownloadPhase.Failed or ModDownloadPhase.NeedsUserAction)
            {
                return job;
            }
            await Task.Delay(20, deadline.Token);
        }
    }

    [Fact]
    public async Task CancelledCheckedDownloadReclaimsOwnedCacheAndEquivalentVersionsDeduplicate()
    {
        using HttpClient client = new(new Handler(_ => Response(Package)));
        coordinator = new(root, client);
        await coordinator.InitializeAsync(TestContext.Current.CancellationToken);
        RemoteModEntry entry = Entry();
        Guid id = coordinator.Enqueue(entry, "profile");
        Assert.Equal(id, coordinator.Enqueue(entry with { Version = "1.100" }, "profile"));
        DownloadJobSnapshot completed = await Finish(id);
        Assert.Equal(ModDownloadPhase.ReadyToInstall, completed.Phase);
        coordinator.Cancel(id);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (File.Exists(completed.ArchivePath))
        {
            await Task.Delay(20, timeout.Token);
        }
        Assert.Equal(ModDownloadPhase.Cancelled, Assert.Single(coordinator.Snapshot).Phase);
        Assert.True(File.Exists(Path.Combine(root, id.ToString("N"), "job.json")));
    }

    private static RemoteModEntry Entry() => new("junction-viii", Guid.NewGuid(), "Synthetic package", "1.10") { Links = new[] { "iroj://Url/https$example.invalid/package" } };
    private static string Document(Guid? id = null) => $"<Catalog><Mods><Mod><ID>{id ?? Guid.NewGuid()}</ID><Name>Fixture</Name><Author>Test</Author><MetaVersion>2</MetaVersion><LatestVersion><Version>1.10</Version><DownloadSize>15400</DownloadSize><Link>iroj://Url/https$example.invalid/file</Link></LatestVersion></Mod></Mods></Catalog>";
    private static HttpResponseMessage Response(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), Headers = { ETag = new EntityTagHeaderValue("\"original\"") } };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }

    public async ValueTask DisposeAsync()
    {
        coordinator?.Dispose();
        for (int attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
                return;
            }
            catch (IOException)
            {
                await Task.Delay(20);
            }
        }
    }
}
