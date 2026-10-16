using System.Buffers.Binary;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BoundedMovieAdvanceTests
{
    [Fact]
    public void Bounded_clock_uses_consecutive_authenticated_frames_and_ordinary_completion()
    {
        string game = InstalledGame();
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.SetMovieSource(new NativeMovieSource(0, 0));
        session.RequestMovie(0);
        MovieClock clock = new(session, game);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceFrames(0));
        Assert.Throws<NotSupportedException>(() => clock.AdvanceFrames(1));
        Assert.Null(session.State.MoviePlayback);
        List<int> frames = [];
        clock.AdvanceFrame = (playback, seconds) =>
        {
            frames.Add(playback.FrameIndex);
            session.SetMoviePlayback(playback);
            session.SetMoviePosition(seconds);
            return session.IsMoviePlaying;
        };
        MovieInformation information = clock.Prepare();
        Assert.Equal(3, clock.AdvanceFrames(3));
        Assert.Equal([2, 3, 4], frames);
        Assert.True(session.IsMoviePlaying);
        Assert.False(session.State.MoviePlayback!.Skipped);
        Assert.Equal(4 / information.FramesPerSecond, session.State.MovieSeconds);
        Assert.Equal(2, clock.AdvanceFrames(2));
        Assert.Equal([2, 3, 4, 5, 6], frames);
        int remaining = information.FrameCount - 7;
        Assert.Equal(remaining, clock.AdvanceFrames(30000));
        Assert.Equal(Enumerable.Range(2, information.FrameCount - 2), frames);
        Assert.Equal(new MoviePlaybackState(information.FrameCount - 1, information.FrameCount - 1, false, true, false), session.State.MoviePlayback);
        Assert.False(session.IsMoviePlaying);
    }

    [Fact]
    public void Actual_movie_wait_and_raw_input_match_decoded_callbacks_and_restore_between_frames()
    {
        string game = InstalledGame();
        string source = Path.Combine(Repository(), "../fixtures/external-fixture.json");
        Assert.True(File.Exists(source), "The actual standalone pre-callback movie source is required.");
        GameState state = GameSession.Load(source);
        GameSession session = new(state);
        MovieClock clock = new(session, game);
        using GameWorld world = new(session, game, clock);
        world.EnsureField();
        GameSession referenceSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!);
        MovieClock referenceClock = new(referenceSession, game);
        using GameWorld reference = new(referenceSession, game, referenceClock);
        reference.EnsureField();
        Assert.True(session.IsMoviePlaying);
        long before = world.Events.Frame;
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, Value: 16)).Accepted);
        Assert.True(reference.Apply(new PlayerAction(PlayerActionKind.FieldButtons, Value: 16)).Accepted);
        Assert.Equal(before, world.Events.Frame);
        PlayerActionResult result = world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 5));
        MovieInformation info = referenceClock.Prepare();
        int first = Math.Max(2, referenceClock.State.FrameIndex + 1);
        for (int index = first; index < first + 5; index++)
        {
            Assert.True(reference.AdvanceMovieFrame(new MoviePlaybackState(index, info.FrameCount - 1, true, false, false), index / info.FramesPerSecond));
        }
        Assert.True(result.Accepted);
        Assert.Equal(5, result.FramesAdvanced);
        Assert.Equal(before + 5, world.Events.Frame);
        Assert.Equal(session.InputConfiguration!.Map(16), session.FieldButtonInput!.Held);
        Assert.Equal(JsonSerializer.SerializeToElement(session.FieldButtonInput).GetRawText(), JsonSerializer.SerializeToElement(referenceSession.FieldButtonInput).GetRawText());
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), reference.Events.Capture()));
        Assert.Equal(session.State.MoviePlayback, referenceSession.State.MoviePlayback);
        Assert.False(session.State.MoviePlayback!.Skipped);
        session.SetEventState(world.Events.Capture());
        GameSession resumed = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        MovieClock resumedClock = new(resumed, game);
        using GameWorld restored = new(resumed, game, resumedClock);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, Value: 0)).Accepted);
        Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.FieldButtons, Value: 0)).Accepted);
        Assert.Equal(3, world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3)).FramesAdvanced);
        Assert.Equal(3, restored.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 3)).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(session.State.MoviePlayback, resumed.State.MoviePlayback);
        Assert.Equal((ushort)0, session.FieldButtonInput!.RawHeld);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void Movie_wait_stops_at_confirmable_dialogue_and_keeps_native_fault_boundary()
    {
        string game = InstalledGame();
        using GameAssets assets = new(game);
        foreach (bool fault in new[] { false, true })
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            EventEngine engine = new(session);
            engine.LoadField(100, Program(fault ? Jsm.Opcode.CARDGAME : Jsm.Opcode.AMESW), ["Stop here"], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            session.SetMovieSource(new NativeMovieSource(0, 0));
            session.RequestMovie(0);
            MovieClock clock = new(session, game);
            engine.MoviePlayback = () => clock.State;
            engine.AdvanceMovieFrames = clock.AdvanceFrames;
            clock.AdvanceFrame = (playback, seconds) =>
            {
                session.SetMoviePlayback(playback);
                session.SetMoviePosition(seconds);
                engine.Step();
                return session.IsMoviePlaying;
            };
            if (fault)
            {
                Assert.Throws<EventScriptException>(() => engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 100)));
                Assert.NotNull(engine.LastFault);
                Assert.Equal("CARDGAME", engine.LastFault.Opcode);
                Assert.True(session.IsMoviePlaying);
                Assert.False(session.State.MoviePlayback!.Completed);
                JsonElement fiber = engine.Capture().GetProperty("Fibers")[1];
                Assert.Equal(engine.LastFault.Operation, fiber.GetProperty("Position").GetInt32());
                Assert.Equal(0, fiber.GetProperty("Stack")[0].GetInt32());
            }
            else
            {
                Assert.Equal(1, engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 100)).FramesAdvanced);
                Assert.True(engine.CanConfirmDialogue);
                int frame = clock.State.FrameIndex;
                Assert.Equal(0, engine.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 100)).FramesAdvanced);
                Assert.Equal(frame, clock.State.FrameIndex);
                Assert.True(session.IsMoviePlaying);
            }
        }
    }

    private static byte[] Program(Jsm.Opcode command)
    {
        (Jsm.Opcode Opcode, int Parameter)[] ops = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (command, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] bytes = new byte[18 + ops.Length * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)ops.Length);
        for (int index = 0; index < ops.Length; index++)
        {
            (Jsm.Opcode opcode, int parameter) = ops[index];
            int encoded = parameter != 0 || opcode == Jsm.Opcode.PSHN_L ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18 + index * 4), encoded);
        }
        return bytes;
    }

    private static string InstalledGame()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original movie and field corpus is required.");
        return game;
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("Research checkout was not found.");
    }
}
