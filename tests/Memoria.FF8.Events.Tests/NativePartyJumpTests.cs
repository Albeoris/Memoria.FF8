using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePartyJumpContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        EventEngine Create(int slot = 0, int duration = 65540, bool plane = true)
        {
            (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] jump = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 901), (Jsm.Opcode.POPI_L, 0), (Jsm.Opcode.PSHN_L, 903), (Jsm.Opcode.POPI_L, 3), (Jsm.Opcode.PSHN_L, 123456), (Jsm.Opcode.PSHM_L, 5100), (Jsm.Opcode.PSHM_L, 5104), (Jsm.Opcode.PJUMPA, 333), (Jsm.Opcode.POPM_L, 5000), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.POPM_L, 5001), (Jsm.Opcode.PSHI_L, 3), (Jsm.Opcode.POPM_L, 5002), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] target = [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)];
            (Jsm.Opcode, int)[] move = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.PSHN_L, 101), (Jsm.Opcode.PSHN_L, -202), (Jsm.Opcode.PSHN_L, 303), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, -1000), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            session.SetEventMemory(session.State.EventMemory.Write(5100, 4, NativeScalarValue.Exact(slot)));
            session.SetEventMemory(session.State.EventMemory.Write(5104, 4, NativeScalarValue.Exact(duration)));
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveNativeTriangleHeight = plane ? (triangle, _, _) => triangle == 7 ? 4 : null : null,
                CanUseNativeScriptMove = actor => actor.ObjectId == 2,
                ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 192, 0),
                ResolveNativeScriptMovement = query =>
                {
                    int x = query.FixedX - 1377;
                    int y = query.FixedY - 701;
                    NativeScriptGroundProbe[] probes = [new(x, y, 3), new(x, y, 3), new(x, y, 3)];
                    return new(NativeScriptMovementStatus.Accepted, x, y, query.FixedZ, query.Triangle, query.Direction, [new(x, y, query.Direction, probes)]);
                }
            };
            engine.LoadField(74, SyntheticObjects([1, 16 << 7, (32 << 7) | 1], [caller, jump, unused, target, move]), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine actual = Create();
        NativeScriptMovementState? sampled = null;
        NativeJumpState? issued = null;
        int issues = 0;
        actual.CommandExecuted += command =>
        {
            if (command.Name == "PJUMPA" && command.Arguments.Count > 0)
            {
                sampled = actual.Actors[2].NativeScriptMovement;
                issued = actual.Actors[0].NativeJump;
                issues++;
                Require(command.Arguments.SequenceEqual([0, 65540]), "PJUMPA narrowed its logged DWORD operands or reversed slot/duration.");
            }
        };
        while (actual.Actors[0].NativeJump is null && actual.Frame < 16)
        {
            actual.Step();
        }
        Require(sampled is not null && issued is not null && actual.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && actual.Scene.PartyBindingMemory is { First: 1 } && actual.Scene.FieldPartyNativeActorIds.SequenceEqual([255, 255, 255]), "PJUMPA component did not reach the saved compact party body independently of detached runtime followers.");
        NativeScriptMovementState sampledTarget = sampled!;
        NativeJumpState issuedJump = issued!;
        Require(issuedJump is { Duration: 4 } && issuedJump.TargetX == sampledTarget.FixedX && issuedJump.TargetY == sampledTarget.FixedY && issuedJump.TargetZ == sampledTarget.FixedZ && (issuedJump.TargetX & 4095) != 0 && (issuedJump.TargetY & 4095) != 0 && actual.Actors[0].NativeLandingTriangle508 == 7, "PJUMPA lost the one-time raw fractional XYZ/triangle snapshot, used display offsets or treated its inline parameter as triangle.");
        Require(actual.Actors[0].NativeJump is { Active: true, TargetZ: 16384, LandingPlaneTriangle: 7 } && actual.Actors[0].NativeMovementCompletion542 == 1, "PJUMPA did not enter the shared mode2 landing-plane setup.");
        JsonElement saved = actual.Capture();
        JsonElement owner = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(owner.GetProperty("Position").GetInt32() == issuedJump.Operation && owner.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([123456]) && owner.GetProperty("Results").GetProperty("0").GetInt32() == 901 && owner.GetProperty("Results").GetProperty("3").GetInt32() == 903, "PJUMPA changed R0, another result, unrelated stack or its original held PC.");
        EventEngine independent = Create();
        independent.Restore(saved);
        for (int callback = 0; callback < 10; callback++)
        {
            actual.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()) && actual.Actors[0].NativeJump!.TargetX == sampledTarget.FixedX && actual.Actors[0].NativeJump!.TargetY == sampledTarget.FixedY, "PJUMPA retargeted the moving party body or diverged after an independent active restore.");
        }
        JsonElement variables = CapturedVariables(actual.Capture());
        Require(issues == 1 && actual.Actors[2].NativeScriptMovement!.FixedX != sampledTarget.FixedX && actual.Actors[0].NativeJump is { Active: false } && actual.Actors[0].WalkmeshTriangle == 7 && variables.GetProperty("5000").GetInt32() == 123456 && variables.GetProperty("5001").GetInt32() == 901 && variables.GetProperty("5002").GetInt32() == 903, "PJUMPA completion did not consume its operands once, preserve result registers/stack and finish on the captured triangle.");

        foreach ((int slot, int duration, bool plane) in new[] { (1, 65540, true), (256, 65540, true), (int.MinValue, 65540, true), (0, 65536, true), (0, -1, true), (0, 65540, false) })
        {
            EventEngine refused = Create(slot, duration, plane);
            try
            {
                while (refused.Frame < 16)
                {
                    refused.Step();
                }
                throw new InvalidOperationException("PJUMPA accepted an unowned target, unsupported duration or missing landing plane.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "PJUMPA")
            {
            }
            JsonElement failed = refused.Capture();
            JsonElement failedOwner = failed.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
            Require(failedOwner.GetProperty("Position").GetInt32() == refused.LastFault!.Operation && failedOwner.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual([duration, slot, 123456]) && refused.Actors[0].NativeJump is null && refused.Actors[0].NativeMovementCompletion542 == 0, "PJUMPA refusal changed its original PC, operands or physical body before validation.");
        }
        JsmProgram original = JsmProgram.Read(assets.LoadField(795).Jsm);
        EventScript site = original.Objects[6].Scripts.Single(script => script.Id == 56);
        Require(site.Start <= 826 && site.End > 826 && original.Operations[826].Opcode == Jsm.Opcode.PJUMPA, "Actual795/6/56/826 PJUMPA boundary changed.");
        Console.WriteLine("Native PJUMPA: full DWORD saved party target, low-WORD duration, one-time raw fractional XYZ/triangle capture, unchanged shared mode2/R0/stack, moving-target restore and atomic refusal PASS.");
    }
}
