using Memoria.FF8.Resources;
using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class MovieReplacementTimingTests
{
    [Fact]
    public void Presentation_frames_map_to_detected_native_timing_without_extra_callbacks()
    {
        MovieInformation native = new(320, 224, 12, 1, 12);
        int[] mapped = Enumerable.Range(0, 24).Select(frame => MovieFrameTimeline.FrameAt(frame / 24d, native)).ToArray();
        Assert.Equal(Enumerable.Range(0, 12).SelectMany(frame => new[] { frame, frame }), mapped);
        Assert.Equal(7, MovieFrameTimeline.FrameAt(7 / native.FramesPerSecond, native));
        Assert.Equal(11, MovieFrameTimeline.FrameAt(100, native));
        MovieFrameTimeline timeline = new(native, 0);
        List<int> callbacks = [];
        foreach (int mediaFrame in Enumerable.Range(0, 24))
        {
            callbacks.AddRange(timeline.AdvanceTo(mediaFrame / 24d));
        }
        Assert.Equal(Enumerable.Range(0, 12), callbacks);
        MovieFrameTimeline resumed = new(native, 5);
        Assert.Equal(new[] { 5 }, resumed.AdvanceTo(5 / native.FramesPerSecond));
        Assert.Empty(resumed.AdvanceTo(5 / native.FramesPerSecond));
        Assert.Equal(new[] { 6, 7, 8 }, resumed.AdvanceTo(8 / native.FramesPerSecond));
        Assert.Equal(new[] { 9, 10, 11 }, resumed.AdvanceTo(100));
        Assert.Throws<InvalidDataException>(() => MovieFrameTimeline.FrameAt(double.NaN, native));
    }

    [Fact]
    public void Replacement_camera_uses_its_interpolated_records_and_keeps_native_flag_contract()
    {
        byte[] original = Camera([16, 8, 0, 16], countHeader: false);
        byte[] replacement = Camera([16, 16, 8, 8, 0, 0], countHeader: true);
        BinaryPrimitives.WriteInt16LittleEndian(replacement.AsSpan(8 + 3 * 44), 1234);
        NativeMovieCameraFlags nativeFlags = NativeMovieCameraReader.Read(original);
        MoviePresentationCamera camera = new(replacement, nativeFlags, new(320, 224, 12, 1, 4), new(960, 672, 24, 1, 6));
        Assert.Equal(6, camera.Flags.Flags.Length);
        Assert.Equal(3, camera.DrawRecord(3));
        Assert.Equal((short)1234, BinaryPrimitives.ReadInt16LittleEndian(camera.Bytes.AsSpan(8 + 3 * 44)));
        Assert.Equal((byte)1, camera.DrawValue(2));
        Assert.Equal((byte)0, camera.DrawValue(3));
        Assert.Equal(5, camera.DrawRecord(100));
        Assert.Throws<NotSupportedException>(() => NativeMovieCameraReader.Read(replacement));
        replacement[8 + 3 * 44 + 40] = 16;
        Assert.Throws<NotSupportedException>(() => new MoviePresentationCamera(replacement, nativeFlags, new(320, 224, 12, 1, 4), new(960, 672, 24, 1, 6)));
    }

    [Fact]
    public void Native_camera_header_bounds_playback_even_with_complete_trailing_records()
    {
        byte[] bytes = Camera([16, 8, 0, 16, 8], countHeader: false);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 2);
        NativeMovieCameraFlags flags = NativeMovieCameraReader.Read(bytes);
        Assert.Equal(new byte[] { 16, 8, 0 }, flags.Flags);
    }

    [Fact]
    public void Resolved_media_is_pinned_together_for_the_active_playback()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original movie and field corpus is required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetMovieSource(new NativeMovieSource(0, 0));
        session.RequestMovie(0);
        List<string> resolved = [];
        bool changed = false;
        MovieClock clock = new(session, game, resolveGameFile: relative =>
        {
            resolved.Add(relative);
            return changed ? Path.Combine("changed", relative) : Path.Combine(game, "Data", relative.Replace('/', Path.DirectorySeparatorChar));
        });
        clock.Prepare();
        string avi = clock.SourcePath;
        string cam = clock.CameraPath;
        changed = true;
        Assert.Equal(avi, clock.SourcePath);
        Assert.Equal(cam, clock.CameraPath);
        Assert.Equal(2, resolved.Count);
    }

    [Fact]
    public void Compatible_standalone_movie_snapshot_continues_without_migration()
    {
        string game = TestResources.GameDirectory;
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOVIE_SAVE");
        Assert.SkipUnless(Directory.Exists(game) && !string.IsNullOrEmpty(source), "Set MEMORIA_FF8_MOVIE_SAVE to an authentic compatible standalone active-movie save.");
        GameSession session = new(GameSession.Load(source!));
        Assert.True(session.IsMoviePlaying);
        MovieClock clock = new(session, game);
        using GameWorld world = new(session, game, clock);
        world.EnsureField();
        MovieInformation native = clock.Prepare();
        long before = world.Events.Frame;
        Assert.Equal(3, clock.AdvanceFrames(3));
        Assert.Equal(before + 3, world.Events.Frame);
        session.SetEventState(world.Events.Capture());
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        MovieClock restoredClock = new(restored, game);
        using GameWorld restoredWorld = new(restored, game, restoredClock);
        restoredWorld.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restoredWorld.Events.Capture()));
        Assert.Equal(native.FrameCount - 1, restored.State.MoviePlayback!.TerminalFrame);
        Assert.Equal(2, clock.AdvanceFrames(2));
        Assert.Equal(2, restoredClock.AdvanceFrames(2));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restoredWorld.Events.Capture()));
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOVIE_SAVE_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            session.SetEventState(world.Events.Capture());
            session.Save(output);
        }
    }

    [Fact]
    public void Installed_cinema_movie_uses_resolved_video_camera_and_actual_decoded_pixels()
    {
        using IResourceSource movieSource = GameDataSourceFactory.Open(TestResources.GameDirectory);
        IGameFileSource movieFiles = Assert.IsAssignableFrom<IGameFileSource>(movieSource);

        string game = TestResources.GameDirectory;
        string package = Path.Combine(game, "Mods", "Junction", "cinema-rf.iroj");
        Assert.SkipUnless(File.Exists(package), "The actual installed cinema-rf Junction package is required.");
        string applicationBase = Path.Combine(Path.GetTempPath(), "Memoria.FF8.MovieTimingTests", Guid.NewGuid().ToString("N"));
        using GameResourceHost host = new(game, applicationBaseDirectory: applicationBase, watch: false);
        // This is a resource-only decoder fixture, not a reconstructed gameplay checkpoint.
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetMovieSource(new NativeMovieSource(0, 0));
        session.RequestMovie(0);
        MovieClock clock = new(session, game, resolveGameFile: host.ResolveGameFile);
        MovieInformation native = clock.Prepare();
        MovieInformation media = clock.PreparePresentation();
        Assert.NotEqual(MovieClock.GetSourcePath(movieFiles, 0, 0), clock.SourcePath);
        Assert.NotEqual(MovieClock.GetCameraPath(movieFiles, 0, 0), clock.CameraPath);
        Assert.True(media.Width > 0 && media.Height > 0);
        Assert.True(media.FramesPerSecond > native.FramesPerSecond);
        Assert.Equal(native.FrameCount - 1, session.State.MoviePlayback!.TerminalFrame);
        NativeMovieCameraFlags nativeFlags = NativeMovieCameraReader.Read(File.ReadAllBytes(MovieClock.GetCameraPath(movieFiles, 0, 0)));
        MoviePresentationCamera camera = new(File.ReadAllBytes(clock.CameraPath), nativeFlags, native, media);
        Assert.True(camera.Flags.Flags.Length > nativeFlags.Flags.Length);
        using FfmpegMovieDecoder decoder = new(clock.ToolsDirectory, clock.SourcePath, information: media);
        MovieFrame? first = null;
        System.Diagnostics.Stopwatch timeout = System.Diagnostics.Stopwatch.StartNew();
        while (first is null && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            decoder.TryGetNextFrame(0, out first);
            if (first is null)
            {
                Thread.Sleep(10);
            }
        }
        Assert.NotNull(first);
        Assert.Equal(checked(media.Width * media.Height * 4), first.Rgba.Length);
        Assert.Null(decoder.Error);
        MovieFrameTimeline nativeTimeline = new(native, 0);
        List<int> callbacks = [];
        for (int mediaFrame = 0; mediaFrame < media.FrameCount; mediaFrame++)
        {
            callbacks.AddRange(nativeTimeline.AdvanceTo(mediaFrame / media.FramesPerSecond));
        }
        callbacks.AddRange(nativeTimeline.AdvanceTo((native.FrameCount - 1) / native.FramesPerSecond));
        Assert.Equal(Enumerable.Range(0, native.FrameCount), callbacks);
        string? proofOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_MOVIE_PROOF_OUTPUT");
        if (!string.IsNullOrEmpty(proofOutput))
        {
            File.WriteAllText(proofOutput, JsonSerializer.Serialize(new { Native = native, Media = media, NativeCamRecords = nativeFlags.Flags.Length, MediaCamRecords = camera.Flags.Flags.Length, FirstDecodedRgbaBytes = first.Rgba.Length, NativeFrameSamples = callbacks.Count, PostStartupNativeCallbackCount = Math.Max(0, callbacks.Count - 2) }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static byte[] Camera(byte[] flags, bool countHeader)
    {
        byte[] bytes = new byte[8 + 44 * flags.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)(flags.Length - (countHeader ? 0 : 1))));
        for (int record = 0; record < flags.Length; record++)
        {
            bytes[8 + record * 44 + 40] = flags[record];
        }
        return bytes;
    }

}
