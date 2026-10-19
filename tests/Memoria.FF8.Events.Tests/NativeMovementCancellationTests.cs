using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeMovementCancellationContract(bool party = false)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        foreach ((bool asynchronous, bool scalar, bool requested) in new[] { (false, false, false), (false, true, false), (true, true, false), (false, false, true), (true, true, true) })
        {
            (Jsm.Opcode, int)[] Movement(int label)
            {
                List<(Jsm.Opcode, int)> operations = [(Jsm.Opcode.LBL, label), (Jsm.Opcode.PSHN_L, 10000)];
                if (scalar)
                {
                    operations.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0)]);
                }
                else
                {
                    operations[1] = (Jsm.Opcode.PSHAC, 0);
                }
                operations.AddRange([(Jsm.Opcode.PSHN_L, 48), (asynchronous ? Jsm.Opcode.RFMOVE : scalar ? Jsm.Opcode.MOVE : Jsm.Opcode.MOVEA, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
                return operations.ToArray();
            }
            List<(Jsm.Opcode, int)> controller = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0)];
            if (party)
            {
                controller.Insert(1, (Jsm.Opcode.REFRESHPARTY, 0));
            }
            if (requested)
            {
                controller.AddRange([(Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.WAIT, 0)]);
            }
            controller.AddRange([(party ? Jsm.Opcode.PSHN_L : Jsm.Opcode.PSHAC, party ? 0 : 1), (party ? Jsm.Opcode.PMOVECANCEL : Jsm.Opcode.MOVECANCEL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            byte[] content = SyntheticObjects([0, (16 << 7) | 4, (32 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.PSHN_L, 10000), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.SETMODEL, 1), .. (party ? new (Jsm.Opcode, int)[] { (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETPC, 0) } : []), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.BASEANIME, 0), (Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.RET, 8)], Movement(17), [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.RET, 8)], Movement(20), [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.RET, 8)], controller.ToArray()]);
            EventEngine Create()
            {
                var session = party ? CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy()) : new GameSession(new GameState());
                var engine = new EventEngine(session)
                {
                    AnimationFrameCount = (_, _) => 4,
                    CanUseNativeScriptMove = actor => actor.ObjectId == 1,
                    ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 64, query.TurnAccumulator),
                    ResolveNativeScriptMovement = query =>
                    {
                        int x = query.FixedX + 4096;
                        NativeScriptGroundProbe[] probes = [new(x, query.FixedY, 0), new(x, query.FixedY, 0), new(x, query.FixedY, 0)];
                        return new(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, probes)]);
                    }
                };
                engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
                return engine;
            }
            EventEngine live = Create();
            NativeScriptMovementState? before = null;
            short? previousTurn = null;
            NativeMovementRequestState? backup = null;
            JsonElement? issued = null;
            while (live.Actors[1].NativeScriptMovement is not { Cancelled: true } && live.Frame < 14)
            {
                before = live.Actors[1].NativeScriptMovement;
                previousTurn = live.Actors[1].NativeTurnAccumulator474;
                backup = live.Actors[1].NativeMovementRequest;
                if (before is { Active: true } && !asynchronous)
                {
                    issued = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == before.FiberSequence);
                }
                live.Step();
            }
            ActorState body = live.Actors[1];
            NativeScriptMovementState cancelled = body.NativeScriptMovement!;
            Require(body.NativeMovementCompletion542 == 1 && before is { Active: true } && body.NativeMovementCompletion542 == 1 && cancelled is { Active: false, Cancelled: true } && cancelled.FixedX == before.FixedX && cancelled.FixedY == before.FixedY && cancelled.FixedZ == before.FixedZ && body.NativeTurnAccumulator474 == previousTurn && body.NativeMotionAuthority!.MovementDirection == before.MovementDirection && body.NativeMotionAuthority.FacingHold == 0 && body.MovementTarget is null && body.AnimationId == body.BaseAnimationId && body.AnimationFirst == 0 && body.AnimationLast == 3 && body.AnimationLoop && body.NativeMovementRequest == backup, "MOVECANCEL changed completion/physical motion scalars/backup or failed mode0/full idle selection.");
            if (!asynchronous)
            {
                JsonElement held = live.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == cancelled.FiberSequence);
                Require(issued is not null && held.GetProperty("Position").GetInt32() == issued.Value.GetProperty("Position").GetInt32() && JsonElement.DeepEquals(held.GetProperty("Stack"), issued.Value.GetProperty("Stack")) && held.GetProperty("WaitReason").GetString() == "NativeScriptMovement", "MOVECANCEL completed the synchronous consumer or consumed its remaining target operand.");
            }
            JsonElement stopped = live.Capture();
            EventEngine restored = Create();
            restored.Restore(stopped);
            foreach (string property in new[] { "Cancelled", "AnimationFlag40000" })
            {
                JsonNode invalid = JsonNode.Parse(stopped.GetRawText())!;
                invalid["Actors"]![1]!["NativeScriptMovement"]![property] = property == "Cancelled" ? JsonValue.Create(false) : null;
                try
                {
                    restored.Restore(JsonSerializer.SerializeToElement(invalid));
                    throw new InvalidOperationException("Malformed cancelled mode0 receipt was accepted.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(stopped, restored.Capture()), "Malformed cancellation restore changed the actual body or issued movement owner.");
            }
            for (int tick = 0; tick < 5; tick++)
            {
                live.Step();
                restored.Step();
                Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && body.X == cancelled.FixedX / 4096f && body.MovementTarget is null && body.NativeMovementCompletion542 == 1 && body.NativeScriptMovement is { Cancelled: true }, "Cancelled movement restarted, fabricated completion2 or diverged during independently restored polling.");
            }
        }
        Console.WriteLine("MOVECANCEL: sync MOVE/MOVEA and async RFMOVE mode1Ã¢â€ â€™0, physical completion/issued operands/flags/backup retained, full idle, stationary held consumer polling and malformed-state atomic refusal passed.");
    }
}
