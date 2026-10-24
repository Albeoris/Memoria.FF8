using Memoria.FF8.Media;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("Installed Junction resources", DisableParallelization = true)]
public sealed class InstalledJunctionResourceCollection;

[Collection("Installed Junction resources")]
public sealed class JunctionInstalledPackageTests
{
    private static readonly string[] Roles = ["video-base", "video-layer", "video-alternate", "card-textures", "world-textures", "external-music"];

    [Fact]
    public void ExternalPackageSetLoadsTogetherThroughTheApplicationResourceHost()
    {
        var input = Inputs();
        using GameResourceHost host = new(TestResources.GameDirectory, Path.Combine(input.Mods, "test-application"), watch: false, modsDirectory: input.Mods);
        foreach (string archive in input.Archives.Values)
        {
            ModPackage package = Assert.Single(host.Mods.Catalog.Packages, package => package.RootDirectory.Equals(archive, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(ModEffectiveStatus.Active, package.Status);
            Assert.Contains(host.Mods.Catalog.Files, file => file.PackageId == package.Id);
        }
        Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "junction-import-failed" or "ffnx-unsupported-target" || diagnostic.Code.StartsWith("junction-dependency-", StringComparison.Ordinal));
    }

    [Fact]
    public void EachExternalVideoPackageResolvesAndDecodesItsOwnMedia()
    {
        var input = Inputs();
        string tools = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS") ?? "";
        Assert.SkipUnless(File.Exists(Path.Combine(tools, "ffmpeg.exe")) && File.Exists(Path.Combine(tools, "ffprobe.exe")), "Configure media tools for the optional external-video check.");
        Dictionary<string, byte[]?> configurations = input.Archives.Values.ToDictionary(path => path + ".ini", path => File.Exists(path + ".ini") ? File.ReadAllBytes(path + ".ini") : null, StringComparer.OrdinalIgnoreCase);
        try
        {
            using GameResourceHost host = new(TestResources.GameDirectory, Path.Combine(input.Mods, "test-application"), watch: false, modsDirectory: input.Mods);
            foreach (string role in new[] { "video-layer", "video-base", "video-alternate" })
            {
                foreach (string video in new[] { "video-base", "video-layer", "video-alternate" })
                {
                    ModPackage package = host.Mods.Catalog.Packages.Single(package => package.RootDirectory.Equals(input.Archives[video], StringComparison.OrdinalIgnoreCase));
                    Assert.True(host.Mods.SetEnabled(package.Id, video == role || video == "video-base" && role == "video-layer").Success);
                }
                host.Pump(true);
                ModPackage selected = host.Mods.Catalog.Packages.Single(package => package.RootDirectory.Equals(input.Archives[role], StringComparison.OrdinalIgnoreCase));
                Assert.Equal(ModEffectiveStatus.Active, selected.Status);
                string extension = "." + (selected.JunctionMetadata?.VideoExtension ?? "avi");
                ModFile movie = host.Mods.Catalog.Files.First(file => file.PackageId == selected.Id && file.RelativePath.StartsWith("movies/", StringComparison.OrdinalIgnoreCase) && file.RelativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
                string resolved = host.ResolveGameFile(Path.ChangeExtension(movie.RelativePath, "avi"));
                ModFileSegment? segment = movie.ResolveFileSegment();
                if (segment is not null)
                {
                    Assert.Contains(segment.Path, resolved);
                    Assert.Contains($"start,{segment.Offset},", resolved);
                }
                MovieInformation information = FfmpegMovieDecoder.ProbePresentation(tools, resolved);
                Assert.True(information.Width > 0 && information.Height > 0 && information.FramesPerSecond > 0);
                using FfmpegMovieDecoder decoder = new(tools, resolved, information: information);
                MovieFrame? frame = null;
                DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                while (frame is null && DateTime.UtcNow < deadline && !decoder.VideoFinished)
                {
                    decoder.TryGetNextFrame(0, out frame);
                    if (frame is null)
                    {
                        Thread.Sleep(10);
                    }
                }
                Assert.NotNull(frame);
                Assert.Equal(checked(information.Width * information.Height * 4), frame.Rgba.Length);
            }
        }
        finally
        {
            foreach (var configuration in configurations)
            {
                if (configuration.Value is null)
                {
                    File.Delete(configuration.Key);
                }
                else
                {
                    File.WriteAllBytes(configuration.Key, configuration.Value);
                }
            }
        }
    }

    private static (string Mods, Dictionary<string, string> Archives) Inputs()
    {
        Dictionary<string, string> archives = Roles.ToDictionary(role => role, role => TestResources.FixturePath("junction-" + role));
        string junction = Path.GetDirectoryName(archives.Values.First())!;
        Assert.All(archives.Values, archive => Assert.Equal(junction, Path.GetDirectoryName(archive), StringComparer.OrdinalIgnoreCase));
        Assert.Equal("Junction", Path.GetFileName(junction), StringComparer.OrdinalIgnoreCase);
        return (Path.GetDirectoryName(junction)!, archives);
    }
}
