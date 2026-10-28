using System.IO.Compression;
using System.Net;
using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class ModLibraryServiceTests
{
    [Fact]
    public async Task DownloadCheckAndInstallAreSeparateAndDoNotPublishDuringGameplay()
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-" + Guid.NewGuid().ToString("N"));
        Guid id = Guid.NewGuid();
        using MemoryStream bytes = new();
        using (ZipArchive archive = new(bytes, ZipArchiveMode.Create, true))
        {
            using StreamWriter writer = new(archive.CreateEntry("mod.xml").Open());
            writer.Write($"<ModInfo><ID>{id}</ID><Name>Example</Name><Version>1</Version></ModInfo>");
        }
        byte[] package = bytes.ToArray();
        int requests = 0;
        using HttpClient client = new(new Handler(request =>
        {
            Interlocked.Increment(ref requests);
            byte[] body = request.RequestUri!.Host == "codeload.github.com"
                ? Encoding.UTF8.GetBytes($"<Catalog><Mods><Mod><ID>{id}</ID><Name>Example</Name><LatestVersion><Version>1</Version><Link>iroj://Url/https$example.test/package</Link></LatestVersion></Mod></Mods></Catalog>")
                : package;
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        }));
        using ModRuntime mods = new(root, watch: false);
        ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "test");
        using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "test", true, client);
        Assert.Equal(0, requests);
        library.RefreshCatalog();
        await Until(() => library.View.Catalog.Entries.Count == 1);
        library.Queue(Assert.Single(library.View.Catalog.Entries));
        await Until(() => library.View.Jobs.Any(job => job.CanInstall));
        ModLibraryJob job = Assert.Single(library.View.Jobs);
        Assert.Equal("Ready to install", job.Phase);
        Assert.Null(job.Error);
        Assert.False(Directory.Exists(Path.Combine(root, "Mods", "Junction")));
        library.Install(job.Id);
        await Until(() => library.View.Jobs.Single().Phase == "Restart required");
        Assert.Null(library.View.Jobs.Single().Error);
        Assert.False(Directory.Exists(Path.Combine(root, "Mods", "Junction")));
        Assert.Empty(mods.Catalog.Packages);
        Assert.True(installer.GetStatus(job.Entry)!.RestartRequired);
        Assert.Equal(2, requests);
        // The service cancels without waiting on network or package workers. Temporary data is outside discovery.
    }

    [Fact]
    public async Task UnsupportedTargetCanBrowseButCannotQueue()
    {
        string root = Path.Combine(Path.GetTempPath(), "mod-library-" + Guid.NewGuid().ToString("N"));
        using HttpClient client = new(new Handler(_ => throw new InvalidOperationException("No network request was expected.")));
        using ModRuntime mods = new(root, watch: false);
        ModPackageInstaller installer = new(Path.Combine(root, "state"), Path.Combine(root, "Mods"), "remastered", false);
        using ModLibraryService library = new(Path.Combine(root, "cache"), installer, mods, "remastered", false, client);
        library.Queue(new("synthetic", Guid.NewGuid(), "Example", "1"));
        await Until(() => library.View.Error is not null);
        Assert.Empty(library.View.Jobs);
        Assert.False(library.View.TargetSupported);
    }

    private static async Task Until(Func<bool> condition)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(handle(request));
    }
}
