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

    [Fact]
    public void MovieComponentWinsAsAUnitAndIncompleteRefreshRetainsIt()
    {
        const string path = "movies/disc00_30h.avi";
        const string camera = "movies/disc00_30.cam";
        WritePackage("loose", 1000);
        Write("loose", path, "loose movie");
        Write("loose", camera, "loose camera");
        Write("loose", SubtitlePath, Document("loose subtitles"));
        WriteMovieComponent("first", -2);
        WriteMovieComponent("winner", 1);
        using GameResourceHost host = OpenHost();

        Assert.Equal("winner video", File.ReadAllText(host.ResolveGameFile(path)));
        Assert.Equal("winner camera", File.ReadAllText(host.ResolveGameFile(camera)));
        Assert.Equal("winner", Assert.Single(host.ResolveMovieSubtitles(0, 30)!.Cues).Text);
        long generation = host.Mods.Catalog.Generation;

        File.Delete(Path.Combine(root, "Mods", "Memoria", "winner", camera));
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Mods.Catalog.Generation);
        Assert.Equal("winner camera", File.ReadAllText(host.ResolveGameFile(camera)));
        Assert.Equal("winner", Assert.Single(host.ResolveMovieSubtitles(0, 30)!.Cues).Text);
        Assert.Contains(host.Mods.Diagnostics, diagnostic => diagnostic.Code == "Memoria.InvalidMovieComponents" && diagnostic.PackageId == "Memoria/winner");

        Assert.True(host.Mods.SetEnabled("Memoria/winner", false).Success);
        Assert.True(host.Pump(true));
        Assert.Equal("first video", File.ReadAllText(host.ResolveGameFile(path)));
        Assert.Equal("first camera", File.ReadAllText(host.ResolveGameFile(camera)));
        Assert.Equal("first", Assert.Single(host.ResolveMovieSubtitles(0, 30)!.Cues).Text);
    }

    [Fact]
    public void AuthoredSubtitlesCanChangeWithoutReplacingDependentVideoOrCamera()
    {
        const string video = "movies/disc00_30h.avi";
        const string camera = "movies/disc00_30.cam";
        WriteSplitMovieComponent("opening-media");
        using GameResourceHost host = OpenHost();
        string videoPath = host.ResolveGameFile(video);
        string cameraPath = host.ResolveGameFile(camera);
        Assert.Equal("dependent video", File.ReadAllText(videoPath));
        Assert.Equal("dependent camera", File.ReadAllText(cameraPath));
        Assert.Equal("original subtitles", Assert.Single(host.ResolveMovieSubtitles(0, 30)!.Cues).Text);

        Write("translation", SubtitlePath, Document("revised subtitles"));
        host.Mods.RequestRefresh();
        Assert.True(host.Pump(true));
        Assert.Equal("revised subtitles", Assert.Single(host.ResolveMovieSubtitles(0, 30)!.Cues).Text);
        Assert.Equal(videoPath, host.ResolveGameFile(video));
        Assert.Equal(cameraPath, host.ResolveGameFile(camera));

        long generation = host.Mods.Catalog.Generation;
        WriteMovieDependencyDeclaration("undeclared-media");
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Mods.Catalog.Generation);
        Assert.Contains(host.Mods.Diagnostics, item => item.Code == "Memoria.InvalidMovieComponents" && item.Message.Contains("must be declared", StringComparison.Ordinal));
        Assert.Equal("revised subtitles", Assert.Single(host.ResolveMovieSubtitles(0, 30)!.Cues).Text);

        WriteMovieDependencyDeclaration("opening-media");
        File.Delete(Path.Combine(root, "Mods", "Memoria", "opening-media", camera));
        host.Mods.Refresh();
        Assert.False(host.Pump(true));
        Assert.Equal(generation, host.Mods.Catalog.Generation);
        Assert.Equal("dependent camera", File.ReadAllText(host.ResolveGameFile(camera)));
        Assert.Equal("revised subtitles", Assert.Single(host.ResolveMovieSubtitles(0, 30)!.Cues).Text);
    }

    [Fact]
    public void InactiveMovieDependencyCannotSupplyAPublishedComponent()
    {
        const string video = "movies/disc00_30h.avi";
        WriteSplitMovieComponent("opening-media");
        string rootDir = Path.Combine(root, "Mods", "Memoria");
        ModPackage owner = new("Memoria/translation", "Translation", ModFormat.Memoria, true, true, ModEffectiveStatus.Active, rootDir)
        {
            Descriptor = NativeModDescriptor.Read(Path.Combine(rootDir, "translation", NativeModDescriptor.FileName))
        };
        ModPackage media = new("Memoria/opening-media", "Opening media", ModFormat.Memoria, false, false, ModEffectiveStatus.Disabled, rootDir)
        {
            Descriptor = NativeModDescriptor.Read(Path.Combine(rootDir, "opening-media", NativeModDescriptor.FileName))
        };
        ModFile manifest = new(owner.Id, ModFormat.Memoria, MoviePackagePaths.ComponentsDocument, 1, () => File.ReadAllBytes(Path.Combine(rootDir, "translation", MoviePackagePaths.ComponentsDocument)));
        ModCatalogSnapshot catalog = new(1, [owner, media], [manifest]);
        Assert.False(GameResourceHost.TryPrepareMovieSubtitles(catalog, out var publication, out var diagnostics));
        Assert.Empty(publication.Components);
        Assert.Contains(diagnostics, item => item.Code == "Memoria.InvalidMovieComponents" && item.Message.Contains("inactive", StringComparison.Ordinal));

        ModFile mediaVideo = new(media.Id, ModFormat.Memoria, video, 2, () => []);
        Assert.Null(GameResourceHost.FindFileReplacement(new(2, [media with { Status = ModEffectiveStatus.Active }], [mediaVideo]), video));
    }

    [Fact]
    public void PublishedSplitMoviePackageComposesItsRealMediaAndTranslation()
    {
        string? translation = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOVIE_TRANSLATION_PACKAGE");
        string? media = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOVIE_MEDIA_PACKAGE");
        Assert.SkipUnless(Directory.Exists(translation) && Directory.Exists(media), "Set MEMORIA_FF8_MOVIE_TRANSLATION_PACKAGE and MEMORIA_FF8_MOVIE_MEDIA_PACKAGE to the two published movie packages.");
        NativeModDescriptor ownerDescriptor = NativeModDescriptor.Read(Path.Combine(translation!, NativeModDescriptor.FileName));
        NativeModDescriptor mediaDescriptor = NativeModDescriptor.Read(Path.Combine(media!, NativeModDescriptor.FileName));
        ModPackage owner = new(ownerDescriptor.RuntimePackageId, ownerDescriptor.Name, ModFormat.Memoria, true, true, ModEffectiveStatus.Active, translation!) { Descriptor = ownerDescriptor };
        ModPackage dependency = new(mediaDescriptor.RuntimePackageId, mediaDescriptor.Name, ModFormat.Memoria, true, true, ModEffectiveStatus.Active, media!) { Descriptor = mediaDescriptor };
        const string videoPath = "movies/disc00_30h.avi";
        const string cameraPath = "movies/disc00_30.cam";
        ModFile manifest = MovieMember(owner, MoviePackagePaths.ComponentsDocument, 1);
        ModFile subtitles = MovieMember(owner, SubtitlePath, 2);
        ModFile video = MovieMember(dependency, videoPath, 3);
        ModFile camera = MovieMember(dependency, cameraPath, 4);
        ModCatalogSnapshot catalog = new(1, [owner, dependency], [manifest, subtitles, video, camera]);

        Assert.True(GameResourceHost.TryPrepareMovieSubtitles(catalog, out var publication, out var diagnostics), string.Join("\n", diagnostics));
        Assert.NotEmpty(publication.Tracks[(0, 30)].Cues);
        Assert.Same(video, GameResourceHost.FindFileReplacement(catalog, videoPath, publication.Components));
        Assert.Same(camera, GameResourceHost.FindFileReplacement(catalog, cameraPath, publication.Components));
    }

    private static ModFile MovieMember(ModPackage package, string relative, long revision)
    {
        string path = Path.Combine(package.RootDirectory, relative);
        Assert.True(new FileInfo(path).Length > 0, relative);
        return new(package.Id, ModFormat.Memoria, relative, revision, () => File.ReadAllBytes(path));
    }

    private void WriteSplitMovieComponent(string dependency)
    {
        WritePackage("translation", 0);
        Write("translation", NativeModDescriptor.FileName, """
            { "id": "translation", "name": "Translation", "package": "Localization", "description": { "en": "Movie translation." },
              "kind": "language", "languageOffers": [ { "locale": "ru", "label": "Translation", "option": "ru", "nativeVariants": [ { "edition": "Classic", "language": "en" } ] } ],
              "dependencies": [ "opening-media" ] }
            """);
        WriteMovieDependencyDeclaration(dependency);
        Write("translation", SubtitlePath, Document("original subtitles"));
        Write("translation", "movies/disc00_30h.avi", "unrelated owner video");
        WritePackage("opening-media", 0);
        Write("opening-media", NativeModDescriptor.FileName, """
            { "id": "opening-media", "name": "Opening Media", "package": "Localization", "description": { "en": "Opening media." },
              "kind": "dependency", "nativeVariants": [ { "edition": "Classic", "language": "en" } ] }
            """);
        Write("opening-media", "movies/disc00_30h.avi", "dependent video");
        Write("opening-media", "movies/disc00_30.cam", "dependent camera");
        Write("opening-media", SubtitlePath, Document("unrelated dependency subtitles"));
    }

    private void WriteMovieDependencyDeclaration(string dependency)
    {
        Write("translation", MoviePackagePaths.ComponentsDocument, $$"""
            { "version": 1, "components": [ { "video": "movies/disc00_30h.avi", "priority": 100, "mediaDependency": "{{dependency}}" } ] }
            """);
    }

    private void WriteMovieComponent(string id, int priority)
    {
        WritePackage(id, 0);
        Write(id, MoviePackagePaths.ComponentsDocument, FormattableString.Invariant($$"""
            { "version": 1, "components": [ { "video": "movies/disc00_30h.mp4", "priority": {{priority}} } ] }
            """));
        Write(id, "movies/disc00_30h.mp4", id + " video");
        Write(id, "movies/disc00_30.cam", id + " camera");
        Write(id, SubtitlePath, Document(id));
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
