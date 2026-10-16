using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void InitialInputDirectionAndRestore(string gameDirectory)
    {
        byte[] content = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.AXIS, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
        EventEngine Create(byte initial)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session) { InitInputAxisDirection = initial };
            engine.LoadField(100, content, []);
            return engine;
        }
        EventEngine engine = Create(128);
        JsonElement loaded = engine.Capture();
        Require(engine.Frame == 0 && engine.InputAxisDirection == 128 && loaded.GetProperty("AxisStart").GetInt32() == 128 && loaded.GetProperty("AxisTarget").GetInt32() == 128 && loaded.GetProperty("AxisDuration").GetInt32() == 0, "Native INF input direction was not installed before the first field VM update.");
        engine.Step();
        engine.Step();
        Require(engine.InputAxisDirection == 112, "AXIS did not interpolate from the native INF initial direction.");
        JsonElement halfway = engine.Capture();
        EventEngine independent = Create(7);
        independent.Restore(halfway);
        Require(JsonElement.DeepEquals(halfway, independent.Capture()), "Restore replaced serialized axis timing with the new field initialization direction.");
        for (int tick = 0; tick < 6; tick++)
        {
            engine.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), independent.Capture()), "Independent restored AXIS progression differed after native initial direction was superseded.");
        }
        Require(engine.InputAxisDirection == 64, "AXIS did not complete at its script target after native initial direction.");
        engine.InitInputAxisDirection = 192;
        engine.LoadField(101, content, []);
        Require(engine.InputAxisDirection == 192 && engine.Capture().GetProperty("AxisStart").GetInt32() == 192, "A fresh field inherited the preceding script AXIS instead of the selected field INF direction.");
        GameSession legacySession = new(new GameState());
        EventEngine legacy = new(legacySession);
        legacy.LoadField(100, content, []);
        Require(legacy.InputAxisDirection == 0, "An unbound synthetic or legacy field lost the zero initial direction default.");

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(172);
        GameSession savedSession = new(GameSession.Load(TestResources.FixturePath("Events.Tests.InitialInputDirectionTests.1")));
        EventEngine saved = new(savedSession) { InitInputAxisDirection = field.Information!.ControlDirection };
        saved.LoadField(172, field.Jsm, field.Messages, savedSession.State.FieldArrival);
        Require(saved.InputAxisDirection == 128, "Selected original172 INF byte9 was not installed on fresh load.");
        saved.Restore(savedSession.State.EventState!.Value);
        GameSession baselineSession = new(GameSession.Load(TestResources.FixturePath("Events.Tests.InitialInputDirectionTests.2")));
        EventEngine baseline = new(baselineSession);
        baseline.LoadField(172, field.Jsm, field.Messages, baselineSession.State.FieldArrival);
        baseline.Restore(baselineSession.State.EventState!.Value);
        Require(saved.Frame == 57 && saved.InputAxisDirection == 0 && JsonElement.DeepEquals(baseline.Capture(), saved.Capture()), "Installing fresh INF direction rewrote the actual historical172 saved axis or other VM state.");
        Console.WriteLine("Native input initialization: INF128 before VM, AXIS supersedes and independent restore retains timing; next field192 and default0 verified, actual172/frame57 saved direction0 remains authoritative.");
    }
}
