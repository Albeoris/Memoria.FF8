using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMoveFlushContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        foreach (bool withBackup in new[] { false, true })
        {
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1)];
            if (withBackup)
            {
                main.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0)]);
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticObjects([4, (16 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)], main.ToArray(), [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.MOVEFLUSH, 0), (Jsm.Opcode.POPM_B, 720), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 4), (Jsm.Opcode.PSHN_L, 55), (Jsm.Opcode.POPM_B, 721), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
            EventEngine Create()
            {
                EventEngine engine = new(CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy()))
                {
                    AnimationFrameCount = (_, _) => 4,
                    CanUseNativeScriptMove = _ => true,
                    ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator),
                    ResolveNativeScriptMovement = query =>
                    {
                        int x = query.FixedX + 4096;
                        NativeScriptGroundProbe[] fan = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                        return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, fan)]);
                    }
                };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            static JsonElement Held(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 3);
            EventEngine live = Create();
            NativeMovementRequestState? beforeBackup = null;
            NativeScriptMovementState? beforeBody = null;
            int executions = 0;
            live.CommandExecuted += command =>
            {
                if (command.Name == "MOVEFLUSH")
                {
                    executions++;
                }
            };
            while (live.Frame < 14)
            {
                if (live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("ScriptId").GetInt32() == 3 && !fiber.GetProperty("Started").GetBoolean()))
                {
                    beforeBackup = live.Actors[0].NativeMovementRequest;
                    beforeBody = live.Actors[0].NativeScriptMovement;
                }
                live.Step();
                if (executions > 0)
                {
                    break;
                }
            }
            JsonElement held = Held(live);
            Require(executions == 1 && held.GetProperty("WaitReason").GetString() == "NativeMovementFlush" && held.GetProperty("Stack").GetArrayLength() == 1 && held.GetProperty("Stack")[0].GetInt32() == 77 && !CapturedVariables(live.Capture()).TryGetProperty("720", out _) && live.Actors[0].NativeScriptMovement == beforeBody, "MOVEFLUSH consumed an operand, advanced its held PC, completed, or changed the physical movement body.");
            if (withBackup)
            {
                Require(beforeBackup is { RestoreSuppressedByFlush: false } && live.Actors[0].NativeMovementRequest == beforeBackup with { RestoreSuppressedByFlush = true }, "MOVEFLUSH did not clear exactly the restore bit while preserving the single saved scalar backup and owner.");
            }
            else
            {
                Require(live.Actors[0].NativeMovementRequest is null, "Idempotent MOVEFLUSH created a movement backup.");
            }
            JsonElement pending = live.Capture();
            EventEngine resumed = Create();
            resumed.Restore(pending);
            NativeMovementRequestState? flushed = live.Actors[0].NativeMovementRequest;
            for (int tick = 0; tick < 14; tick++)
            {
                live.Step();
                resumed.Step();
                Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()), "Held MOVEFLUSH or higher request diverged after independent restore.");
            }
            Require(executions == 1 && Held(live).GetProperty("Position").GetInt32() == held.GetProperty("Position").GetInt32() && Held(live).GetProperty("WaitReason").GetString() == "NativeMovementFlush" && !CapturedVariables(live.Capture()).TryGetProperty("720", out _) && CapturedVariables(live.Capture()).GetProperty("721").GetInt32() == 55 && live.Actors[0].NativeMovementRequest == flushed && live.Actors[0].NativeScriptMovement == beforeBody, "MOVEFLUSH stopped higher request execution, reissued its first-entry write, transferred/re-enabled its cleared backup or invented completion.");
            JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
            JsonNode changed = malformed["Fibers"]!.AsArray().Single(fiber => fiber!["ObjectId"]!.GetValue<int>() == 0 && fiber["ScriptId"]!.GetValue<int>() == 3)!;
            changed["Position"] = held.GetProperty("Position").GetInt32() + 1;
            string before = resumed.Capture().GetRawText();
            try
            {
                resumed.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("MOVEFLUSH restored a held wait at its successor.");
            }
            catch (InvalidDataException)
            {
            }
            Require(before == resumed.Capture().GetRawText(), "Malformed MOVEFLUSH restore mutated the current VM.");
        }
        Console.WriteLine("MOVEFLUSH: zero operands, native held PC/return1, exact restore-bit clearing with retained physical scalars, higher-slot preemption and independent restore passed; no movement geometry parity is claimed.");
    }
}
