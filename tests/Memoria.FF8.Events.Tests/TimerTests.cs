using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeExamTimerContract()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] content = TimerProgram((Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 250), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.DISPTIMER, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.KILLTIMER, 0), (Jsm.Opcode.HALT, 0));
        const uint unrelatedFlags = 0xA500023F;
        GameSession session = CreateTimerSession(NativeExamTimerState.FromReachedSet(0), unrelatedFlags);
        EventEngine first = LoadTimerField(session, content);
        first.Step();
        Require(first.ExamTimer is { ClockCalls: 2, DisplayX: 250, DisplayY: 10, DisplayEnabled: true } && first.ExamTimer.RequireRemaining() == 600, "SET/DISP reset unknown phase, lost unsigned screen coordinates or discarded a pre-SET clock.");
        Require(session.WorldControls!.EventFlags104 == (unrelatedFlags | 0x40u), "DISPTIMER overwrote unrelated guest, salary, battle or source bits.");
        first.Step();
        Require(first.ExamTimer!.ClockCalls == 4 && first.ExamTimer.Alternatives.Any(item => item.Remaining == 599) && first.ExamTimer.Alternatives.Any(item => item.Remaining == 600), "Field clock did not preserve correlated remaining alternatives after two pre-VM native calls.");
        GameSession independentSession = CreateTimerSession(NativeExamTimerState.FromReachedSet(0));
        EventEngine resumed = LoadTimerField(independentSession, content);
        resumed.Restore(first.Capture());
        for (int frame = 0; frame < 25; frame++)
        {
            first.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(first.Capture(), resumed.Capture()), "Timer interval correlations diverged after independent VM-only restore.");
        }
        Require(first.ExamTimer is { DisplayEnabled: false, Event182Mask4: false, SaveGlobalMask4: false } && (session.WorldControls!.EventFlags104 & 0x40) == 0 && first.ExamTimer.Alternatives.All(item => item.Remaining > 0), "KILLTIMER erased the remaining clock or failed to clear only timer display masks.");
        Require(session.WorldControls!.EventFlags104 == unrelatedFlags, "KILLTIMER did not preserve every other DWORD104 bit.");
        long beforeLoadCalls = first.ExamTimer.ClockCalls;
        first.LoadField(101, TimerProgram((Jsm.Opcode.HALT, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        first.Step();
        Require(first.ExamTimer.ClockCalls == beforeLoadCalls && first.Scene.NativeFieldInitialization?.Stage == NativeFieldInitializationStage.Completed, "Synchronous field initialization fabricated a timer callback.");
        first.Step();
        Require(first.ExamTimer.ClockCalls == beforeLoadCalls + 2 && !first.ExamTimer.DisplayEnabled, "Normal field load reset native timer phase or display state.");

        byte[] ambiguousGet = TimerProgram((Jsm.Opcode.PSHN_L, 600), (Jsm.Opcode.SETTIMER, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.HALT, 0));
        GameSession uncertainSession = CreateTimerSession(NativeExamTimerState.FromReachedSet(0));
        EventEngine uncertain = LoadTimerField(uncertainSession, ambiguousGet);
        uncertain.Step();
        uncertain.Step();
        JsonElement haltedRead = TimerMain(uncertain);
        Require(!haltedRead.GetProperty("Complete").GetBoolean() && haltedRead.GetProperty("WaitReason").GetString() == "NativeHalt" && haltedRead.GetProperty("Results").GetProperty("0").GetProperty("Values").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([599, 600]), "GETTIMER did not retain both native results from its unknown historical phase.");

        NativeExamTimerState exact = NativeExamTimerState.FromReachedSet(5) with { Alternatives = [new NativeTimerAlternative(131071, 131071, 5)] };
        GameSession exactSession = CreateTimerSession(exact);
        EventEngine exactEngine = LoadTimerField(exactSession, TimerProgram((Jsm.Opcode.GETTIMER, 0), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.HALT, 0)));
        exactEngine.Step();
        Require(TimerMain(exactEngine).GetProperty("Results").GetProperty("3").GetInt32() == 4, "GETTIMER did not observe clock calls before VM dispatch.");

        NativeExamTimerState expired = NativeExamTimerState.FromReachedSet(0).Display(250, 10) with { Event183Mask1 = false };
        GameSession killSession = CreateTimerSession(expired, 0x58);
        EventEngine kill = LoadTimerField(killSession, TimerProgram((Jsm.Opcode.KILLTIMER, 0), (Jsm.Opcode.HALT, 0)));
        kill.Step();
        Require((killSession.WorldControls!.EventFlags104 & 0x40) == 0 && killSession.State.FieldId == 74, "Expiry ran before same-frame native KILLTIMER.");

        GameSession boundarySession = CreateTimerSession(expired, 0x58);
        EventEngine boundary = LoadTimerField(boundarySession, TimerProgram((Jsm.Opcode.HALT, 0)));
        try
        {
            boundary.Step();
            throw new InvalidOperationException("Expired timer manufactured untouched field75 packet coordinates.");
        }
        catch (NotSupportedException exception) when (exception.Message.Contains("field75", StringComparison.Ordinal))
        {
        }
        Require(boundarySession.State.FieldArrival is null, "Unsupported timer expiry published a guessed arrival.");

        NativeFieldRequestPacket retained = new() { Kind = 0, FieldId = 100, X = short.MinValue, Y = short.MaxValue, ExtraWord = -923, Triangle = 48, DirectionWord = ushort.MaxValue };
        GameSession knownSession = CreateTimerSession(expired, 0x58, retained);
        EventEngine known = LoadTimerField(knownSession, TimerProgram((Jsm.Opcode.HALT, 0)));
        JsonElement knownBefore = known.Capture();
        GameSession coldSession = new(knownSession.State);
        EventEngine cold = new(coldSession);
        cold.LoadField(100, TimerProgram((Jsm.Opcode.HALT, 0)), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        cold.Restore(knownBefore);
        known.Step();
        cold.Step();
        Require(knownSession.State.FieldId == 75 && knownSession.State.FieldArrival is { X: short.MinValue, Y: short.MaxValue, Triangle: 0x7FFF, Direction: byte.MaxValue, NativeExtraWord: -923 } && knownSession.FieldRequestPacket == retained.WithTimerExpiry(), "Expiry did not preserve inherited signed coordinates, extra word and full raw direction WORD while replacing only kind, field and triangle.");
        Require(known.NextTrigger.Kind == PlayerTriggerKind.FieldTransition && JsonElement.DeepEquals(known.Capture(), cold.Capture()) && JsonElement.DeepEquals(JsonSerializer.SerializeToElement(knownSession.State), JsonSerializer.SerializeToElement(coldSession.State)), "Independently restored native timer expiry diverged from the genuine packet publication.");
        foreach (byte competingKind in new byte[] { 1, 3, 4, 5, 6, 7, 8 })
        {
            NativeFieldRequestPacket competing = retained with { Kind = competingKind };
            GameSession competingSession = CreateTimerSession(expired, 0x58, competing);
            EventEngine competingEngine = LoadTimerField(competingSession, TimerProgram((Jsm.Opcode.HALT, 0)));
            competingEngine.Step();
            Require(competingSession.FieldRequestPacket == competing && competingSession.State.FieldId == 74 && competingSession.State.FieldArrival is null && competingEngine.NextTrigger.Kind != PlayerTriggerKind.FieldTransition, "Timer expiry replaced an already admitted competing native request kind.");
        }
        foreach (NativeFieldRequestPacket unknown in new[] { retained with { Kind = null }, retained with { X = null }, retained with { DirectionWord = null } })
        {
            GameSession unknownSession = CreateTimerSession(expired, 0x58, unknown);
            EventEngine unknownEngine = LoadTimerField(unknownSession, TimerProgram((Jsm.Opcode.HALT, 0)));
            bool refused = false;
            try
            {
                unknownEngine.Step();
            }
            catch (NotSupportedException)
            {
                refused = true;
            }
            Require(refused && unknownSession.FieldRequestPacket == unknown && unknownSession.State.FieldId == 74 && unknownSession.State.FieldArrival is null, "Unproved request arbitration or inherited packet words were guessed or partially published at timer expiry.");
        }

        GameSession aliasSession = CreateTimerSession(NativeExamTimerState.FromReachedSet(600).Display(250, 10));
        EventEngine alias = LoadTimerField(aliasSession, TimerProgram((Jsm.Opcode.PSHM_B, 205), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.HALT, 0)));
        try
        {
            alias.Step();
            throw new InvalidOperationException("Timer coordinate memory supplied a legacy zero instead of stopping its unproved alias access.");
        }
        catch (EventScriptException) when (alias.LastFault?.Opcode == "PSHM_B")
        {
        }
        Require(TimerMain(alias).GetProperty("Position").GetInt32() == 3, "Unproved timer memory alias advanced its issuing PC.");
        GameSession movieSession = CreateTimerSession(NativeExamTimerState.FromReachedSet(600).Display(250, 10).KillDisplay());
        EventEngine movie = LoadTimerField(movieSession, TimerProgram((Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.HALT, 0)));
        movie.MoviePlayback = () => movieSession.State.MoviePlayback;
        NativeMovieCameraFlags movieCamera = NativeMovieCameraReader.Read(File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "Data", "movies", "disc00_02.cam")));
        Require(movieCamera.Flags.Length > 3, "Declared timer/movie component requires actual nonterminal CAM records for its first active callback.");
        movie.ReadMovieCameraFlags = (disc, index) => disc == 0 && index == 2 ? movieCamera : throw new NotSupportedException("The declared timer component has no other prepared movie resource.");
        movie.Step();
        Require(!movieSession.IsMoviePlaying && movieSession.ExamTimer!.ClockCalls == 2, "MOVIEREADY did not yield its original return3 ordinary callback before starting playback.");
        movie.Step();
        Require(movieSession.IsMoviePlaying && movieSession.ExamTimer!.ClockCalls == 4, "Accepted movie start changed the pre-VM ordinary callback count.");
        // Explicit decoder samples are component inputs, not native callback clocks.
        movieSession.SetMoviePlayback(new(0, 8, true, false, false));
        movieSession.SetMoviePlayback(new(1, 8, true, false, false));
        movieSession.SetMoviePlayback(new(2, 8, true, false, false));
        movie.Step();
        Require(movieSession.ExamTimer!.ClockCalls == 8 && !movieSession.ExamTimer.DisplayEnabled, "Killed timer lost its still-running four-call movie callback clock.");
        Console.WriteLine("Native exam timer: synchronous INIT, SET/DISP/GET/KILL, unknown correlated phase, ordinary/movie clocks, strict unknown-packet refusal, exact inherited expiry packet, competing requests and cold restore verified.");

        GameSession CreateTimerSession(NativeExamTimerState timer, uint? flags = null, NativeFieldRequestPacket? packet = null)
        {
            GameSession created = GameSession.CreateColdProcess();
            created.StartNewGame();
            created.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            created.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            created.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
            created.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            // Declared timer-domain components retain an explicitly unknown or exact phase;
            // they do not claim an original gameplay route to this synthetic program.
            created.SetExamTimer(timer);
            return new GameSession(created.State with { WorldControls = flags is uint value ? created.WorldControls! with { EventFlags104 = value } : created.WorldControls, FieldRequestPacket = packet });
        }

        static EventEngine LoadTimerField(GameSession owner, byte[] program)
        {
            EventEngine engine = new(owner);
            long calls = owner.ExamTimer!.ClockCalls;
            engine.LoadField(100, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            engine.Step();
            Require(engine.Frame == 1 && engine.Scene.NativeFieldInitialization?.Stage == NativeFieldInitializationStage.Completed && owner.ExamTimer.ClockCalls == calls, "Declared timer fixture failed to complete its zero-clock synchronous INIT transaction.");
            return engine;
        }

        static byte[] TimerProgram(params (Jsm.Opcode Opcode, int Parameter)[] operations)
        {
            return SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), .. operations, (Jsm.Opcode.RET, 8)]]);
        }

        static JsonElement TimerMain(EventEngine engine)
        {
            return engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        }
    }
}
