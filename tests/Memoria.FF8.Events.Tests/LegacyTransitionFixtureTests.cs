using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void MovieAxisAndFaultResume()
    {
        NativeMovieCameraFlags camera = NativeMovieCameraReader.Read(File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "Data", "movies", "disc00_02.cam")));
        byte[] content = FieldMediaProgram((Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.AXIS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.MOVIESYNC, 0), (Jsm.Opcode.PSHM_L, 80), (Jsm.Opcode.POPM_W, 512));
        (GameSession Session, EventEngine Engine) Create(GameState? state = null)
        {
            GameSession session = state is null ? CreateInitializedSession() : new GameSession(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!);
            EventEngine engine = new(session) { MoviePlayback = () => session.State.MoviePlayback, ReadMovieCameraFlags = (disc, index) => disc == 0 && index == 2 ? camera : throw new NotSupportedException("No other movie camera resource was declared by this component.") };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var current = Create();
        current.Engine.Step();
        current.Engine.Step();
        current.Engine.Step();
        Require(current.Session.IsMoviePlaying && current.Session.State.Variables[80] == 0 && !current.Session.State.Variables.ContainsKey(512) && current.Engine.InputAxisDirection == 16, "Movie startup frame projection, held synchronization or AXIS pre-VM tick ordering failed.");
        current.Session.SetMoviePlayback(new MoviePlaybackState(0, 325, true, false, false));
        current.Session.SetMoviePlayback(new MoviePlaybackState(1, 325, true, false, false));
        current.Session.SetMoviePlayback(new MoviePlaybackState(2, 325, true, false, false));
        current.Engine.Step();
        Require(current.Session.State.Variables[80] == 2 && current.Engine.InputAxisDirection == 32 && !current.Session.State.Variables.ContainsKey(512), "Actual consecutive movie samples did not project the predecode frame word or preserve the held VM.");
        JsonElement snapshot = current.Engine.Capture();
        var independent = Create(current.Session.State);
        independent.Engine.Restore(snapshot);
        Require(JsonElement.DeepEquals(snapshot, independent.Engine.Capture()), "Mid-movie AXIS snapshot lost timing or decoder frame state.");
        current.Session.CompleteMovie(skipped: true);
        independent.Session.CompleteMovie(skipped: true);
        for (int callback = 0; callback < 2; callback++)
        {
            current.Engine.Step();
            independent.Engine.Step();
            Require(JsonElement.DeepEquals(current.Engine.Capture(), independent.Engine.Capture()), "Post-movie AXIS continuation diverged after independent restore.");
        }
        Require(current.Session.State.Variables[512] == 2 && current.Engine.InputAxisDirection == 64 && current.Session.State.MoviePlayback is { FrameIndex: 2, TerminalFrame: 325, Completed: true, Skipped: true }, "Movie skip sought to an unseen terminal frame or AXIS failed to reach its byte target.");

        EventEngine resumed = independent.Engine;
        byte[] unsupported = FieldMediaProgram((Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, 456), (Jsm.Opcode.DRAWPOINT, 0));
        resumed.LoadField(101, unsupported, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Step();
        try
        {
            resumed.Step();
            throw new InvalidOperationException("Unsupported command executed.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement fault = resumed.Capture();
        JsonElement fiber = fault.GetProperty("Fibers").EnumerateArray().Single(candidate => !candidate.GetProperty("Initialization").GetBoolean());
        Require(resumed.LastFault?.Opcode == nameof(Jsm.Opcode.DRAWPOINT) && fiber.GetProperty("Position").GetInt32() == 5 && fiber.GetProperty("Stack")[0].GetInt32() == 456 && fiber.GetProperty("Stack")[1].GetInt32() == 123 && resumed.NextTrigger.Kind == PlayerTriggerKind.Faulted, "Unsupported command advanced the PC, consumed operands or hid its fault.");
        ExpectInterruptedRestoreRefusal(resumed, fault);
        try
        {
            resumed.Step();
            throw new InvalidOperationException("A faulted VM continued past its unsupported opcode.");
        }
        catch (EventScriptException)
        {
        }
        Require(JsonElement.DeepEquals(fault, resumed.Capture()), "A repeated unsupported command changed its guarded state.");

        byte[] freshMovie = FieldMediaProgram((Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVIEREADY, 0), (Jsm.Opcode.MOVIE, 0), (Jsm.Opcode.PSHM_L, 80), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.MOVIESYNC, 0));
        EventEngine fresh = new(current.Session) { MoviePlayback = () => current.Session.State.MoviePlayback, ReadMovieCameraFlags = (_, _) => camera };
        fresh.LoadField(102, freshMovie, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        fresh.Step();
        fresh.Step();
        fresh.Step();
        Require(current.Session.State.Variables[512] == 0, "A previous completed movie frame leaked into the new clip before its first decoder sample.");
    }

    private static void MapJump3PacketAndArrival()
    {
        GameSession session = CreateInitializedSession();
        EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4, ResolvePartyTraceTriangle = (_, _, _) => 50, ResolveNativeTriangleHeight = (_, _, _) => -7 };
        byte[] departure = FieldMediaProgram((Jsm.Opcode.PSHN_L, 65636), (Jsm.Opcode.PSHN_L, 65526), (Jsm.Opcode.PSHN_L, -65516), (Jsm.Opcode.PSHN_L, 98305), (Jsm.Opcode.PSHN_L, 288), (Jsm.Opcode.MAPJUMP3, 50), (Jsm.Opcode.PSHN_L, 999), (Jsm.Opcode.POPM_W, 512));
        engine.LoadField(101, departure, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        engine.Step();
        engine.Step();
        FieldTransition arrival = new(100, -10, 20, 50, 32, -32767);
        Require(session.State.FieldArrival == arrival && !session.State.Variables.ContainsKey(512), "MAPJUMP3 packet truncation or stop-before-next-instruction failed.");
        byte[] destination = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 111), (Jsm.Opcode.PSHN_L, 222), (Jsm.Opcode.PSHN_L, 333), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0));
        engine.ResolveFieldArrival = request => new WalkmeshPosition(new System.Numerics.Vector3(request.X, request.Y, -7), request.Triangle);
        engine.LoadField(100, destination, [], arrival, nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        var pending = engine.Capture();
        Require(pending.GetProperty("PendingArrival").GetProperty("NativeExtraWord").GetInt32() == -32767, "Pending arrival discarded its native packet word.");
        engine.Restore(pending);
        Require(pending.GetRawText() == engine.Capture().GetRawText(), "Pending MAPJUMP3 arrival changed during restore.");
        engine.Step();
        Require(engine.Actors[0].X == -10 && engine.Actors[0].Y == 20 && engine.Actors[0].Z == -7 && engine.Actors[0].WalkmeshTriangle == 50 && engine.Actors[0].Direction == 32 && engine.Capture().GetProperty("PendingArrival").ValueKind == System.Text.Json.JsonValueKind.Null, "MAPJUMP3 arrival used its extra word as height or failed to override init before defaults.");
    }

}
