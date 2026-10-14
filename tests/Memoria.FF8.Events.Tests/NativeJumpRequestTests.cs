using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJumpMovementRequestContract()
    {
        foreach ((bool completedUpperMove, bool animatedLower) in new[] { (false, false), (true, false), (false, true) })
        {
            (Jsm.Opcode, int)[] actor = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] lower = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (animatedLower ? Jsm.Opcode.MOVE : Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> upper = [(Jsm.Opcode.LBL, 4)];
            if (completedUpperMove)
            {
                upper.AddRange([(Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RCMOVE, 0)]);
            }
            upper.AddRange([(Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 900), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.JUMP3, 23), (Jsm.Opcode.RET, 8)]);
            (Jsm.Opcode, int)[] controller = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([5, (16 << 7) | 1], [actor, lower, [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.RET, 8)], upper.ToArray(), [(Jsm.Opcode.LBL, 5), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.RET, 8)], controller]);
            EventEngine Create()
            {
                var session = new GameSession(new GameState());
                var result = new EventEngine(session, session.State.Variables)
                {
                    AnimationFrameCount = (_, _) => 4,
                    CanUseNativeScriptMove = body => body.ObjectId == 0,
                    ResolveNativeTriangleHeight = (triangle, x, y) => triangle == 23 && x == 400 && y == 5 ? 4 : null,
                    ResolveNativeScriptDirection = query => query.TargetX == 500 * 4096 ? new(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator),
                    ResolveNativeScriptMovement = query =>
                    {
                        int x = query.FixedX + 4096;
                        NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                        return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, 0, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fans)]);
                    }
                };
                result.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return result;
            }
            EventEngine live = Create();
            while (live.Actors[0].NativeJump is null && live.Frame < 12)
            {
                live.Step();
            }
            ActorState body = live.Actors[0];
            NativeJumpState jump = body.NativeJump ?? throw new InvalidOperationException("Higher native request did not replace its genuine suspended/completed mode1 body with JUMP3.");
            NativeMovementRequestState backup = body.NativeMovementRequest ?? throw new InvalidOperationException("JUMP3 discarded the one original lower movement backup.");
            Require(body.NativeMovementCompletion542 == 1 && body.NativeLandingTriangle508 == 23 && body.NativeTurnAccumulator474 == 0 && jump is { Active: true, TargetZ: 16384 } && jump.AsyncFlag20000 == !animatedLower && jump.AnimationFlag40000 == animatedLower && body.NativeScriptMovement is null && body.MovementTarget is null && backup.SavedMovement is { ScriptId: 1, Active: true, Speed: 1000, TargetX: 40960000 } && backup.SavedMovement.Asynchronous == !animatedLower && body.NativeMotionAuthority is { MovementDirection: 64 } authority && authority.FacingHold == (completedUpperMove ? 1 : 0), "Requested JUMP3 fabricated an idle state, lost its physical flags/hold or changed the lower target/speed backup.");
            JsonElement issued = live.Capture();
            EventEngine restored = Create();
            restored.Restore(issued);
            for (int update = 1; update <= 4; update++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && body.NativeMovementRequest == backup && body.NativeScriptMovement is null, "Requested jump changed its original lower backup or diverged during independently restored flight.");
                Require(body.NativeMotionAuthority!.FacingHold == (completedUpperMove ? 1 : 0), "Requested JUMP3 cleared live576 before native529FF0 could consume completion542.");
            }
            NativeJumpState landed = body.NativeJump!;
            Require(body.NativeMovementCompletion542 == 2 && landed is { Active: true } && landed.AsyncFlag20000 == !animatedLower && landed.FixedZ == 16383 && body.WalkmeshTriangle == 23, "Requested JUMP3 missed its native raw endpoint residue or delayed triangle completion.");
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && body.NativeJump is { Active: false, AsyncFlag20000: false } completedJump && completedJump.AnimationFlag40000 == animatedLower && body.NativeMotionAuthority!.FacingHold == 0 && body.NativeMovementRequest == backup, "Native post-VM jump completion did not consume inherited20000 and clear576 independently of RET8.");
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && body.NativeJump is null && body.NativeMovementRequest is null && body.NativeMovementCompletion542 == 1 && body.NativeTurnAccumulator474 == (animatedLower ? 0 : 3) && body.NativeScriptMovement is { Active: true, ScriptId: 1, Speed: 1000 } resumed && resumed.FixedX == landed.FixedX + 4096 && resumed.FixedY == landed.FixedY && body.NativeMotionAuthority!.FacingHold == 0, "RET8 failed to restore the lower target/speed from the actual raw jump landing or retained an obsolete jump body.");
            EventEngine resumedCopy = Create();
            resumedCopy.Restore(live.Capture());
            Require(JsonElement.DeepEquals(live.Capture(), resumedCopy.Capture()), "Completed JUMP phase survived RET8 and incorrectly constrained its rewritten physical474 during restore.");
            JsonNode malformed = JsonNode.Parse(issued.GetRawText())!;
            malformed["Actors"]![0]!["NativeJump"]!.AsObject().Remove("AnimationFlag40000");
            JsonElement valid = restored.Capture();
            try
            {
                restored.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Requested JUMP3 restored without known inherited physical animation flags.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, restored.Capture()), "Invalid jump/request flags partially restored the original lower backup or current body.");
        }
        Console.WriteLine("Requested JUMP3: real suspended and completed upper RCMOVE replacement, single backup, inherited20000/40000/576, full restored flight, native completion cleanup and current-landing RET8 lower resumption passed.");
    }
}
