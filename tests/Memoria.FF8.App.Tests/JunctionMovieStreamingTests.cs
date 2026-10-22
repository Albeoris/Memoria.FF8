using System.Diagnostics;
using System.Globalization;
using System.Text;
using Memoria.FF8.Media;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionMovieStreamingTests(ITestOutputHelper output) : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ff8-junction-streaming-" + Guid.NewGuid().ToString("N"));
    private const string Manifest = "<ModInfo><Name>Example</Name><ModFolder Folder='Assets' /></ModInfo>";

    [Theory]
    [InlineData(0x10000)]
    [InlineData(0x10001)]
    [InlineData(0x10002)]
    public void UncompressedSegmentsIdentifyOnlyTheirOwnPayload(int version)
    {
        string archivePath = Archive(version, ("movies/first.avi", 0, "first"u8.ToArray()), ("movies/second.avi", 0, "second"u8.ToArray()));
        JunctionArchive archive = new(archivePath);
        var first = archive.Find("MOVIES\\FIRST.AVI")!.FileSegment;
        var second = archive.Find("movies/second.avi")!.FileSegment;
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(Path.GetFullPath(archivePath), first.Path);
        Assert.True(first.Offset > 20);
        Assert.Equal(5L, first.Length);
        Assert.Equal(first.Offset + first.Length, second.Offset);
        Assert.Equal(6L, second.Length);
        Assert.Equal(new FileInfo(archivePath).Length, second.Offset + second.Length);
        using FileStream file = File.OpenRead(first.Path);
        file.Position = first.Offset;
        byte[] bytes = new byte[checked((int)first.Length)];
        file.ReadExactly(bytes);
        Assert.Equal("first"u8.ToArray(), bytes);
        Assert.Equal((int)'s', file.ReadByte());
    }

    [Theory]
    [InlineData(1, "B0FCQ+7z")]
    [InlineData(2, "CQAAAAUAAABdAAAQAAAgkISHJf6Bo///YWQAAA==")]
    public void CompressedMoviesHaveNoDirectSegmentAndStillMaterializeDecodedBytes(int flags, string encoded)
    {
        string archivePath = Package(("Assets/movies/opening.avi", flags, Convert.FromBase64String(encoded)));
        Assert.Null(new JunctionArchive(archivePath).Find("Assets/movies/opening.avi")!.FileSegment);
        Assert.Null(Assert.Single(JunctionModImporter.Open(archivePath).Files).FileSegment);
        using GameResourceHost host = Host();
        ModFile movie = Assert.Single(host.Mods.Catalog.FindAll(ModFormat.Junction, "movies/opening.avi"));
        Assert.Null(movie.ResolveFileSegment());
        string resolved = host.ResolveGameFile("movies/opening.avi");
        Assert.Equal("ABCABCABC", File.ReadAllText(resolved));
        Assert.Equal(resolved, host.ResolveGameFile("movies/opening.avi"));
    }

    [Fact]
    public void SelectedArchiveMovieUsesExactSubfileBoundsWhileCameraUsesASmallFile()
    {
        string archivePath = Package(("Assets/movies/opening.avi", 0, "movie bytes"u8.ToArray()), ("Assets/movies/opening.cam", 0, "camera bytes"u8.ToArray()), ("Inactive/movies/opening.avi", 0, "wrong movie"u8.ToArray()));
        JunctionReplacement imported = Assert.Single(JunctionModImporter.Open(archivePath).Files, file => file.RelativePath == "movies/opening.avi");
        Assert.NotNull(imported.FileSegment);
        using GameResourceHost host = Host();
        ModFile movie = Assert.Single(host.Mods.Catalog.FindAll(ModFormat.Junction, "movies/opening.avi"));
        var segment = movie.ResolveFileSegment();
        Assert.NotNull(segment);
        Assert.Equal(imported.FileSegment, segment);
        string resolved = host.ResolveGameFile("MOVIES\\OPENING.AVI");
        Assert.Equal(Subfile(segment.Path, segment.Offset, segment.Length), resolved);
        Assert.Equal(resolved, host.ResolveGameFile("movies/opening.avi"));
        string camera = host.ResolveGameFile("movies/opening.cam");
        Assert.Equal("camera bytes", File.ReadAllText(camera));
        Assert.Equal(camera, host.ResolveGameFile("movies/opening.cam"));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(camera)!));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedArchiveIsRejectedAtEveryResolutionAndRefreshIndexesTheNewVersion(bool resolveBeforeChange)
    {
        string archivePath = Package(("Assets/movies/opening.avi", 0, "old movie"u8.ToArray()));
        using GameResourceHost host = Host();
        ModFile stale = Assert.Single(host.Mods.Catalog.FindAll(ModFormat.Junction, "movies/opening.avi"));
        if (resolveBeforeChange)
        {
            Assert.StartsWith("subfile,", host.ResolveGameFile("movies/opening.avi"));
        }
        WriteArchive(archivePath, 0x10002, ("mod.xml", 0, Encoding.UTF8.GetBytes(Manifest)), ("Assets/movies/opening.avi", 0, "new and longer movie"u8.ToArray()));
        Assert.Throws<IOException>(() => stale.ResolveFileSegment());
        Assert.Throws<IOException>(() => host.ResolveGameFile("movies/opening.avi"));
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "junction-replacement-failed");
        host.Mods.RequestRefresh();
        Assert.True(host.Pump(true));
        var current = Assert.Single(host.Mods.Catalog.FindAll(ModFormat.Junction, "movies/opening.avi")).ResolveFileSegment();
        Assert.NotNull(current);
        Assert.Equal(20L, current.Length);
        Assert.Equal(Subfile(archivePath, current.Offset, current.Length), host.ResolveGameFile("movies/opening.avi"));
        Assert.Throws<IOException>(() => stale.ResolveFileSegment());
    }

    [Fact]
    public void PinnedMovieSurvivesDisablingTheModButRejectsAChangedArchive()
    {
        string archivePath = Package(("Assets/movies/disc00_00h.avi", 0, "movie"u8.ToArray()), ("Assets/movies/disc00_00.cam", 0, "camera"u8.ToArray()));
        using GameResourceHost host = Host();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetMovieSource(new NativeMovieSource(0, 0));
        session.RequestMovie(0);
        using MovieClock clock = new(session, root, resolveGameFile: host.ResolveGameFile);
        string pinned = clock.SourcePath;
        Assert.True(host.Mods.SetEnabled(Assert.Single(host.Mods.Catalog.Packages).Id, false).Success);
        Assert.True(host.Pump(true));
        Assert.Equal(pinned, clock.SourcePath);
        File.AppendAllText(archivePath, "changed");
        Assert.Throws<IOException>(() => clock.SourcePath);
    }

    [Theory]
    [InlineData(16L)]
    [InlineData(long.MaxValue)]
    public void InvalidDirectoryOrOverflowingPayloadBoundsCannotPublishSegments(long offset)
    {
        string path = Archive(0x10002, ("x", 0, [1]));
        using (FileStream stream = new(path, FileMode.Open, FileAccess.Write))
        using (BinaryWriter writer = new(stream))
        {
            stream.Position = 20 + 4 + 2 + 4;
            writer.Write(offset);
        }
        Assert.Throws<InvalidDataException>(() => new JunctionArchive(path));
    }

    [Fact]
    public void InstalledArchiveMovieProbesAndDecodesFirstAndSeekFramesDirectly()
    {
        string game = TestResources.GameDirectory;
        string archivePath = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_CINEMA_ARCHIVE") ?? Path.Combine(game, "Mods", "Junction", "cinema-rf.iroj");
        Assert.SkipUnless(Directory.Exists(game) && File.Exists(archivePath), "An installed game and the configured Junction cinema archive are required.");
        string? junctionDirectory = Path.GetDirectoryName(Path.GetFullPath(archivePath));
        Assert.SkipUnless(string.Equals(Path.GetFileName(junctionDirectory), "Junction", StringComparison.OrdinalIgnoreCase), "The configured archive must be in a Mods/Junction directory for runtime discovery.");
        string tools = MediaToolsInstallation.ResolveDirectory(Environment.GetEnvironmentVariable("MEMORIA_FF8_MEDIA_TOOLS"));
        Assert.SkipUnless(File.Exists(Path.Combine(tools, "ffprobe.exe")) && File.Exists(Path.Combine(tools, "ffmpeg.exe")), "Installed FFmpeg tools are required; MEMORIA_FF8_MEDIA_TOOLS may select their directory.");
        JunctionModPackage package = JunctionModImporter.Open(archivePath);
        JunctionReplacement movie = package.Files.FirstOrDefault(file => file.RelativePath == "movies/disc00_30h.avi" && file.FileSegment is not null) ?? package.Files.First(file => file.RelativePath.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) && file.FileSegment is not null);
        var expected = movie.FileSegment!;
        using GameResourceHost host = new(game, Path.Combine(root, "application"), watch: false, modsDirectory: Path.GetDirectoryName(junctionDirectory)!);
        Stopwatch timer = Stopwatch.StartNew();
        string source = host.ResolveGameFile(movie.RelativePath);
        Assert.Equal(Subfile(expected.Path, expected.Offset, expected.Length), source);
        output.WriteLine($"Archive source resolution: {timer.Elapsed.TotalMilliseconds:F1} ms; movie payload: {expected.Length} bytes.");
        timer.Restart();
        MovieInformation information = FfmpegMovieDecoder.ProbePresentation(tools, source);
        output.WriteLine($"Presentation metadata probe: {timer.Elapsed.TotalMilliseconds:F1} ms.");
        Assert.True(information.Width > 0 && information.Height > 0);
        Assert.True(double.IsFinite(information.FramesPerSecond) && information.FramesPerSecond > 0);
        Assert.True(double.IsFinite(information.DurationSeconds) && information.DurationSeconds > 0);
        Assert.Equal(0, information.FrameCount);
        DecodeFirstFrame(tools, source, information, 0);
        DecodeFirstFrame(tools, source, information, Math.Min(2, information.DurationSeconds / 2));
        JunctionReplacement? camera = package.Files.FirstOrDefault(file => file.RelativePath == Path.ChangeExtension(movie.RelativePath, ".cam"));
        if (camera is not null)
        {
            string cameraPath = host.ResolveGameFile(camera.RelativePath);
            using MemoryStream expectedCamera = new();
            camera.CopyTo(expectedCamera);
            Assert.Equal(expectedCamera.ToArray(), File.ReadAllBytes(cameraPath));
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(cameraPath)!));
        }
    }

    private void DecodeFirstFrame(string tools, string source, MovieInformation information, double startSeconds)
    {
        Stopwatch timer = Stopwatch.StartNew();
        using FfmpegMovieDecoder decoder = new(tools, source, startSeconds, information);
        MovieFrame? frame = null;
        while (frame is null && timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            decoder.TryGetNextFrame(0, out frame);
            if (frame is null)
            {
                Assert.Null(decoder.Error);
                Thread.Sleep(10);
            }
        }
        Assert.NotNull(frame);
        Assert.Equal(checked(information.Width * information.Height * 4), frame.Rgba.Length);
        Assert.Equal(0L, frame.Index);
        Assert.Null(decoder.Error);
        output.WriteLine($"First decoded frame at {startSeconds:F3}s: {timer.Elapsed.TotalMilliseconds:F1} ms; {frame.Rgba.Length} RGBA bytes.");
    }

    private GameResourceHost Host()
    {
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        return new(root, Path.Combine(root, "application"), watch: false);
    }

    private static string Subfile(string path, long offset, long length)
    {
        return $"subfile,,start,{offset.ToString(CultureInfo.InvariantCulture)},end,{checked(offset + length).ToString(CultureInfo.InvariantCulture)},,:file:{path}";
    }

    private string Package(params (string Name, int Flags, byte[] Content)[] files)
    {
        return Archive(0x10002, new[] { ("mod.xml", 0, Encoding.UTF8.GetBytes(Manifest)) }.Concat(files).ToArray());
    }

    private string Archive(int version, params (string Name, int Flags, byte[] Content)[] files)
    {
        string path = Path.Combine(root, "Mods", "Junction", Guid.NewGuid().ToString("N") + ".iroj");
        WriteArchive(path, version, files);
        return path;
    }

    private static void WriteArchive(string path, int version, params (string Name, int Flags, byte[] Content)[] files)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream, Encoding.Unicode);
        writer.Write(0x534f5249);
        writer.Write(version);
        writer.Write(0);
        writer.Write(16);
        writer.Write(files.Length);
        int fixedSize = version == 0x10000 ? 16 : 20;
        long offset = 20 + files.Sum(file => fixedSize + Encoding.Unicode.GetByteCount(file.Name));
        foreach ((string name, int flags, byte[] content) in files)
        {
            byte[] encoded = Encoding.Unicode.GetBytes(name);
            writer.Write((ushort)(fixedSize + encoded.Length));
            writer.Write((ushort)encoded.Length);
            writer.Write(encoded);
            writer.Write(flags);
            if (version == 0x10000)
            {
                writer.Write(checked((int)offset));
            }
            else
            {
                writer.Write(offset);
            }
            writer.Write(content.Length);
            offset += content.Length;
        }
        foreach ((string _, int _, byte[] content) in files)
        {
            writer.Write(content);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
