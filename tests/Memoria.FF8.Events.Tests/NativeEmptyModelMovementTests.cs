using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeEmptyModelMovementContract()
    {
        foreach (bool explicitEmptyModel in new[] { false, true })
        {
            List<(Jsm.Opcode, int)> initial = [(Jsm.Opcode.LBL, 16)];
            if (explicitEmptyModel)
            {
                initial.Add((Jsm.Opcode.SETMODEL, 65535));
            }
            initial.AddRange([(Jsm.Opcode.PSHN_L, -10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.SET, 1), (Jsm.Opcode.PSHN_L, 2000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.HIDE, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticObjects([0, (16 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, -10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 1), (Jsm.Opcode.RET, 8)], initial.ToArray(), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.GETINFO, 0), (Jsm.Opcode.JMP, -3), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                var session = new GameSession(new GameState());
                var engine = new EventEngine(session, session.State.Variables)
                {
                    AnimationFrameCount = (_, _) => throw new InvalidOperationException("An empty model requested rendered animation frame metadata."),
                    CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                    ResolveNativeScriptDirection = query => query.FixedX >= -8 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                    ResolveNativeScriptMovement = query =>
                    {
                        int x = query.FixedX + 1234;
                        int y = query.FixedY + 987;
                        NativeScriptGroundProbe[] probes = [new(x, y, 0), new(x, y, 0), new(x, y, 0)];
                        Require(probes.All(probe => query.HasActorCollision!(probe) == false), "Moving WORD536=FFFF collided with a modeled peer instead of native47A720's early return.");
                        return new(NativeScriptMovementStatus.Accepted, x, y, query.FixedZ, query.Triangle, query.Direction, [new(x, y, query.Direction, probes)]);
                    }
                };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            live.Step();
            live.Step();
            ActorState body = live.Actors[1];
            Require(body.ModelId == (explicitEmptyModel ? 65535 : -1) && !body.Visible && body.NativeScriptMovement is { Active: true, Asynchronous: true, Completion: 1 } movement && (movement.FixedX & 4095) != 0 && body.NativeMotionAuthority is { Version: 1, MovementDirection: 64, FacingHold: 0 } && !body.AnimationPlaying, "Retained empty body did not execute genuine fractional native mode1 movement independently from a rendered model.");
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            bool queryObserved = false;
            live.CommandExecuted += command =>
            {
                if (command.ObjectId == 1 && command.Name == "GETINFO")
                {
                    queryObserved = true;
                    NativeScriptMovementState raw = body.NativeScriptMovement!;
                    JsonElement fiber = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
                    Require(fiber.GetProperty("Results").GetProperty("0").GetInt32() == raw.FixedX / 4096 && fiber.GetProperty("Results").GetProperty("1").GetInt32() == raw.FixedY / 4096 && fiber.GetProperty("Results").GetProperty("6").GetInt32() == 2000, "GETINFO did not sample the executed raw empty-body position with native truncation/current speed.");
                }
            };
            for (int tick = 0; tick < 15; tick++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Empty-body raw motion/GETINFO diverged after independent full VM restore.");
            }
            Require(queryObserved && body.X == 100 && body.Y == 100 && body.NativeScriptMovement is { Active: false, Completion: 2, AnimationFlag40000: false } && !body.AnimationPlaying, "Empty-body async movement did not complete/clean physical flags without inventing a rendered animation.");
        }
        Console.WriteLine("Empty WORD536 body: hidden allocated -1/FFFF RMOVE raw fixed movement, SET placement authority, no animation resource or actor collision, exact GETINFO sampling, async completion and independent VM restore passed.");
    }
}
