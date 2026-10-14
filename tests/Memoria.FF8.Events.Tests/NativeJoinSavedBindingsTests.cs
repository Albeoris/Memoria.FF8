using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJoinSavedBindingsContract()
    {
        (Jsm.Opcode, int)[] Actor(int label, int character, int y) => [(Jsm.Opcode.LBL, label), (Jsm.Opcode.SETMODEL, character), (Jsm.Opcode.PSHN_L, character), (Jsm.Opcode.SETPC, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, y), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.THROUGHON, 0), (Jsm.Opcode.PUSHOFF, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PUSHRADIUS, 0), (Jsm.Opcode.RET, 8)];
        List<(Jsm.Opcode, int)> leader = Actor(8, 0, 0).ToList();
        leader.InsertRange(2, [(Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.ADDPARTY, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.ADDPARTY, 0)]);
        byte[] content = SyntheticObjects([1, (8 << 7) | 1, (16 << 7) | 4, (24 << 7) | 4], [[(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.REFRESHPARTY, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], leader.ToArray(), [(Jsm.Opcode.LBL, 9), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.REQ, 2), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 28), (Jsm.Opcode.REQ, 3), (Jsm.Opcode.JOIN, 0), (Jsm.Opcode.RET, 8)], Actor(16, 1, 300), [(Jsm.Opcode.LBL, 17), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 18), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 19), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 20), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.FOLLOWON, 0), (Jsm.Opcode.RET, 8)], Actor(24, 5, -300), [(Jsm.Opcode.LBL, 25), (Jsm.Opcode.FOLLOWOFF, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 26), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 27), (Jsm.Opcode.RET, 8)], [(Jsm.Opcode.LBL, 28), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.FOLLOWON, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create()
        {
            var session = new GameSession(new GameState());
            var engine = new EventEngine(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolvePartyTraceTriangle = (_, _, _) => 0,
                CanUseNativeJoinMovement = actor => actor.ObjectId is 2 or 3,
                ResolveNativeJoinMovement = query =>
                {
                    int x = query.FixedX + 123;
                    int y = query.FixedY + (query.FixedY > 0 ? -20 : 20) * 4096;
                    return new NativeJoinMovementResult(true, x, y, query.FixedZ, query.Triangle, query.Direction, [new NativeJoinMovementProposal(x, y, query.Direction)], [[new NativePlayerGroundProbe(x, y, query.FixedZ >> 12), new NativePlayerGroundProbe(x, y, query.FixedZ >> 12), new NativePlayerGroundProbe(x, y, query.FixedZ >> 12)]]);
                }
            };
            engine.LoadField(600, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        while (live.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && live.Frame < 20)
        {
            live.Step();
        }
        JsonElement pending = live.Capture();
        Require(pending.GetProperty("PartyJoin").GetProperty("Targets").EnumerateArray().All(target => target.GetProperty("Completion").GetInt32() == 1), "JOIN failed to advance all ordinary mode1 targets in its issuing frame's post-VM update.");
        Require(live.Scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 1, 255, 255 }) && live.Scene.PartyBindingMemory is { First: 1, Second: 2, Third: 3 } && pending.GetProperty("PartyJoin").GetProperty("Targets").EnumerateArray().Select(target => target.GetProperty("ObjectId").GetInt32()).SequenceEqual(new[] { 2, 3 }), $"JOIN substituted detached runtime slots for original saved B194 body pointers: frame{live.Frame}, runtime[{string.Join(",", live.Scene.FieldPartyNativeActorIds)}], saved={live.Scene.PartyBindingMemory}, targets={pending.GetProperty("PartyJoin")}.");
        EventEngine restored = Create();
        restored.Restore(pending);
        JsonNode invalid = JsonNode.Parse(pending.GetRawText())!;
        invalid["PartyJoin"]!["Targets"] = new JsonArray();
        try
        {
            restored.Restore(JsonSerializer.SerializeToElement(invalid));
            throw new InvalidOperationException("Missing original saved JOIN targets were accepted.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(pending, restored.Capture()), "Invalid saved JOIN binding restore changed pending follower requests.");
        bool completedReceipt = false;
        live.CommandExecuted += command =>
        {
            if (command.ObjectId == 1 && command.Name == "JOIN" && live.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null)
            {
                completedReceipt = true;
                ActorState actor = live.Actors[2];
                Require(actor.NativeJoinedPosition is { } raw && raw.FixedX % 4096 != 0 && actor.NativeMotionAuthority == raw.Authority, "Completed native JOIN lost its actual fractional body or movement575/576 authority.");
                JsonElement completed = live.Capture();
                EventEngine copy = Create();
                copy.Restore(completed);
                JsonNode damaged = JsonNode.Parse(completed.GetRawText())!;
                damaged["Actors"]![2]!["NativeJoinedPosition"]!["FixedX"] = actor.NativeJoinedPosition!.FixedX + 1;
                try
                {
                    copy.Restore(JsonSerializer.SerializeToElement(damaged));
                    throw new InvalidOperationException("Corrupt completed JOIN raw body was accepted.");
                }
                catch (InvalidDataException)
                {
                }
                Require(JsonElement.DeepEquals(completed, copy.Capture()), "Corrupt joined raw-body restore changed current positions or trace.");
            }
        };
        for (int tick = 0; tick < 15; tick++)
        {
            live.Step();
            restored.Step();
            Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()), "Detached saved-pointer JOIN or queued FOLLOWON diverged after independent restore.");
        }
        Require(completedReceipt && live.Scene.FieldPartyNativeActorIds.SequenceEqual(new[] { 1, 2, 3 }) && live.Capture().GetProperty("PartyJoin").ValueKind == JsonValueKind.Null && live.Actors[2].NativeJoinedPosition is null && live.Actors[3].NativeJoinedPosition is null, "JOIN did not release original pending FOLLOWON requests or clear completed raw receipts when the proved trace producer replaced placement.");
        Console.WriteLine("JOIN saved B194 bodies despite detached runtimeFF: actual targets, blocked/queued FOLLOWON, native raw/575/576 receipt, corrupt target/position atomic refusal, completion trace and independent VM continuation passed.");
    }
}
