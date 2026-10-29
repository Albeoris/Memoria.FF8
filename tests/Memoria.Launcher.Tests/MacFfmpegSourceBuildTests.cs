using System.Formats.Tar;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;

namespace Memoria.Launcher.Tests;

public sealed class MacFfmpegSourceBuildTests : IDisposable
{
    private const string Root = "ffmpeg-" + MacFfmpegSourceBuild.Version;
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FfmpegSource.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(Architecture.Arm64)]
    [InlineData(Architecture.X64)]
    public void ConfigurationStaysLgplAndUsesOnlySystemLibraries(Architecture architecture)
    {
        string[] arguments = MacFfmpegSourceBuild.ConfigureArguments("/prefix", architecture);
        Assert.Equal("--prefix=/prefix", arguments[0]);
        Assert.Contains("--disable-autodetect", arguments);
        Assert.DoesNotContain("--enable-iconv", arguments);
        Assert.DoesNotContain(arguments, argument => argument.StartsWith("--enable-gpl", StringComparison.Ordinal) || argument.StartsWith("--enable-nonfree", StringComparison.Ordinal) || argument.StartsWith("--enable-version3", StringComparison.Ordinal));
        Assert.Equal(architecture == Architecture.X64, arguments.Contains("--disable-x86asm"));
    }

    [Fact]
    public void ConfigurationKeepsThePlaybackOutputs()
    {
        string[] arguments = MacFfmpegSourceBuild.ConfigureArguments("/prefix", Architecture.Arm64);
        Assert.Contains("--disable-encoders", arguments);
        Assert.Contains("--disable-muxers", arguments);
        Assert.DoesNotContain("--disable-decoders", arguments);
        Assert.DoesNotContain("--disable-demuxers", arguments);
        Assert.DoesNotContain("--disable-filters", arguments);
        string[] encoders = arguments.Single(argument => argument.StartsWith("--enable-encoder=", StringComparison.Ordinal))["--enable-encoder=".Length..].Split(',');
        string[] muxers = arguments.Single(argument => argument.StartsWith("--enable-muxer=", StringComparison.Ordinal))["--enable-muxer=".Length..].Split(',');
        Assert.Contains("pcm_s16le", encoders);
        Assert.Contains("rawvideo", encoders);
        Assert.Contains("s16le", muxers);
        Assert.Contains("rawvideo", muxers);
        Assert.Contains("ivf", muxers);
    }

    [Fact]
    public void ComponentListingReadsNamesAfterTheFlagColumn()
    {
        const string encoders = "Encoders:\n V..... = Video\n ------\n V....D rawvideo             raw video\n A....D pcm_s16le            PCM signed 16-bit little-endian\n";
        const string muxers = "Formats:\n D. = Demuxing supported\n --\n  E ivf             On2 IVF\n  E rawvideo,null   raw video\n  E s16le           PCM signed 16-bit little-endian\n";
        Assert.Superset(new HashSet<string> { "rawvideo", "pcm_s16le" }, MacFfmpegSourceBuild.ListedComponents(encoders));
        Assert.Superset(new HashSet<string> { "ivf", "rawvideo", "null", "s16le" }, MacFfmpegSourceBuild.ListedComponents(muxers));
        Assert.DoesNotContain("pcm_s16be", MacFfmpegSourceBuild.ListedComponents(encoders));
    }

    [Fact]
    public async Task StalledHeadersTimeOut()
    {
        using HttpClient client = new(new StallingHandler(stallBody: false)) { Timeout = Timeout.InfiniteTimeSpan };
        using MemoryStream destination = new();
        await Assert.ThrowsAsync<TimeoutException>(() => MacFfmpegSourceBuild.DownloadAsync(client, new Uri("https://example.invalid/ffmpeg.tar.xz"), destination, TimeSpan.FromMilliseconds(200), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StalledBodyTimesOutAfterReceivedData()
    {
        using HttpClient client = new(new StallingHandler(stallBody: true)) { Timeout = Timeout.InfiniteTimeSpan };
        using MemoryStream destination = new();
        await Assert.ThrowsAsync<TimeoutException>(() => MacFfmpegSourceBuild.DownloadAsync(client, new Uri("https://example.invalid/ffmpeg.tar.xz"), destination, TimeSpan.FromMilliseconds(200), null, TestContext.Current.CancellationToken));
        Assert.Equal(StallingStream.Prefix.Length, destination.Length);
    }

    [Fact]
    public async Task CallerCancellationIsNotReportedAsTimeout()
    {
        using HttpClient client = new(new StallingHandler(stallBody: true)) { Timeout = Timeout.InfiniteTimeSpan };
        using MemoryStream destination = new();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MacFfmpegSourceBuild.DownloadAsync(client, new Uri("https://example.invalid/ffmpeg.tar.xz"), destination, TimeSpan.FromMinutes(5), null, cancellation.Token));
    }

    [Fact]
    public void PromptNamesThePinnedSourceAndDestination()
    {
        string prompt = MacFfmpegSourceBuild.Prompt(directory);
        Assert.Contains(MacFfmpegSourceBuild.ArchiveName, prompt);
        Assert.Contains(directory, prompt);
        Assert.Contains("Xcode Command Line Tools", prompt);
        Assert.Equal(64, MacFfmpegSourceBuild.ArchiveSha256.Length);
    }

    [Fact]
    public void SourceArchiveExtractsTheReleaseTree()
    {
        using MemoryStream archive = Archive(Release());
        MacFfmpegSourceBuild.ExtractSourceTar(archive, directory);
        Assert.Equal("configure", File.ReadAllText(Path.Combine(directory, Root, "configure")));
        Assert.Equal("lgpl", File.ReadAllText(Path.Combine(directory, Root, "COPYING.LGPLv2.1")));
        Assert.True(File.Exists(Path.Combine(directory, Root, "libavcodec", "h264dec.c")));
    }

    [Theory]
    [InlineData("other-1.0/configure")]
    [InlineData(Root + "/../escape")]
    [InlineData(Root + "/./configure2")]
    [InlineData("/" + Root + "/absolute")]
    public void SourceArchiveRejectsForeignOrEscapingPaths(string name)
    {
        using MemoryStream archive = Archive(Release().Append((name, "x")).ToArray());
        Assert.Throws<InvalidDataException>(() => MacFfmpegSourceBuild.ExtractSourceTar(archive, directory));
    }

    [Fact]
    public void SourceArchiveRejectsLinks()
    {
        using MemoryStream archive = new();
        using (TarWriter writer = new(archive, leaveOpen: true))
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, Root + "/configure") { LinkName = "/etc/passwd" });
        }
        archive.Position = 0;
        Assert.Throws<InvalidDataException>(() => MacFfmpegSourceBuild.ExtractSourceTar(archive, directory));
    }

    [Fact]
    public void SourceArchiveRequiresConfigureAndLicenseNotices()
    {
        using MemoryStream archive = Archive((Root + "/configure", "configure"), (Root + "/LICENSE.md", "license"));
        Assert.Throws<InvalidDataException>(() => MacFfmpegSourceBuild.ExtractSourceTar(archive, directory));
    }

    [Fact]
    public async Task SourceBuildIsOfferedOnlyOnMacOS()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }
        using HttpClient client = new();
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => MacFfmpegSourceBuild.InstallAsync(client, directory, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(directory));
    }

    private sealed class StallingHandler(bool stallBody) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!stallBody)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(new StallingStream()) };
        }
    }

    /// <summary>Returns a few bytes, then waits for data that never arrives, like a server that stops mid-transfer.</summary>
    private sealed class StallingStream : Stream
    {
        internal static readonly byte[] Prefix = [1, 2, 3, 4];
        private bool sent;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!sent)
            {
                sent = true;
                Prefix.CopyTo(buffer);
                return Prefix.Length;
            }
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush()
        {
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static (string Name, string Content)[] Release() => [(Root + "/configure", "configure"), (Root + "/LICENSE.md", "license"), (Root + "/COPYING.LGPLv2.1", "lgpl"), (Root + "/libavcodec/h264dec.c", "source")];

    private static MemoryStream Archive(params (string Name, string Content)[] files)
    {
        MemoryStream stream = new();
        using (TarWriter writer = new(stream, leaveOpen: true))
        {
            foreach ((string name, string content) in files)
            {
                using MemoryStream data = new(Encoding.UTF8.GetBytes(content));
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = data });
            }
        }
        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
