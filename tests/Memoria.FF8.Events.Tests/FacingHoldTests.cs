using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeFacingHoldContract()
    {
        using GameAssets assets = new(@"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        EventEngine Create(byte[] content)
        {
            GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4, CanUseNativeScriptMove = _ => true };
            engine.ResolveNativeScriptDirection = query => new NativeScriptDirectionResult(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, query.HoldsFacing ? (byte)128 : (byte)64, 0);
            engine.ResolveNativeScriptMovement = query => new NativeScriptMovementResult(NativeScriptMovementStatus.Blocked, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(query.FixedX, query.FixedY, query.Direction, [new NativeScriptGroundProbe(query.FixedX, query.FixedY, 0), new NativeScriptGroundProbe(query.FixedX, query.FixedY, 0), new NativeScriptGroundProbe(query.FixedX, query.FixedY, 0)])]);
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        foreach (int raw in new[] { 0, 1, 2, 255, 256, -1 })
        {
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.Unknown4, 0), (Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_B, 4000), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.POPM_B, 4001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine live = Create(content);
            List<int[]> consumed = [];
            live.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.Unknown4))
                {
                    consumed.Add(command.Arguments.ToArray());
                }
            };
            live.Step();
            live.Step();
            ActorState body = live.Actors[0];
            Require(body.NativeMotionAuthority == new NativeMotionAuthority(1, 0, unchecked((byte)raw)) && body.Direction == 0 && body.NativeTurnMode580 == 0 && body.NativeTurnAccumulator474 == 0 && body.NativeMovementCompletion542 == 0 && body.RotationTarget is null && body.NativeScriptMovement is null && live.EventMemory.Read(4000, 1).RequireExact() == 9 && consumed.Count == 1 && consumed[0].SequenceEqual(new[] { raw }), "Opcode361 lost DWORD evidence/lowBYTE576, changed unrelated body fields or delayed its successor.");
            EventEngine independent = Create(content);
            independent.Restore(live.Capture());
            for (int callback = 0; callback < 2; callback++)
            {
                live.Step();
                independent.Step();
                Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()), "Facing-hold byte failed independent restore and next Wait continuation.");
            }
            Require(live.EventMemory.Read(4001, 1).RequireExact() == 77 && consumed.Count == 1, "Opcode361 consumed another stack word or ran again after restore.");
        }
        foreach (bool missingOperand in new[] { false, true })
        {
            (Jsm.Opcode, int)[] script = missingOperand ? [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.Unknown4, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)] : [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.Unknown4, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 8)], script]);
            EventEngine refused = Create(content);
            refused.Step();
            if (!missingOperand)
            {
                typeof(ActorState).GetProperty(nameof(ActorState.NativeMotionAuthority))!.SetValue(refused.Actors[0], null);
            }
            try
            {
                refused.Step();
                throw new InvalidOperationException("Opcode361 admitted missing physical ownership or operand.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.Unknown4))
            {
            }
            JsonElement fiber = refused.Capture().GetProperty("Fibers").EnumerateArray().Single(item => item.GetProperty("ScriptId").GetInt32() == 1);
            Require(fiber.GetProperty("Position").GetInt32() == JsmProgram.Read(content).Operations.Single(operation => operation.Opcode == Jsm.Opcode.Unknown4).Index && fiber.GetProperty("Stack").GetArrayLength() == (missingOperand ? 0 : 1) && (missingOperand ? refused.Actors[0].NativeMotionAuthority!.FacingHold == 0 : refused.Actors[0].NativeMotionAuthority is null && fiber.GetProperty("Stack")[0].GetInt32() == 255), "Refused facing hold changed current body ownership, issuing PC or stack.");
        }
        foreach (int raw in new[] { 2, 255 })
        {
            byte[] content = SyntheticObjects([1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 5000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.PSHN_L, raw), (Jsm.Opcode.Unknown4, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine live = Create(content);
            live.Step();
            live.Step();
            Require(live.Actors[0].NativeScriptMovement is { Active: true, Asynchronous: true } && live.Actors[0].Direction == 64, "Facing-hold overlap fixture did not execute its original asynchronous MOVE first.");
            live.Step();
            ActorState body = live.Actors[0];
            Require(body.NativeMotionAuthority == new NativeMotionAuthority(1, 128, (byte)raw) && body.NativeScriptMovement is { Active: true, Asynchronous: true, MovementDirection: 128, FacingDirection: 64 } movement && movement.FacingHold576 == raw && body.Direction == 64 && body.NativeMovementCompletion542 == 1, "Opcode361 did not update the same active body576 owner or nonzero hold failed to preserve577 while575 changed.");
            EventEngine independent = Create(content);
            independent.Restore(live.Capture());
            live.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(live.Capture(), independent.Capture()) && body.Direction == 64 && body.NativeMotionAuthority!.FacingHold == raw, "Raw nonboolean facing hold failed active MOVE restoration and next callback.");
        }
        Console.WriteLine("Native opcode361: one DWORD to caller lowBYTE576, 0/1/2/255/256/-1, immediate successor, unchanged pose/turn fields, active MOVE shared owner/nonzero hold, independent next-Wait restore and atomic missing-owner/operand refusals pass.");
    }
}
