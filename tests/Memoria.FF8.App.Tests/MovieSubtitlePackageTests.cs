using System.Text;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Media;
using Memoria.FF8.Mods;

namespace Memoria.FF8.App.Tests;

public sealed class MovieSubtitlePackageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "memoria-movie-subtitles-" + Guid.NewGuid().ToString("N"));
    private const string SubtitlePath = "movies/disc00_30.subtitles.json";

    [Fact]
    public void TracksBindByMovieAndMalformedRefreshRetainsThePreviousPublication()
    {
        WritePackage("lower", 10);
        Write("lower", SubtitlePath, Document("lower priority"));
        WritePackage("preferred", 20);
        Write("preferred", SubtitlePath, Document("Субтитры"));
        Write("preferred", "movies/disc04_00.subtitles.json", Document("startup"));
        using GameResourceHost host = OpenHost();
        MovieSubtitleTrack accepted = Assert.IsType<MovieSubtitleTrack>(host.ResolveMovieSubtitles(0, 30));
        Assert.Equal("Субтитры", Assert.Single(accepted.Cues).Text);
        Assert.Equal("startup", Assert.Single(Assert.IsType<MovieSubtitleTrack>(host.ResolveMovieSubtitles(4, 0)).Cues).Text);
        Assert.Null(host.ResolveMovieSubtitles(1, 30));

        long generation = host.Mods.Catalog.Generation;
        Write("preferred", SubtitlePath, "{ incomplete");
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Mods.Catalog.Generation);
        Assert.Same(accepted, host.ResolveMovieSubtitles(0, 30));
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidMovieSubtitles" && diagnostic.PackageId == "Memoria/preferred" && diagnostic.RelativePath == SubtitlePath);

        Assert.True(host.Mods.SetEnabled("Memoria/preferred", false).Success);
        Assert.True(host.Pump(true));
        Assert.Equal("lower priority", Assert.Single(Assert.IsType<MovieSubtitleTrack>(host.ResolveMovieSubtitles(0, 30)).Cues).Text);
        Assert.Null(host.ResolveMovieSubtitles(4, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeMp4AndCameraUseTheWinningMoviePackage(bool hasCompanion)
    {
        const string videoPath = "movies/disc00_30h.avi";
        const string cameraPath = "movies/disc00_30.cam";
        WritePackage("camera-only", 50);
        Write("camera-only", cameraPath, "other camera");
        WritePackage("movie", 5);
        Write("movie", "movies/disc00_30h.mp4", "movie bytes");
        if (hasCompanion)
        {
            Write("movie", cameraPath, "same-package camera");
        }
        using GameResourceHost host = OpenHost();

        Assert.Equal("movie bytes", File.ReadAllText(host.ResolveGameFile(videoPath)));
        if (hasCompanion)
        {
            Assert.Equal("same-package camera", File.ReadAllText(host.ResolveGameFile(cameraPath)));
        }
        else
        {
            IGameFileSource original = Assert.IsAssignableFrom<IGameFileSource>(host.Resources.GetSource<IGameFileSource>());
            Assert.Equal(original.ResolveFile(cameraPath), host.ResolveGameFile(cameraPath));
        }
    }

    [Fact]
    public void NativeMovieVideoWinsOverJunctionForTheSameAddress()
    {
        const string path = "movies/disc00_30h.avi";
        ModPackage junction = new("Junction/legacy", "Legacy", ModFormat.Junction, true, true, ModEffectiveStatus.Active, root);
        ModPackage native = new("Memoria/movie", "Native", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, root);
        ModFile legacyVideo = new(junction.Id, junction.Format, path, 1, () => []);
        ModFile nativeVideo = new(native.Id, native.Format, "movies/disc00_30h.mp4", 2, () => []);
        ModCatalogSnapshot catalog = new(1, [junction, native], [legacyVideo, nativeVideo]);

        Assert.Same(nativeVideo, GameResourceHost.FindFileReplacement(catalog, path));
    }

    [Theory]
    [InlineData("\"startSeconds\":0,\"startSeconds\":0,")]
    [InlineData("\"startSeconds\":-0.25,")]
    [InlineData("\"startSeconds\":0,\"keyframes\":[{\"seconds\":0.75},{\"seconds\":0.5}],")]
    [InlineData("\"startSeconds\":0,\"keyframes\":[{\"seconds\":0,\"reveal\":1.1}],")]
    [InlineData("\"startSeconds\":0,\"style\":{\"fontSize\":0},")]
    [InlineData("\"startSeconds\":0,\"style\":{\"colour\":\"#AA00FF\"},")]
    public void ParserRejectsAmbiguousOrUnsupportedCueValues(string properties)
    {
        byte[] document = Encoding.UTF8.GetBytes("{\"version\":1,\"cues\":[{" + properties + "\"endSeconds\":1,\"text\":\"word\"}]}");
        Exception? error = Record.Exception(() => MovieSubtitleTrack.Parse(document));
        Assert.True(error is InvalidDataException or JsonException, error?.ToString());
    }

    private static string Document(string text) => $$"""
        { "version": 1, "cues": [ { "startSeconds": 0, "endSeconds": 1, "text": {{JsonSerializer.Serialize(text)}} } ] }
        """;

    private GameResourceHost OpenHost() => new(Path.Combine(root, "empty-classic-game"), root, watch: false);

    private void WritePackage(string id, int priority)
    {
        Write(id, NativeModDescriptor.FileName, $$"""{ "id": "{{id}}", "name": "Movie test package", "package": "Memoria", "description": { "en": "Movie test package." } }""");
        Write(id, "mod.ini", FormattableString.Invariant($"enabled=1\npriority={priority}\n"));
    }

    private void Write(string package, string relative, string content)
    {
        string path = Path.Combine(root, "Mods", "Memoria", package, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
