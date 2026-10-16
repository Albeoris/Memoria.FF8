using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeForcedActorMoveContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        (Jsm.Opcode, int)[] Init(int label, int model, int x) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, model), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, x), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)];
        (EventEngine Engine, GameSession Session) Create(int targetId = 0, bool inheritedAnimation = false)
        {
            (Jsm.Opcode, int)[] prefix = inheritedAnimation ? [(Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0)] : [];
            byte[] content = SyntheticObjects([1, (16 << 7) | 1], [Init(0, 0, 1000), [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Init(16, 1, 0), [(Jsm.Opcode.LBL, 17), .. prefix, (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHAC, targetId), (Jsm.Opcode.RANIMELOOP, 3), (Jsm.Opcode.PSHN_L, 65535), (Jsm.Opcode.FMOVEA, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_W, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            var session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
            var engine = new EventEngine(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = _ => true,
                ResolveNativeScriptDirection = query => new(query.ObjectId == 1 && query.FixedX >= 6 * 4096 ? NativeScriptDirectionStatus.Stopped : NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, 0),
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
            if (command.Name == "FMOVEA")
            {
                operands.Add(command.Arguments.ToArray());
            }
        };
        for (int tick = 0; tick < 4 && live.Engine.Actors[1].NativeScriptMovement is null; tick++)
        {
            live.Engine.Step();
        }
        NativeScriptMovementState issued = live.Engine.Actors[1].NativeScriptMovement!;
        Require(issued is { Active: true, Asynchronous: false, AutomaticAnimation: false, TargetNativeObjectId: 0, StopDistance: -1 } && live.Engine.Actors[1].AnimationId == 3 && !live.Session.State.Variables.ContainsKey(5000), "FMOVEA did not preserve forced animation, narrow the full DWORD stop to signed WORD or hold its PC.");
        JsonElement owner = live.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        Require(owner.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([0]) && owner.GetProperty("WaitReason").GetString() == "NativeScriptMovement", "FMOVEA failed to retain the full DWORD native target operand.");
        var restored = Create();
        restored.Engine.Restore(live.Engine.Capture());
        int initialTargetX = issued.TargetX;
        for (int tick = 0; tick < 12; tick++)
        {
            live.Engine.Step();
            restored.Engine.Step();
            Require(JsonElement.DeepEquals(live.Engine.Capture(), restored.Engine.Capture()), "FMOVEA active/completed independent restore diverged.");
        }
        Require(live.Engine.Actors[1].NativeScriptMovement is { Active: false, AutomaticAnimation: false } completed && completed.TargetX != initialTargetX && live.Engine.Actors[1].AnimationId == 3 && live.Session.State.Variables[5000] == 77 && live.Engine.Actors[1].NativeMovementCompletion542 == 2, "FMOVEA failed live target refresh, completion operand release or forced-animation preservation.");
        var inherited = Create(inheritedAnimation: true);
        for (int tick = 0; tick < 10 && inherited.Engine.Actors[1].NativeScriptMovement?.AutomaticAnimation != false; tick++)
        {
            inherited.Engine.Step();
        }
        Require(inherited.Engine.Actors[1].NativeScriptMovement is { AutomaticAnimation: false, ResetAnimationOnCompletion: true }, "FMOVEA dropped inherited native flag40000 from the replaced asynchronous body.");
        var inheritedCopy = Create(inheritedAnimation: true);
        inheritedCopy.Engine.Restore(inherited.Engine.Capture());
        for (int tick = 0; tick < 12; tick++)
        {
            inherited.Engine.Step();
            inheritedCopy.Engine.Step();
            Require(JsonElement.DeepEquals(inherited.Engine.Capture(), inheritedCopy.Engine.Capture()), "Inherited FMOVEA flag40000 changed after restore.");
        }
        Require(inherited.Session.State.Variables[5000] == 77 && inherited.Engine.Actors[1].AnimationId == inherited.Engine.Actors[1].BaseAnimationId, "FMOVEA completion failed inherited flag40000 idle cleanup.");
        var absent = Create(99);
        absent.Engine.Step();
        absent.Engine.Step();
        try
        {
            for (int tick = 0; tick < 5; tick++)
            {
                absent.Engine.Step();
            }
            throw new InvalidOperationException("FMOVEA accepted an absent original pointer-table target.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement refused = absent.Engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 17);
        Require(refused.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([65535, 99]) && absent.Engine.Actors[1].NativeScriptMovement is null, "FMOVEA refusal consumed the original target/stop or injected motion state.");
        JsmProgram rescue = JsmProgram.Read(assets.LoadField(675).Jsm);
        Require(rescue.Operations[351].Opcode == Jsm.Opcode.FMOVEA, "Original field675 reached instruction changed.");
        Console.WriteLine("FMOVEA177: synchronous native target refresh, signed WORD stop, retained target operand, forced-animation preservation, completion release, independent active/completed restore and atomic missing-target refusal PASS.");
    }
}
