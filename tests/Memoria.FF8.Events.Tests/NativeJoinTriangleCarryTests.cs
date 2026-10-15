using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJoinTriangleCarryContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeEconomy economy = assets.ReadNewGameEconomy();
        int? Hit(int query) => query switch { 0 => 7, 31 => 9, 40 => 11, _ => null };
        List<(int X, int Y, int Z)> queries = [];
        EventEngine live = CreateJoinTriangleCarryFixture(vitals, economy, (x, y, z) =>
        {
            int query = queries.Count;
            queries.Add((x, y, z));
            return Hit(query);
        });
        ReachJoinTriangleCarryCompletion(live);
        JsonElement pending = live.Capture();
        Require(queries.Count == 0 && live.Scene.PartyTrace!.FirstTrail.All(sample => sample is { Triangle: 7 }), "JOIN carry fixture did not reach a genuine pending completion before any lookup; its initial trace must not seed the native local.");
        List<(int X, int Y, int Z)> restoredQueries = [];
        EventEngine restored = CreateJoinTriangleCarryFixture(vitals, economy, (x, y, z) =>
        {
            int query = restoredQueries.Count;
            restoredQueries.Add((x, y, z));
            return Hit(query);
        });
        restored.Restore(pending);
        live.Step();
        restored.Step();
        Require(queries.Count == 64 && queries.Take(32).All(point => point == (0, 10, 0)) && queries.Skip(32).All(point => point == (0, -10, 0)) && queries.SequenceEqual(restoredQueries) && JsonElement.DeepEquals(live.Capture(), restored.Capture()), "JOIN did not perform both original32-query trails in order or recompute its synchronous carry independently after restore.");
        PartyTraceState trace = live.Scene.PartyTrace!;
        Require(trace.FirstTrail[63] is { Triangle: 7 } && trace.FirstTrail[33] is { Triangle: 7 } && trace.FirstTrail[32] is { Triangle: 9 } && trace.SecondTrail[63] is { Triangle: 9 } && trace.SecondTrail[56] is { Triangle: 9 } && trace.SecondTrail[55] is { Triangle: 11 } && trace.SecondTrail[32] is { Triangle: 11 } && trace.FirstTrail.Take(32).All(sample => sample is { Triangle: 7, FirstFlag: 1, SecondFlag: 1 }) && trace.SecondTrail.Take(32).All(sample => sample is { Triangle: 7, FirstFlag: 1, SecondFlag: 1 }) && live.Capture().GetProperty("Variables").GetProperty("6000").GetInt32() == 73 && live.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null, "JOIN misses did not retain the last real hit across rings, a later hit failed to overwrite it, untouched half-rings changed or the completion successor was delayed.");
        JsonElement completed = live.Capture();
        EventEngine completedCopy = CreateJoinTriangleCarryFixture(vitals, economy, (_, _, _) => throw new InvalidOperationException("Completed JOIN restore queried transient native triangle memory."));
        completedCopy.Restore(completed);
        Require(JsonElement.DeepEquals(completed, completedCopy.Capture()), "Completed trace restore required a persisted or guessed native stack-local carry.");

        int firstMissCalls = 0;
        EventEngine? firstMiss = null;
        JsonElement? firstLookup = null;
        firstMiss = CreateJoinTriangleCarryFixture(vitals, economy, (_, _, _) =>
        {
            firstMissCalls++;
            firstLookup = firstMiss!.Capture();
            Require(firstMiss.Actors[1].NativeAnimationHeader is { AnimationId: 2 } && firstMiss.Actors[2].NativeAnimationHeader is { AnimationId: 2 }, "JOIN published completed idle headers before validating its first trace lookup.");
            return null;
        });
        ReachJoinTriangleCarryCompletion(firstMiss);
        JsonElement beforeMiss = firstMiss.Capture();
        ExpectJoinTriangleCarryMiss(firstMiss);
        JsonElement refused = firstMiss.Capture();
        int pendingPosition = beforeMiss.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1).GetProperty("Position").GetInt32();
        JsonElement beforeCaller = beforeMiss.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
        JsonElement afterCaller = JoinTriangleCarryCaller(firstMiss);
        string changedFiberKeys = string.Join(",", beforeCaller.EnumerateObject().Where(property => !JsonElement.DeepEquals(property.Value, afterCaller.GetProperty(property.Name))).Select(property => property.Name));
        Require(firstMissCalls == 1 && firstLookup is not null && JsonElement.DeepEquals(firstLookup.Value.GetProperty("Scene"), refused.GetProperty("Scene")) && JsonElement.DeepEquals(firstLookup.Value.GetProperty("Actors"), refused.GetProperty("Actors")) && JsonElement.DeepEquals(beforeMiss.GetProperty("PartyJoin"), refused.GetProperty("PartyJoin")) && JsonElement.DeepEquals(beforeCaller, afterCaller) && firstMiss.Actors[1].X == 0 && firstMiss.Actors[1].Y == 10 && firstMiss.Actors[2].X == 0 && firstMiss.Actors[2].Y == -10 && afterCaller.GetProperty("Position").GetInt32() == pendingPosition && afterCaller.GetProperty("WaitReason").GetString() == "PartyJoin" && !refused.GetProperty("Variables").TryGetProperty("6000", out _), $"An unseeded first miss guessed a triangle or lost pending JOIN state: calls={firstMissCalls}, sceneEqual={firstLookup is not null && JsonElement.DeepEquals(firstLookup.Value.GetProperty("Scene"), refused.GetProperty("Scene"))}, joinEqual={JsonElement.DeepEquals(beforeMiss.GetProperty("PartyJoin"), refused.GetProperty("PartyJoin"))}, fiberChanges={changedFiberKeys}, PC={afterCaller.GetProperty("Position").GetInt32()}/{pendingPosition}, wait={afterCaller.GetProperty("WaitReason").GetString()}.");

        int repeatQueries = 0;
        EventEngine? repeated = null;
        JsonElement? secondLookup = null;
        repeated = CreateJoinTriangleCarryFixture(vitals, economy, (_, _, _) =>
        {
            repeatQueries++;
            if (repeatQueries <= 64)
            {
                return 23;
            }
            secondLookup = repeated!.Capture();
            return null;
        }, repeat: true);
        ReachJoinTriangleCarryCompletion(repeated);
        repeated.Step();
        Require(repeatQueries == 64 && repeated.Capture().GetProperty("Variables").GetProperty("6000").GetInt32() == 73, "Repeated-JOIN fixture did not finish its first genuinely seeded call.");
        repeated.Step();
        JsonElement secondPending = repeated.Capture();
        Require(secondPending.GetProperty("PartyJoin").ValueKind == JsonValueKind.Object, "Repeated-JOIN fixture did not issue its independent second call.");
        ExpectJoinTriangleCarryMiss(repeated);
        JsonElement secondRefused = repeated.Capture();
        Require(repeatQueries == 65 && secondLookup is not null && JsonElement.DeepEquals(secondLookup.Value.GetProperty("Scene"), secondRefused.GetProperty("Scene")) && JsonElement.DeepEquals(secondLookup.Value.GetProperty("Actors"), secondRefused.GetProperty("Actors")) && JsonElement.DeepEquals(secondPending.GetProperty("PartyJoin"), secondRefused.GetProperty("PartyJoin")) && JoinTriangleCarryCaller(repeated).GetProperty("WaitReason").GetString() == "PartyJoin" && !secondRefused.GetProperty("Variables").TryGetProperty("6001", out _), "JOIN leaked an earlier call's winner into a later completion or partially published the second call's trace.");
        Console.WriteLine("Native JOIN triangle lookup: real-hit replacement, miss carry across both32-sample rings, unseeded first-miss refusal, call-local lifetime and independent pending/completed restoration pass; synthetic component geometry, no original-runtime parity claim.");
    }

    private static EventEngine CreateJoinTriangleCarryFixture(NativeCharacterVitals vitals, NativeEconomy economy, Func<int, int, int, int?> resolveTriangle, bool repeat = false)
    {
        List<(Jsm.Opcode, int)[]> scripts = [];
        foreach (int slot in new[] { 0, 1, 2 })
        {
            List<(Jsm.Opcode, int)> init = [(Jsm.Opcode.LBL, slot * 8)];
            if (slot == 0)
            {
                init.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.SETPARTY, 0)]);
            }
            init.AddRange([(Jsm.Opcode.SETMODEL, slot), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.BASEANIME, 1), (Jsm.Opcode.PSHN_L, slot), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, slot == 1 ? 10 : slot == 2 ? -10 : 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 7), (Jsm.Opcode.RET, 8)]);
            scripts.Add(init.ToArray());
            List<(Jsm.Opcode, int)> main = [(Jsm.Opcode.LBL, slot * 8 + 1)];
            if (slot == 0)
            {
                main.AddRange([(Jsm.Opcode.INITTRACE, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.PSHN_L, 73), (Jsm.Opcode.POPM_B, 6000)]);
                if (repeat)
                {
                    main.AddRange([(Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.PSHN_L, 74), (Jsm.Opcode.POPM_B, 6001)]);
                }
            }
            else
            {
                // Field entry legitimately places followers; ordinary scripts establish the nearby component poses afterwards.
                main.AddRange([(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, slot == 1 ? 10 : -10), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 7)]);
            }
            main.AddRange([(Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]);
            scripts.Add(main.ToArray());
        }
        GameSession session = CreateDirectedLinearTurnSession(vitals, economy);
        EventEngine? engine = null;
        engine = new(session, session.State.Variables)
        {
            AnimationFrameCount = (_, _) => 4,
            ResolveNativeTriangleHeight = (_, _, _) => 0,
            ResolvePartyTraceTriangle = (x, y, z) => engine is not null && engine.Actors[1].MovementStopDistance == 96 && engine.Actors[2].MovementStopDistance == 96 ? resolveTriangle(x, y, z) : 7,
            CanUseNativeJoinMovement = actor => actor.ObjectId is 1 or 2,
            ResolveNativeJoinMovement = _ => throw new InvalidOperationException("Nearby JOIN component should finish through its native direction/stop-distance kernel before a movement proposal."),
            ResolveNativeScriptDirection = query =>
            {
                FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
            }
        };
        engine.LoadField(600, SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 1], scripts), [], new FieldTransition(600, 0, 0, 7, 0), nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        return engine;
    }

    private static void ReachJoinTriangleCarryCompletion(EventEngine engine)
    {
        for (int callback = 0; callback < 12; callback++)
        {
            engine.Step();
            if (engine.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Object && engine.Actors[1].NativeMovementCompletion542 == 2 && engine.Actors[2].NativeMovementCompletion542 == 2)
            {
                return;
            }
        }
        throw new InvalidOperationException("Native JOIN fixture did not reach its genuine physically completed pending trace callback.");
    }

    private static void ExpectJoinTriangleCarryMiss(EventEngine engine)
    {
        try
        {
            engine.Step();
            throw new InvalidOperationException("Native JOIN guessed an unseeded lookup winner.");
        }
        catch (EventScriptException) when (engine.LastFault?.Opcode == nameof(Jsm.Opcode.JOIN))
        {
        }
    }

    private static JsonElement JoinTriangleCarryCaller(EventEngine engine) => engine.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 1);
}
