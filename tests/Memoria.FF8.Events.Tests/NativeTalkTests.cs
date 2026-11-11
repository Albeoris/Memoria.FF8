using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeTalkSelectionContract()
    {
        EventEngine Create(int distance = 175, int height = 0, int facing = 128, int secondDistance = 1000, bool hidden = false, bool talkOff = false)
        {
            (Jsm.Opcode, int)[] player = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 48), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.RET, 0)];
            (Jsm.Opcode, int)[] Target(int label, int y, int z, bool hide, bool disabled)
            {
                List<(Jsm.Opcode, int)> instructions = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, z), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 128), (Jsm.Opcode.TALKRADIUS, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.BASEANIME, 0)];
                if (hide)
                {
                    instructions.Add((Jsm.Opcode.HIDE, 0));
                }
                if (disabled)
                {
                    instructions.Add((Jsm.Opcode.TALKOFF, 0));
                }
                instructions.Add((Jsm.Opcode.RET, 0));
                return instructions.ToArray();
            }
            byte[] content = SyntheticObjects([0, 16 << 7, 32 << 7], [player, Target(16, distance, height, hidden, talkOff), Target(32, secondDistance, 0, false, false)]);
            GameSession session = new(new GameState());
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 1,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                ResolveNativeTriangleHeight = (_, _, y) => y == distance ? height : 0
            };
            engine.LoadField(100, content, [], new FieldTransition(100, 0, 0, 0, facing));
            engine.Step();
            engine.Step();
            Require(engine.Scene.NativeActorLayoutVersion == 1 && engine.Scene.NativeFieldLoadVersion == 1, "Declared synthetic talk fixture did not execute canonical initialization.");
            return engine;
        }

        Require(Create().SelectInteractionTarget() == 1 && Create(distance: 176).SelectInteractionTarget() is null, "Native talk lost the strict playerPush+targetTalk distance boundary.");
        Require(Create(height: 255).SelectInteractionTarget() == 1 && Create(height: -255).SelectInteractionTarget() == 1 && Create(height: 256).SelectInteractionTarget() is null && Create(height: -256).SelectInteractionTarget() is null, "Native action query lost strict vertical ±256 bounds.");
        Require(Create(facing: 65).SelectInteractionTarget() == 1 && Create(facing: 64).SelectInteractionTarget() is null && Create(facing: 192).SelectInteractionTarget() is null, "Native action query lost its strict facing error below64.");
        Require(Create(secondDistance: 100).SelectInteractionTarget() == 1, "Equal-angle action candidates changed native array-order tie selection to nearest distance.");
        Require(Create(hidden: true).SelectInteractionTarget() == 1 && Create(talkOff: true).SelectInteractionTarget() is null, "Native action query changed retained hidden-model or TALKOFF semantics.");
        EventEngine pure = Create();
        JsonElement before = pure.Capture();
        Require(pure.SelectInteractionTarget() == 1 && JsonElement.DeepEquals(before, pure.Capture()), "Pure action selection changed actor, fiber or scene state.");
        JsonNode legacy = JsonNode.Parse(before.GetRawText())!;
        legacy["Scene"]!["NativeFieldLoadVersion"] = 0;
        legacy["Scene"]!.AsObject().Remove("NativePlayerEarlyRetryState");
        pure.Restore(JsonSerializer.SerializeToElement(legacy));
        Require(pure.SelectInteractionTarget() is null, "Unknown legacy field-load marker silently acquired native summed-radius action behavior.");
        Console.WriteLine("Native action4796E0: integer XYZ, strict summed radius/height/facing, angle-first native-order ties, hidden retained models, TALKOFF, pure selection and legacy fallback verified.");
    }

    private static void NativeTalkAndJumpContract()
    {
        NativeTalkDuringJumpContract();
        NativeTalkDuringPushMovementContract();
    }

    private static void NativeTalkDuringJumpContract()
    {
        byte[] content = SyntheticObjects([2], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.JUMP, 9), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)], [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            EventEngine engine = new(CreateNativeJumpSession())
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveNativeTriangleHeight = (_, _, _) => 4
            };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        live.Step();
        live.Step();
        ActorState actor = live.Actors[0];
        NativeJumpState issued = actor.NativeJump ?? throw new InvalidOperationException("Talk fixture did not issue native mode2.");
        JsonElement lower = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(live.TalkTo(0) && actor.NativeJump == issued && actor.NativeMovementRequest is null, "Talk failed to select slot7 above mode2 without suspending its physical body or creating a mode1 backup.");
        JsonElement pending = live.Capture();
        Require(pending.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 2 && fiber.GetProperty("NativeActorSlot").GetInt32() == 7), "Talk failed to retain its native slot7 owner.");
        EventEngine restored = Create();
        restored.Restore(pending);
        for (int update = 1; update <= 14; update++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Talk above mode2 diverged after independent pending-request restoration.");
            Require(actor.NativeMovementRequest is null && actor.NativeScriptMovement is null && actor.NativeJump!.FiberSequence == issued.FiberSequence, "Talk created a movement backup or replaced the lower jump owner.");
            if (update <= 4)
            {
                JsonElement held = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
                Require(JsonElement.DeepEquals(lower, held) && actor.NativeJump is { Active: true }, "Talk changed the held lower jump PC, operands or wait before RET8.");
                Require(actor.NativeMovementCompletion542 == (update == 4 ? 2 : 1) && actor.NativeTurnAccumulator474 == Math.Min(update, 3), "Talk suspended native mode2 flight or changed its delayed completion timing.");
                EventEngine activeCopy = Create();
                activeCopy.Restore(live.Capture());
                Require(JsonElement.DeepEquals(live.Capture(), activeCopy.Capture()), "Active talk and independent lower mode2 did not restore together.");
            }
        }
        Require(actor.NativeJump is { Active: false } && actor.NativeMovementCompletion542 == 2 && actor.WalkmeshTriangle == 9, "Talk RET8 failed to release the original lower jump completion consumer.");
    }

    private static void NativeTalkDuringPushMovementContract()
    {
        (Jsm.Opcode, int)[] other = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 200), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] move = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 30), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] talk = [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)];
        (Jsm.Opcode, int)[] push = [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 260), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.JUMP3, 9), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([0, (16 << 7) | 3], [other, init, move, talk, push]);
        EventEngine Create()
        {
            EventEngine engine = new(CreateNativeJumpSession())
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                ResolveNativeTriangleHeight = (_, _, _) => 0,
                ResolveNativeScriptDirection = query => query.FixedX >= 225 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 4096;
                    NativeScriptGroundProbe[] fans = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                    return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, 0, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fans)]);
                }
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }

        foreach ((bool jumpIssued, bool jumpCompleted) in new[] { (false, false), (true, false), (true, true) })
        {
            EventEngine live = Create();
            EventEngine? restored = null;
            List<ScriptCommand> commands = [];
            live.CommandExecuted += commands.Add;
            ActorState actor = live.Actors[1];
            JsonElement Fiber(int script) => live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == script);
            void CheckRestore()
            {
                JsonElement snapshot = live.Capture();
                restored = Create();
                restored.Restore(snapshot);
                Require(JsonElement.DeepEquals(snapshot, restored.Capture()), "Talk and push movement state changed on independent restore.");
            }
            void AdvanceUntil(Func<bool> condition)
            {
                for (int callback = 0; callback < 24 && !condition(); callback++)
                {
                    live.Step();
                    if (restored is not null)
                    {
                        restored.Step();
                        Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Talk and push movement diverged after independent restore.");
                    }
                }
                Require(condition(), "Talk and push movement failed to reach the expected phase.");
            }

            AdvanceUntil(() => actor.NativeScriptMovement is { Active: true, Asynchronous: false, ScriptId: 17 });
            NativeScriptMovementState original = actor.NativeScriptMovement!;
            JsonElement lower = Fiber(17);
            typeof(ActorState).GetProperty(nameof(ActorState.NativePushContactPending))!.SetValue(actor, true);
            AdvanceUntil(() => commands.Any(command => command.ScriptId == 19 && command.Name == "WAIT"));
            Require(actor.NativeMovementRequest is { RequestSlot: 6 } backup && backup.SavedMovement == original && actor.NativeScriptMovement is { Suspended: true }, "Push did not suspend the original MOVE and retain its physical backup.");
            if (jumpIssued)
            {
                AdvanceUntil(() => jumpCompleted ? actor.NativeJump is { Active: false } : actor.NativeJump is { Active: true } && actor.NativeTurnAccumulator474 == 1);
            }
            NativeJumpState? jumpBeforeTalk = actor.NativeJump;
            Require(live.TalkTo(1) && actor.NativeMovementRequest is { RequestSlot: 7 } transferred && transferred.SavedMovement == original && actor.NativeJump == jumpBeforeTalk, "Talk did not take ownership of the existing backup while retaining the current body.");
            Require(JsonElement.DeepEquals(lower, Fiber(17)), "Talk modified the original issued MOVE fiber.");
            CheckRestore();

            if (jumpIssued)
            {
                AdvanceUntil(() => actor.NativeMovementRequest is null && actor.NativeJump is null);
                Require(actor.NativeScriptMovement is { Active: true, Suspended: false } resumed && resumed.FiberSequence == original.FiberSequence && resumed.TargetX == original.TargetX && resumed.FixedX > jumpBeforeTalk!.FixedX, "Talk RET8 did not resume the original mode1 target from the actual airborne position.");
                if (!jumpCompleted)
                {
                    JsonElement displaced = Fiber(19);
                    Require(displaced.GetProperty("WaitReason").GetString() == "NativeJump" && displaced.GetProperty("Stack").GetArrayLength() == 0 && actor.NativeMovementCompletion542 == 1, "Talk RET8 lost the displaced JUMP3's issued state or completed its wait before mode1 reached the target.");
                }
                CheckRestore();
            }
            else
            {
                AdvanceUntil(() => actor.NativeJump is { Active: true });
                Require(actor.NativeScriptMovement is null && actor.NativeMovementRequest is null && JsonElement.DeepEquals(lower, Fiber(17)), "Push JUMP3 did not replace the restored body while retaining the issued MOVE fiber.");
                CheckRestore();
            }

            AdvanceUntil(() => commands.Any(command => command.ScriptId == 17 && command.Name == "WAIT"));
            ScriptCommand[] jumps = commands.Where(command => command.ScriptId == 19 && command.Name == "JUMP3").ToArray();
            ScriptCommand[] moves = commands.Where(command => command.ScriptId == 17 && command.Name == "MOVE").ToArray();
            Require(jumps.Length >= 2 && jumps[0].Arguments.SequenceEqual(new[] { 260, 0, 0, 3 }) && jumps.Skip(1).All(command => command.Arguments.Count == 0), "The original push consumed its JUMP3 operands more than once or skipped the held completion poll.");
            Require(moves.Length >= 2 && Fiber(17).GetProperty("WaitReason").GetString() == "Frames" && actor.NativeScriptMovement is not { Active: true } && actor.NativeMovementCompletion542 == 2, "The lower MOVE reissued instead of consuming shared completion after the push.");
            Require(jumpIssued ? actor.NativeJump is null && actor.X == 1000 : actor.NativeJump is { Active: false } && actor.WalkmeshTriangle == 9 && actor.X < 261, "The two request orderings failed to preserve their respective mode1 and mode2 endpoints.");
        }
    }
}
