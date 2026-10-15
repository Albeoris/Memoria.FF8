using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeAsyncFaceActorContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        foreach (Jsm.Opcode opcode in new[] { Jsm.Opcode.RFACEDIRA, Jsm.Opcode.FACEDIRA })
        {
            EventEngine live = CreateAsyncFaceActorFixture(vitals, economy, opcode, duration: 65540, moving: true);
            NativeScriptMovementState? capturedMotion = null;
            Vector3? capturedPoint = null;
            int issues = 0;
            live.CommandExecuted += command =>
            {
                if (command.Name == opcode.ToString())
                {
                    issues++;
                    capturedMotion = live.Actors[2].NativeScriptMovement;
                    capturedPoint = live.Actors[0].FaceTargetPoint;
                    Require(command.Arguments.SequenceEqual([1, 65540]) && live.Actors[0].FaceDuration == 4 && live.Actors[0].FaceProgress == 0, "Actor-facing did not consume target/full duration in original push order or publish the low unsigned duration WORD before post-VM stepping.");
                }
            };
            ReachAsyncFaceActorIssue(live);
            ActorState actor = live.Actors[0];
            Require(issues == 1 && capturedMotion is not null && capturedMotion.FixedX < 0 && (capturedMotion.FixedX & 4095) != 0 && capturedMotion.FixedY < 0 && (capturedMotion.FixedY & 4095) != 0, "Actor-facing fixture did not produce its target through actual negative fractional native movement.");
            NativeScriptMovementState sampledMotion = capturedMotion ?? throw new InvalidOperationException("Actor-facing issue did not retain its target movement sample.");
            Vector3 expected = new(sampledMotion.FixedX / 4096, sampledMotion.FixedY / 4096, -32756);
            Require(live.Scene.NativeActorObjectIds.SequenceEqual([0, 2]) && capturedPoint == expected && actor.FaceTargetPoint == expected && live.Actors[2].DisplayOffset == new Vector3(101, -202, 303), "Actor-facing used a party/managed object index, floored negative fixed coordinates, included target display offset or lost Z+target joint WORD wrapping.");
            bool asynchronous = opcode == Jsm.Opcode.RFACEDIRA;
            Require(actor.FaceProgress == 1 && actor.FirstControlRotation is not null && !actor.FaceReturning && HasAsyncFaceActorVariable(live, "5000") == asynchronous && !HasAsyncFaceActorVariable(live, "5001"), "RFACEDIRA waited for completion, FACEDIRA failed to wait, or the post-VM first face sample/SYNC boundary changed.");
            JsonElement issued = live.Capture();
            EventEngine restored = CreateAsyncFaceActorFixture(vitals, economy, opcode, duration: 65540, moving: true);
            restored.Restore(issued);
            while (actor.FaceProgress < actor.FaceDuration)
            {
                int previous = actor.FaceProgress;
                live.Step();
                restored.Step();
                Require(actor.FaceProgress == previous + 1 && actor.FaceTargetPoint == expected && !HasAsyncFaceActorVariable(live, "5001") && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Captured actor target changed during subsequent target movement, completed before the next VM observation or diverged after independent pending restore.");
            }
            Require(live.Actors[2].X != sampledMotion.FixedX / 4096f && issues == 1 && actor.FaceTargetPoint == expected, "Actor-facing tracked the target again instead of retaining its original issue point.");
            EventEngine completedCopy = CreateAsyncFaceActorFixture(vitals, economy, opcode, duration: 65540, moving: true);
            completedCopy.Restore(live.Capture());
            live.Step();
            restored.Step();
            completedCopy.Step();
            JsonElement complete = live.Capture();
            JsonElement fiber = AsyncFaceActorCaller(live);
            Require(CapturedVariables(complete).GetProperty("5000").GetInt32() == 77 && CapturedVariables(complete).GetProperty("5001").GetInt32() == 78 && actor.FaceProgress == 4 && actor.FaceTargetPoint == expected && actor.X == 0 && actor.Y == 0 && actor.Z == 0 && actor.Direction == 0 && actor.FirstControlRotation is not null && fiber.GetProperty("Stack").GetArrayLength() == 0 && fiber.GetProperty("Results").GetProperty("7").GetProperty("ActorInfoOrigin").GetProperty("NativeActorIndex").GetInt32() == 255 && JsonElement.DeepEquals(complete, restored.Capture()) && JsonElement.DeepEquals(complete, completedCopy.Capture()), "Actor-facing/SYNC completion changed body ownership, consumed the unrelated opaque scalar, delayed the common successor or diverged at an independently restored equality phase.");

            JsonNode malformed = JsonNode.Parse(issued.GetRawText())!;
            malformed["Actors"]![0]!["FaceProgress"] = 5;
            JsonElement before = live.Capture();
            try
            {
                live.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Actor-facing admitted progress beyond its captured native duration.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, live.Capture()), "Malformed actor-facing phase partially mutated its restore target.");
        }

        foreach (int duration in new[] { 65536, 65535, -1 })
        {
            EventEngine edge = CreateAsyncFaceActorFixture(vitals, economy, Jsm.Opcode.RFACEDIRA, duration: duration);
            ReachAsyncFaceActorIssue(edge);
            int word = unchecked((ushort)duration);
            Require(edge.Actors[0].FaceDuration == word && edge.Actors[0].FaceProgress == (word == 0 ? 0 : 1) && edge.Actors[0].FaceTargetPoint == new Vector3(-19, -22, -32756) && HasAsyncFaceActorVariable(edge, "5000") && HasAsyncFaceActorVariable(edge, "5001") == (word == 0) && (word != 0 || edge.Actors[0].FirstControlRotation is null), "RFACEDIRA zero/full unsigned duration WORD inserted a wait, skipped target capture or fabricated a zero-duration joint override.");
            EventEngine copy = CreateAsyncFaceActorFixture(vitals, economy, Jsm.Opcode.RFACEDIRA, duration: duration);
            copy.Restore(edge.Capture());
            edge.Step();
            copy.Step();
            Require(JsonElement.DeepEquals(edge.Capture(), copy.Capture()), "RFACEDIRA duration edge changed after independent current-phase restore.");
        }

        foreach (string variation in new[] { "target-unretained", "target-byte-wrap", "target-word-wrap", "target-negative", "missing-facing", "missing-height", "invalid-target-height", "empty-target", "unknown-target", "unknown-duration", "unknown-face-owner" })
        {
            int target = variation switch { "target-unretained" => 2, "target-byte-wrap" => 257, "target-word-wrap" => 65537, "target-negative" => -1, _ => 1 };
            EventEngine refused = CreateAsyncFaceActorFixture(vitals, economy, Jsm.Opcode.RFACEDIRA, target, 4, unknownTarget: variation == "unknown-target", unknownDuration: variation == "unknown-duration", emptyTarget: variation == "empty-target");
            if (variation == "missing-facing")
            {
                refused.ResolveNativeActorFacing = null;
            }
            if (variation == "missing-height")
            {
                refused.ReadFaceAnchorHeight = null;
            }
            if (variation == "invalid-target-height")
            {
                refused.ReadFaceAnchorHeight = body => body.ObjectId == 2 ? float.NaN : 5;
            }
            if (variation == "empty-target")
            {
                refused.ReadFaceAnchorHeight = body => body.ObjectId == 2 ? throw new InvalidOperationException("Empty target reached query30 before its binding refusal.") : 5;
            }
            JsonElement? beforeActor = null;
            JsonElement? beforeFiber = null;
            refused.CommandExecuted += command =>
            {
                if (command.Name == nameof(Jsm.Opcode.NOP) && command.ObjectId == 0)
                {
                    if (variation == "unknown-face-owner")
                    {
                        // Invalid live component injection tests preflight only; it is never restored or given a player journal.
                        typeof(ActorState).GetProperty(nameof(ActorState.FaceStateVersion))!.SetValue(refused.Actors[0], 0);
                    }
                    beforeActor = JsonSerializer.SerializeToElement(refused.Actors[0]);
                    beforeFiber = AsyncFaceActorCaller(refused);
                }
            };
            try
            {
                ReachAsyncFaceActorIssue(refused);
                throw new InvalidOperationException($"RFACEDIRA accepted unproved native ownership/provider/operand case {variation}.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == nameof(Jsm.Opcode.RFACEDIRA))
            {
            }
            JsonElement held = AsyncFaceActorCaller(refused);
            Require(beforeActor is not null && beforeFiber is not null && held.GetProperty("Position").GetInt32() == refused.LastFault!.Operation && held.GetProperty("Stack").GetArrayLength() == 3 && JsonElement.DeepEquals(beforeActor.Value, JsonSerializer.SerializeToElement(refused.Actors[0])) && JsonElement.DeepEquals(beforeFiber.Value, held) && refused.Actors[0].FaceTargetPoint is null && refused.Actors[0].FaceProgress == 0 && refused.Actors[0].FaceDuration == 0 && !HasAsyncFaceActorVariable(refused, "5000"), $"RFACEDIRA {variation} refusal changed its PC, typed operands, face flags or body before validation.");
        }
        Console.WriteLine("RFACEDIRA: full native target ownership, signed fractional target capture/query30 wrapping, async successor and shared blocking sibling/SYNC clocks, unsigned duration edges, independent restores and atomic unsupported input refusal pass.");
    }

    private static EventEngine CreateAsyncFaceActorFixture(NativeCharacterVitals vitals, NativeEconomy economy, Jsm.Opcode opcode, int target = 1, int duration = 65540, bool moving = false, bool unknownTarget = false, bool unknownDuration = false, bool emptyTarget = false)
    {
        (Jsm.Opcode, int)[] caller = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.FACEDIRINIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.FACEDIRLIMIT, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 255), (Jsm.Opcode.EFFECTLOAD, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 8), (Jsm.Opcode.PGETINFO, 0), (Jsm.Opcode.PSHI_L, 0)];
        main.Add(unknownTarget ? (Jsm.Opcode.PSHI_L, 0) : (Jsm.Opcode.PSHN_L, target));
        main.Add(unknownDuration ? (Jsm.Opcode.PSHI_L, 0) : (Jsm.Opcode.PSHN_L, duration));
        main.AddRange([(Jsm.Opcode.NOP, 0), (opcode, 0), (Jsm.Opcode.POPI_L, 7), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.FACEDIRSYNC, 0), (Jsm.Opcode.PSHN_L, 78), (Jsm.Opcode.POPM_B, 5001), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        (Jsm.Opcode, int)[] unused = [(Jsm.Opcode.LBL, 16), (Jsm.Opcode.UNUSE, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> targetInit = [(Jsm.Opcode.LBL, 32)];
        if (!emptyTarget)
        {
            targetInit.Add((Jsm.Opcode.SETMODEL, 1));
        }
        targetInit.AddRange([(Jsm.Opcode.PSHN_L, -19), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 32760), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.RET, 8)]);
        List<(Jsm.Opcode, int)> targetDefault = [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 101), (Jsm.Opcode.PSHN_L, -202), (Jsm.Opcode.PSHN_L, 303), (Jsm.Opcode.DOFFSET, 0)];
        if (moving)
        {
            targetDefault.AddRange([(Jsm.Opcode.PSHN_L, 1000), (Jsm.Opcode.MSPEED, 0), (Jsm.Opcode.PSHN_L, -1000), (Jsm.Opcode.PSHN_L, -22), (Jsm.Opcode.PSHN_L, 32760), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RFMOVE, 0)]);
        }
        targetDefault.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
        // The native pointer table keeps raw group order; managed objects are sorted by label.
        byte[] content = SyntheticObjects([1, (32 << 7) | 1, 16 << 7], [caller, main.ToArray(), unused, targetInit.ToArray(), targetDefault.ToArray()]);
        int operations = BinaryPrimitives.ReadUInt16LittleEndian(content.AsSpan(6));
        for (int offset = operations; offset < content.Length; offset += 4)
        {
            int encoded = BinaryPrimitives.ReadInt32LittleEndian(content.AsSpan(offset));
            if (encoded == (int)Jsm.Opcode.SETMODEL)
            {
                BinaryPrimitives.WriteInt32LittleEndian(content.AsSpan(offset), encoded << 24);
            }
        }
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine engine = new(session)
        {
            LoadSoundResource = _ => SoundResourceStatus.Muted,
            AnimationFrameCount = (_, _) => 4,
            ReadFaceBaseline = _ => new FaceDirectionBaseline(0, 0),
            ReadFaceAnchorHeight = body => body.ObjectId == 2 ? 65556 : 5,
            ResolveNativeActorFacing = query => FieldNativeScriptDirectionProbe.ResolveFacing(query.FixedX, query.FixedY, query.TargetX, query.TargetY),
            CanUseNativeScriptMove = body => body.ObjectId == 2,
            ResolveNativeScriptDirection = query => new(NativeScriptDirectionStatus.Moving, query.FixedX, query.FixedY, 192, 0),
            ResolveNativeScriptMovement = query =>
            {
                int x = query.FixedX - 1377;
                int y = query.FixedY - 701;
                NativeScriptGroundProbe[] fan = Enumerable.Range(0, 3).Select(_ =>
                {
                    NativeScriptGroundProbe probe = new(x, y, query.FixedZ >> 12);
                    return probe with { ActorCollision = query.HasActorCollision!(probe) };
                }).ToArray();
                return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, x, y, query.FixedZ, query.Triangle, query.Direction, [new NativeScriptMovementProposal(x, y, query.Direction, fan)]);
            }
        };
        engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void ReachAsyncFaceActorIssue(EventEngine engine)
    {
        while (engine.Actors[0].FaceTargetPoint is null && engine.Frame < 20)
        {
            engine.Step();
        }
        Require(engine.Actors[0].FaceTargetPoint is not null, "Native actor-facing fixture never reached its instruction.");
    }

    private static JsonElement AsyncFaceActorCaller(EventEngine engine)
    {
        return engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
    }

    private static bool HasAsyncFaceActorVariable(EventEngine engine, string address)
    {
        return CapturedVariables(engine.Capture()).TryGetProperty(address, out _);
    }
}
