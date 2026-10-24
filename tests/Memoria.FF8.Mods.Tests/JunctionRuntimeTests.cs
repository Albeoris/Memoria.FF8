using System.Text;

namespace Memoria.FF8.Mods.Tests;

public sealed class JunctionRuntimeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-junction-runtime-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FolderMetadataAndFilesFollowTheGlobalAndExplicitEnablePolicy()
    {
        Write("mods.ini", "auto_enable_new_mods = 0\n");
        WritePackage("Example", "Example display name");
        Write("Junction/Example/Assets/movies/opening.avi", "replacement movie");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal("Junction/Example", package.Id);
        Assert.Equal("Example display name", package.Name);
        Assert.Equal(ModFormat.Junction, package.Format);
        Assert.Equal(ModEffectiveStatus.Disabled, package.Status);
        Assert.Null(package.ExplicitEnabled);
        Assert.Empty(runtime.Catalog.Files);
        Assert.True(runtime.SetEnabled(package.Id, true).Success);
        Publish(runtime);
        ModFile file = Assert.Single(runtime.Catalog.FindAll(ModFormat.Junction, "MOVIES\\OPENING.AVI"));
        Assert.Equal("replacement movie", Encoding.UTF8.GetString(file.ReadBytes()));
        Assert.Contains("enabled = 1", File.ReadAllText(Path.Combine(root, "Mods/Junction/Example/mod.ini")));
        Assert.True(runtime.SetEnabled(package.Id, false).Success);
        Publish(runtime);
        Assert.Empty(runtime.Catalog.Files);
        Assert.Equal(ModEffectiveStatus.Disabled, Assert.Single(runtime.Catalog.Packages).Status);
    }

    [Fact]
    public void ArchiveSettingsUseASidecarAndInheritedPackagesArePinnedWhenTheGlobalPolicyChanges()
    {
        string archive = Path.Combine(root, "Mods", "Junction", "Example.iroj");
        WriteArchive(archive, ("mod.xml", Metadata("Archive name")), ("Assets/movies/intro.cam", "camera"));
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        ModPackage package = Assert.Single(runtime.Catalog.Packages);
        Assert.Equal("Junction/Example.iroj", package.Id);
        Assert.Equal("Archive name", package.Name);
        Assert.Equal(archive, package.RootDirectory);
        Assert.Equal("camera", Encoding.UTF8.GetString(Assert.Single(runtime.Catalog.Files).ReadBytes()));
        Assert.True(runtime.SetAutoEnableNewMods(false).Success);
        Publish(runtime);
        Assert.Contains("enabled = 1", File.ReadAllText(archive + ".ini"));
        Assert.True(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        Assert.True(runtime.SetEnabled(package.Id, false).Success);
        Publish(runtime);
        Assert.Contains("enabled = 0", File.ReadAllText(archive + ".ini"));
        Assert.Empty(runtime.Catalog.Files);
        Assert.True(runtime.SetAutoEnableNewMods(true).Success);
        Publish(runtime);
        Assert.False(Assert.Single(runtime.Catalog.Packages).RequestedEnabled);
        Assert.Empty(runtime.Catalog.Files);
    }

    [Fact]
    public void RootsAreDeduplicatedAndGamePackagesWinInNameOrder()
    {
        string game = Path.Combine(root, "game");
        WritePackage("zeta", "Zeta");
        Write("Junction/zeta/Assets/movies/intro.cam", "zeta");
        WritePackage("EXAMPLE", "Shadowed");
        Write("Junction/EXAMPLE/Assets/movies/intro.cam", "application");
        WriteAt(game, "Junction/example/mod.xml", Metadata("Installed"));
        WriteAt(game, "Junction/example/Assets/movies/intro.cam", "game");
        WriteAt(game, "Junction/Alpha/mod.xml", Metadata("Alpha"));
        WriteAt(game, "Junction/Alpha/Assets/movies/intro.cam", "alpha");
        using ModRuntime runtime = new(root, watch: false, additionalBaseDirectories: [game, Path.Combine(game, "."), root]);
        Publish(runtime);
        Assert.Equal(new[] { "Junction/Alpha", "Junction/example", "Junction/zeta" }, runtime.Catalog.Packages.Select(package => package.Id));
        IReadOnlyList<ModFile> files = runtime.Catalog.FindAll(ModFormat.Junction, "movies/intro.cam");
        Assert.Equal(new[] { "alpha", "game", "zeta" }, files.Select(file => Encoding.UTF8.GetString(file.ReadBytes())));
        Assert.Single(runtime.Diagnostics, diagnostic => diagnostic.Code == "duplicate-package");
    }

    [Fact]
    public void RefreshAdvancesRevisionsAndAnUnreadObsoleteRevisionCannotReadNewBytes()
    {
        WritePackage("Example", "Example");
        Write("Junction/Example/Assets/movies/intro.cam", "old");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        ModFile old = Assert.Single(runtime.Catalog.Files);
        Write("Junction/Example/Assets/movies/intro.cam", "new camera bytes");
        Assert.Throws<IOException>(() => old.CopyTo(new MemoryStream()));
        runtime.RequestRefresh();
        Publish(runtime);
        ModFile current = Assert.Single(runtime.Catalog.Files);
        Assert.True(current.Revision > old.Revision);
        using MemoryStream destination = new();
        current.CopyTo(destination);
        Assert.Equal("new camera bytes", Encoding.UTF8.GetString(destination.ToArray()));
    }

    [Fact]
    public void UnsupportedPackageDoesNotHideOtherPackagesAndEnablingFfmpegHasNoDiagnostic()
    {
        Write("Junction/Broken/mod.xml", Metadata("Broken", "<FFNxConfig><foo>1</foo></FFNxConfig>"));
        Write("Junction/Broken/Assets/movies/intro.cam", "broken");
        Write("Junction/Working/mod.xml", Metadata("Working", "<FFNxConfig><enable_ffmpeg_videos>1</enable_ffmpeg_videos></FFNxConfig>"));
        Write("Junction/Working/Assets/movies/intro.cam", "working");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.ReloadFailure, runtime.Catalog.Packages.Single(package => package.Id == "Junction/Broken").Status);
        Assert.Equal(ModEffectiveStatus.Active, runtime.Catalog.Packages.Single(package => package.Id == "Junction/Working").Status);
        Assert.Equal("Junction/Working", Assert.Single(runtime.Catalog.Files).PackageId);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-import-failed" && diagnostic.PackageId == "Junction/Broken");
        Assert.DoesNotContain(runtime.Diagnostics, diagnostic => diagnostic.PackageId == "Junction/Working");
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("<enable_ffmpeg_videos>1</enable_ffmpeg_videos>", false)]
    [InlineData("<enable_ffmpeg_videos>true</enable_ffmpeg_videos>", false)]
    [InlineData("<enable_ffmpeg_videos>0</enable_ffmpeg_videos>", true)]
    [InlineData("<enable_ffmpeg_videos>false</enable_ffmpeg_videos>", true)]
    [InlineData("<enable_ffmpeg_videos Enabled='0'>0</enable_ffmpeg_videos>", false)]
    public void OnlyExplicitActiveFfmpegDisablingProducesAWarning(string setting, bool warningExpected)
    {
        Write("Junction/Example/mod.xml", Metadata("Example", $"<ConfigOption><ID>Enabled</ID><Type>Bool</Type><Default>1</Default></ConfigOption><FFNxConfig>{setting}<ffmpeg_video_ext>mp4</ffmpeg_video_ext></FFNxConfig>"));
        Write("Junction/Example/Assets/movies/intro.mp4", "movie");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.Equal(ModEffectiveStatus.Active, Assert.Single(runtime.Catalog.Packages).Status);
        Assert.Equal("Junction/Example", Assert.Single(runtime.Catalog.Files).PackageId);
        if (warningExpected)
        {
            ModDiagnostic diagnostic = Assert.Single(runtime.Diagnostics);
            Assert.Equal("junction-setting-ignored", diagnostic.Code);
            Assert.Equal("Junction/Example", diagnostic.PackageId);
            Assert.Contains("disables FFmpeg videos, which is not supported", diagnostic.Message);
            Assert.Contains("remains enabled", diagnostic.Message);
        }
        else
        {
            Assert.Empty(runtime.Diagnostics);
        }
    }

    [Fact]
    public void RequiredPackagesMustBeActiveAndBeforeOverridesAlphabeticalPrecedence()
    {
        Guid baseId = Guid.NewGuid();
        Guid addonId = Guid.NewGuid();
        Write("Junction/A_Base/mod.xml", $"<ModInfo><ID>{baseId}</ID><ModFolder Folder='Assets'/></ModInfo>");
        Write("Junction/A_Base/Assets/movies/intro.cam", "base");
        Write("Junction/Z_Addon/mod.xml", $"<ModInfo><ID>{addonId}</ID><Compatibility><Require ModID='{baseId}'>Base</Require></Compatibility><OrderConstraints><Before>{baseId}</Before></OrderConstraints><ModFolder Folder='Assets'/><FFNxConfig><ffmpeg_video_ext>mp4</ffmpeg_video_ext></FFNxConfig></ModInfo>");
        Write("Junction/Z_Addon/Assets/movies/intro.cam", "addon");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.Equal(new[] { "Junction/Z_Addon", "Junction/A_Base" }, runtime.Catalog.FindAll(ModFormat.Junction, "movies/intro.cam").Select(file => file.PackageId));
        Assert.Equal("mp4", runtime.Catalog.Packages[0].JunctionMetadata!.VideoExtension);
        Assert.True(runtime.SetEnabled("Junction/A_Base", false).Success);
        Publish(runtime);
        Assert.Empty(runtime.Catalog.Files);
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-dependency-inactive" && diagnostic.PackageId == "Junction/Z_Addon");
    }

    [Fact]
    public void MissingRequirementsAndOrderCyclesRejectOnlyAffectedPackages()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        Write("Junction/A/mod.xml", $"<ModInfo><ID>{first}</ID><OrderConstraints><Before>{second}</Before></OrderConstraints></ModInfo>");
        Write("Junction/A/movies/intro.cam", "a");
        Write("Junction/B/mod.xml", $"<ModInfo><ID>{second}</ID><OrderConstraints><Before>{first}</Before></OrderConstraints></ModInfo>");
        Write("Junction/B/movies/intro.cam", "b");
        Write("Junction/C/mod.xml", $"<ModInfo><Compatibility><Require ModID='{Guid.NewGuid()}'>Missing</Require></Compatibility></ModInfo>");
        Write("Junction/C/movies/intro.cam", "c");
        WritePackage("Working", "Working");
        Write("Junction/Working/Assets/movies/intro.cam", "working");
        using ModRuntime runtime = Runtime();
        Publish(runtime);
        Assert.Equal("Junction/Working", Assert.Single(runtime.Catalog.Files).PackageId);
        Assert.Equal(2, runtime.Diagnostics.Count(diagnostic => diagnostic.Code == "junction-order-cycle"));
        Assert.Contains(runtime.Diagnostics, diagnostic => diagnostic.Code == "junction-dependency-missing");
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private ModRuntime Runtime()
    {
        return new(root, watch: false, debounce: TimeSpan.Zero);
    }

    private void WritePackage(string folder, string name)
    {
        Write($"Junction/{folder}/mod.xml", Metadata(name));
    }

    private static string Metadata(string name, string extra = "")
    {
        return $"<ModInfo><Name>{name}</Name><ID>99e6cd7c-970d-45a4-8096-3c980b94bd7a</ID><Version>1</Version><ModFolder Folder=\"Assets\" />{extra}</ModInfo>";
    }

    private void Write(string relative, string text)
    {
        WriteAt(root, relative, text);
    }

    private static void WriteAt(string directory, string relative, string text)
    {
        string path = Path.Combine(directory, "Mods", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void WriteArchive(string path, params (string Name, string Text)[] files)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.Unicode, true);
        writer.Write(0x534f5249);
        writer.Write(0x10002);
        writer.Write(0);
        writer.Write(16);
        writer.Write(files.Length);
        long offset = 20 + files.Sum(file => 20 + Encoding.Unicode.GetByteCount(file.Name));
        foreach ((string name, string text) in files)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes(name);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            writer.Write((ushort)(20 + nameBytes.Length));
            writer.Write((ushort)nameBytes.Length);
            writer.Write(nameBytes);
            writer.Write(0);
            writer.Write(offset);
            writer.Write(bytes.Length);
            offset += bytes.Length;
        }
        foreach ((string name, string text) in files)
        {
            writer.Write(Encoding.UTF8.GetBytes(text));
        }
    }

    private static void Publish(ModRuntime runtime)
    {
        Assert.True(runtime.PreparePending(force: true));
        Assert.True(runtime.CommitPending());
    }
}

