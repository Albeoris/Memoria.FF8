using System.Formats.Tar;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

public sealed class MacFfmpegPrebuiltInstallTests : IDisposable
{
    private const string Root = "ffmpeg-9.0.1-1-lgpl-macos-arm64";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Memoria.FfmpegPrebuilt.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(Architecture.Arm64, "ffmpeg-9.0.1-1-lgpl-macos-arm64")]
    [InlineData(Architecture.X64, "ffmpeg-9.0.1-1-lgpl-macos-x86_64")]
    public void PackageMatchesHostArchitectureAndLgplVariant(Architecture architecture, string root)
    {
        (string name, string sha256) = MacFfmpegPrebuiltInstall.Package(architecture);
        Assert.Equal(root, name);
        Assert.Equal(64, sha256.Length);
        string prompt = MacFfmpegPrebuiltInstall.Prompt(directory, architecture);
        Assert.Contains(root + ".tar.xz", prompt);
        Assert.Contains(MacFfmpegPrebuiltInstall.ReleasePage, prompt);
        Assert.Contains(directory, prompt);
        Assert.Throws<PlatformNotSupportedException>(() => MacFfmpegPrebuiltInstall.Package(Architecture.X86));
    }

    [Fact]
    public void SourceBuildPromptExplainsThePrebuiltFailure()
    {
        Assert.Contains("now?", MacFfmpegSourceBuild.Prompt(directory));
        string prompt = MacFfmpegSourceBuild.Prompt(directory, "Response status code does not indicate success: 404 (Not Found).");
        Assert.Contains("404", prompt);
        Assert.Contains("instead?", prompt);
    }

    [Fact]
    public void PackageExtractsToolsLibrariesAndNoticesButNotHeaders()
    {
        using MemoryStream archive = Archive(Package());
        MacFfmpegPrebuiltInstall.ExtractPackageTar(archive, directory, Root, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("ffmpeg", File.ReadAllText(Path.Combine(directory, "bin", "ffmpeg")));
        Assert.Equal("ffprobe", File.ReadAllText(Path.Combine(directory, "bin", "ffprobe")));
        Assert.Equal("avcodec", File.ReadAllText(Path.Combine(directory, "lib", "libavcodec.63.1.101.dylib")));
        Assert.True(File.Exists(Path.Combine(directory, "LICENSES", "ffmpeg", "COPYING.LGPLv2.1")));
        Assert.True(File.Exists(Path.Combine(directory, "BUILDINFO.txt")));
        Assert.False(Directory.Exists(Path.Combine(directory, "include")));
        Assert.False(File.Exists(Path.Combine(directory, "bin", "ffplay")));
        Assert.Equal(Path.Combine(directory, "bin", "ffmpeg"), MediaToolPaths.ResolveExecutable(directory, "ffmpeg", HostPlatform.MacOS));
    }

    [Fact]
    public void LibraryVersionLinksPointToSiblingLibraries()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Windows symlinks require additional machine privileges.");
        using MemoryStream archive = Archive(Package(), Link("lib/libavcodec.63.dylib", "libavcodec.63.1.101.dylib"));
        MacFfmpegPrebuiltInstall.ExtractPackageTar(archive, directory, Root, cancellationToken: TestContext.Current.CancellationToken);
        string link = Path.Combine(directory, "lib", "libavcodec.63.dylib");
        Assert.Equal("libavcodec.63.1.101.dylib", new FileInfo(link).LinkTarget);
        Assert.Equal("avcodec", File.ReadAllText(link));
    }

    [Theory]
    [InlineData("../../escape.dylib")]
    [InlineData("/usr/lib/libSystem.B.dylib")]
    [InlineData("libmissing.63.dylib")]
    public void LibraryLinksOutsideThePackageAreRejected(string target)
    {
        using MemoryStream archive = Archive(Package(), Link("lib/libavcodec.63.dylib", target));
        Assert.Throws<InvalidDataException>(() => MacFfmpegPrebuiltInstall.ExtractPackageTar(archive, directory, Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(Root + "/share/ffmpeg/presets.txt")]
    [InlineData(Root + "/lib/pkgconfig/libavcodec.pc")]
    [InlineData("other-root/bin/ffmpeg")]
    [InlineData(Root + "/../bin/ffmpeg")]
    public void UnexpectedPackageEntriesAreRejected(string name)
    {
        using MemoryStream archive = Archive([.. Package(), (name, "unexpected")]);
        Assert.Throws<InvalidDataException>(() => MacFfmpegPrebuiltInstall.ExtractPackageTar(archive, directory, Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void PackageWithoutLicenseNoticesIsRejected()
    {
        using MemoryStream archive = Archive(Package().Where(file => !file.Name.Contains("/LICENSES/", StringComparison.Ordinal)).ToArray());
        Assert.Throws<InvalidDataException>(() => MacFfmpegPrebuiltInstall.ExtractPackageTar(archive, directory, Root, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void ForeignBinOrLibDirectoriesAreNeverReplaced()
    {
        Directory.CreateDirectory(Path.Combine(directory, "lib"));
        Assert.Throws<InvalidOperationException>(() => MacFfmpegPrebuiltInstall.RequireReplaceable(directory));
        File.WriteAllText(Path.Combine(directory, MacFfmpegPrebuiltInstall.MarkerName), "lib");
        MacFfmpegPrebuiltInstall.RequireReplaceable(directory);
    }

    [Fact]
    public void PackagedToolsAreResolvedOnlyOnMacOSAndOnlyWithoutATopLevelTool()
    {
        Directory.CreateDirectory(Path.Combine(directory, "bin"));
        File.WriteAllText(Path.Combine(directory, "bin", "ffmpeg"), "packaged");
        Assert.Equal(Path.Combine(directory, "bin", "ffmpeg"), MediaToolPaths.ResolveExecutable(directory, "ffmpeg", HostPlatform.MacOS));
        Assert.Equal(Path.Combine(directory, "ffmpeg"), MediaToolPaths.ResolveExecutable(directory, "ffmpeg", HostPlatform.Linux));
        File.WriteAllText(Path.Combine(directory, "ffmpeg"), "top-level");
        Assert.Equal(Path.Combine(directory, "ffmpeg"), MediaToolPaths.ResolveExecutable(directory, "ffmpeg", HostPlatform.MacOS));
    }

    [Fact]
    public async Task PrebuiltInstallIsOfferedOnlyOnMacOS()
    {
        if (OperatingSystem.IsMacOS())
        {
            return;
        }
        using HttpClient client = new();
        await Assert.ThrowsAsync<PlatformNotSupportedException>(() => MacFfmpegPrebuiltInstall.InstallAsync(client, directory, cancellationToken: TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(directory));
    }

    private static (string Name, string Content)[] Package() =>
    [
        (Root + "/BUILDINFO.txt", "build"),
        (Root + "/LICENSES/ffmpeg/COPYING.LGPLv2.1", "lgpl"),
        (Root + "/LICENSES/ffmpeg/LICENSE.md", "license"),
        (Root + "/bin/ffmpeg", "ffmpeg"),
        (Root + "/bin/ffprobe", "ffprobe"),
        (Root + "/bin/ffplay", "excluded"),
        (Root + "/include/libavcodec/avcodec.h", "header"),
        (Root + "/lib/libavcodec.63.1.101.dylib", "avcodec")
    ];

    private static PaxTarEntry Link(string relative, string target) => new(TarEntryType.SymbolicLink, Root + "/" + relative) { LinkName = target };

    private static MemoryStream Archive(params (string Name, string Content)[] files) => Archive(files, []);

    private static MemoryStream Archive((string Name, string Content)[] files, params PaxTarEntry[] links)
    {
        MemoryStream stream = new();
        using (TarWriter writer = new(stream, leaveOpen: true))
        {
            foreach ((string name, string content) in files)
            {
                using MemoryStream data = new(Encoding.UTF8.GetBytes(content));
                writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = data });
            }
            foreach (PaxTarEntry link in links)
            {
                writer.WriteEntry(link);
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
