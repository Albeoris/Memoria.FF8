using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMeaningfulContactContract()
    {
        foreach ((bool talk, bool placement) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            (Jsm.Opcode, int)[] Contact(int label)
            {
                List<(Jsm.Opcode, int)> ops = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, placement ? 500 : 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0)];
                if (placement)
                {
                    ops.AddRange([(Jsm.Opcode.SET3, 1), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.DIR, 0)]);
                }
                else
                {
                    ops.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0)]);
                }
                ops.AddRange([(Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]);
                return ops.ToArray();
            }
            byte[] content = SyntheticObjects([0, (16 << 7) | 3], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 111), (Jsm.Opcode.POPI_L, 1), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.RET, 8)], Contact(18), Contact(19)]);
            EventEngine Create()
            {
                var session = new GameSession(new GameState());
                var result = new EventEngine(session, session.State.Variables)
                {
                    AnimationFrameCount = (_, _) => 4,
                    CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                    ResolveNativeScriptDirection = query => query.TargetX == 300 * 4096 && query.FixedX >= 202 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                    ResolveNativeScriptMovement = query =>
                    {
                        int x = query.FixedX + 4096;
                        NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                        return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fans)]);
                    }
                };
                result.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return result;
            }
            EventEngine live = Create();
            live.Step();
            live.Step();
            NativeScriptMovementState original = live.Actors[1].NativeScriptMovement!;
            JsonElement lower = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
            // Declared component input boundary; no research journal is fabricated.
            if (talk)
            {
                Require(live.TalkTo(1), "Native talk slot7 failed to enqueue its original local2.");
            }
            else
            {
                typeof(ActorState).GetProperty(nameof(ActorState.NativePushContactPending))!.SetValue(live.Actors[1], true);
            }
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            bool higherContextObserved = false;
            live.CommandExecuted += command =>
            {
                if (command.ObjectId == 1 && command.ScriptId == (talk ? 18 : 19) && command.Name == "WAIT")
                {
                    higherContextObserved = true;
                    NativeMovementRequestState request = live.Actors[1].NativeMovementRequest!;
                    Require(request.RequestSlot == (talk ? 7 : 6) && request.SavedMovement == original, "Meaningful contact changed the one original target/stop/speed backup or native action slot.");
                    JsonElement state = live.Capture();
                    Require(JsonElement.DeepEquals(lower, state.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17)), "Meaningful contact replaced the lower PC, unfinished operands, results or wait.");
                    EventEngine paused = Create();
                    paused.Restore(state);
                    Require(JsonElement.DeepEquals(state, paused.Capture()), "Meaningful contact did not restore its independent upper result bank and body mode at WAIT.");
                }
            };
            for (int tick = 0; tick < 10; tick++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Meaningful push/talk or native SET3/RET8 diverged after pending-request restore.");
            }
            ActorState actor = live.Actors[1];
            Require(higherContextObserved && actor.NativeMovementRequest is null && actor.NativeScriptMovement is { Active: true, ScriptId: 17 } current && current.TargetX == original.TargetX && actor.X > (placement ? 500 : 300) && actor.AnimationId == (placement ? actor.WalkAnimationId : actor.BaseAnimationId), "Meaningful contact failed actual-current-position RET8 resumption or inferred movement-animation flags from the saved lower opcode.");
        }
        Console.WriteLine("Meaningful native push6/talk7: independent upper/lower operand/result/PC ownership, MOVE replacement or suspended SET3+DIR preserving physical flags, pending/mid-request restore, actual-position RET8 resumption and animation policy passed.");
    }
}
