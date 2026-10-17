using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("Media tools environment", DisableParallelization = true)]
public sealed class MediaToolsEnvironmentCollection
{
}

[Collection("Media tools environment")]
public sealed class MediaToolsInstallationTests
{
    private static readonly FfmpegDownloadOptions Options = new(new Uri("https://example.invalid/releases/latest/download/"), "ffmpeg-test-win64-lgpl.zip");

    [Fact]
    public void Malformed_download_configuration_reports_manual_installation_without_prompting()
    {
        using TestDirectory directory = new();
        const string variable = "MEMORIA_FF8_FFMPEG_RELEASE_URL";
        string? previous = Environment.GetEnvironmentVariable(variable);
        List<string> reports = [];
        try
        {
            Environment.SetEnvironmentVariable(variable, "not-a-uri");

            MediaToolsInstallation.PrepareForStartup(directory.Path, _ => throw new InvalidOperationException("Invalid configuration must not request download consent"), reports.Add);

            Assert.True(Directory.Exists(directory.Path));
            string report = string.Join(Environment.NewLine, reports);
            Assert.Contains(directory.Path, report);
            Assert.Contains(MediaToolsInstallation.LatestReleaseUrl, report);
            Assert.Contains("Continuing without FFmpeg", report);
            Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    [Fact]
    public async Task Validation_rejects_missing_executables()
    {
        using TestDirectory directory = new();

        Assert.False(await MediaToolsInstallation.ValidateAsync(directory.Path));
    }

    [Fact]
    public async Task Validation_rejects_corrupt_executables_without_throwing()
    {
        using TestDirectory directory = new();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(System.IO.Path.Combine(directory.Path, "ffmpeg.exe"), "not a PE executable");
        File.WriteAllText(System.IO.Path.Combine(directory.Path, "ffprobe.exe"), "not a PE executable");

        Assert.False(await MediaToolsInstallation.ValidateAsync(directory.Path));
    }

    [Fact]
    public async Task Validation_rejects_runnable_programs_without_the_ffmpeg_version_contract()
    {
        using TestDirectory directory = new();
        Directory.CreateDirectory(directory.Path);
        string executable = System.IO.Path.Combine(Environment.SystemDirectory, "whoami.exe");
        File.Copy(executable, System.IO.Path.Combine(directory.Path, "ffmpeg.exe"));
        File.Copy(executable, System.IO.Path.Combine(directory.Path, "ffprobe.exe"));

        Assert.False(await MediaToolsInstallation.ValidateAsync(directory.Path));
    }

    [Fact]
    public async Task Valid_tools_need_neither_consent_nor_network()
    {
        using TestDirectory directory = new();
        using DownloadHandler handler = new(_ => throw new InvalidOperationException("Must not download"));
        using HttpClient client = new(handler);
        List<string> validated = [];
        MediaToolsInstallation installation = new(client, Options, validate: path =>
        {
            validated.Add(path);
            return Task.FromResult(true);
        });

        bool available = await installation.EnsureAsync(directory.Path, _ => throw new InvalidOperationException("Must not request consent"), _ => { });

        Assert.True(available);
        Assert.Equal([directory.Path], validated);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Declining_creates_the_target_but_never_downloads()
    {
        using TestDirectory directory = new();
        using DownloadHandler handler = new(_ => throw new InvalidOperationException("Must not download"));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => Task.FromResult(false));
        int prompts = 0;

        bool available = await installation.EnsureAsync(directory.Path, _ =>
        {
            prompts++;
            Assert.True(Directory.Exists(directory.Path));
            return false;
        }, _ => { });

        Assert.False(available);
        Assert.Equal(1, prompts);
        Assert.Empty(handler.Requests);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Fact]
    public async Task Download_failure_reports_manual_installation_and_allows_startup_to_continue()
    {
        using TestDirectory directory = new();
        using DownloadHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => Task.FromResult(false));
        List<string> reports = [];

        bool available = await installation.EnsureAsync(directory.Path, _ => true, reports.Add);

        Assert.False(available);
        string report = string.Join(Environment.NewLine, reports);
        Assert.Contains(directory.Path, report);
        Assert.Contains("https://", report);
        Assert.Contains("latest", report, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Fact]
    public async Task Invalid_download_reports_manual_installation_and_allows_startup_to_continue()
    {
        using TestDirectory directory = new();
        using DownloadHandler handler = ArchiveHandler(CreateArchive(), new string('0', 64));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => Task.FromResult(false));
        List<string> reports = [];

        bool available = await installation.EnsureAsync(directory.Path, _ => true, reports.Add);

        Assert.False(available);
        string report = string.Join(Environment.NewLine, reports);
        Assert.Contains(directory.Path, report);
        Assert.Contains(MediaToolsInstallation.LatestReleaseUrl, report);
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Fact]
    public async Task Verified_archive_installs_both_tools_and_bundled_notices_after_staging_validation()
    {
        using TestDirectory directory = new();
        byte[] archive = CreateArchive();
        using DownloadHandler handler = ArchiveHandler(archive);
        using HttpClient client = new(handler);
        string? validatedPath = null;
        bool installedValidated = false;
        MediaToolsInstallation installation = new(client, Options, validate: path =>
        {
            Assert.Equal("ffmpeg executable", File.ReadAllText(System.IO.Path.Combine(path, "ffmpeg.exe")));
            Assert.Equal("ffprobe executable", File.ReadAllText(System.IO.Path.Combine(path, "ffprobe.exe")));
            if (path == directory.Path)
            {
                Assert.NotNull(validatedPath);
                installedValidated = true;
            }
            else
            {
                validatedPath = path;
                Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
            }
            return Task.FromResult(true);
        });

        await installation.InstallAsync(directory.Path);

        Assert.NotNull(validatedPath);
        Assert.True(installedValidated);
        Assert.Equal(["/releases/latest/download/checksums.sha256", "/releases/latest/download/ffmpeg-test-win64-lgpl.zip"], handler.Requests.Select(uri => uri.AbsolutePath));
        Assert.Equal("ffmpeg executable", File.ReadAllText(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.Equal("ffprobe executable", File.ReadAllText(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
        Assert.Equal("license text", File.ReadAllText(System.IO.Path.Combine(directory.Path, "LICENSE")));
        Assert.Equal("readme text", File.ReadAllText(System.IO.Path.Combine(directory.Path, "README.txt")));
        Assert.Equal("documentation", File.ReadAllText(System.IO.Path.Combine(directory.Path, "doc", "index.html")));
        Assert.DoesNotContain(Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories), path => System.IO.Path.GetFileName(path).Equals("ffplay.exe", StringComparison.OrdinalIgnoreCase) || System.IO.Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Installation_reports_download_bytes_and_each_verified_installation_stage(bool knownLength)
    {
        using TestDirectory directory = new();
        byte[] archive = CreateArchive();
        using DownloadHandler handler = ArchiveHandler(archive, stream: () => new DelayedReadStream(archive, 64, TimeSpan.FromMilliseconds(15), knownLength));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => Task.FromResult(true));
        List<MediaToolsProgress> updates = [];

        await installation.InstallAsync(directory.Path, updates.Add);

        Assert.Equal([MediaToolsStage.FetchingChecksums, MediaToolsStage.Downloading, MediaToolsStage.Verifying, MediaToolsStage.Extracting, MediaToolsStage.Validating, MediaToolsStage.Complete], updates.Select(update => update.Stage).Distinct());
        MediaToolsProgress[] downloads = updates.Where(update => update.Stage == MediaToolsStage.Downloading).ToArray();
        Assert.Equal(0, downloads[0].BytesReceived);
        Assert.Equal(archive.Length, downloads[^1].BytesReceived);
        Assert.Contains(downloads, update => update.BytesReceived > 0 && update.BytesReceived < archive.Length);
        Assert.Equal(downloads.Select(update => update.BytesReceived).Order(), downloads.Select(update => update.BytesReceived));
        Assert.All(downloads.Skip(1), update => Assert.Equal(knownLength ? archive.LongLength : (long?)null, update.TotalBytes));
        Assert.Empty(Directory.GetDirectories(directory.Path, ".install-*"));
    }

    [Fact]
    public async Task Checksum_mismatch_preserves_existing_tools_and_does_not_validate_untrusted_content()
    {
        using TestDirectory directory = new();
        Directory.CreateDirectory(directory.Path);
        string existing = System.IO.Path.Combine(directory.Path, "ffmpeg.exe");
        await File.WriteAllTextAsync(existing, "existing tool", TestContext.Current.CancellationToken);
        using DownloadHandler handler = ArchiveHandler(CreateArchive(), new string('0', 64));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => throw new InvalidOperationException("Must not execute untrusted tools"));
        List<MediaToolsProgress> updates = [];

        Exception failure = await Assert.ThrowsAnyAsync<Exception>(() => installation.InstallAsync(directory.Path, updates.Add));

        Assert.Contains("SHA-256", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MediaToolsStage.Verifying, updates[^1].Stage);
        Assert.Equal("existing tool", await File.ReadAllTextAsync(existing, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Fact]
    public async Task Archive_requires_its_exact_checksum_entry()
    {
        using TestDirectory directory = new();
        byte[] archive = CreateArchive();
        string checksum = Convert.ToHexString(SHA256.HashData(archive));
        using DownloadHandler handler = new(request => request.RequestUri!.AbsolutePath.EndsWith("checksums.sha256", StringComparison.Ordinal) ? Response(Encoding.UTF8.GetBytes($"{checksum}  unrelated.zip\n")) : Response(archive));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => throw new InvalidOperationException("Must not execute tools without an exact checksum"));

        await Assert.ThrowsAnyAsync<Exception>(() => installation.InstallAsync(directory.Path));

        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Theory]
    [InlineData("ffmpeg.exe")]
    [InlineData("ffprobe.exe")]
    public async Task Archive_missing_either_tool_is_not_installed(string omittedTool)
    {
        using TestDirectory directory = new();
        using DownloadHandler handler = ArchiveHandler(CreateArchive(omittedTool));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => Task.FromResult(true));

        await Assert.ThrowsAnyAsync<Exception>(() => installation.InstallAsync(directory.Path));

        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Fact]
    public async Task Failed_staging_validation_does_not_replace_existing_tools()
    {
        using TestDirectory directory = new();
        Directory.CreateDirectory(directory.Path);
        string existing = System.IO.Path.Combine(directory.Path, "ffmpeg.exe");
        await File.WriteAllTextAsync(existing, "existing tool", TestContext.Current.CancellationToken);
        using DownloadHandler handler = ArchiveHandler(CreateArchive());
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, validate: _ => Task.FromResult(false));

        await Assert.ThrowsAnyAsync<Exception>(() => installation.InstallAsync(directory.Path));

        Assert.Equal("existing tool", await File.ReadAllTextAsync(existing, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Fact]
    public async Task Stalled_response_headers_time_out_before_validation_or_installation()
    {
        using TestDirectory directory = new();
        using DownloadHandler handler = new(async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            return Response([]);
        });
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, TimeSpan.FromMilliseconds(150), _ => throw new InvalidOperationException("Must not validate an incomplete download"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installation.InstallAsync(directory.Path));

        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Fact]
    public async Task Stalled_response_body_times_out_before_validation_or_installation()
    {
        using TestDirectory directory = new();
        byte[] archive = CreateArchive();
        using DownloadHandler handler = ArchiveHandler(archive, stream: () => new DelayedReadStream(archive, archive.Length, TimeSpan.FromSeconds(2)));
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, TimeSpan.FromMilliseconds(150), _ => throw new InvalidOperationException("Must not validate an incomplete download"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => installation.InstallAsync(directory.Path));

        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.False(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    [Fact]
    public async Task Active_transfer_can_take_longer_than_the_idle_timeout()
    {
        using TestDirectory directory = new();
        byte[] archive = CreateArchive();
        DelayedReadStream stream = new(archive, 64, TimeSpan.FromMilliseconds(25));
        using DownloadHandler handler = ArchiveHandler(archive, stream: () => stream);
        using HttpClient client = new(handler);
        MediaToolsInstallation installation = new(client, Options, TimeSpan.FromMilliseconds(300), _ => Task.FromResult(true));

        await installation.InstallAsync(directory.Path);

        Assert.True(stream.ReadCount > 12);
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "ffmpeg.exe")));
        Assert.True(File.Exists(System.IO.Path.Combine(directory.Path, "ffprobe.exe")));
    }

    private static DownloadHandler ArchiveHandler(byte[] archive, string? checksum = null, Func<Stream>? stream = null)
    {
        checksum ??= Convert.ToHexString(SHA256.HashData(archive));
        byte[] manifest = Encoding.UTF8.GetBytes($"{checksum}  {Options.ArchiveName}\n");
        return new DownloadHandler(request => request.RequestUri!.AbsolutePath.EndsWith("checksums.sha256", StringComparison.Ordinal) ? Response(manifest) : stream == null ? Response(archive) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream()) });
    }

    private static HttpResponseMessage Response(byte[] content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
    }

    private static byte[] CreateArchive(string? omittedTool = null)
    {
        using MemoryStream buffer = new();
        using (ZipArchive archive = new(buffer, ZipArchiveMode.Create, true))
        {
            Dictionary<string, string> files = new()
            {
                ["bin/ffmpeg.exe"] = "ffmpeg executable",
                ["bin/ffprobe.exe"] = "ffprobe executable",
                ["bin/ffplay.exe"] = "unused player",
                ["bin/shared.dll"] = "unused library",
                ["LICENSE"] = "license text",
                ["README.txt"] = "readme text",
                ["doc/index.html"] = "documentation"
            };
            foreach ((string path, string contents) in files)
            {
                if (System.IO.Path.GetFileName(path) == omittedTool)
                {
                    continue;
                }
                ZipArchiveEntry entry = archive.CreateEntry("ffmpeg-test/" + path, CompressionLevel.NoCompression);
                using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
                writer.Write(contents);
            }
        }
        return buffer.ToArray();
    }

    private sealed class DownloadHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public DownloadHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : this((request, _) => Task.FromResult(respond(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return respond(request, cancellationToken);
        }
    }

    private sealed class DelayedReadStream(byte[] content, int chunkSize, TimeSpan delay, bool knownLength = true) : MemoryStream(content, false)
    {
        public int ReadCount { get; private set; }
        public override bool CanSeek => knownLength && base.CanSeek;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delay, cancellationToken);
            ReadCount++;
            return await base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Memoria.FF8.MediaToolsInstallationTests", Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            string parent = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Memoria.FF8.MediaToolsInstallationTests")) + System.IO.Path.DirectorySeparatorChar;
            string target = System.IO.Path.GetFullPath(Path);
            if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Test cleanup escaped its temporary directory.");
            }
            if (Directory.Exists(target))
            {
                Directory.Delete(target, true);
            }
        }
    }
}
