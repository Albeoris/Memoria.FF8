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
        Assert.DoesNotContain(arguments, argument => argument.StartsWith("--enable-gpl", StringComparison.Ordinal) || argument.StartsWith("--enable-nonfree", StringComparison.Ordinal) || argument.StartsWith("--enable-version3", StringComparison.Ordinal));
        Assert.Equal(architecture == Architecture.X64, arguments.Contains("--disable-x86asm"));
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
