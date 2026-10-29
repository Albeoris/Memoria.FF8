using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Media;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class OpeningMusicTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AuthenticOpeningPublishesMusicBeforeTheClassroom(bool devices, bool playCorridorMovie)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        var clock = new MovieClock(session, directory);
        using var world = new GameWorld(session, directory, clock);
        using FieldAudioPresentation? audio = devices ? new FieldAudioPresentation(directory, clock.ToolsDirectory) : null;
        audio?.SetOutputPreferences(0, 0);
        audio?.Attach(world.Events);
        List<NativeMusicPlayRequest> requests = [];
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeMusicPlayRequest or NativeMusicVolume or NativeMusicStop)
            {
                Console.WriteLine(JsonSerializer.Serialize(command, command.GetType()));
            }
            if (command is NativeMusicPlayRequest request)
            {
                requests.Add(request);
            }
        };
        int centering = 0;
        bool playingBeforeClassroom = false;
        for (int action = 0; action < 8000; action++)
        {
            world.EnsureField();
            if (world.Field!.Id == 232 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                break;
            }
            if (session.IsMoviePlaying && session.State.MovieIndex == 0 && playCorridorMovie)
            {
                PlayDecodedCorridorMovie(world, session, clock, directory, audio!);
                continue;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next;
            if (session.IsMoviePlaying)
            {
                next = new PlayerAction(PlayerActionKind.SkipMovie);
            }
            else if (dialogue?.IsNamePrompt == true)
            {
                next = new PlayerAction(PlayerActionKind.EnterName, Text: "Squall");
            }
            else if (dialogue is { CanConfirm: true, FirstChoice: int choice })
            {
                next = new PlayerAction(PlayerActionKind.Choose, choice);
            }
            else if (world.Events.CanConfirmDialogue)
            {
                next = new PlayerAction(PlayerActionKind.Confirm);
            }
            else if (world.Field.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                next = centering++ < 2 ? new PlayerAction(PlayerActionKind.Move, Horizontal: -1) : new PlayerAction(PlayerActionKind.Move, Vertical: 1);
            }
            else
            {
                next = new PlayerAction(PlayerActionKind.Wait);
            }
            PlayerActionResult result = world.Apply(next);
            Assert.True(result.Accepted);
            AdvanceClosingDialogue(world, next, result);
            audio?.Update();
            if (audio is not null && !playingBeforeClassroom && requests.Any(request => request is { FieldId: 229, Music: 26, ConsumedPending: true }))
            {
                Assert.Equal(229, world.Events.FieldId);
                Assert.Null(audio.Error);
                var deadline = Stopwatch.StartNew();
                while (!audio.IsMidiPlaying && deadline.ElapsedMilliseconds < 3000)
                {
                    Thread.Sleep(20);
                }
                Assert.True(audio.IsMidiPlaying, "Original Garden segment must start in the corridor before classroom entry.");
                playingBeforeClassroom = true;
            }
        }
        Assert.Equal(232, world.Field!.Id);
        Assert.Contains(requests, request => request.ConsumedPending && request.Music == 26 && request.FieldId != 232);
        if (audio is not null)
        {
            Assert.True(playingBeforeClassroom);
            Assert.Null(audio.Error);
            Assert.True(audio.IsMidiPlaying, "Original Garden segment must be playing before classroom input is enabled.");
        }
    }

    private static void AdvanceClosingDialogue(GameWorld world, PlayerAction action, PlayerActionResult result)
    {
        if (action.Kind == PlayerActionKind.Wait && result.FramesAdvanced == 0 && world.Events.CurrentDialogue is { CanConfirm: false, IsNamePrompt: false })
        {
            long frame = world.Events.Frame;
            world.AdvanceAutoPlayInputFrame();
            Assert.Equal(frame + 1, world.Events.Frame);
            Assert.Null(world.Events.LastFault);
        }
    }

    private static void PlayDecodedCorridorMovie(GameWorld world, GameSession session, MovieClock clock, string directory, FieldAudioPresentation audio)
    {
        MovieInformation information = clock.Prepare();
        using var decoder = new FfmpegMovieDecoder(clock.ToolsDirectory, clock.SourcePath, 0, information);
        var deadline = Stopwatch.StartNew();
        int count = 0;
        while ((!decoder.VideoFinished || !decoder.AudioFinished) && session.IsMoviePlaying)
        {
            Assert.True(deadline.Elapsed < TimeSpan.FromSeconds(30), "Full corridor movie decoding must complete.");
            while (decoder.TryGetAudio(out _))
            {
            }
            while (session.IsMoviePlaying && decoder.TryGetNextFrame(double.PositiveInfinity, out MovieFrame? frame))
            {
                int index = checked((int)frame!.Index);
                session.SetMoviePlayback(new MoviePlaybackState(index, information.FrameCount - 1, true, false, false));
                session.SetMoviePosition(index / information.FramesPerSecond);
                if (index >= 2)
                {
                    world.Events.Step();
                }
                audio.Update();
                count++;
            }
            Thread.Sleep(1);
        }
        Assert.Null(decoder.Error);
        Assert.Equal(information.FrameCount, count);
        if (session.IsMoviePlaying)
        {
            session.SetMoviePlayback(new MoviePlaybackState(information.FrameCount - 1, information.FrameCount - 1, false, true, false));
            session.SetMoviePosition(information.DurationSeconds);
            session.CompleteMovie();
        }
        Assert.Null(audio.Error);
        Assert.True(audio.IsMidiPlaying, "Garden music must survive every ordinary movie0 callback and completion.");
        Console.WriteLine($"Decoded all {count} original movie0 frames and drained PCM; Garden DirectMusic remains playing.");
    }

    [DllImport("user32.dll")]
    private static extern nint GetDesktopWindow();
}
