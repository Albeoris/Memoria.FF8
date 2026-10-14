using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativePhysicalMotionWordContract()
    {
        (Jsm.Opcode, int)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode, int)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.MOVE, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.PSHN_L, 4), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.MOVESYNC, 0), (Jsm.Opcode.PSHN_L, 77), (Jsm.Opcode.POPM_B, 5000), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1], [init, main]);
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                CanUseNativeScriptMove = _ => true,
                ResolveNativeScriptDirection = query => new NativeScriptDirectionResult(NativeScriptDirectionStatus.Stopped, query.FixedX, query.FixedY, query.MovementDirection, query.TurnAccumulator),
                ResolveNativeScriptMovement = _ => null
            };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine live = Create();
        Require(live.Actors[0].NativeMovementCompletion542 == 0 && live.Actors[0].NativeLandingTriangle508 == 0, "Fresh native allocator did not explicitly zero physical WORD508/542.");
        live.Step();
        live.Step();
        Require(live.Actors[0].NativeMovementCompletion542 == 2 && live.Actors[0].NativeScriptMovement is { Active: true }, "Actual native stopped MOVE did not publish physical542=2 before its following VM consumer.");
        live.Step();
        Require(live.Actors[0].NativeScriptMovement is { Active: false } && live.Actors[0].NativeMovementCompletion542 == 2, "MOVE completion did not retain its physical word after mode0 cleanup.");
        live.Step();
        ActorState actor = live.Actors[0];
        Require(actor.NativeScriptMovement is { Active: false } && actor.NativeMovementCompletion542 == 0 && actor.NativeLandingTriangle508 == 240 && actor.NativeDisplayOffset is { X: 3, Y: 4, Z: 5 } && !live.Capture().GetProperty("Variables").TryGetProperty("5000", out _), "DOFFSET failed its physical lowWORD508/write542=0 or MOVESYNC used stale completed motion instead.");
        JsonElement held = live.Capture();
        JsonElement fiber = held.GetProperty("Fibers").EnumerateArray().Single(value => !value.GetProperty("Complete").GetBoolean());
        int position = fiber.GetProperty("Position").GetInt32();
        Require(fiber.GetProperty("WaitReason").GetString() == "Movement", "Native MOVESYNC did not retain its busy physical-word poll.");
        EventEngine restored = Create();
        restored.Restore(held);
        live.Step();
        restored.Step();
        Require(JsonElement.DeepEquals(live.Capture(), restored.Capture()) && live.Capture().GetProperty("Fibers").EnumerateArray().Single(value => !value.GetProperty("Complete").GetBoolean()).GetProperty("Position").GetInt32() == position && actor.NativeMovementCompletion542 == 0, "Inactive completed MOVE restarted after DOFFSET or independent MOVESYNC polling changed its original PC.");
        foreach (string property in new[] { "NativeMovementCompletion542", "NativeLandingTriangle508" })
        {
            JsonNode malformed = JsonNode.Parse(held.GetRawText())!;
            malformed["Actors"]![0]!.AsObject().Remove(property);
            JsonElement before = live.Capture();
            try
            {
                live.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Missing physical motion word acquired a restore default.");
            }
            catch (JsonException)
            {
            }
            Require(JsonElement.DeepEquals(before, live.Capture()), "Missing physical word partially mutated the VM during restore.");
        }
        foreach (int? completion in new int?[] { null, 9 })
        {
            JsonNode malformed = JsonNode.Parse(held.GetRawText())!;
            malformed["Actors"]![0]!["NativeMovementCompletion542"] = completion;
            JsonElement before = live.Capture();
            try
            {
                live.Restore(JsonSerializer.SerializeToElement(malformed));
                throw new InvalidOperationException("Unknown/out-of-range allocated physical542 restored.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(before, live.Capture()), "Invalid physical542 partially mutated the VM during restore.");
        }
        byte[] pollOnly = FieldMediaProgram([(Jsm.Opcode.MOVESYNC, 0)]);
        EventEngine allocatedPoll = CreateFieldMedia(pollOnly);
        allocatedPoll.Step();
        Require(allocatedPoll.Actors[0].NativeScriptMovement is null && allocatedPoll.Actors[0].NativeJump is null && allocatedPoll.Actors[0].NativeMovementCompletion542 == 0 && allocatedPoll.Capture().GetProperty("Fibers").EnumerateArray().Single(value => !value.GetProperty("Complete").GetBoolean()).GetProperty("WaitReason").GetString() == "Movement", "Allocated physical542=0 without a motion phase was treated as completed MOVESYNC.");
        EventEngine unknown = CreateFieldMedia(pollOnly, native: false);
        try
        {
            unknown.Step();
            throw new InvalidOperationException("Unknown physical542 was treated as an idle or completed MOVESYNC.");
        }
        catch (EventScriptException)
        {
        }
        Require(unknown.LastFault is { Opcode: "MOVESYNC" } && unknown.Actors[0].NativeMovementCompletion542 is null, "Unknown MOVESYNC did not refuse before mutation.");
        Console.WriteLine("Allocated508/542, completed MOVE/DOFFSET physical writes, unconditional MOVESYNC originalPC hold, independent restore/nextcallback, absent-current shape and unknown allocator refusal passed.");
    }
}
