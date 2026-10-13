using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeBackgroundLoopAndBoundary(string gameDirectory)
    {
        EventEngine Create(byte[] program)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, program, []);
            return engine;
        }
        foreach ((int first, int last, int divider) in new[] { (1, 3, 4), (2, 0, 2), (2, 2, 3) })
        {
            byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, divider), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, first), (Jsm.Opcode.PSHN_L, last), (Jsm.Opcode.RBGANIMELOOP, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_W, 512), (Jsm.Opcode.BGANIMESYNC, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.RET, 0));
            program[2] = 1;
            program[3] = 0;
            EventEngine engine = Create(program);
            EventEngine? independent = null;
            for (int frame = 1; frame <= 40; frame++)
            {
                engine.Step();
                independent?.Step();
                int expected = first + Math.Sign(last - first) * (frame / divider % (Math.Abs(last - first) + 1));
                JsonElement saved = engine.Capture();
                JsonElement loop = saved.GetProperty("BackgroundAnimations")[0];
                Require(engine.BackgroundStates[0] == expected && loop.GetProperty("Loop").GetBoolean() && loop.GetProperty("Active").GetBoolean() && !loop.GetProperty("Complete").GetBoolean() && saved.GetProperty("Variables").GetProperty("512").GetInt32() == 7 && !saved.GetProperty("Variables").TryGetProperty("514", out _), "Native background loop changed inclusive frame/divider order, immediate continuation or held synchronization.");
                if (frame == 5)
                {
                    independent = Create(program);
                    independent.Restore(saved);
                }
                if (independent is not null)
                {
                    Require(JsonElement.DeepEquals(saved, independent.Capture()), "Restored native background loop lost its fixed position, counter or repeated phase.");
                }
            }
            JsonElement before = engine.Capture();
            JsonNode invalid = JsonNode.Parse(before.GetRawText())!;
            invalid["BackgroundAnimations"]![0]!["Complete"] = true;
            try
            {
                engine.Restore(JsonSerializer.SerializeToElement(invalid));
                throw new InvalidOperationException("An active completed background loop was accepted as native state.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, engine.Capture()), "Invalid loop restore partially changed the VM.");
        }
        byte[] replace = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.RBGANIMELOOP, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.RBGANIME, 0), (Jsm.Opcode.BGANIMESYNC, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 514), (Jsm.Opcode.RET, 0));
        replace[2] = 1;
        replace[3] = 0;
        EventEngine finite = Create(replace);
        finite.Step();
        finite.Step();
        Require(!finite.Capture().GetProperty("BackgroundAnimations")[0].GetProperty("Loop").GetBoolean(), "Finite RBGANIME retained the former loop flag.");
        finite.Step();
        Require(!finite.Capture().GetProperty("Variables").TryGetProperty("514", out _), "BGANIMESYNC continued in the completion update rather than the next script update.");
        finite.Step();
        Require(finite.Capture().GetProperty("Variables").GetProperty("514").GetInt32() == 9, "Replacing a loop with a finite animation did not release native synchronization.");
        foreach (Jsm.Opcode hold in new[] { Jsm.Opcode.BGDRAW, Jsm.Opcode.BGOFF })
        {
            List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.BGANIMESPEED, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.RBGANIMELOOP, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0)];
            if (hold == Jsm.Opcode.BGDRAW)
            {
                operations.Add((Jsm.Opcode.PSHN_L, 0));
            }
            operations.Add((hold, 0));
            operations.Add((Jsm.Opcode.RET, 0));
            byte[] program = SyntheticProgram(operations.ToArray());
            program[2] = 1;
            program[3] = 0;
            EventEngine held = Create(program);
            held.Step();
            held.Step();
            Require(!held.Capture().GetProperty("BackgroundAnimations")[0].GetProperty("Loop").GetBoolean() && held.BackgroundStates[0] == (hold == Jsm.Opcode.BGOFF ? -1 : 0), "Held/off background state failed to cancel the loop.");
        }

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(165);
        GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
        EventEngine actual = new(session, session.State.Variables) { AnimationFrameCount = field.GetAnimationFrameCount };
        actual.LoadField(165, field.Jsm, field.Messages, session.State.FieldArrival);
        actual.Restore(session.State.EventState!.Value);
        actual.Apply(new PlayerAction(PlayerActionKind.Wait));
        JsonElement phase = actual.Capture().GetProperty("BackgroundAnimations").EnumerateArray().Single(value => value.GetProperty("Id").GetInt32() == 0);
        Require(actual.Frame == 5 && JsmProgram.Read(field.Jsm).Objects[37].BackgroundModuleIndex == 0 && phase.GetProperty("Loop").GetBoolean() && phase.GetProperty("First").GetInt32() == 0 && phase.GetProperty("Last").GetInt32() == 2 && phase.GetProperty("Position").GetInt32() == 0 && phase.GetProperty("Counter").GetInt32() == 5 && phase.GetProperty("Speed").GetInt32() == 6 && actual.BackgroundStates[0] == 0 && actual.Actors[20].MovementDirectionLimit == 4, $"Actual165 background module0 changed its native first-loop phase or MLIMIT side effect: {phase.GetRawText()}, frame{actual.Frame}, limit{actual.Actors[20].MovementDirectionLimit}.");
        try
        {
            actual.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        catch (EventScriptException)
        {
        }
        Require(actual.LastFault is { Opcode: "MOVE", ObjectId: 20, ScriptId: 103 } && actual.Actors[20].MovementTarget is null, "Actual background-loop continuation bypassed the next unsupported MLIMIT movement consumer.");
        Console.WriteLine($"Original165 RBGANIMELOOP: ordinary Wait1/frame4→5 module0 frames0..2/divider6/counter5/position0, immediate continuation; repeated forward/reverse/single-frame cycles and independent restore retain held sync; next native MLIMIT MOVE{actual.LastFault!.Operation}/frame{actual.Frame} preserved.");
    }
}
