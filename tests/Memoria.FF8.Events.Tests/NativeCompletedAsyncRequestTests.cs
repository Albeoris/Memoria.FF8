using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeCompletedAsyncRequestContract(GameAssets assets)
    {
        foreach (Jsm.Opcode request in new[] { Jsm.Opcode.REQ, Jsm.Opcode.REQSW, Jsm.Opcode.REQEW, Jsm.Opcode.PREQ, Jsm.Opcode.PREQSW, Jsm.Opcode.PREQEW })
        {
            List<(Jsm.Opcode, int)> controller = [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (request, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
            byte[] content = SyntheticObjects([3, (16 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.SETPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 2), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RCMOVE, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 3), (Jsm.Opcode.PSHN_L, 2000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, 500), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], controller.ToArray()]);
            EventEngine Create()
            {
                GameSession session = CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
                EventEngine engine = new(session)
                {
                    AnimationFrameCount = (_, _) => 4,
                    CanUseNativeScriptMove = _ => true,
                    ResolveNativeScriptDirection = query => query.TargetX == 500 * 4096 ? new(NativeScriptDirectionStatus.Snapped, query.TargetX, query.TargetY, query.MovementDirection, query.TurnAccumulator) : new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator),
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
            EventEngine live = Create();
            while (live.Actors[0].NativeScriptMovement is not { ScriptId: 2 } && live.Frame < 8)
            {
                live.Step();
            }
            live.Step();
            NativeScriptMovementState physical = live.Actors[0].NativeScriptMovement!;
            Require(physical is { Active: true, Asynchronous: true, HoldsFacing: true } && !live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("Sequence").GetInt64() == physical.FiberSequence), "Completed async fixture did not retain actual RCMOVE after reaping its issuing VM fiber.");
            EventEngine restored = Create();
            restored.Restore(live.Capture());
            while (live.Actors[0].NativeMovementRequest is null && live.Frame < 12)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Physical-only movement diverged between original-owner completion and request issue after independent restore.");
            }
            NativeMovementRequestState backup = live.Actors[0].NativeMovementRequest ?? throw new InvalidOperationException("Request omitted the active physical-only RCMOVE backup.");
            Require(backup.SavedMovement is { Active: true, Asynchronous: true, Suspended: false, ScriptId: 2, Speed: 1000, TargetX: 40960000 } && backup.SavedMovement.FiberSequence == physical.FiberSequence, "Physical-only request changed saved target/speed/provenance or invented another movement owner.");
            JsonElement pending = live.Capture();
            Require(!pending.GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("Sequence").GetInt64() == physical.FiberSequence), "Physical-only request fabricated a replacement VM fiber.");
            EventEngine pendingCopy = Create();
            pendingCopy.Restore(pending);
            foreach (string property in new[] { "Asynchronous", "ScriptId", "Operation", "LowerSlot" })
            {
                JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
                JsonNode movement = malformed["Actors"]![0]!["NativeMovementRequest"]!["SavedMovement"]!;
                if (property == "LowerSlot")
                {
                    JsonArray slots = malformed["Fibers"]!.AsArray();
                    slots.Remove(slots.Single(fiber => fiber!["ObjectId"]!.GetValue<int>() == 0 && fiber["ScriptId"]!.GetValue<int>() == 1));
                }
                else
                {
                    movement[property] = property == "Asynchronous" ? JsonValue.Create(false) : JsonValue.Create(property == "ScriptId" ? 17 : 0);
                }
                try
                {
                    pendingCopy.Restore(JsonSerializer.SerializeToElement(malformed));
                    throw new InvalidOperationException("Malformed physical-only movement backup was accepted.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(pending, pendingCopy.Capture()), "Rejected physical-only backup restore mutated the VM or current body.");
            }
            while (live.Actors[0].NativeMovementRequest is not null && live.Frame < 20)
            {
                live.Step();
                restored.Step();
                pendingCopy.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && JsonElement.DeepEquals(live.Capture(), pendingCopy.Capture()), "Pending physical-only backup or RET8 restoration diverged after independent restore.");
            }
            ActorState actor = live.Actors[0];
            Require(actor.NativeMovementRequest is null && actor.NativeScriptMovement is { Active: true, Asynchronous: true, Suspended: false, ScriptId: 2, Speed: 1000, AnimationFlag40000: false } && actor.NativeScriptMovement.FiberSequence == physical.FiberSequence && actor.NativeMovementBaseSpeed == 2000 && actor.X >= 500, "RET8 did not consume the physical-only backup once and resume its old target/speed from the current higher-move position.");
            float before = actor.X;
            live.Step();
            restored.Step();
            pendingCopy.Step();
            Require(actor.X > before && actor.NativeMovementRequest is null && !live.Capture().GetProperty("Fibers").EnumerateArray().Any(fiber => fiber.GetProperty("Sequence").GetInt64() == physical.FiberSequence) && JsonElement.DeepEquals(live.Capture(), pendingCopy.Capture()) && CapturedVariables(live.Capture()).GetProperty("5000").GetInt32() == 73, "Restored physical-only movement stalled, recreated its old VM slot or held the completed caller request.");
        }
        Console.WriteLine("Completed async mode1 requests: six REQ/PREQ forms, reaped RCMOVE owner, single physical backup, moved-current-pose RET8, saved/current speed distinction, pending restore and malformed source rejection passed.");
    }
}
