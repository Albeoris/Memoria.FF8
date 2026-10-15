using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeHeldFacingCmoveContract()
    {
        byte[] Content(bool priorMove)
        {
            (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.MLIMIT, 0), (Jsm.Opcode.RET, 0)];
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
            if (priorMove)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0)]);
            }
            main.AddRange([(Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 123), (Jsm.Opcode.PSHN_L, 65539), (Jsm.Opcode.CMOVE, 65), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.PSHN_L, 192), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)]);
            return SyntheticObjects([1], [init, main.ToArray()]);
        }
        (GameSession Session, EventEngine Engine) Create(bool priorMove = false, bool geometry = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.NativeMotionAuthority is { Version: 1, FacingHold: <= 1 },
                ResolveNativeScriptDirection = query =>
                {
                    if (!query.HoldsFacing)
                    {
                        return new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator);
                    }
                    Require(query.DirectionLimit == 0 && query.FacingDirection == 128 && query.StopDistance == 3, "CMOVE did not clear610 or preserve body577/lowWORD538.");
                    return query.FixedX >= 4 * 4096 ? new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator) : new NativeScriptDirectionResult(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 8, 0);
                },
                ResolveNativeScriptMovement = query =>
                {
                    if (!geometry)
                    {
                        return null;
                    }
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                    return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(x, query.FixedY, query.Direction, fans)]);
                }
            };
            engine.LoadField(600, Content(priorMove), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        foreach (bool priorMove in new[] { false, true })
        {
            var live = Create(priorMove);
            List<int[]> issued = [];
            live.Engine.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.CMOVE) && command.Arguments.Count != 0)
                {
                    issued.Add(command.Arguments.ToArray());
                }
            };
            live.Engine.Step();
            int animation = live.Engine.Actors[0].AnimationId;
            live.Engine.Step();
            if (priorMove)
            {
                Require(live.Engine.Actors[0].NativeMovementCompletion542 == 2 && live.Engine.Actors[0].NativeScriptMovement is { Active: true, HoldsFacing: false }, "CMOVE prior-motion regression did not reach a genuine native MOVE completion.");
                animation = live.Engine.Actors[0].BaseAnimationId;
                live.Engine.Step();
            }
            ActorState actor = live.Engine.Actors[0];
            NativeScriptMovementState initial = actor.NativeScriptMovement!;
            Require(actor.NativeMovementCompletion542 == 1 && initial is { HoldsFacing: true, Asynchronous: false, AutomaticAnimation: false, MovementDirection: 8, FacingDirection: 128, StopDistance: 3 } && actor.NativeMotionAuthority == new NativeMotionAuthority(1, 8, 1) && actor.X == 1 && actor.Z == 0 && actor.Direction == 128 && actor.AnimationId == animation && !actor.MovementIgnoresWalkmesh && !live.Session.State.Variables.ContainsKey(5000) && issued.Count == 1 && issued[0].SequenceEqual(new[] { 100, 100, 123, 65539 }), $"CMOVE lost synchronous wait, held577/independent575, ground substep, unchanged animation or original fullDWORD issue arguments: prior{priorMove}, {initial}, actorXYZ{actor.X}/{actor.Y}/{actor.Z}, anim{animation}/{actor.AnimationId}, argsCount{issued.Count}.");
            JsonElement saved = live.Engine.Capture();
            JsonElement owner = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == initial.FiberSequence);
            Require(owner.GetProperty("Position").GetInt32() == initial.Operation && owner.GetProperty("Stack").GetArrayLength() == 0 && owner.GetProperty("WaitReason").GetString() == "NativeScriptMovement", "CMOVE did not hold its issuing PC with the four operands consumed exactly once.");
            var restored = Create(priorMove);
            restored.Engine.Restore(saved);
            bool completedHeld = false;
            for (int update = 0; update < 5; update++)
            {
                live.Engine.Step();
                restored.Engine.Step();
                Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()) && actor.AnimationId == animation && actor.X <= 4, "CMOVE diverged after independent restore, changed animation or teleported to its target.");
                if (actor.NativeMovementCompletion542 == 2 && actor.NativeScriptMovement is { Active: true })
                {
                    completedHeld = true;
                    Require(actor.NativeMotionAuthority is { FacingHold: 1 } && actor.Direction == 128 && !live.Session.State.Variables.ContainsKey(5000), "CMOVE cleared576 or continued its VM before the native completion consumer.");
                }
            }
            Require(completedHeld && actor.NativeMovementCompletion542 == 2 && actor.NativeScriptMovement is { Active: false, HoldsFacing: true, Asynchronous: false } && actor.NativeMotionAuthority == new NativeMotionAuthority(1, 8, 0) && live.Session.State.Variables[5000] == 77 && actor.Direction == 192 && issued.Count == 1, "CMOVE did not clear576 and continue the next command within the completion VM budget.");
            JsonNode malformed = JsonNode.Parse(saved.GetRawText())!;
            malformed["Actors"]![0]!["NativeScriptMovement"]!["Asynchronous"] = true;
            JsonElement before = live.Engine.Capture();
            try
            {
                live.Engine.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("CMOVE restored as an asynchronous RCMOVE receipt.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, live.Engine.Capture()), "Malformed CMOVE receipt partially mutated the VM.");
        }
        var refused = Create(geometry: false);
        refused.Engine.Step();
        try
        {
            refused.Engine.Step();
            throw new InvalidOperationException("CMOVE accepted missing original ground geometry.");
        }
        catch (EventScriptException) when (refused.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.CMOVE))
        {
        }
        JsonElement refusal = refused.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean());
        Require(refused.Engine.Actors[0].NativeScriptMovement is null && refused.Engine.Actors[0].MovementDirectionLimit == 6 && refused.Engine.Actors[0].NativeMotionAuthority is { FacingHold: 0 } && refusal.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 65539, 123, 100, 100 }), "CMOVE preflight consumed operands or changed body/motion scalars before geometry certification.");
        Console.WriteLine("CMOVE: synchronous grounded substeps, held577/independent575, cleared610, unchanged animation, delayed completion poll/return2 budget, issue args, prior native MOVE, full VM restore and atomic refusal passed.");
    }
}
