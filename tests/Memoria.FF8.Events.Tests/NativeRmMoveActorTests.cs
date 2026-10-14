using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeRmMoveActorContract()
    {
        (Jsm.Opcode, int)[] Init(int label, int model, int x) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, model), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        (EventEngine Engine, GameSession Session) Create(int targetId = 0)
        {
            byte[] content = SyntheticObjects([1, (16 << 7) | 1], [Init(0, 0, 1000), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Init(16, 1, 0), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, targetId), (Jsm.Opcode.PSHN_L, 150), (Jsm.Opcode.RMOVEA, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            var session = new GameSession(new GameState());
            var engine = new EventEngine(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = _ => true,
                ResolveNativeScriptDirection = query => new(query.ObjectId == 1 && query.FixedX >= 3 * 4096 ? NativeScriptDirectionStatus.Stopped : NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX + 3072;
                    int y = query.FixedY + 2048;
                    NativeScriptGroundProbe[] fans = [new(x, y, 0), new(x, y, 0), new(x, y, 0)];
                    return new(NativeScriptMovementStatus.Accepted, x, y, query.FixedZ, query.Triangle, query.Direction, [new(x, y, query.Direction, fans)]);
                }
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return (engine, session);
        }
        var live = Create();
        live.Engine.Step();
        live.Engine.Step();
        NativeScriptMovementState targetBefore = live.Engine.Actors[0].NativeScriptMovement!;
        Require((targetBefore.FixedX & 4095) == 3072 && (targetBefore.FixedY & 4095) == 2048, "Synthetic target did not establish genuine executed fractional native raw placement.");
        List<int[]> operands = [];
        live.Engine.CommandExecuted += command =>
        {
            if (command.Name == "RMOVEA")
            {
                operands.Add(command.Arguments.ToArray());
            }
        };
        live.Engine.Step();
        NativeScriptMovementState issued = live.Engine.Actors[1].NativeScriptMovement!;
        Require(issued is { Active: true, Asynchronous: true, AutomaticAnimation: true, TargetNativeObjectId: 0, StopDistance: 150 } && issued.TargetX == targetBefore.FixedX && issued.TargetY == targetBefore.FixedY && issued.TargetZ == targetBefore.FixedZ && issued.TargetX != live.Engine.Actors[0].NativeScriptMovement!.FixedX && operands.Single().SequenceEqual([0, 150]) && !live.Session.State.Variables.ContainsKey(5000), "RMOVEA did not sample original target rawXYZ once, consume both operands once and return3/yield with automatic native locomotion.");
        JsonElement owner = live.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        Require(owner.GetProperty("Stack").GetArrayLength() == 0 && owner.GetProperty("WaitReason").GetString() == "Frames" && live.Engine.Actors[1].AnimationId == live.Engine.Actors[1].WalkAnimationId, "RMOVEA left a live-follow operand or held-PC wait instead of its original asynchronous yield.");
        var restored = Create();
        restored.Engine.Restore(live.Engine.Capture());
        for (int tick = 0; tick < 8; tick++)
        {
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()) && live.Engine.Actors[1].NativeScriptMovement!.TargetX == issued.TargetX && live.Engine.Actors[1].NativeScriptMovement!.TargetY == issued.TargetY, "Frozen RMOVEA target refreshed after issue or active/completed restore diverged.");
        }
        Require(operands.Count == 1 && live.Session.State.Variables[5000] == 77 && live.Engine.Actors[1].NativeMovementCompletion542 == 2 && live.Engine.Actors[1].NativeScriptMovement is { Active: false, Asynchronous: true } && live.Engine.Actors[1].AnimationId == live.Engine.Actors[1].BaseAnimationId, "RMOVEA did not release its original VM independently and finish with next-VM automatic idle cleanup.");
        var absent = Create(99);
        absent.Engine.Step();
        absent.Engine.Step();
        try
        {
            absent.Engine.Step();
            throw new InvalidOperationException("RMOVEA accepted an absent original pointer-table target.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement refused = absent.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        Require(refused.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([150, 99]) && absent.Engine.Actors[1].NativeScriptMovement is null, "RMOVEA refusal consumed the original target/stop or injected motion state.");
        Console.WriteLine("RMOVEA121: original raw target sampled once with fractional bits, both operands consumed once, mode1 asynchronous return3, automatic animation/idle cleanup, frozen target despite target motion, independent active/completed restore and missing-pointer atomic refusal passed.");
    }
}
