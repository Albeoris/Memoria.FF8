using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void NativeJumpContract()
    {
        Require(NativeJumpMath.Interpolate(0, 524287, 3, 3) == 524287 && NativeJumpMath.Interpolate(0, 524288, 3, 3) == 524286 && NativeJumpMath.Interpolate(0, -524288, 3, 3) == -524286, "Native JUMP lost the original475C60 large-delta division order or its truncated endpoint.");
        Require(NativeJumpMath.Height(0, NativeJumpMath.Velocity(0, 4096, 3), 3) == 4095, "Native JUMP snapped its ballistic endpoint and discarded native velocity truncation.");
        (Jsm.Opcode Opcode, int Parameter)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode Opcode, int Parameter)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 65539), (Jsm.Opcode.JUMP, 9), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1], [init, main]);
        EventEngine Create()
        {
            GameSession session = new(new GameState());
            EventEngine engine = new(session, session.State.Variables)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveNativeTriangleHeight = (triangle, x, y) => triangle == 9 && x == 400 && y == 5 ? 4 : null
            };
            engine.LoadField(100, content, []);
            return engine;
        }
        EventEngine actual = Create();
        actual.Step();
        int animation = actual.Actors[0].AnimationId;
        float direction = actual.Actors[0].Direction;
        int originalTriangle = actual.Actors[0].WalkmeshTriangle;
        actual.Step();
        ActorState actor = actual.Actors[0];
        NativeJumpState jump = actor.NativeJump ?? throw new InvalidOperationException("Native JUMP did not establish mode2 raw state.");
        Require(jump is { Active: true, Duration: 3, Elapsed: 0, Completion: 1, TargetTriangle: 9, TargetZ: 16384 } && actor.X == 1 && actor.Y == 2 && actor.Z == 3 && actor.WalkmeshTriangle == originalTriangle && actor.AnimationId == animation && actor.Direction == direction, $"JUMP changed placement, animation or direction on its setup update, or confused parameter9 with durationWORD472: {jump}.");
        JsonElement saved = actual.Capture();
        JsonElement owner = saved.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("Sequence").GetInt64() == jump.FiberSequence);
        Require(owner.GetProperty("Position").GetInt32() == jump.Operation && owner.GetProperty("Stack").GetArrayLength() == 0 && owner.GetProperty("WaitReason").GetString() == "NativeJump", "JUMP did not preserve its consumed operand stack and held instruction.");
        EventEngine independent = Create();
        independent.Restore(saved);
        for (int elapsed = 1; elapsed <= 3; elapsed++)
        {
            actual.Step();
            independent.Step();
            Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()), "Independent JUMP restore changed ballistic phase, raw body or PC wait.");
            Require(actor.NativeJump is { Completion: 1 } phase && phase.Elapsed == elapsed && actor.WalkmeshTriangle == originalTriangle && actor.AnimationId == animation && actor.Direction == direction, "JUMP changed triangle before its separate completion update or altered actor animation/direction.");
        }
        Require(actor.NativeJump!.FixedX == NativeJumpMath.Interpolate(4096, 400 * 4096, 3, 3) && actor.NativeJump.FixedZ == 16383, "JUMP forced an exact endpoint instead of preserving native truncated XY/velocity arithmetic.");
        actual.Step();
        independent.Step();
        Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()) && actor.NativeJump is { Active: true, Completion: 2 } && actor.WalkmeshTriangle == 9, "JUMP missed its delayed landing triangle/completion update.");
        actual.Step();
        independent.Step();
        Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()) && actor.NativeJump is { Active: false, Completion: 2 }, "JUMP completion failed to advance/yield independently of its body driver.");
        JsonElement valid = actual.Capture();
        JsonNode malformed = JsonNode.Parse(valid.GetRawText())!;
        malformed["Actors"]![0]!["NativeJump"]!["Velocity"] = actor.NativeJump!.Velocity + 1;
        try
        {
            actual.Restore(JsonSerializer.SerializeToElement(malformed));
            throw new InvalidOperationException("Malformed jump velocity restored.");
        }
        catch (InvalidDataException)
        {
        }
        Require(JsonElement.DeepEquals(valid, actual.Capture()), "Malformed JUMP restore partially mutated the VM.");

        foreach (bool wrongPlane in new[] { true, false })
        {
            JsonNode forged = JsonNode.Parse(valid.GetRawText())!;
            JsonNode forgedJump = forged["Actors"]![0]!["NativeJump"]!;
            if (wrongPlane)
            {
                NativeJumpState completed = actor.NativeJump!;
                int targetZ = completed.TargetZ + 4096;
                int velocity = NativeJumpMath.Velocity(completed.StartZ, targetZ, completed.Duration);
                int fixedZ = NativeJumpMath.Height(completed.StartZ, velocity, completed.Elapsed);
                forgedJump["TargetZ"] = targetZ;
                forgedJump["Velocity"] = velocity;
                forgedJump["FixedZ"] = fixedZ;
                forged["Actors"]![0]!["Z"] = fixedZ / 4096f;
            }
            else
            {
                forgedJump["ScriptId"] = 0;
            }
            try
            {
                actual.Restore(JsonSerializer.SerializeToElement(forged));
                throw new InvalidOperationException(wrongPlane ? "Internally consistent fabricated JUMP plane restored." : "Completed JUMP receipt restored with the wrong issuing script.");
            }
            catch (InvalidDataException)
            {
            }
            Require(JsonElement.DeepEquals(valid, actual.Capture()), "JUMP landing-plane or completed-site authority refusal partially mutated the VM.");
        }

        EventEngine missing = Create();
        missing.ResolveNativeTriangleHeight = null;
        missing.Step();
        try
        {
            missing.Step();
            throw new InvalidOperationException("JUMP consumed operands without original landing plane geometry.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement refusal = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(missing.LastFault is { Opcode: "JUMP" } && refusal.GetProperty("Position").GetInt32() == jump.Operation && refusal.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 65539, 5, 400 }) && missing.Actors[0].NativeJump is null, "JUMP plane refusal failed to preserve original operands, PC and raw body.");
    }
}
