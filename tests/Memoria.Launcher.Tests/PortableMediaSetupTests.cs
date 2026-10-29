using System.Formats.Tar;
using System.Net;
using System.Net.Http;
using System.Text;
using Memoria.FF8;
using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

public sealed class PortableMediaSetupTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.MediaSetup.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(HostPlatform.Windows, "ffmpeg.exe")]
    [InlineData(HostPlatform.Linux, "ffmpeg")]
    [InlineData(HostPlatform.MacOS, "ffmpeg")]
    public void ToolPathsMatchSelectedHost(HostPlatform platform, string executable)
    {
        Assert.Equal(Path.Combine(directory, executable), MediaToolPaths.ResolveExecutable(directory, "ffmpeg", platform));
        Assert.Throws<ArgumentException>(() => MediaToolPaths.ResolveExecutable(directory, "../ffmpeg", platform));
    }

    [Fact]
    public void LinuxArchiveInstallsToolsAndNoticesWithoutOtherBinExecutables()
    {
        using MemoryStream archive = Archive(("package/bin/ffmpeg", "ffmpeg"), ("package/bin/ffprobe", "ffprobe"), ("package/bin/ffplay", "excluded"), ("package/LICENSE.txt", "license"), ("package/doc/readme.txt", "documentation"));
        MediaToolsInstallation.ExtractLinuxTar(archive, directory);
        Assert.Equal("ffmpeg", File.ReadAllText(Path.Combine(directory, "ffmpeg")));
        Assert.Equal("ffprobe", File.ReadAllText(Path.Combine(directory, "ffprobe")));
        Assert.Equal("license", File.ReadAllText(Path.Combine(directory, "LICENSE.txt")));
        Assert.Equal("documentation", File.ReadAllText(Path.Combine(directory, "doc", "readme.txt")));
        Assert.False(File.Exists(Path.Combine(directory, "ffplay")));
        if (!OperatingSystem.IsWindows())
        {
            Assert.NotEqual((UnixFileMode)0, File.GetUnixFileMode(Path.Combine(directory, "ffmpeg")) & UnixFileMode.UserExecute);
        }
    }

    [Theory]
    [InlineData("package/../escape")]
    [InlineData("/absolute/escape")]
    [InlineData("other/LICENSE")]
    [InlineData("package/bin/ffmpeg")]
    public void LinuxArchiveRejectsTraversalMixedRootsAndDuplicateTools(string invalid)
    {
        using MemoryStream archive = Archive(("package/bin/ffmpeg", "tool"), (invalid, "invalid"));
        Assert.Throws<InvalidDataException>(() => MediaToolsInstallation.ExtractLinuxTar(archive, directory));
    }

    [Fact]
    public void LinuxArchiveRejectsLinks()
    {
        using MemoryStream archive = new();
        using (TarWriter writer = new(archive, leaveOpen: true))
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.SymbolicLink, "package/bin/ffmpeg") { LinkName = "/usr/bin/ffmpeg" });
        }
        archive.Position = 0;
        Assert.Throws<InvalidDataException>(() => MediaToolsInstallation.ExtractLinuxTar(archive, directory));
    }

    [Fact]
    public void LinuxArchiveRequiresLicenseNoticesAndBothTools()
    {
        using MemoryStream archive = Archive(("package/bin/ffmpeg", "tool"), ("package/bin/ffprobe", "tool"));
        Assert.Throws<InvalidDataException>(() => MediaToolsInstallation.ExtractLinuxTar(archive, directory));
    }

    [Theory]
    [InlineData("osx-x64")]
    [InlineData("osx-arm64")]
    public async Task MacSetupReportsManualInstructionsWithoutConsentOrNetwork(string rid)
    {
        using HttpClient client = new(new RejectNetwork());
        MediaToolsInstallation installation = new(client, new FfmpegDownloadOptions(new Uri("https://example.invalid/"), "", rid), validate: _ => Task.FromResult(false));
        List<string> reports = [];
        Assert.False(await installation.EnsureAsync(directory, _ => throw new InvalidOperationException("No unreviewed download should request consent."), reports.Add, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Contains("Manual installation", Assert.Single(reports));
        Assert.Contains(directory, reports[0]);
    }

    [Fact]
    public async Task SelectedRuntimeCannotDownloadAnotherPlatformsArchive()
    {
        using HttpClient client = new(new RejectNetwork());
        MediaToolsInstallation installation = new(client, new FfmpegDownloadOptions(new Uri("https://example.invalid/"), "ffmpeg-master-latest-win64-lgpl.zip", "linux-x64"));
        await Assert.ThrowsAsync<ArgumentException>(() => installation.InstallAsync(directory, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InvalidExecutableHeadersNeverRun()
    {
        Directory.CreateDirectory(directory);
        string executable = Path.Combine(directory, "tool");
        File.WriteAllBytes(executable, new byte[128]);
        Assert.False(MediaToolsInstallation.IsHostExecutable(executable));
    }

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

    private sealed class RejectNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new InvalidOperationException("No network request is expected.");
    }
}
