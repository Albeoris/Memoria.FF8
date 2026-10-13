using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMovementLimitAndBoundary(string gameDirectory)
    {
        EventEngine Create(byte[] program)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables);
            engine.LoadField(100, program, []);
            return engine;
        }
        foreach (int operand in new[] { 0, 260, -1 })
        {
            byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, operand), (Jsm.Opcode.MLIMIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0));
            EventEngine engine = Create(program);
            engine.Step();
            Require(engine.Actors[0].MovementDirectionLimit == (byte)operand && engine.Actors[0].MovementTarget is null && engine.Actors[0].X == 0 && engine.Capture().GetProperty("Fibers")[0].GetProperty("Stack").GetArrayLength() == 0, "MLIMIT did not consume one DWORD, store only its low byte and leave placement unchanged.");
            JsonElement saved = engine.Capture();
            EventEngine restored = Create(program);
            restored.Restore(saved);
            Require(JsonElement.DeepEquals(saved, restored.Capture()), "MLIMIT actor byte610 did not persist independently.");
            engine.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(engine.Capture(), restored.Capture()), "Restored MLIMIT changed ordinary wait progression.");
        }
        foreach (Jsm.Opcode movement in new[] { Jsm.Opcode.MOVE, Jsm.Opcode.RMOVE, Jsm.Opcode.FMOVE, Jsm.Opcode.RFMOVE })
        {
            byte[] program = SyntheticProgram((Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.MLIMIT, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.PSHN_L, 0), (movement, 0), (Jsm.Opcode.RET, 0));
            EventEngine engine = Create(program);
            try
            {
                engine.Step();
                throw new InvalidOperationException("Nonzero MLIMIT was silently ignored by an uncertified movement consumer.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement refused = engine.Capture();
            JsonElement fiber = refused.GetProperty("Fibers")[0];
            Require(engine.Actors[0].MovementDirectionLimit == 4 && engine.Actors[0].MovementTarget is null && fiber.GetProperty("Position").GetInt32() == 7 && fiber.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 0, 30, 20, 10 }), "MLIMIT consumer refusal changed movement targets, PC or four operands.");
            EventEngine restored = Create(program);
            restored.Restore(refused);
            Require(JsonElement.DeepEquals(refused, restored.Capture()), "Frozen MLIMIT movement boundary did not restore exactly.");
        }

        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(165);
        GameSession session = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
        EventEngine actual = new(session, session.State.Variables) { AnimationFrameCount = field.GetAnimationFrameCount, LoadSoundResource = _ => SoundResourceStatus.Muted, ResolveGlobalSoundResource = MutedGlobalSample };
        actual.LoadField(165, field.Jsm, field.Messages, session.State.FieldArrival);
        actual.Restore(session.State.EventState!.Value);
        Require(actual.Frame == 4 && actual.Actors[20].MovementDirectionLimit == 0, "Actual165 MLIMIT source changed its frame or default actor byte610.");
        List<ScriptCommand> commands = [];
        actual.CommandExecuted += commands.Add;
        try
        {
            actual.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        catch (EventScriptException)
        {
        }
        Require(actual.Frame == 5 && actual.Actors[20].MovementDirectionLimit == 4 && commands.Any(command => command.ObjectId == 20 && command.Operation == 1634 && command.Name == "MLIMIT") && actual.LastFault is null && actual.Actors[20].MovementTarget is null, $"Actual165 ordinary Wait did not execute MLIMIT4 immediately with supported background-loop continuation: frame{actual.Frame}, limit{actual.Actors[20].MovementDirectionLimit}, fault{actual.LastFault}.");
        JsonElement actualSaved = actual.Capture();
        GameSession independentSession = new(GameSession.Load(Path.Combine("../fixtures/external-fixture.json")));
        EventEngine independent = new(independentSession, independentSession.State.Variables);
        independent.LoadField(165, field.Jsm, field.Messages, independentSession.State.FieldArrival);
        independent.Restore(actualSaved);
        Require(JsonElement.DeepEquals(actualSaved, independent.Capture()), "Actual165 MLIMIT byte4 and its bounded successor did not preserve complete independent VM state.");
        Console.WriteLine("Original165 MLIMIT: ordinary Wait1/frame4→5 actor20 byte610=4, immediate low-byte store/independent restore; background-loop continuation supported; nonzero movement consumers preserve PC/operands.");
    }
}
