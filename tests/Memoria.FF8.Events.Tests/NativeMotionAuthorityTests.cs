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
            GameSession session = GameSession.CreateColdProcess();
            session.Restore(session.State with { WorldControls = NativeWorldControlState.CreateNewGame(), WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame(), EventRandom = session.State.EventRandom!.ClearSavedStateForNewGame() });
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = _ => native,
                ResolveNativeScriptDirection = query => new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator),
                ResolveNativeScriptMovement = _ => throw new InvalidOperationException("Stopped native motion queried ground geometry.")
            };
            engine.LoadField(100, content ?? program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine fresh = Create();
        Require(fresh.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "Fresh native allocation did not initialize independent575/576 to zero.");
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
        Require(JsonElement.DeepEquals(fresh.Capture(), independent.Capture()) && fresh.Actors[0].Direction == 200 && fresh.Actors[0].NativeScriptMovement is { Active: false, FixedX: 4 * 4096, FixedY: 5 * 4096, FixedZ: 6 * 4096 } && fresh.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0), "DIR/SET3 changed native direction authority after independently restored completion.");

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
            EventEngine unsupported = Create(false, SyntheticObjects([1], [init, commands.ToArray()]));
            try
            {
                for (int tick = 0; tick < 3; tick++)
                {
                    unsupported.Step();
                }
                throw new InvalidOperationException($"Uncertified {opcode} advanced without native movement authority.");
            }
            catch (EventScriptException)
            {
            }
            JsonElement fiber = unsupported.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(unsupported.LastFault?.Opcode == opcode.ToString() && fiber.GetProperty("Position").GetInt32() == init.Length + commands.FindIndex(command => command.Opcode == opcode) && fiber.GetProperty("Stack").GetArrayLength() == (opcode == Jsm.Opcode.MOVEA ? 2 : 4) && unsupported.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 0, 0) && unsupported.Actors[0].NativeScriptMovement is null, $"Uncertified {opcode} consumed its native operands or changed movement authority.");
        }

        EventEngine hold = Create();
        hold.Step();
        JsonNode held = JsonNode.Parse(hold.Capture().GetRawText())!;
        held["Actors"]![0]!["NativeMotionAuthority"]!["FacingHold"] = 1;
        hold.Restore(JsonSerializer.SerializeToElement(held));
        NativeScriptDirectionQuery? heldQuery = null;
        hold.ResolveNativeScriptDirection = query =>
        {
            heldQuery = query;
            return new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator);
        };
        hold.Step();
        Require(heldQuery is { HoldsFacing: true, FacingDirection: 128 } && hold.Actors[0].NativeMovementCompletion542 == 2 && hold.Actors[0].NativeMotionAuthority is { FacingHold: 1 }, "Native MOVE lost the saved facing hold while publishing physical completion.");
        hold.Step();
        Require(hold.Actors[0].NativeMotionAuthority is { FacingHold: 0 }, "Native MOVE did not clear the facing hold when its instruction consumed physical completion.");
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
        Console.WriteLine("Native motion authority: fresh575/576 zero, DIR/SET3 preserve independent scalars, native blocked/stop/completion restore, uncertified movement refusal, held facing and legacy unknown retained.");
    }
}
