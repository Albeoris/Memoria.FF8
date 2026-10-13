using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMovieTimerClockContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0));
        (GameSession Session, EventEngine Engine) Create(GameState? state = null)
        {
            GameSession session = new(state ?? new GameState { Scene = SceneKind.Field, ExamTimer = NativeExamTimerState.FromReachedSet(600).KillDisplay() });
            EventEngine engine = new(session, session.State.Variables) { MoviePlayback = () => session.State.MoviePlayback };
            engine.MovieSkipRequested += () => session.CompleteMovie(true);
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }

        var current = Create();
        current.Engine.Step();
        Require(current.Session.NativeMovieClockFlag == true && current.Session.ExamTimer!.ClockCalls == 2 && current.Engine.Scene.FieldCallbackClock is { OrdinaryCallbacks: 1, MovieCallbacks: 0 }, "MOVIE start retroactively added active-movie calls to the earlier pre-VM sample.");
        current.Engine.Step();
        Require(current.Session.ExamTimer!.ClockCalls == 6 && current.Engine.Scene.FieldCallbackClock is { OrdinaryCallbacks: 1, MovieCallbacks: 1 }, "An actual active movie callback did not run four native clock calls.");
        NativeExamTimerState expected = NativeExamTimerState.FromReachedSet(600).KillDisplay().AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 1).AdvanceCallbacks(NativeTimerCallbackKind.MovieField, 1);
        Require(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(current.Session.ExamTimer)), "Movie callbacks changed the correlated unknown native fractional phase.");

        current.Session.SetMoviePlayback(new MoviePlaybackState(350, 999, true, false, false));
        current.Session.SetMoviePosition(12345);
        current.Session.SetMoviePlayback(new MoviePlaybackState(999, 999, true, false, false));
        Require(current.Session.ExamTimer.ClockCalls == 6, "Decoded frame or duration samples fabricated native callback clocks.");
        JsonElement saved = current.Engine.Capture();
        var independent = Create(current.Session.State);
        independent.Engine.Restore(saved);
        Require(JsonElement.DeepEquals(saved, independent.Engine.Capture()), "Active movie callback ledger did not restore independently.");
        current.Engine.Step();
        independent.Engine.Step();
        Require(current.Session.ExamTimer.ClockCalls == 10 && JsonElement.DeepEquals(current.Engine.Capture(), independent.Engine.Capture()), "Restored active movie callback or timer phase diverged.");
        Require(current.Engine.Apply(new PlayerAction(PlayerActionKind.SkipMovie)) is { Accepted: true, FramesAdvanced: 0 }, "Ordinary movie skip was not accepted without a fabricated field callback.");
        independent.Engine.Apply(new PlayerAction(PlayerActionKind.SkipMovie));
        Require(current.Session.NativeMovieClockFlag == false && current.Session.ExamTimer.ClockCalls == 10 && current.Session.State.MoviePlayback is { Completed: true, Skipped: true }, "Skip completion failed to clear the movie lifecycle or inferred callbacks from the terminal frame.");
        current.Engine.Step();
        independent.Engine.Step();
        Require(current.Session.ExamTimer.ClockCalls == 12 && current.Session.State.Variables[5000] == 77 && JsonElement.DeepEquals(current.Engine.Capture(), independent.Engine.Capture()), "Post-skip VM continuation failed to return to two ordinary calls.");

        var natural = Create();
        natural.Engine.Step();
        natural.Session.SetMoviePlayback(new MoviePlaybackState(100, 100, false, true, false));
        natural.Session.CompleteMovie();
        Require(natural.Session.ExamTimer!.ClockCalls == 2 && natural.Session.NativeMovieClockFlag == false, "Natural movie cleanup introduced an unrecorded callback.");
        natural.Engine.Step();
        Require(natural.Session.ExamTimer.ClockCalls == 4 && natural.Session.State.Variables[5000] == 77, "Natural completion failed to resume the normal field clock.");

        UnprovedMovieClockServices unproved = new();
        EventEngine unsupported = new(unproved, new Dictionary<int, int>());
        unsupported.LoadField(100, content, []);
        try
        {
            unsupported.Step();
            throw new InvalidOperationException("Movie departure accepted a service without native lifecycle flag authority.");
        }
        catch (EventScriptException) when (unsupported.LastFault?.Opcode == "MOVIE")
        {
        }
        Require(!unproved.IsMoviePlaying && unproved.ExamTimer.ClockCalls == 2 && unsupported.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 4, "Unproved movie departure mutated its scene or advanced the original MOVIE operation.");

        foreach (Action<JsonNode> corrupt in new Action<JsonNode>[]
        {
            node => node["Scene"]!["FieldCallbackClock"] = null,
            node => node["Scene"]!["FieldCallbackClock"]!["MovieCallbacks"] = -1,
            node =>
            {
                node["Scene"]!["FieldCallbackClock"]!["OrdinaryCallbacks"] = 2;
                node["Scene"]!["FieldCallbackClock"]!["MovieCallbacks"] = 0;
            }
        })
        {
            var refused = Create(current.Session.State);
            JsonElement before = refused.Engine.Capture();
            JsonNode malformed = JsonNode.Parse(saved.GetRawText())!;
            corrupt(malformed);
            try
            {
                refused.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Missing or inconsistent native callback history was accepted.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, refused.Engine.Capture()), "Callback ledger refusal mutated the receiving VM.");
        }
        Console.WriteLine("Movie timer: accepted start callback2, active callbacks4, decoded samples0, skip/natural cleanup0, post-return callbacks2, independent restore and exact loader ledger validation passed.");
    }

    private sealed class UnprovedMovieClockServices : IEventServices
    {
        public NativeExamTimerState ExamTimer { get; private set; } = NativeExamTimerState.FromReachedSet(600).KillDisplay();
        public bool IsMoviePlaying { get; private set; }

        public void SetExamTimer(NativeExamTimerState timer)
        {
            ExamTimer = timer;
        }

        public void RequestMovie(int index)
        {
            IsMoviePlaying = true;
        }

        public void RequestField(int fieldId, int entryPoint)
        {
            throw new NotSupportedException();
        }

        public void ShowDialog(string text)
        {
            throw new NotSupportedException();
        }
    }
}
