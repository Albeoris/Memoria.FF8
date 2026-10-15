using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeRcMoveContract()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.MLIMIT, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.RCMOVE, 0), (Jsm.Opcode.PSHN_L, 192), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.MOVESYNC, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1], [init, main]);
        (GameSession Session, EventEngine Engine) Create(bool geometry = true)
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.NativeMotionAuthority is { Version: 1, FacingHold: <= 1 },
                ResolveNativeScriptDirection = query =>
                {
                    Require(query.HoldsFacing && query.DirectionLimit == 0, "RCMOVE did not select the native cleared direction-limit request.");
                    return query.FixedX >= 4 * 4096 ? new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator) : new NativeScriptDirectionResult(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 8, 0);
                },
                ResolveNativeScriptMovement = query =>
                {
                    if (!geometry)
                    {
                        return null;
                    }
                    // Synthetic accepted ground advances by one unit per callback; no target teleport.
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                    return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(x, query.FixedY, query.Direction, fans)]);
                }
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (session, engine);
        }
        var live = Create();
        live.Engine.Step();
        int animation = live.Engine.Actors[0].AnimationId;
        live.Engine.Step();
        ActorState actor = live.Engine.Actors[0];
        Require(actor.NativeMovementCompletion542 == 1 && actor.NativeScriptMovement is { HoldsFacing: true, Asynchronous: true, AutomaticAnimation: false, MovementDirection: 8, FacingDirection: 128 } && actor.NativeMotionAuthority == new NativeMotionAuthority(1, 8, 1) && actor.X == 1 && actor.Direction == 128 && actor.AnimationId == animation && !actor.MovementIgnoresWalkmesh && !live.Session.State.Variables.ContainsKey(5000), "RCMOVE lost asynchronous budget yield, distinct movement/facing bytes, grounded substep or unchanged animation.");
        JsonElement saved = live.Engine.Capture();
        var restored = Create();
        restored.Engine.Restore(saved);
        bool completedHeld = false;
        for (int frame = 0; frame < 8; frame++)
        {
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "RCMOVE diverged after independent restore.");
            Require(actor.Direction == 192 && actor.AnimationId == animation && actor.X <= 4, "Held RCMOVE overwrote an explicit DIR/animation or teleported to its target.");
            if (actor.NativeMovementCompletion542 == 2 && actor.NativeScriptMovement is { Active: true })
            {
                completedHeld = true;
                Require(actor.NativeMotionAuthority is { FacingHold: 1 }, "RCMOVE cleared576 before the next actor VM completion consumer.");
            }
        }
        Require(completedHeld && actor.NativeMovementCompletion542 == 2 && actor.NativeScriptMovement is { Active: false, HoldsFacing: true } && actor.NativeMotionAuthority == new NativeMotionAuthority(1, 8, 0) && live.Session.State.Variables[5000] == 7 && live.Session.State.Variables[5001] == 9, "RCMOVE did not preserve async completion timing, clear native hold or release MOVESYNC.");
        JsonNode invalid = JsonNode.Parse(saved.GetRawText())!;
        invalid["Actors"]![0]!["NativeMotionAuthority"]!["FacingHold"] = 0;
        try
        {
            Create().Engine.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Saved RCMOVE accepted a mismatched facing-hold byte.");
        }
        catch (InvalidDataException)
        {
        }
        var refused = Create(false);
        refused.Engine.Step();
        try
        {
            refused.Engine.Step();
            throw new InvalidOperationException("RCMOVE accepted missing certified ground geometry.");
        }
        catch (EventScriptException) when (refused.Engine.LastFault?.Opcode == nameof(Jsm.Opcode.RCMOVE))
        {
        }
        Require(refused.Engine.Actors[0].NativeScriptMovement is null && refused.Engine.Actors[0].MovementDirectionLimit == 6 && refused.Engine.Actors[0].NativeMotionAuthority is { FacingHold: 0 } && refused.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => !fiber.GetProperty("Initialization").GetBoolean()).GetProperty("Stack").GetArrayLength() == 4, "RCMOVE preflight consumed operands or changed native scalars before geometry certification.");
        Console.WriteLine("RCMOVE: grounded async substeps, held body577/independent575, direction-limit reset, unchanged animation, return3 yield, next-VM cleanup/MOVESYNC, independent restore and atomic refusal passed.");
    }
}
