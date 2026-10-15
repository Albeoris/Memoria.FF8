using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static GameSession CreateNativeJumpSession()
    {
        using GameAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        return CreateDirectedLinearTurnSession(assets.ReadNewGameCharacterVitals(), assets.ReadNewGameEconomy());
    }

    private static void NativeJump3Contract()
    {
        (Jsm.Opcode Opcode, int Parameter)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode Opcode, int Parameter)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 900), (Jsm.Opcode.PSHN_L, 65539), (Jsm.Opcode.JUMP3, 333), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1], [init, main]);
        EventEngine Create()
        {
            GameSession session = CreateNativeJumpSession();
            EventEngine engine = new(session) { AnimationFrameCount = (_, _) => 4, ResolveNativeTriangleHeight = (triangle, x, y) => triangle == 333 && x == 400 && y == 5 ? 4 : null };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
            return engine;
        }
        EventEngine actual = Create();
        List<int[]> evidence = [];
        int? issuedTargetZ = null;
        actual.CommandExecuted += command =>
        {
            if (command.Name == nameof(Jsm.Opcode.JUMP3))
            {
                evidence.Add(command.Arguments.ToArray());
                issuedTargetZ = actual.Actors[0].NativeJump!.TargetZ;
            }
        };
        actual.Step();
        int sourceTriangle = actual.Actors[0].WalkmeshTriangle;
        actual.Step();
        NativeJumpState setup = actual.Actors[0].NativeJump ?? throw new InvalidOperationException("JUMP3 did not establish raw mode2 state.");
        Require(evidence.Count == 1 && evidence[0].SequenceEqual(new[] { 400, 5, 900, 65539 }) && issuedTargetZ == 900 * 4096 && actual.Actors[0].NativeMovementCompletion542 == 1 && actual.Actors[0].NativeLandingTriangle508 == 333 && actual.Actors[0].NativeTurnAccumulator474 == 0 && setup is { Duration: 3, TargetZ: 16384 } && setup.FixedX == 4096 && setup.FixedY == 8192 && setup.FixedZ == 12288 && setup.Triangle == sourceTriangle, "JUMP3 did not consume all four original operands or failed the native suppliedZ444 overwrite by the inline333 original plane.");
        EventEngine restored = Create();
        restored.Restore(actual.Capture());
        for (int update = 1; update <= 5; update++)
        {
            actual.Step();
            restored.Step();
            NativeJumpState state = actual.Actors[0].NativeJump!;
            Require(JsonElement.DeepEquals(actual.Capture(), restored.Capture()), "Independent JUMP3 restoration diverged in raw ballistic placement, target plane or held PC.");
            if (update <= 3)
            {
                Require(actual.Actors[0].NativeMovementCompletion542 == 1 && state.Triangle == sourceTriangle && actual.Actors[0].NativeTurnAccumulator474 == update && state.FixedZ == NativeJumpMath.Height(12288, NativeJumpMath.Velocity(12288, 16384, 3), (short)update), "JUMP3 used its discarded suppliedZ as the ballistic endpoint or changed triangle early.");
            }
            else
            {
                Require(actual.Actors[0].NativeMovementCompletion542 == 2 && state.FixedZ == 16383 && state.Triangle == 333 && state.Active == (update == 4), "JUMP3 failed native endpoint residue, separate triangle completion or following VM return3 advance/yield.");
            }
        }
        EventEngine missing = Create();
        missing.ResolveNativeTriangleHeight = null;
        missing.Step();
        try
        {
            missing.Step();
            throw new InvalidOperationException("JUMP3 used its suppliedZ to bypass missing original triangle geometry.");
        }
        catch (EventScriptException)
        {
        }
        JsonElement refusal = missing.Capture().GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ScriptId").GetInt32() == 1);
        Require(missing.LastFault is { Opcode: "JUMP3" } && refusal.GetProperty("Position").GetInt32() == setup.Operation && refusal.GetProperty("Stack").EnumerateArray().Select(value => value.GetInt32()).SequenceEqual(new[] { 65539, 900, 5, 400 }) && missing.Actors[0].NativeJump is null, "JUMP3 geometry refusal consumed an operand, advanced PC or manufactured a landing plane from suppliedZ.");
    }

    private static void NativeJumpContract()
    {
        Require(NativeJumpMath.Interpolate(0, 524287, 3, 3) == 524287 && NativeJumpMath.Interpolate(0, 524288, 3, 3) == 524286 && NativeJumpMath.Interpolate(0, -524288, 3, 3) == -524286, "Native JUMP lost the original475C60 large-delta division order or its truncated endpoint.");
        Require(NativeJumpMath.Height(0, NativeJumpMath.Velocity(0, 4096, 3), 3) == 4095, "Native JUMP snapped its ballistic endpoint and discarded native velocity truncation.");
        (Jsm.Opcode Opcode, int Parameter)[] init = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 0), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.SET3, 0), (Jsm.Opcode.RET, 0)];
        (Jsm.Opcode Opcode, int Parameter)[] main = [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.PSHN_L, 400), (Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.PSHN_L, 65539), (Jsm.Opcode.JUMP, 9), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.WAIT, 0), (Jsm.Opcode.RET, 0)];
        byte[] content = SyntheticObjects([1], [init, main]);
        EventEngine Create()
        {
            GameSession session = CreateNativeJumpSession();
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (_, _) => 4,
                ResolveNativeTriangleHeight = (triangle, x, y) => triangle == 9 && x == 400 && y == 5 ? 4 : null
            };
            engine.LoadField(100, content, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
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
        Require(actor.NativeMovementCompletion542 == 1 && actor.NativeLandingTriangle508 == 9 && actor.NativeTurnAccumulator474 == 0 && jump is { Active: true, Duration: 3, TargetZ: 16384 } && actor.X == 1 && actor.Y == 2 && actor.Z == 3 && actor.WalkmeshTriangle == originalTriangle && actor.AnimationId == animation && actor.Direction == direction, $"JUMP changed placement, animation or direction on its setup update, or confused parameter9 with durationWORD472: {jump}.");
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
            Require(actor.NativeMovementCompletion542 == 1 && actor.NativeJump is {  } phase && actor.NativeTurnAccumulator474 == elapsed && actor.WalkmeshTriangle == originalTriangle && actor.AnimationId == animation && actor.Direction == direction, "JUMP changed triangle before its separate completion update or altered actor animation/direction.");
        }
        Require(actor.NativeJump!.FixedX == NativeJumpMath.Interpolate(4096, 400 * 4096, 3, 3) && actor.NativeJump.FixedZ == 16383, "JUMP forced an exact endpoint instead of preserving native truncated XY/velocity arithmetic.");
        actual.Step();
        independent.Step();
        Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()) && actor.NativeMovementCompletion542 == 2 && actor.NativeJump is { Active: true } && actor.WalkmeshTriangle == 9, "JUMP missed its delayed landing triangle/completion update.");
        actual.Step();
        independent.Step();
        Require(JsonElement.DeepEquals(actual.Capture(), independent.Capture()) && actor.NativeMovementCompletion542 == 2 && actor.NativeJump is { Active: false }, "JUMP completion failed to advance/yield independently of its body driver.");
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
                int fixedZ = NativeJumpMath.Height(completed.StartZ, velocity, actor.NativeTurnAccumulator474!.Value);
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
