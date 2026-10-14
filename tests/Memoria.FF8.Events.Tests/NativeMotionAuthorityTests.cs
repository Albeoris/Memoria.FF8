using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMotionAuthorityContract()
    {
        (Jsm.Opcode Opcode, int Parameter)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode Opcode, int Parameter)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
        byte[] program = SyntheticObjects([1], [init, main]);
        EventEngine Create(bool native = true, byte[]? content = null)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = _ => native,
                ResolveNativeScriptDirection = query => new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator),
                ResolveNativeScriptMovement = _ => throw new InvalidOperationException("Stopped native motion queried ground geometry.")
            };
            engine.LoadField(100, content ?? program, []);
            return engine;
        }
        EventEngine fresh = Create();
        Require(fresh.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "Fresh native allocation did not initialize independent575/576 to zero.");
        Require(JsonSerializer.Deserialize<ActorState>("{\"ObjectId\":0,\"Label\":0}")!.NativeMotionAuthority is null, "Legacy actor deserialization manufactured native motion authority.");
        for (int tick = 0; tick < 2; tick++)
        {
            fresh.Step();
        }
        ActorState stopped = fresh.Actors[0];
        Require(stopped.NativeMovementCompletion542 == 2 && stopped.NativeScriptMovement is { MovementDirection: 0, FacingDirection: 128 } && stopped.NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "Native stop used body577 as movement575 after DIR/SET3 or changed preserved scalars.");
        JsonElement stoppedSaved = fresh.Capture();
        EventEngine independent = Create();
        independent.Restore(stoppedSaved);
        fresh.Step();
        independent.Step();
        Require(JsonElement.DeepEquals(fresh.Capture(), independent.Capture()) && fresh.Actors[0].Direction == 200 && fresh.Actors[0].NativeScriptMovement is null && fresh.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "DIR/SET3 changed native direction authority after independently restored completion.");

        EventEngine moving = Create();
        int proposals = 0;
        moving.ResolveNativeScriptDirection = query => proposals == 0 ? new NativeScriptDirectionResult(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 8, 0) : new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator);
        moving.ResolveNativeScriptMovement = query =>
        {
            // Explicit synthetic blocked geometry preserves XYZ but changes575.
            NativeScriptGroundProbe[] fans = [new(query.FixedX, query.FixedY, query.FixedZ >> 12), new(query.FixedX, query.FixedY, query.FixedZ >> 12), new(query.FixedX, query.FixedY, query.FixedZ >> 12)];
            return new NativeScriptMovementResult(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, 8, [new NativeScriptMovementProposal(query.FixedX, query.FixedY, query.Direction, fans)]);
        };
        for (int tick = 0; tick < 2; tick++)
        {
            moving.Step();
        }
        Require(moving.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 8, 0) && moving.Actors[0].NativeScriptMovement is { MovementDirection: 8, FacingDirection: 8 }, "Native blocked movement did not publish exact575 authority alongside its raw state.");
        JsonElement activeSaved = moving.Capture();
        JsonNode inconsistent = JsonNode.Parse(activeSaved.GetRawText())!;
        inconsistent["Actors"]![0]!["NativeMotionAuthority"]!["MovementDirection"] = 9;
        try
        {
            moving.Restore(JsonSerializer.SerializeToElement(inconsistent));
            throw new InvalidOperationException("Inconsistent active movement575 passed authority restore.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(activeSaved, moving.Capture()), "Inconsistent active scalar restore partially mutated the VM.");
        proposals = 1;
        moving.Step();
        moving.Step();
        Require(moving.Actors[0].Direction == 200 && moving.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 8, 0), "Completion/SET3/DIR discarded known movement575 or confused it with body577.");

        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.MOVE, Jsm.Opcode.RMOVE, Jsm.Opcode.FMOVE, Jsm.Opcode.RFMOVE, Jsm.Opcode.MOVEA })
        {
            List<(Jsm.Opcode Opcode, int Parameter)> commands = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0)];
            if (opcode != Jsm.Opcode.MOVEA)
            {
                commands.Add((Jsm.Opcode.PSHN_L, 0));
                commands.Add((Jsm.Opcode.PSHN_L, 0));
            }
            commands.Add((opcode, 0));
            commands.Add((Jsm.Opcode.PSHN_L, 20));
            commands.Add((Jsm.Opcode.WAIT, 0));
            commands.Add((Jsm.Opcode.RET, 0));
            byte[] managedProgram = SyntheticObjects([1], [init, commands.ToArray()]);
            EventEngine managed = Create(false, managedProgram);
            bool invalidatedAtCommand = false;
            managed.CommandExecuted += command =>
            {
                if (command.Name == opcode.ToString())
                {
                    invalidatedAtCommand = managed.Actors[0].NativeMotionAuthority is null;
                }
            };
            for (int tick = 0; tick < 3; tick++)
            {
                managed.Step();
            }
            Require(invalidatedAtCommand && managed.Actors[0].NativeMotionAuthority is null, $"Unbound managed {opcode} retained native scalar authority before positional mutation.");
        }

        EventEngine hold = Create();
        hold.Step();
        JsonNode held = JsonNode.Parse(hold.Capture().GetRawText())!;
        held["Actors"]![0]!["NativeMotionAuthority"]!["FacingHold"] = 1;
        hold.Restore(JsonSerializer.SerializeToElement(held));
        try
        {
            hold.Step();
            throw new InvalidOperationException("Unsupported facing hold576 was ignored by native MOVE.");
        }
        catch (EventScriptException)
        {
        }
        Require(hold.LastFault is { Opcode: "MOVE" } && hold.Actors[0].NativeMotionAuthority is { FacingHold: 1 } && hold.Actors[0].NativeScriptMovement is null && hold.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Stack").GetArrayLength() == 4, "Facing hold refusal consumed operands or changed original scalar state.");
        JsonElement valid = moving.Capture();
        JsonNode invalid = JsonNode.Parse(valid.GetRawText())!;
        invalid["Actors"]![0]!["NativeMotionAuthority"]!["Version"] = 2;
        try
        {
            moving.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Unknown native motion authority schema passed restore.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(valid, moving.Capture()), "Invalid authority restore partially mutated the VM.");
        JsonNode legacy = JsonNode.Parse(valid.GetRawText())!;
        legacy["Actors"]![0]!.AsObject().Remove("NativeMotionAuthority");
        independent.Restore(JsonSerializer.SerializeToElement(legacy));
        Require(independent.Actors[0].NativeMotionAuthority is null, "Legacy checkpoint restore inferred native575 from its visible body or fresh allocation.");
        Console.WriteLine("Native motion authority: fresh575/576 zero, DIR/SET3 preserve independent scalars, native blocked/stop/completion restore, unbound managed movement invalidation, nonzero576/schema refusal and legacy unknown retained.");
    }
}
