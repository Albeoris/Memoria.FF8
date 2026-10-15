using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeSplitSavedBindingsContract()
    {
        using GameAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        NativeCharacterVitals initialVitals = assets.ReadNewGameCharacterVitals();
        byte[] Content(bool nonzeroLimit)
        {
            (Jsm.Opcode, int)[] Actor(int label, int character) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, character), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
            List<(Jsm.Opcode, int)> leader = Actor(8, 0).ToList();
            leader.InsertRange(2, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SUBPARTY, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0)]);
            List<(Jsm.Opcode, int)> follower = Actor(16, 1).ToList();
            if (nonzeroLimit)
            {
                follower.InsertRange(2, [(Jsm.Opcode.PSHN_L, 39), (Jsm.Opcode.DIR, 0), (Jsm.Opcode.PSHN_L, 6), (Jsm.Opcode.MLIMIT, 0)]);
            }
            return SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 1, (24 << 7) | 1, (32 << 7) | 1], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], leader.ToArray(), [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.UCOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, -400), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SPLIT, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], follower.ToArray(), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], Actor(24, 5), [(Jsm.Opcode.LBL, 25), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 32), (Jsm.Opcode.SETMODEL, 6), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 33), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 300), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.RMOVE, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        }
        EventEngine Create(bool nonzeroLimit = false)
        {
            GameSession session = new(new GameState { CharacterVitals = initialVitals.DeepCopy(), WorldControls = NativeWorldControlState.CreateNewGame() });
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeJoinMovement = _ => true,
                CanUseNativeScriptMove = actor => actor.ObjectId == 4,
                ResolveNativeScriptDirection = query =>
                {
                    Require((query.ObjectId == 4 ? query.StopDistance == 0 : query.StopDistance == 8) && query.DirectionLimit == (nonzeroLimit && query.ObjectId == 2 ? 6 : 0), "SPLIT bypassed its shared native stop8/direction contract.");
                    FieldNativeScriptDirectionResult result = FieldNativeScriptDirectionProbe.Resolve(new(query.FixedX, query.FixedY, query.TargetX, query.TargetY, query.Speed, query.StopDistance, query.Radius, query.MovementDirection, query.FacingDirection, query.TurnAccumulator, query.DirectionLimit));
                    return result.Status == FieldNativeScriptDirectionStatus.Unsupported ? null : new NativeScriptDirectionResult((NativeScriptDirectionStatus)result.Status, result.FixedX, result.FixedY, result.Direction, result.TurnAccumulator);
                },
                ResolveNativeJoinMovement = query =>
                {
                    int x = query.FixedX + 123;
                    int y = query.FixedY + (query.Direction == 128 ? 20 : -20) * 4096;
                    NativePlayerGroundProbe fan = new(x, y, query.FixedZ >> 12);
                    return new NativeJoinMovementResult(true, x, y, query.FixedZ, query.Triangle, query.Direction, [new(x, y, query.Direction)], [[fan, fan, fan]]);
                }
            };
            engine.ResolveNativeScriptMovement = query =>
            {
                int x = query.FixedX + 4096;
                NativeScriptGroundProbe fan = new(x, query.FixedY, query.FixedZ >> 12);
                return new NativeScriptMovementResult(NativeScriptMovementStatus.Accepted, x, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(x, query.FixedY, query.Direction, [fan, fan, fan])]);
            };
            engine.LoadField(600, Content(nonzeroLimit), [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        List<int> order = [];
        int laterMoverCallbacks = 0;
        Func<NativeScriptMovementQuery, NativeScriptMovementResult?> scriptGeometry = live.ResolveNativeScriptMovement!;
        live.ResolveNativeScriptMovement = query =>
        {
            if (live.Actors[4].NativeScriptMovement is not null)
            {
                laterMoverCallbacks++;
                Require(live.Actors[1].Y == 20 && live.Actors[2].Y == -20 && live.Actors[3].Y == 20, "Later native scripted MOVE observed uncommitted SPLIT bodies.");
            }
            return scriptGeometry(query);
        };
        Func<NativeJoinMovementQuery, NativeJoinMovementResult?> geometry = live.ResolveNativeJoinMovement!;
        live.ResolveNativeJoinMovement = query =>
        {
            order.Add(query.ObjectId);
            if (query.ObjectId == 2)
            {
                Require(live.Actors[1].Y == 20 && live.Scene.NativePlayerPosition == new FieldFixedPosition(123, 20 * 4096, 0), "SPLIT's later body did not observe committed leader/raw-player coordinates.");
            }
            if (query.ObjectId == 3)
            {
                Require(live.Actors[2].Y == -20 && live.Actors[2].X == 123 / 4096f, "SPLIT retained an aggregate rather than native body-order commit.");
            }
            return geometry(query);
        };
        while (live.Capture().GetProperty("PartySplit").ValueKind == JsonValueKind.Null && live.Frame < 20)
        {
            live.Step();
        }
        JsonElement pending = live.Capture();
        Require(order.SequenceEqual(new[] { 1, 2, 3 }) && laterMoverCallbacks == 1 && pending.GetProperty("PartySplit").GetProperty("Targets").EnumerateArray().Select(target => target.GetProperty("ObjectId").GetInt32()).SequenceEqual(new[] { 1, 3, 2 }) && live.Scene.PartyBindingMemory is { First: 1, Second: 3, Third: 2 } && live.Scene.FieldPartyNativeActorIds.Skip(1).All(binding => binding == 255), $"SPLIT replaced saved B194 bindings or traversal: order[{string.Join(",", order)}], later{laterMoverCallbacks}, frame{live.Frame}, actor4={live.Actors[4].NativeScriptMovement}.");
        live.ResolveNativeJoinMovement = geometry;
        live.ResolveNativeScriptMovement = scriptGeometry;
        EventEngine resumed = Create();
        resumed.Restore(pending);
        JsonNode malformed = JsonNode.Parse(pending.GetRawText())!;
        malformed["PartySplit"]!["Targets"]!.AsArray().RemoveAt(1);
        try
        {
            resumed.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("SPLIT accepted a missing saved party body.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(pending, resumed.Capture()), "SPLIT binding refusal mutated current VM/service state.");
        for (int tick = 0; tick < 30; tick++)
        {
            live.Step();
            resumed.Step();
            Require(JsonElement.DeepEquals(live.Capture(), resumed.Capture()), "Saved-body SPLIT diverged after independent restore.");
        }
        Require(live.Capture().GetProperty("PartySplit").ValueKind == JsonValueKind.Null && new[] { 1, 2, 3 }.All(id => live.Actors[id].NativeSplitPosition is { } raw && raw.FixedX % 4096 != 0 && raw.Authority == live.Actors[id].NativeMotionAuthority), "SPLIT lost completed raw-body receipts or stop8 completion.");
        EventEngine completedCopy = Create();
        completedCopy.Restore(live.Capture());
        Require(JsonElement.DeepEquals(live.Capture(), completedCopy.Capture()), "Completed native SPLIT receipts failed independent restore.");
        // Isolated current physical-byte contract, not a research-save migration.
        JsonNode heldState = JsonNode.Parse(pending.GetRawText())!;
        heldState["Actors"]![2]!["NativeMotionAuthority"]!["FacingHold"] = 1;
        heldState["Actors"]![2]!["Direction"] = 17;
        EventEngine blocked = Create();
        blocked.Restore(JsonSerializer.SerializeToElement(heldState));
        EventEngine blockedCopy = Create();
        blockedCopy.Restore(blocked.Capture());
        foreach (EventEngine engine in new[] { blocked, blockedCopy })
        {
            engine.ResolveNativeJoinMovement = query =>
            {
                NativePlayerGroundProbe fan = new(query.FixedX, query.FixedY, query.FixedZ >> 12);
                return new NativeJoinMovementResult(false, query.FixedX, query.FixedY, query.FixedZ, query.Triangle, query.Direction, [new(query.FixedX, query.FixedY, query.Direction)], [[fan, fan, fan]]);
            };
        }
        blocked.Step();
        blockedCopy.Step();
        Require(JsonElement.DeepEquals(blocked.Capture(), blockedCopy.Capture()) && blocked.Actors[2].Direction == 17 && blocked.Actors[2].NativeMotionAuthority is { FacingHold: 1 } authority && authority.MovementDirection != 17 && blocked.Actors[2].Y == -20 && blocked.Capture().GetProperty("PartySplit").GetProperty("Targets").EnumerateArray().All(target => blocked.Capture().GetProperty("Actors")[target.GetProperty("ObjectId").GetInt32()].GetProperty("NativeMovementCompletion542").GetInt32() == 1), "Blocked SPLIT lost completion1, held577/independent575 or restore consistency.");
        EventEngine partial = Create();
        partial.Restore(pending);
        partial.ResolveNativeJoinMovement = query => query.ObjectId == 2 ? null : geometry(query);
        try
        {
            partial.Step();
            throw new InvalidOperationException("Unproved later SPLIT geometry was admitted.");
        }
        catch (EventScriptException) when (partial.LastFault?.Opcode == "SPLIT")
        {
        }
        Require(partial.Actors[1].Y == 40 && partial.Actors[2].Y == -20 && partial.Actors[3].Y == 20 && partial.Scene.NativePlayerPosition == new FieldFixedPosition(246, 40 * 4096, 0), "Later native SPLIT refusal rolled back an earlier committed body or advanced a later body.");
        EventEngine frozenCopy = Create();
        frozenCopy.Restore(partial.Capture());
        Require(JsonElement.DeepEquals(partial.Capture(), frozenCopy.Capture()), "Native partial SPLIT boundary failed exact current restore.");
        EventEngine limited = Create(true);
        while (limited.Capture().GetProperty("PartySplit").ValueKind == JsonValueKind.Null && limited.Frame < 20)
        {
            limited.Step();
        }
        Require(limited.Actors[2].MovementDirectionLimit == 6 && Math.Abs(limited.Actors[2].NativeTurnAccumulator474!.Value) == 6, "SPLIT ignored its current limited-direction WORD474 turn.");
        EventEngine limitedCopy = Create(true);
        limitedCopy.Restore(limited.Capture());
        limited.Step();
        limitedCopy.Step();
        Require(JsonElement.DeepEquals(limited.Capture(), limitedCopy.Capture()), "Nonzero-MLIMIT SPLIT lost the shared native turn after restore.");
        foreach (bool nonzero in new[] { false, true })
        {
            EventEngine refused = Create(nonzero);
            if (!nonzero)
            {
                refused.ResolveNativeScriptDirection = null;
            }
            else
            {
                refused.CanUseNativeJoinMovement = _ => false;
            }
            try
            {
                while (refused.Frame < 20)
                {
                    refused.Step();
                }
                throw new InvalidOperationException("SPLIT accepted missing direction or unproved shared body adapter.");
            }
            catch (EventScriptException) when (refused.LastFault?.Opcode == "SPLIT")
            {
            }
            Require(refused.Capture().GetProperty("PartySplit").ValueKind == JsonValueKind.Null && refused.Actors.All(actor => actor.X == 0 && actor.Y == 0) && refused.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 9).GetProperty("Stack").GetArrayLength() == 9, "SPLIT unsupported issue consumed operands, targets or coordinates.");
        }
        Console.WriteLine("SPLIT: saved B194/FOLLOWOFF, reversed native traversal/later MOVE, per-body partial commit, shared stop8, blocked-held575/577, completed receipt, independent restore and atomic binding/preissue refusal passed.");
    }
}
