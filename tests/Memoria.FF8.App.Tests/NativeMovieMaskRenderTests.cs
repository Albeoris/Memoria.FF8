using System.Buffers.Binary;
using System.Numerics;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMovieMaskRenderTests
{
    [Fact]
    public void ActualCorridorMskUsesSignedSvectorTriplesAndCannotPaintOverDecodedMovie()
    {
        using GameAssets assets = new(GameDirectory());
        FieldAssets field = assets.LoadField(229);
        Assert.Equal(1, field.Information!.NativeMovieMaskFlag14);
        Assert.NotNull(field.MovieMask);
        Assert.NotEmpty(field.MovieMask.Triangles);
        byte[] cam = File.ReadAllBytes(Path.Combine(GameDirectory(), "Data/movies/disc00_00.cam"));
        NativeMovieCameraFlags flags = NativeMovieCameraReader.Read(cam);
        Assert.Equal(8, flags.Flags[88]);
        Assert.Equal(8, flags.Flags[89]);
        FieldCamera camera = FieldCamera.Read(cam.AsSpan(8 + 89 * 44, 40).ToArray());
        byte[] movie = MoviePixels();
        FieldRenderer renderer = new(field, 320, 224, nativeMovieViewport: true);
        Assert.Equal(movie, renderer.RenderMovieFrame(movie, 320, 224, camera, [], field.MovieMask));
        Assert.Throws<InvalidDataException>(() => FieldMovieMask.Read([1, 0, 0, 0]));
        byte[] record = new byte[28];
        BinaryPrimitives.WriteUInt16LittleEndian(record, 1);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(4), -1234);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(6), 2345);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(8), -32768);
        Assert.Equal(new Vector3(-1234, 2345, -32768), Assert.Single(FieldMovieMask.Read(record).Triangles).A);
    }

    [Fact]
    public void NativeMaskDepthPreservesMovieAndOccludesOnlyCoveredModelFragments()
    {
        using GameAssets assets = new(GameDirectory());
        FieldAssets original = assets.LoadField(229);
        FieldModel model = original.Models.First(model => model.Faces.Count > 0);
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(0, 0, 2048), Vector2.Zero, 1024);
        FieldAssets viewer = new(229, "declared-native-msk-component", [], [], [], camera, new FieldWalkmesh([]), [model]);
        FieldRenderer renderer = new(viewer, 320, 224, nativeMovieViewport: true);
        ActorVisual actor = new(0, 0, 0, 0, 0, 192, 0, 0, true, PolygonColor: new Vector3(128));
        FieldMovieMask half = new([
            new(new Vector3(-4096, -4096, 0), new Vector3(0, -4096, 0), new Vector3(0, 4096, 0)),
            new(new Vector3(-4096, -4096, 0), new Vector3(0, 4096, 0), new Vector3(-4096, 4096, 0))
        ]);
        byte[] movie = MoviePixels();
        byte[] visible = renderer.RenderMovieFrame(movie, 320, 224, camera, [actor], null).ToArray();
        byte[] masked = renderer.RenderMovieFrame(movie, 320, 224, camera, [actor], half).ToArray();
        int coveredModelPixels = 0;
        int retainedModelPixels = 0;
        for (int pixel = 0; pixel < movie.Length / 4; pixel++)
        {
            int offset = pixel * 4;
            bool modelVisible = !visible.AsSpan(offset, 4).SequenceEqual(movie.AsSpan(offset, 4));
            if (pixel % 320 < 160)
            {
                Assert.True(masked.AsSpan(offset, 4).SequenceEqual(movie.AsSpan(offset, 4)));
                if (modelVisible)
                {
                    coveredModelPixels++;
                }
            }
            else
            {
                Assert.True(masked.AsSpan(offset, 4).SequenceEqual(visible.AsSpan(offset, 4)));
                if (modelVisible)
                {
                    retainedModelPixels++;
                }
            }
        }
        Assert.True(coveredModelPixels > 0, "The negative control must contain real model fragments behind the mask.");
        Assert.True(retainedModelPixels > 0, "The mask must retain model fragments outside its projected triangles.");
        TestContext.Current.TestOutputHelper!.WriteLine($"Actual model {model.Name}: occluded={coveredModelPixels}, retained={retainedModelPixels}.");
        Assert.Equal(visible, renderer.RenderMovieFrame(movie, 320, 224, camera, [actor], null));
    }

    [Fact]
    public void GenuineCorridorMovieFramesMatchIndependentCamMaskComposition()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        MovieClock clock = new(session, GameDirectory());
        using GameWorld world = new(session, GameDirectory(), clock);
        StorySafeMovieSkipTests.ReachCorridorMovie(session, world);
        world.SetRenderSize(320, 224);
        FieldAssets field = world.Field!;
        byte[] cam = File.ReadAllBytes(Path.Combine(GameDirectory(), "Data/movies/disc00_00.cam"));
        FieldRenderer renderer = new(field, 320, 224, nativeMovieViewport: true);
        MovieInformation information = clock.Prepare();
        using FfmpegMovieDecoder decoder = new(clock.ToolsDirectory, clock.SourcePath, information: information);
        string evidence = Path.Combine(Repository(), "../fixtures");
        Directory.CreateDirectory(evidence);
        List<object> frames = [];
        int visibleTotal = 0;
        int occludedTotal = 0;
        Stopwatch timeout = Stopwatch.StartNew();
        while (session.IsMoviePlaying && session.State.MoviePlayback!.FrameIndex < information.FrameCount - 1 && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            while (decoder.TryGetAudio(out _))
            {
            }
            if (!decoder.TryGetNextFrame(double.MaxValue, out MovieFrame? frame))
            {
                Thread.Sleep(1);
                continue;
            }
            int record = checked((int)frame!.Index);
            if (record < 2)
            {
                continue;
            }
            bool stillPlaying = world.AdvanceMovieFrame(new(record, information.FrameCount - 1, true, false, false), record / information.FramesPerSecond);
            Assert.Equal(record < information.FrameCount - 1, stillPlaying);
            if (record is not (35 or 89 or 110 or 143))
            {
                continue;
            }
            FieldCamera camera = FieldCamera.Read(cam.AsSpan(8 + record * 44, 40).ToArray());
            ActorVisual[] actors = world.Events.Actors.Select(world.CreateVisual).ToArray();
            if ((cam[8 + (record - 1) * 44 + 40] & 0x61) != 0)
            {
                actors = [];
            }
            byte[] movie = renderer.RenderMovieFrame(frame.Rgba, information.Width, information.Height, camera, [], null).ToArray();
            byte[] visible = renderer.RenderMovieFrame(frame.Rgba, information.Width, information.Height, camera, actors, null).ToArray();
            byte[] masked = world.RenderMovieFrame(frame.Rgba, information.Width, information.Height).ToArray();
            //The original CAM enables the mask before89 and keeps it enabled at110/143.
            //This route's current movement/animation phase need not intersect the mask;
            //the declared depth component above requires real occluded fragments.
            Assert.Equal<byte?>((byte)(record == 35 ? 1 : 0), world.Events.Scene.NativeMovieMask!.Playback!.DrawValue);
            FieldMovieMask? expectedMask = record == 35 ? null : field.MovieMask;
            byte[] expected = renderer.RenderMovieFrame(frame.Rgba, information.Width, information.Height, camera, actors, expectedMask).ToArray();
            Assert.Equal(expected, masked);
            int visiblePixels = Enumerable.Range(0, movie.Length / 4).Count(pixel => !masked.AsSpan(pixel * 4, 4).SequenceEqual(movie.AsSpan(pixel * 4, 4)));
            int occludedPixels = Enumerable.Range(0, movie.Length / 4).Count(pixel => !visible.AsSpan(pixel * 4, 4).SequenceEqual(masked.AsSpan(pixel * 4, 4)));
            visibleTotal += visiblePixels;
            occludedTotal += occludedPixels;
            frames.Add(new { Record = record, world.Events.Frame, VisibleActorPixels = visiblePixels, OccludedActorPixels = occludedPixels, Mask = world.Events.Scene.NativeMovieMask, Actors = actors });
            WritePng(Path.Combine(evidence, $"corridor-{record}-movie.png"), movie);
            WritePng(Path.Combine(evidence, $"corridor-{record}-actors-without-mask.png"), visible);
            WritePng(Path.Combine(evidence, $"corridor-{record}-composite.png"), masked);
            if (record == 89)
            {
                session.SetEventState(world.Events.Capture());
                session.Save(Path.Combine(evidence, "corridor-movie89-authentic.json"));
                GameState rebuilt = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
                Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(rebuilt)));
                using GameWorld replay = new(new GameSession(rebuilt), GameDirectory());
                replay.EnsureField();
                replay.SetRenderSize(320, 224);
                Assert.Equal(masked, replay.RenderMovieFrame(frame.Rgba, information.Width, information.Height));
                JsonElement beforeRedisplay = replay.Events.Capture();
                Assert.True(replay.AdvanceMovieFrame(new MoviePlaybackState(89, information.FrameCount - 1, true, false, false), 89 / information.FramesPerSecond));
                Assert.True(JsonElement.DeepEquals(beforeRedisplay, replay.Events.Capture()));
                Assert.Equal(masked, replay.RenderMovieFrame(frame.Rgba, information.Width, information.Height));
                MovieFrame? next = null;
                Stopwatch nextTimeout = Stopwatch.StartNew();
                while (next is null && nextTimeout.Elapsed < TimeSpan.FromSeconds(10))
                {
                    while (decoder.TryGetAudio(out _))
                    {
                    }
                    decoder.TryGetNextFrame(double.MaxValue, out next);
                    if (next is null)
                    {
                        Thread.Sleep(1);
                    }
                }
                Assert.NotNull(next);
                Assert.Equal(90, next.Index);
                MoviePlaybackState nextPlayback = new(90, information.FrameCount - 1, true, false, false);
                Assert.True(world.AdvanceMovieFrame(nextPlayback, 90 / information.FramesPerSecond));
                Assert.True(replay.AdvanceMovieFrame(nextPlayback, 90 / information.FramesPerSecond));
                Assert.True(JsonElement.DeepEquals(world.Events.Capture(), replay.Events.Capture()));
                Assert.Equal(world.RenderMovieFrame(next.Rgba, information.Width, information.Height), replay.RenderMovieFrame(next.Rgba, information.Width, information.Height));
                File.WriteAllText(Path.Combine(evidence, "corridor-movie89-reconstruction-proof.json"), JsonSerializer.Serialize(new { Baseline = "Current Memoria movie89 snapshot and decoded AVI89/90", Implementation = "Independent current Memoria snapshot continuation without a decoder", FullState89 = true, Pixels89 = true, NextDecoded90VmState = true, NextDecoded90Pixels = true, OriginalRuntimePixelParity = false }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        File.WriteAllText(Path.Combine(evidence, "corridor-movie-mask-pixels.json"), JsonSerializer.Serialize(new { Baseline = "Current Memoria actual decoder/field VM", Implementation = "Current Memoria CPU CAM/MSK actor compositor", OriginalRuntimePixelParity = false, Frames = frames }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.Current.TestOutputHelper!.WriteLine($"Genuine original-script movie0 frames: retained actor pixels={visibleTotal}, MSK-occluded fragments={occludedTotal}; native runtime pixel parity is unclaimed.");
        Assert.Equal(information.FrameCount - 1, session.State.MoviePlayback!.FrameIndex);
        Assert.False(session.IsMoviePlaying);
        Assert.True(visibleTotal > 0);
        Assert.True(world.CanPresentCurrentField);
        world.Events.Step();
        Assert.Equal(232, session.State.FieldId);
        Assert.Equal(229, world.Field!.Id);
        Assert.Equal(PlayerTriggerKind.FieldTransition, world.Events.NextTrigger.Kind);
        Assert.False(world.CanPresentCurrentField);
        NativeFieldModelInstance instance = world.Events.Scene.ModelParts!.Instances.First(item => world.Events.Actors[item.ObjectId].Visible);
        NativeModelPartDraw draw = world.Events.Scene.ModelParts.Draws[instance.ObjectId];
        Assert.Equal(world.Events.Frame - 1, draw.Frame);
        Assert.Throws<NotSupportedException>(() => world.Events.GetNativeModelPartTranslations(instance.ObjectId));
        Assert.Throws<NotSupportedException>(() => world.Render(TimeSpan.Zero));
        object departure = new { VmField = world.Events.FieldId, SessionField = session.State.FieldId, Frame = world.Events.Frame, DrawFrame = draw.Frame, Trigger = world.Events.NextTrigger.Kind.ToString(), PresentationReady = world.CanPresentCurrentField };
        Assert.True(world.EnsureField());
        for (int callback = 0; callback < 500 && world.Events.CurrentDialogue is null; callback++)
        {
            world.Events.Step();
        }
        Assert.Equal(232, world.Field.Id);
        Assert.True(world.CanPresentCurrentField);
        Assert.Equal(0, world.Events.CurrentDialogue!.MessageId);
        Assert.NotEmpty(world.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
        File.WriteAllText(Path.Combine(evidence, "corridor-natural-movie-departure-proof.json"), JsonSerializer.Serialize(new { Baseline = "Current Memoria genuine decoded movie0 through original MAP232", Implementation = "Current Memoria presentation readiness gate", Departure = departure, Destination = new { Field = world.Field.Id, Frame = world.Events.Frame, PresentationReady = world.CanPresentCurrentField, Message = world.Events.CurrentDialogue.MessageId }, SkinValidatorStillRefusesStaleSample = true }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Repository()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
        {
            current = current.Parent;
        }
        return current!.FullName;
    }

    private static void WritePng(string path, byte[] rgba)
    {
        using Bitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, Color.FromArgb(rgba[offset + 3], rgba[offset], rgba[offset + 1], rgba[offset + 2]));
            }
        }
        bitmap.Save(path, ImageFormat.Png);
    }

    private static string GameDirectory()
    {
        return Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
    }

    private static byte[] MoviePixels()
    {
        byte[] pixels = new byte[320 * 224 * 4];
        for (int pixel = 0; pixel < pixels.Length / 4; pixel++)
        {
            pixels[pixel * 4] = (byte)(40 + pixel % 31);
            pixels[pixel * 4 + 1] = (byte)(90 + pixel % 47);
            pixels[pixel * 4 + 2] = (byte)(150 + pixel % 53);
            pixels[pixel * 4 + 3] = 255;
        }
        return pixels;
    }
}
