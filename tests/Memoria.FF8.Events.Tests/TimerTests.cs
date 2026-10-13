using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeExamTimerContract()
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 250), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.DISPTIMER, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.KILLTIMER, 0), (Jsm.Opcode.RET, 0));
        GameSession session = new(new GameState());
        EventEngine first = new(session, session.State.Variables);
        first.LoadField(100, content, []);
        first.Step();
        Require(first.ExamTimer is { ClockCalls: 0, DisplayX: 250, DisplayY: 10, DisplayEnabled: true } && first.ExamTimer.RequireRemaining() == 600, "SET/DISP reset unknown phase, lost unsigned screen coordinates or advanced a pre-SET clock.");
        first.Step();
        Require(first.ExamTimer!.ClockCalls == 2 && first.ExamTimer.Alternatives.Any(item => item.Remaining == 599) && first.ExamTimer.Alternatives.Any(item => item.Remaining == 600), "Field clock did not preserve correlated remaining alternatives after two pre-VM native calls.");
        GameSession independentSession = new(new GameState());
        EventEngine resumed = new(independentSession, independentSession.State.Variables);
        resumed.LoadField(100, content, []);
        resumed.Restore(first.Capture());
        for (int frame = 0; frame < 25; frame++)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Timer interval correlations diverged after independent VM-only restore.");
        }
        Require(first.ExamTimer is { DisplayEnabled: false, Event104Mask40: false, Event182Mask4: false, SaveGlobalMask4: false } && first.ExamTimer.Alternatives.All(item => item.Remaining > 0), "KILLTIMER erased the remaining clock or failed to clear only timer display masks.");
        long beforeLoadCalls = first.ExamTimer.ClockCalls;
        first.LoadField(101, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        first.Step();
        Require(first.ExamTimer.ClockCalls == beforeLoadCalls + 2 && !first.ExamTimer.DisplayEnabled, "Normal field load reset native timer phase or display state.");

        byte[] ambiguousGet = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.RET, 0));
        GameSession uncertainSession = new(new GameState());
        EventEngine uncertain = new(uncertainSession, uncertainSession.State.Variables);
        uncertain.LoadField(100, ambiguousGet, []);
        uncertain.Step();
        uncertain.Step();
        JsonElement completedRead = uncertain.Capture().GetProperty("Fibers")[0];
        Require(completedRead.GetProperty("Complete").GetBoolean() && completedRead.GetProperty("Results").GetProperty("0").GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([599, 600]), "GETTIMER did not retain both native results from its unknown historical phase.");

        NativeExamTimerState exact = NativeExamTimerState.FromReachedSet(5) with { Alternatives = [new NativeTimerAlternative(131071, 131071, 5)] };
        GameSession exactSession = new(new GameState { ExamTimer = exact });
        EventEngine exactEngine = new(exactSession, exactSession.State.Variables);
        exactEngine.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.RET, 0)), []);
        exactEngine.Step();
        Require(exactEngine.Capture().GetProperty("Fibers")[0].GetProperty("Results").GetProperty("3").GetInt32() == 4, "GETTIMER did not observe clock calls before VM dispatch.");

        NativeExamTimerState expired = NativeExamTimerState.FromReachedSet(0).Display(250, 10) with { Event183Mask1 = false };
        GameSession killSession = new(new GameState { ExamTimer = expired });
        EventEngine kill = new(killSession, killSession.State.Variables);
        kill.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.KILLTIMER, 0), (Jsm.Opcode.RET, 0)), []);
        kill.Step();
        Require(kill.ExamTimer is { Event104Mask40: false } && killSession.State.FieldId == 74, "Expiry ran before same-frame native KILLTIMER.");

        GameSession boundarySession = new(new GameState { ExamTimer = expired });
        EventEngine boundary = new(boundarySession, boundarySession.State.Variables);
        boundary.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 0)), []);
        try
        {
            boundary.Step();
            throw new InvalidOperationException("Expired timer manufactured untouched field75 packet coordinates.");
        }
        catch (NotSupportedException exception) when (exception.Message.Contains("field75", StringComparison.Ordinal))
        {
        }
        Require(boundarySession.State.FieldArrival is null, "Unsupported timer expiry published a guessed arrival.");

        GameSession aliasSession = new(new GameState { ExamTimer = NativeExamTimerState.FromReachedSet(600).Display(250, 10) });
        EventEngine alias = new(aliasSession, aliasSession.State.Variables);
        alias.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHM_B, 205), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.RET, 0)), []);
        try
        {
            alias.Step();
            throw new InvalidOperationException("Timer coordinate memory supplied a legacy zero instead of stopping its unproved alias access.");
        }
        catch (EventScriptException) when (alias.LastFault?.Opcode == "PSHM_B")
        {
        }
        Require(alias.Capture().GetProperty("Fibers")[0].GetProperty("Position").GetInt32() == 1, "Unproved timer memory alias advanced its issuing PC.");
        GameSession movieSession = new(new GameState { Scene = SceneKind.Field, ExamTimer = NativeExamTimerState.FromReachedSet(600).Display(250, 10).KillDisplay() });
        EventEngine movie = new(movieSession, movieSession.State.Variables);
        movie.LoadField(100, SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.RET, 0)), []);
        movie.Step();
        Require(movieSession.IsMoviePlaying && movieSession.ExamTimer!.ClockCalls == 2, "Accepted movie start changed the pre-VM ordinary callback count.");
        movie.Step();
        Require(movieSession.ExamTimer!.ClockCalls == 6 && !movieSession.ExamTimer.DisplayEnabled, "Killed timer lost its still-running four-call movie callback clock.");
        Console.WriteLine("Native exam timer: SET/DISP/GET/KILL, unknown correlated fractional phase, two pre-VM field clocks, post-VM expiry refusal, exact or range HUD, independent/cross-field restore verified.");
    }
}
